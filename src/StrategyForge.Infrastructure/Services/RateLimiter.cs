using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using StrategyForge.Domain.Configuration;

namespace StrategyForge.Infrastructure.Services;

/// <summary>
/// Window-based rate limiter per source with configurable limits.
/// Configuration hierarchy:
///   Source-specific RateLimit settings → Global DefaultRateLimit → built-in safe fallback (10/min).
/// Thread-safe and suitable for concurrent use.
/// </summary>
public sealed class RateLimiter : IDisposable
{
    private readonly ConcurrentDictionary<string, SlidingWindowBucket> _buckets = new();
    private readonly IOptions<DataSourceSettings> _settings;
    private Timer? _cleanupTimer;

    public RateLimiter(IOptions<DataSourceSettings> settings)
    {
        _settings = settings;
        _cleanupTimer = new Timer(Cleanup, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Resolves the effective rate limit for a given source key.
    /// Hierarchy: source-specific → global default → built-in fallback.
    /// </summary>
    public RateLimitSettings GetEffectiveRateLimit(string sourceKey)
    {
        var config = _settings.Value;

        // 1. Check source-specific override
        if (config.Sources.TryGetValue(sourceKey, out var sourceConfig) && sourceConfig.RateLimit != null && sourceConfig.RateLimit.IsValid)
        {
            return sourceConfig.RateLimit;
        }

        // 2. Global default
        if (config.DefaultRateLimit != null && config.DefaultRateLimit.IsValid)
        {
            return config.DefaultRateLimit;
        }

        // 3. Built-in safe fallback: 10 requests per minute
        return RateLimitSettings.Default;
    }

    /// <summary>
    /// Waits until a request slot is available for the given source.
    /// Uses sliding-window rate limiting based on configured limits.
    /// </summary>
    public async Task WaitForSlotAsync(
        string sourceKey,
        CancellationToken cancellationToken = default)
    {
        var settings = GetEffectiveRateLimit(sourceKey);
        var bucket = _buckets.GetOrAdd(sourceKey, _ => new SlidingWindowBucket(settings));

        // If config changed, update the bucket
        bucket.UpdateSettings(settings);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (bucket.TryTake())
                return;

            var waitMs = bucket.TimeUntilNextSlot();
            await Task.Delay(Math.Max(1, (int)waitMs), cancellationToken);
        }
    }

    private void Cleanup(object? state)
    {
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-10);
        var keysToRemove = _buckets
            .Where(kvp => kvp.Value.LastAccess < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _buckets.TryRemove(key, out _);
        }
    }

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
        _cleanupTimer = null;
    }
}

/// <summary>
/// Sliding window rate-limit bucket for a single source.
/// Tracks requests within a configurable time window.
/// </summary>
internal sealed class SlidingWindowBucket
{
    private readonly object _lock = new();
    private readonly Queue<DateTimeOffset> _timestamps = new();
    private RateLimitSettings _settings;
    private DateTimeOffset _lastAccess;

    public DateTimeOffset LastAccess
    {
        get { lock (_lock) return _lastAccess; }
    }

    public SlidingWindowBucket(RateLimitSettings settings)
    {
        _settings = settings;
        _lastAccess = DateTimeOffset.UtcNow;
    }

    public void UpdateSettings(RateLimitSettings settings)
    {
        lock (_lock)
        {
            _settings = settings;
        }
    }

    public bool TryTake()
    {
        lock (_lock)
        {
            _lastAccess = DateTimeOffset.UtcNow;
            var now = DateTimeOffset.UtcNow;
            var windowStart = now - _settings.Window;

            // Remove expired timestamps
            while (_timestamps.Count > 0 && _timestamps.Peek() < windowStart)
            {
                _timestamps.Dequeue();
            }

            if (_timestamps.Count < _settings.MaxRequests)
            {
                _timestamps.Enqueue(now);
                return true;
            }

            return false;
        }
    }

    public double TimeUntilNextSlot()
    {
        lock (_lock)
        {
            if (_timestamps.Count == 0)
                return 0;

            var windowStart = DateTimeOffset.UtcNow - _settings.Window;
            var oldestInWindow = _timestamps.Peek();

            if (oldestInWindow < windowStart)
                return 0;

            var waitUntil = oldestInWindow + _settings.Window;
            var waitMs = (waitUntil - DateTimeOffset.UtcNow).TotalMilliseconds;
            return Math.Max(0, waitMs);
        }
    }
}

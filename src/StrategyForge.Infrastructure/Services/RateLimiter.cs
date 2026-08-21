using System.Collections.Concurrent;

namespace StrategyForge.Infrastructure.Services;

/// <summary>
/// Token-bucket rate limiter for HTTP requests.
/// Limits requests per domain to respect provider rate limits.
/// Thread-safe and suitable for concurrent use.
/// </summary>
public sealed class RateLimiter : IDisposable
{
    private readonly ConcurrentDictionary<string, TokenBucket> _buckets = new();
    private readonly double _defaultRate;
    private Timer? _refillTimer;

    /// <summary>
    /// Creates a new rate limiter.
    /// </summary>
    /// <param name="defaultRatePerSecond">Default requests per second per domain.</param>
    public RateLimiter(double defaultRatePerSecond = 1.0)
    {
        _defaultRate = defaultRatePerSecond;
        // Refill tokens every 100ms for smooth rate limiting
        _refillTimer = new Timer(RefillAll, null, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));
    }

    /// <summary>
    /// Waits until a request slot is available for the given domain.
    /// Respects the configured rate limit.
    /// </summary>
    /// <param name="domain">The domain to rate-limit against.</param>
    /// <param name="ratePerSecond">Override rate for this domain.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task WaitForSlotAsync(
        string domain,
        double? ratePerSecond = null,
        CancellationToken cancellationToken = default)
    {
        var rate = ratePerSecond ?? _defaultRate;
        var bucket = _buckets.GetOrAdd(domain, _ => new TokenBucket(rate));

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (bucket.TryTake())
                return;

            // Wait until next token is available
            var waitMs = bucket.TimeUntilNextToken();
            await Task.Delay(Math.Max(1, (int)waitMs), cancellationToken);
        }
    }

    private void RefillAll(object? state)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        foreach (var bucket in _buckets.Values)
        {
            bucket.Refill(now);
        }
    }

    public void Dispose()
    {
        _refillTimer?.Dispose();
        _refillTimer = null;
    }
}

/// <summary>
/// Token bucket for a single domain.
/// </summary>
internal sealed class TokenBucket
{
    private readonly double _ratePerMs;
    private readonly int _maxTokens;
    private double _tokens;
    private long _lastRefillMs;

    public TokenBucket(double ratePerSecond, int maxBurst = 5)
    {
        _ratePerMs = ratePerSecond / 1000.0;
        _maxTokens = maxBurst;
        _tokens = maxBurst;
        _lastRefillMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    public bool TryTake()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Refill(now);

        if (_tokens >= 1.0)
        {
            _tokens -= 1.0;
            return true;
        }
        return false;
    }

    public double TimeUntilNextToken()
    {
        if (_tokens >= 1.0)
            return 0;

        var needed = 1.0 - _tokens;
        return needed / _ratePerMs;
    }

    public void Refill(long nowMs)
    {
        var elapsed = nowMs - _lastRefillMs;
        if (elapsed <= 0) return;

        _tokens = Math.Min(_maxTokens, _tokens + elapsed * _ratePerMs);
        _lastRefillMs = nowMs;
    }
}

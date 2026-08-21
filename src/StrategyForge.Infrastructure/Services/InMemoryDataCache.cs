using System.Collections.Concurrent;

namespace StrategyForge.Infrastructure.Services;

/// <summary>
/// In-memory cache for data acquisition results.
/// Supports configurable TTL per entry.
/// Thread-safe for concurrent use.
/// </summary>
public sealed class InMemoryDataCache
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private Timer? _cleanupTimer;

    public InMemoryDataCache()
    {
        // Cleanup expired entries every 60 seconds
        _cleanupTimer = new Timer(CleanupExpired, null, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));
    }

    /// <summary>
    /// Tries to get a cached value.
    /// </summary>
    /// <typeparam name="T">The cached value type.</typeparam>
    /// <param name="key">Cache key.</param>
    /// <param name="value">The cached value if found and not expired.</param>
    /// <returns>True if found and not expired.</returns>
    public bool TryGet<T>(string key, out T? value)
    {
        value = default;

        if (!_cache.TryGetValue(key, out var entry))
            return false;

        if (entry.IsExpired)
        {
            _cache.TryRemove(key, out _);
            return false;
        }

        if (entry.Value is T typedValue)
        {
            value = typedValue;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Sets a value in the cache with a TTL.
    /// </summary>
    public void Set<T>(string key, T value, TimeSpan ttl)
    {
        var entry = new CacheEntry
        {
            Value = value,
            ExpiresAt = DateTimeOffset.UtcNow.Add(ttl)
        };
        _cache[key] = entry;
    }

    /// <summary>
    /// Removes a specific key from the cache.
    /// </summary>
    public bool Remove(string key) => _cache.TryRemove(key, out _);

    /// <summary>
    /// Clears all cached entries.
    /// </summary>
    public void Clear() => _cache.Clear();

    /// <summary>
    /// Gets the number of cached entries.
    /// </summary>
    public int Count => _cache.Count;

    /// <summary>
    /// Generates a cache key for market data requests.
    /// </summary>
    public static string MarketDataKey(string instrumentId, string sourceType, DateOnly from, DateOnly to)
        => $"market:{instrumentId}:{sourceType}:{from:yyyyMMdd}:{to:yyyyMMdd}";

    /// <summary>
    /// Generates a cache key for latest candle requests.
    /// </summary>
    public static string LatestCandleKey(string instrumentId, string sourceType)
        => $"latest:{instrumentId}:{sourceType}";

    private void CleanupExpired(object? state)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var kvp in _cache)
        {
            if (kvp.Value.IsExpired)
            {
                _cache.TryRemove(kvp.Key, out _);
            }
        }
    }
}

internal sealed class CacheEntry
{
    public required object? Value { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;
}

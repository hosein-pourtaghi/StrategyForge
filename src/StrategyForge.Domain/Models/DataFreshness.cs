namespace StrategyForge.Domain.Models;

/// <summary>
/// Tracks the freshness and age of acquired data.
/// Every response from the Data Acquisition Layer must include this information.
/// </summary>
public sealed record DataFreshness
{
    /// <summary>When this data was fetched by StrategyForge.</summary>
    public required DateTimeOffset FetchedAtUtc { get; init; }

    /// <summary>The original timestamp from the source (if available).</summary>
    public DateTimeOffset? SourceTimestampUtc { get; init; }

    /// <summary>How old the data is in milliseconds since fetch time.</summary>
    public long AgeMs => (DateTimeOffset.UtcNow - FetchedAtUtc).TotalMilliseconds > 0
        ? (long)(DateTimeOffset.UtcNow - FetchedAtUtc).TotalMilliseconds
        : 0;

    /// <summary>Maximum acceptable age in milliseconds for this data type.</summary>
    public required long MaxAllowedAgeMs { get; init; }

    /// <summary>Whether the data is considered fresh (not stale).</summary>
    public bool IsFresh => AgeMs <= MaxAllowedAgeMs;

    /// <summary>Whether this data came from a cache.</summary>
    public required bool IsCached { get; init; }

    /// <summary>Create a freshness record for freshly fetched data.</summary>
    public static DataFreshness Fresh(long maxAgeMs = 86400000) => new()
    {
        FetchedAtUtc = DateTimeOffset.UtcNow,
        MaxAllowedAgeMs = maxAgeMs,
        IsCached = false
    };

    /// <summary>Create a freshness record for freshly fetched data from a TimeSpan.</summary>
    public static DataFreshness Fresh(TimeSpan maxAge) => new()
    {
        FetchedAtUtc = DateTimeOffset.UtcNow,
        MaxAllowedAgeMs = (long)maxAge.TotalMilliseconds,
        IsCached = false
    };

    /// <summary>Create a freshness record for cached data.</summary>
    public static DataFreshness Cached(DateTimeOffset fetchedAt, long maxAgeMs = 86400000) => new()
    {
        FetchedAtUtc = fetchedAt,
        MaxAllowedAgeMs = maxAgeMs,
        IsCached = true
    };
}

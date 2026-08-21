namespace StrategyForge.Domain.Configuration;

/// <summary>
/// Configuration for a rate limit policy: maximum requests within a time window.
/// Used for both global defaults and per-source overrides.
/// </summary>
public sealed record RateLimitSettings
{
    /// <summary>Maximum number of requests allowed within the window.</summary>
    public int MaxRequests { get; init; } = 10;

    /// <summary>Time window for the rate limit (e.g., "00:01:00" for 1 minute).</summary>
    public TimeSpan Window { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>Effective requests per second, computed from MaxRequests and Window.</summary>
    public double RequestsPerSecond => Window.TotalSeconds > 0
        ? MaxRequests / Window.TotalSeconds
        : MaxRequests;

    /// <summary>Default: 10 requests per minute.</summary>
    public static RateLimitSettings Default => new() { MaxRequests = 10, Window = TimeSpan.FromMinutes(1) };

    /// <summary>
    /// Validates the settings are sensible.
    /// </summary>
    public bool IsValid => MaxRequests > 0 && Window > TimeSpan.Zero;
}

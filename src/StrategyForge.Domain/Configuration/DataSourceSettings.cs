using StrategyForge.Domain.Enums;

namespace StrategyForge.Domain.Configuration;

/// <summary>
/// Configuration for data source providers.
/// Maps to the "DataSourceSettings" section in appsettings.json.
/// </summary>
public sealed record DataSourceSettings
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "DataSourceSettings";

    // --- Global Defaults ---

    /// <summary>HTTP request timeout in seconds for data providers.</summary>
    public int HttpTimeoutSeconds { get; init; } = 30;

    /// <summary>Number of retry attempts for failed data requests.</summary>
    public int RetryAttempts { get; init; } = 3;

    /// <summary>Base delay in milliseconds for exponential backoff.</summary>
    public int RetryBaseDelayMs { get; init; } = 1000;

    /// <summary>Maximum delay in milliseconds for exponential backoff.</summary>
    public int RetryMaxDelayMs { get; init; } = 30000;

    /// <summary>User agent string for HTTP requests.</summary>
    public string UserAgent { get; init; } = "StrategyForge/1.0";

    /// <summary>Maximum number of candles to request by default.</summary>
    public int DefaultMaxCandles { get; init; } = 365;

    /// <summary>Default cache duration in minutes for market data.</summary>
    public int CacheDurationMinutes { get; init; } = 15;

    /// <summary>Default rate limit: requests per second per domain.</summary>
    public double DefaultRateLimitPerSecond { get; init; } = 1.0;

    // --- Per-Source Configuration ---

    /// <summary>Per-source adapter configurations.</summary>
    public IDictionary<string, SourceAdapterConfig> Sources { get; init; }
        = new Dictionary<string, SourceAdapterConfig>
        {
            ["tsetmc"] = new SourceAdapterConfig
            {
                Name = "TSETMC",
                SourceType = SourceAdapterType.Tsetmc,
                Enabled = true,
                BaseUrl = "https://cdn.tsetmc.com",
                RateLimitPerSecond = 1.0,
                CacheMinutes = 15,
                MaxRetries = 3
            },
            ["tgju"] = new SourceAdapterConfig
            {
                Name = "TGJU",
                SourceType = SourceAdapterType.Tgju,
                Enabled = true,
                BaseUrl = "https://tgju.org",
                RateLimitPerSecond = 1.0,
                CacheMinutes = 5,
                MaxRetries = 3
            },
            ["rahavard365"] = new SourceAdapterConfig
            {
                Name = "Rahavard365",
                SourceType = SourceAdapterType.Rahavard365,
                Enabled = true,
                BaseUrl = "https://rahavard365.com",
                RateLimitPerSecond = 1.0,
                CacheMinutes = 15,
                MaxRetries = 2
            },
            ["cbi"] = new SourceAdapterConfig
            {
                Name = "CentralBankOfIran",
                SourceType = SourceAdapterType.Cbi,
                Enabled = true,
                BaseUrl = "https://cbi.ir",
                RateLimitPerSecond = 0.5,
                CacheMinutes = 60,
                MaxRetries = 2
            }
        };
}

/// <summary>
/// Configuration for a specific source adapter.
/// </summary>
public sealed record SourceAdapterConfig
{
    /// <summary>Human-readable name of this source.</summary>
    public required string Name { get; init; }

    /// <summary>The source adapter type.</summary>
    public required SourceAdapterType SourceType { get; init; }

    /// <summary>Whether this source is enabled.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Base URL for HTTP requests.</summary>
    public required string BaseUrl { get; init; }

    /// <summary>Rate limit: maximum requests per second.</summary>
    public double RateLimitPerSecond { get; init; } = 1.0;

    /// <summary>Cache duration in minutes for data from this source.</summary>
    public int CacheMinutes { get; init; } = 15;

    /// <summary>Maximum number of retries for this source.</summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>Request timeout in seconds (overrides global if set).</summary>
    public int? TimeoutSeconds { get; init; }

    /// <summary>Additional source-specific configuration key-value pairs.</summary>
    public IDictionary<string, string> ExtraSettings { get; init; }
        = new Dictionary<string, string>();
}

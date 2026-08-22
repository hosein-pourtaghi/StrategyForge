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

    /// <summary>Default rate limit: requests per minute (configurable).</summary>
    public RateLimitSettings DefaultRateLimit { get; init; } = RateLimitSettings.Default;

    /// <summary>Cross-source validation settings.</summary>
    public CrossValidationSettings CrossValidation { get; init; } = new();

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
                CacheMinutes = 15,
                MaxRetries = 3,
                Authentication = new AuthenticationSettings { Mode = AuthenticationMode.None }
            },
            ["tgju"] = new SourceAdapterConfig
            {
                Name = "TGJU",
                SourceType = SourceAdapterType.Tgju,
                Enabled = true,
                BaseUrl = "https://tgju.org",
                CacheMinutes = 5,
                MaxRetries = 3,
                Authentication = new AuthenticationSettings { Mode = AuthenticationMode.None }
            },
            ["rahavard365"] = new SourceAdapterConfig
            {
                Name = "Rahavard365",
                SourceType = SourceAdapterType.Rahavard365,
                Enabled = true,
                BaseUrl = "https://rahavard365.com",
                CacheMinutes = 15,
                MaxRetries = 2,
                Authentication = new AuthenticationSettings { Mode = AuthenticationMode.None }
            },
            ["cbi"] = new SourceAdapterConfig
            {
                Name = "CentralBankOfIran",
                SourceType = SourceAdapterType.Cbi,
                Enabled = true,
                BaseUrl = "https://cbi.ir",
                CacheMinutes = 60,
                MaxRetries = 2,
                Authentication = new AuthenticationSettings { Mode = AuthenticationMode.None }
            },
            ["tsewebgateway"] = new SourceAdapterConfig
            {
                Name = "TSEWebGateway",
                SourceType = SourceAdapterType.TseWebGateway,
                Enabled = true,
                BaseUrl = "https://cdn.tsetmc.com",
                CacheMinutes = 15,
                MaxRetries = 3,
                Authentication = new AuthenticationSettings { Mode = AuthenticationMode.None }
            },
            ["brsapi"] = new SourceAdapterConfig
            {
                Name = "BRSAPI",
                SourceType = SourceAdapterType.BrsApi,
                Enabled = true,
                BaseUrl = "https://Api.BrsApi.ir",
                CacheMinutes = 5,
                MaxRetries = 2,
                Authentication = new AuthenticationSettings
                {
                    Mode = AuthenticationMode.ApiKey,
                    CredentialReference = "StrategyForge:BrsApi:ApiKey"
                }
            },
            ["nobitex"] = new SourceAdapterConfig
            {
                Name = "Nobitex",
                SourceType = SourceAdapterType.Nobitex,
                Enabled = true,
                BaseUrl = "https://apiv2.nobitex.ir",
                CacheMinutes = 5,
                MaxRetries = 3,
                Authentication = new AuthenticationSettings { Mode = AuthenticationMode.None }
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

    /// <summary>Per-source rate limit override. Falls back to DataSourceSettings.DefaultRateLimit.</summary>
    public RateLimitSettings? RateLimit { get; init; }

    /// <summary>Cache duration in minutes for data from this source.</summary>
    public int CacheMinutes { get; init; } = 15;

    /// <summary>Maximum number of retries for this source.</summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>Request timeout in seconds (overrides global if set).</summary>
    public int? TimeoutSeconds { get; init; }

    /// <summary>Authentication settings for this source.</summary>
    public AuthenticationSettings Authentication { get; init; } = new() { Mode = AuthenticationMode.None };

    /// <summary>Additional source-specific configuration key-value pairs.</summary>
    public IDictionary<string, string> ExtraSettings { get; init; }
        = new Dictionary<string, string>();
}

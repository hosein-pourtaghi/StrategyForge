namespace StrategyForge.Domain.Models;

/// <summary>
/// Aggregated data bundle for a specific asset, collected from all available providers.
/// This is the primary output of the Data Layer and input to the Analysis Layer.
/// </summary>
public sealed record MarketDataBundle
{
    /// <summary>The asset this data bundle is about.</summary>
    public required Asset Asset { get; init; }

    /// <summary>When this bundle was assembled.</summary>
    public required DateTimeOffset CollectedAt { get; init; }

    /// <summary>Historical OHLCV candle data, ordered oldest to newest.</summary>
    public IReadOnlyList<Candle> Candles { get; init; } = [];

    /// <summary>Company fundamental information (if available).</summary>
    public CompanyInfo? CompanyInfo { get; init; }

    /// <summary>Recent news items related to this asset.</summary>
    public IReadOnlyList<NewsItem> News { get; init; } = [];

    /// <summary>Relevant economic indicators.</summary>
    public IReadOnlyList<EconomicIndicator> EconomicIndicators { get; init; } = [];

    /// <summary>Relevant currency exchange rates.</summary>
    public IReadOnlyList<CurrencyRate> CurrencyRates { get; init; } = [];

    /// <summary>Relevant gold prices.</summary>
    public IReadOnlyList<GoldPrice> GoldPrices { get; init; } = [];

    /// <summary>Names of providers that were successfully queried.</summary>
    public IReadOnlyList<string> SuccessfulProviders { get; init; } = [];

    /// <summary>Names of providers that failed or were unavailable.</summary>
    public IReadOnlyList<string> FailedProviders { get; init; } = [];

    /// <summary>Errors encountered during data collection.</summary>
    public IReadOnlyList<DataCollectionError> Errors { get; init; } = [];

    /// <summary>The earliest data point timestamp in this bundle.</summary>
    public DateTimeOffset? DataStartTime { get; init; }

    /// <summary>The latest data point timestamp in this bundle.</summary>
    public DateTimeOffset? DataEndTime { get; init; }
}

/// <summary>
/// Records an error that occurred during data collection.
/// </summary>
public sealed record DataCollectionError
{
    /// <summary>The provider that encountered the error.</summary>
    public required string ProviderName { get; init; }

    /// <summary>Description of the error.</summary>
    public required string ErrorMessage { get; init; }

    /// <summary>When the error occurred.</summary>
    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary>The original exception message (if any).</summary>
    public string? ExceptionMessage { get; init; }
}

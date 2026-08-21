using StrategyForge.Domain.Enums;

namespace StrategyForge.Domain.Models;

/// <summary>
/// Structured analytical evidence assembled from the Data and Analysis layers.
/// This is the primary input to AI agents — it contains everything the AI needs to reason about,
/// without requiring the AI to calculate anything itself.
/// </summary>
public sealed record AnalysisEvidence
{
    /// <summary>The asset being analyzed.</summary>
    public required Asset Asset { get; init; }

    /// <summary>When this evidence was assembled.</summary>
    public required DateTimeOffset AssembledAt { get; init; }

    /// <summary>The date range of the underlying data.</summary>
    public DateOnly DataStartDate { get; init; }

    /// <summary>The date range of the underlying data.</summary>
    public DateOnly DataEndDate { get; init; }

    // --- Market Context ---

    /// <summary>Most recent price information.</summary>
    public decimal? CurrentPrice { get; init; }

    /// <summary>Daily change percentage.</summary>
    public decimal? DailyChangePercent { get; init; }

    /// <summary>Volume information.</summary>
    public long? LatestVolume { get; init; }

    /// <summary>Average volume over recent period.</summary>
    public decimal? AverageVolume { get; init; }

    /// <summary>Volume ratio (current vs average).</summary>
    public decimal? VolumeRatio { get; init; }

    // --- Technical Evidence ---

    /// <summary>Latest indicator values from the IndicatorEngine.</summary>
    public IReadOnlyDictionary<string, IndicatorResult> IndicatorValues { get; init; }
        = new Dictionary<string, IndicatorResult>();

    /// <summary>Full indicator history (for trend analysis by AI).</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<IndicatorResult>> IndicatorHistory { get; init; }
        = new Dictionary<string, IReadOnlyList<IndicatorResult>>();

    /// <summary>Detected market regime (uptrend, downtrend, sideways, etc.).</summary>
    public MarketRegime? MarketRegime { get; init; }

    /// <summary>Detected support levels.</summary>
    public IReadOnlyList<decimal> SupportLevels { get; init; } = [];

    /// <summary>Detected resistance levels.</summary>
    public IReadOnlyList<decimal> ResistanceLevels { get; init; } = [];

    /// <summary>Recent price action summary (human-readable).</summary>
    public string? PriceActionSummary { get; init; }

    // --- Fundamental Evidence ---

    /// <summary>Company fundamental data (if available).</summary>
    public CompanyInfo? CompanyInfo { get; init; }

    // --- Contextual Evidence ---

    /// <summary>Relevant economic indicators.</summary>
    public IReadOnlyList<EconomicIndicator> EconomicIndicators { get; init; } = [];

    /// <summary>Relevant currency rates.</summary>
    public IReadOnlyList<CurrencyRate> CurrencyRates { get; init; } = [];

    /// <summary>Relevant gold prices.</summary>
    public IReadOnlyList<GoldPrice> GoldPrices { get; init; } = [];

    /// <summary>Recent news items.</summary>
    public IReadOnlyList<NewsItem> RecentNews { get; init; } = [];

    // --- Data Quality ---

    /// <summary>Data sources that contributed to this evidence.</summary>
    public IReadOnlyList<string> DataSources { get; init; } = [];

    /// <summary>What information is missing or unavailable.</summary>
    public IReadOnlyList<string> MissingData { get; init; } = [];

    /// <summary>Warnings about data quality or freshness.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

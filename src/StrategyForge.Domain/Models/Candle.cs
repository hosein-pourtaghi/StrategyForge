namespace StrategyForge.Domain.Models;

/// <summary>
/// Represents a single OHLCV (Open, High, Low, Close, Volume) price bar.
/// The fundamental unit of market price data.
/// 
/// Supports:
/// - Standard OHLCV fields
/// - Price adjustment status tracking
/// - Provenance and data quality metadata
/// - Calendar-aware date information (Jalali/Gregorian)
/// - Extended fields for source-specific data
/// </summary>
public sealed record Candle
{
    /// <summary>The date this candle represents (Gregorian).</summary>
    public required DateOnly Date { get; init; }

    /// <summary>Opening price for the period.</summary>
    public required decimal Open { get; init; }

    /// <summary>Highest price during the period.</summary>
    public required decimal High { get; init; }

    /// <summary>Lowest price during the period.</summary>
    public required decimal Low { get; init; }

    /// <summary>Closing price for the period.</summary>
    public required decimal Close { get; init; }

    /// <summary>Trading volume during the period.</summary>
    public required long Volume { get; init; }

    /// <summary>Total traded value (price × volume) if available.</summary>
    public decimal? Value { get; init; }

    /// <summary>Number of trades during the period (if available).</summary>
    public long? TradeCount { get; init; }

    /// <summary>Last price if different from close (e.g., settlement price).</summary>
    public decimal? LastPrice { get; init; }

    /// <summary>Change from previous period if available.</summary>
    public decimal? Change { get; init; }

    /// <summary>Percentage change from previous period if available.</summary>
    public decimal? ChangePercent { get; init; }

    /// <summary>Bid price if available.</summary>
    public decimal? BidPrice { get; init; }

    /// <summary>Ask price if available.</summary>
    public decimal? AskPrice { get; init; }

    // --- Calendar Information ---

    /// <summary>The market timezone this candle is from (e.g., "Asia/Tehran").</summary>
    public string? MarketTimezone { get; init; }

    /// <summary>Source date in original calendar format (e.g., "1405/05/30" for Jalali).</summary>
    public string? SourceDate { get; init; }

    /// <summary>Source calendar type (e.g., "jalali", "gregorian").</summary>
    public string? SourceCalendar { get; init; }

    // --- Price Adjustment ---

    /// <summary>Adjustment status of the price data.</summary>
    public DataAdjustment Adjustment { get; init; } = DataAdjustment.Unadjusted;

    // --- Metadata and Provenance ---

    /// <summary>Provenance information tracking the source of this data.</summary>
    public DataProvenance? Provenance { get; init; }

    /// <summary>Legacy metadata (prefer Provenance for new code).</summary>
    public DataMetadata? Metadata { get; init; }

    /// <summary>Source-specific extra fields not covered by the standard model.</summary>
    public IReadOnlyDictionary<string, string>? ExtraFields { get; init; }

    // --- Validation ---

    /// <summary>
    /// Validates that the candle has consistent OHLC relationships.
    /// High must be >= Open, Close, and Low. Low must be <= Open, Close, and High.
    /// </summary>
    public bool IsValid =>
        High >= Open && High >= Close && High >= Low &&
        Low <= Open && Low <= Close &&
        Open > 0 && Close > 0;
}

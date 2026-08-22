using StrategyForge.Domain.Enums;

namespace StrategyForge.Domain.Models;

/// <summary>
/// Canonical market data request used by the generic evidence query pipeline.
/// Expresses WHAT data is needed without specifying HOW to fetch it.
/// The DataSourceRegistry and adapters handle provider selection, fallback, and pagination.
/// </summary>
public sealed record MarketDataRequest
{
    /// <summary>The canonical instrument to query.</summary>
    public required InstrumentMapping Instrument { get; init; }

    /// <summary>The type of market data being requested.</summary>
    public required MarketDataType DataType { get; init; }

    /// <summary>Start date for time-series data (inclusive, Gregorian). Ignored for non-time-series types.</summary>
    public DateOnly? From { get; init; }

    /// <summary>End date for time-series data (inclusive, Gregorian). Ignored for non-time-series types.</summary>
    public DateOnly? To { get; init; }

    /// <summary>Maximum number of records to return. Provider may return fewer.</summary>
    public int? Limit { get; init; }

    /// <summary>Preferred source adapter, if any. Null means BestAvailable.</summary>
    public SourceAdapterType? PreferredSource { get; init; }

    /// <summary>Source selection mode. Default is BestAvailable.</summary>
    public SourceSelectionMode SelectionMode { get; init; } = SourceSelectionMode.BestAvailable;

    /// <summary>Maximum acceptable data age for freshness filtering. Null means use default.</summary>
    public TimeSpan? MaxAge { get; init; }
}

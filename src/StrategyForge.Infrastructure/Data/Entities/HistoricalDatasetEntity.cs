using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Models;
using StrategyForge.Domain.Interfaces.Providers;

namespace StrategyForge.Infrastructure.Data.Entities;

/// <summary>
/// EF Core entity for one normalized market observation in the historical dataset.
///
/// Identity (unique index): (InstrumentId, Source, ObservationDate).
/// This is the idempotency boundary of historical ingestion: re-importing the same
/// observation updates this row in place and never creates a duplicate.
///
/// Complex provenance is stored as JSON to avoid brittle relational mappings
/// against mutable record types, following the EvidenceEntity convention.
/// </summary>
public sealed class HistoricalDatasetEntity
{
    public Guid Id { get; set; }

    /// <summary>StrategyForge canonical instrument ID (not a provider identifier).</summary>
    public required string InstrumentId { get; set; }

    /// <summary>Provider source that supplied this observation.</summary>
    public required string Source { get; set; }

    /// <summary>Canonical Gregorian observation date (the unique-key date component).</summary>
    public required DateOnly ObservationDate { get; set; }

    // --- Normalized OHLCV ---

    public required decimal Open { get; set; }
    public required decimal High { get; set; }
    public required decimal Low { get; set; }
    public required decimal Close { get; set; }

    /// <summary>Trading volume; 0 when the provider does not supply volume (never fabricated).</summary>
    public long Volume { get; set; }

    /// <summary>Optional traded value (price × volume).</summary>
    public decimal? Value { get; set; }

    /// <summary>Optional number of trades.</summary>
    public long? TradeCount { get; set; }

    /// <summary>Last/settlement price when different from close.</summary>
    public decimal? LastPrice { get; set; }

    /// <summary>Change from previous period, if the provider supplies it.</summary>
    public decimal? Change { get; set; }

    /// <summary>Percentage change from previous period, if the provider supplies it.</summary>
    public decimal? ChangePercent { get; set; }

    // --- Calendar fidelity ---

    /// <summary>Market timezone of the observation (e.g., "Asia/Tehran").</summary>
    public string? MarketTimezone { get; set; }

    /// <summary>Source date in its original calendar (e.g., "1405/05/30" Jalali).</summary>
    public string? SourceDate { get; set; }

    /// <summary>Source calendar type (e.g., "jalali").</summary>
    public string? SourceCalendar { get; set; }

    /// <summary>Price adjustment status as string (DataAdjustmentType).</summary>
    public string? AdjustmentType { get; set; }

    /// <summary>Adjustment source when the data is adjusted.</summary>
    public string? AdjustmentSource { get; set; }

    // --- Provenance / retrieval metadata ---

    /// <summary>Source's native symbol for the instrument.</summary>
    public string? SourceSymbol { get; set; }

    /// <summary>Source's native instrument identifier (e.g., TSETMC InsCode, TGJU slug).</summary>
    public string? SourceInstrumentId { get; set; }

    /// <summary>When StrategyForge fetched this observation.</summary>
    public DateTimeOffset FetchedAtUtc { get; set; }

    /// <summary>The endpoint/data-type requested from the source, if recorded.</summary>
    public string? Endpoint { get; set; }

    /// <summary>Serialized DataProvenance.ExtraProperties, preserved for reproducibility.</summary>
    public string? ExtraPropertiesJson { get; set; }

    /// <summary>When this dataset row was first written.</summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>When this dataset row was last overwritten by an upsert.</summary>
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

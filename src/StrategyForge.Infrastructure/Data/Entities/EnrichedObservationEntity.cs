using StrategyForge.Domain.Enums;

namespace StrategyForge.Infrastructure.Data.Entities;

/// <summary>
/// EF Core entity for one derived (enriched) historical observation produced by the
/// processing pipeline: the validated raw observation plus deterministic indicator values.
///
/// Identity (unique index): (InstrumentId, Source, ObservationDate) — mirrors the raw
/// historical dataset identity, making re-processing idempotent.
///
/// The raw dataset is never mutated: this is a separate derived table that always
/// remains traceable to the raw observation (same identity) and to the pipeline
/// version that produced it.
///
/// Indicator values are stored as strongly typed JSON columns per indicator name
/// (name → component dictionary), keeping known indicators queryable without
/// per-indicator tables and without an opaque blob.
/// </summary>
public sealed class EnrichedObservationEntity
{
    public Guid Id { get; set; }

    /// <summary>StrategyForge canonical instrument ID (not a provider identifier).</summary>
    public required string InstrumentId { get; set; }

    /// <summary>Provider source this derived observation belongs to (never merged).</summary>
    public required string Source { get; set; }

    /// <summary>Observation date; identical to the raw observation's date (unique-key component).</summary>
    public required DateOnly ObservationDate { get; set; }

    // --- Normalized market data (projection of the validated raw observation) ---

    public required decimal Open { get; set; }
    public required decimal High { get; set; }
    public required decimal Low { get; set; }
    public required decimal Close { get; set; }

    /// <summary>Trading volume; 0 when the provider does not supply volume (never fabricated).</summary>
    public long Volume { get; set; }

    public decimal? Change { get; set; }
    public decimal? ChangePercent { get; set; }

    // --- Deterministic indicator enrichment ---

    /// <summary>Serialized indicator values: { "RSI": {"RSI": 55.2}, "MACD": {"MACD": .., "Signal": .., "Histogram": ..} }.</summary>
    public required string IndicatorsJson { get; set; }

    // --- Quality ---

    /// <summary>Quality status of the underlying observation (DataQualityStatus name).</summary>
    public required string QualityStatus { get; set; }

    /// <summary>Warning reason codes attached to the underlying observation (JSON array; empty when valid).</summary>
    public required string WarningCodesJson { get; set; }

    // --- Traceability ---

    /// <summary>Pipeline version that produced this derived observation.</summary>
    public required string ProcessedBy { get; set; }

    /// <summary>When this derived observation was computed.</summary>
    public DateTimeOffset ProcessedAtUtc { get; set; }

    /// <summary>When this derived row was first written.</summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>When this derived row was last overwritten by an upsert.</summary>
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

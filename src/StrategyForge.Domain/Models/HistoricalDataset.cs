using StrategyForge.Domain.Enums;

namespace StrategyForge.Domain.Models;

/// <summary>
/// A request to import historical candles for one canonical instrument, provider source,
/// and inclusive date range into the historical dataset.
/// </summary>
public sealed record HistoricalDatasetRequest
{
    /// <summary>The resolved canonical instrument.</summary>
    public required InstrumentMapping Instrument { get; init; }

    /// <summary>The provider source to import from.</summary>
    public required SourceAdapterType Source { get; init; }

    /// <summary>Inclusive start of the observation range (Gregorian).</summary>
    public required DateOnly From { get; init; }

    /// <summary>Inclusive end of the observation range (Gregorian).</summary>
    public required DateOnly To { get; init; }

    /// <summary>Optional candle resolution. Null uses the source default.</summary>
    public CandleResolution? Resolution { get; init; }
}

/// <summary>
/// Deterministic summary of one historical dataset import run.
/// Chunking, upsert, and failure accounting are all explicit — nothing is silent.
/// </summary>
public sealed record HistoricalDatasetImportResult
{
    /// <summary>Canonical instrument ID that was imported.</summary>
    public required string InstrumentId { get; init; }

    /// <summary>Provider source that was imported from.</summary>
    public required SourceAdapterType Source { get; init; }

    /// <summary>The requested range start.</summary>
    public required DateOnly RequestedFrom { get; init; }

    /// <summary>The requested range end.</summary>
    public required DateOnly RequestedTo { get; init; }

    /// <summary>Range start after chunk-boundary adjustment.</summary>
    public required DateOnly EffectiveFrom { get; init; }

    /// <summary>Range end after chunk-boundary adjustment.</summary>
    public required DateOnly EffectiveTo { get; init; }

    /// <summary>Whether the import completed without a terminal chunk failure.</summary>
    public required bool Ok { get; init; }

    /// <summary>Number of date-range chunks fetched from the provider.</summary>
    public required int ChunksFetched { get; init; }

    /// <summary>Number of chunks that failed after the configured retry attempts.</summary>
    public required int ChunksFailed { get; init; }

    /// <summary>Candle rows newly inserted into the dataset.</summary>
    public required int Inserted { get; init; }

    /// <summary>Existing candle rows updated in place.</summary>
    public required int Updated { get; init; }

    /// <summary>Rows rejected by validation before persistence.</summary>
    public required int Rejected { get; init; }

    /// <summary>Rows skipped because they fell outside the requested range.</summary>
    public required int OutOfRange { get; init; }

    /// <summary>Elapsed wall-clock time of the import.</summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>Machine-readable error code when <see cref="Ok"/> is false.</summary>
    public string? ErrorCode { get; init; }

    /// <summary>Human-readable error description when <see cref="Ok"/> is false.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Warning codes accumulated across chunks (e.g., provider-side partial-data warnings).
    /// Kept for observability; they do not fail the import.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    public static HistoricalDatasetImportResult Empty(
        HistoricalDatasetRequest request) => new()
    {
        InstrumentId = request.Instrument.InstrumentId,
        Source = request.Source,
        RequestedFrom = request.From,
        RequestedTo = request.To,
        EffectiveFrom = request.From,
        EffectiveTo = request.To,
        Ok = false,
        ChunksFetched = 0,
        ChunksFailed = 0,
        Inserted = 0,
        Updated = 0,
        Rejected = 0,
        OutOfRange = 0,
        Duration = TimeSpan.Zero
    };
}

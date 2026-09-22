using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Models;

namespace StrategyForge.Domain.Interfaces.Providers;

/// <summary>
/// Persistence contract for the historical market dataset.
///
/// Stores normalized, provider-neutral <see cref="Candle"/> observations keyed by
/// <em>canonical instrument identity + provider source + observation date</em>.
///
/// The store is the idempotency boundary of the historical ingestion pipeline:
/// importing the same observation twice must update the existing row in place
/// (and report it as an update), never create a duplicate market observation.
///
/// Provenance is preserved: each observation keeps the source that produced it
/// and the fetch metadata needed to reproduce downstream analysis.
/// </summary>
public interface IHistoricalDatasetStore
{
    /// <summary>
    /// Upserts a batch of candles for one instrument and one source.
    /// Observations are identified by (canonical instrument ID, source, observation date).
    /// New observations are inserted; existing observations are updated in place.
    /// Duplicate dates within <paramref name="candles"/> are collapsed (last wins).
    /// </summary>
    Task<HistoricalDatasetUpsertOutcome> UpsertCandlesAsync(
        InstrumentMapping instrument,
        SourceAdapterType source,
        IReadOnlyList<Candle> candles,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads stored candles for a canonical instrument, optionally filtered by source
    /// and inclusive date range. Results are ordered chronologically by observation date.
    /// </summary>
    Task<IReadOnlyList<Candle>> GetCandlesAsync(
        string instrumentId,
        SourceAdapterType? source = null,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the most recent stored observations for a canonical instrument
    /// (newest first). When <paramref name="source"/> is null, observations from
    /// all sources are considered.
    /// </summary>
    Task<IReadOnlyList<Candle>> GetLatestCandlesAsync(
        string instrumentId,
        int limit,
        SourceAdapterType? source = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts stored observations for a canonical instrument, optionally filtered by source.
    /// </summary>
    Task<int> CountCandlesAsync(
        string instrumentId,
        SourceAdapterType? source = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Outcome of an idempotent upsert batch.
/// Inserted + Updated equals the number of distinct observation dates accepted;
/// <see cref="DuplicateInBatch"/> counts same-date rows collapsed within the batch.
/// </summary>
public sealed record HistoricalDatasetUpsertOutcome
{
    /// <summary>Rows newly inserted.</summary>
    public required int Inserted { get; init; }

    /// <summary>Existing rows updated in place.</summary>
    public required int Updated { get; init; }

    /// <summary>Same-date rows collapsed within the submitted batch (not stored twice).</summary>
    public required int DuplicateInBatch { get; init; }

    public static HistoricalDatasetUpsertOutcome Empty => new() { Inserted = 0, Updated = 0, DuplicateInBatch = 0 };
}

using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Models;

namespace StrategyForge.Domain.Interfaces.Providers;

/// <summary>
/// Persistence contract for the derived (enriched) historical dataset.
///
/// Stores analysis-ready observations produced by the historical processing pipeline,
/// keyed by <em>canonical instrument identity + provider source + observation date</em> —
/// the same identity as the raw historical dataset — so processing is idempotent:
/// re-running the same request updates existing derived rows in place and never
/// creates duplicate derived observations.
///
/// The raw historical dataset is never mutated by this store; enrichment is a
/// separate derived layer that always remains traceable to raw provenance.
/// </summary>
public interface IEnrichedDatasetStore
{
    /// <summary>
    /// Upserts a batch of derived observations for one instrument and one source.
    /// Observations are identified by (canonical instrument ID, source, observation date).
    /// New observations are inserted; existing observations are updated in place.
    /// Duplicate dates within <paramref name="observations"/> are collapsed (last wins).
    /// </summary>
    Task<EnrichedUpsertOutcome> UpsertEnrichedAsync(
        string instrumentId,
        SourceAdapterType source,
        IReadOnlyList<EnrichedObservation> observations,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads derived observations for a canonical instrument, optionally filtered by
    /// source and inclusive date range. Results are ordered chronologically by
    /// observation date. Pagination is applied after ordering.
    /// </summary>
    Task<IReadOnlyList<EnrichedObservation>> GetEnrichedAsync(
        string instrumentId,
        SourceAdapterType? source = null,
        DateOnly? from = null,
        DateOnly? to = null,
        int skip = 0,
        int take = 0,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts derived observations for a canonical instrument, optionally filtered by source
    /// and inclusive date range.
    /// </summary>
    Task<int> CountEnrichedAsync(
        string instrumentId,
        SourceAdapterType? source = null,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Outcome of an idempotent derived-dataset upsert batch.
/// </summary>
public sealed record EnrichedUpsertOutcome
{
    /// <summary>Derived observations newly inserted.</summary>
    public required int Inserted { get; init; }

    /// <summary>Derived observations whose stored values changed.</summary>
    public required int Changed { get; init; }

    /// <summary>Derived observations that already existed with identical values.</summary>
    public required int Unchanged { get; init; }

    /// <summary>Same-date rows collapsed within the submitted batch (not stored twice).</summary>
    public required int DuplicateInBatch { get; init; }

    public static EnrichedUpsertOutcome Empty => new()
    {
        Inserted = 0, Changed = 0, Unchanged = 0, DuplicateInBatch = 0
    };
}

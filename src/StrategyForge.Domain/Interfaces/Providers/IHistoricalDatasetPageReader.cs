using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Models;

namespace StrategyForge.Domain.Interfaces.Providers;

/// <summary>
/// Paged read extension over the raw historical dataset.
///
/// The processing pipeline must handle datasets substantially larger than memory,
/// so reads are batched with a keyset cursor on ObservationDate rather than one
/// unbounded ToListAsync. Deterministic ordering is part of the contract: pages are
/// consecutive, non-overlapping, and chronologically ordered (ascending).
///
/// Observation identity is (instrument, source, date) — one row per date — so a
/// strictly-after date cursor can never skip or split an observation.
/// </summary>
public interface IHistoricalDatasetPageReader
{
    /// <summary>
    /// Reads up to <paramref name="pageSize"/> raw observations for one instrument/source
    /// within [from, to], starting strictly after <paramref name="afterDate"/> (or at
    /// <paramref name="from"/> when the cursor is null). Results are ordered by
    /// observation date ascending. Returns an empty page when no more rows exist.
    /// </summary>
    Task<IReadOnlyList<Candle>> ReadPageAsync(
        string instrumentId,
        SourceAdapterType source,
        DateOnly? from,
        DateOnly? to,
        DateOnly? afterDate,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the most recent observations strictly before <paramref name="beforeDate"/>
    /// (used as indicator warm-up context), returned in ascending date order.
    /// Returns at most <paramref name="maxRows"/> observations.
    /// </summary>
    Task<IReadOnlyList<Candle>> ReadLastPageAsync(
        string instrumentId,
        SourceAdapterType source,
        DateOnly? beforeDate,
        int maxRows,
        CancellationToken cancellationToken = default);
}

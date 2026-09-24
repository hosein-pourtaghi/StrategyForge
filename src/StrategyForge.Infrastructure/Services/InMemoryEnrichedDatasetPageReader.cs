using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Interfaces.Providers;
using StrategyForge.Domain.Models;

namespace StrategyForge.Infrastructure.Services;

/// <summary>
/// In-memory paged reader over the enriched dataset, backed by a shared
/// <see cref="InMemoryEnrichedDatasetStore"/> instance. Mirrors the semantics
/// of the EF Core <c>EnrichedDatasetPageReader</c> (chronological ordering,
/// keyset-cursor pagination) so behavior is identical in tests and development.
///
/// DI registers this via a factory that resolves the registered
/// <see cref="IEnrichedDatasetStore"/> singleton, so the reader always sees the
/// same data as the store — mirroring how the Phase 7 raw-dataset reader is
/// forwarded from the raw store.
/// </summary>
public sealed class InMemoryEnrichedDatasetPageReader : IEnrichedDatasetPageReader
{
    private readonly InMemoryEnrichedDatasetStore _store;

    public InMemoryEnrichedDatasetPageReader(InMemoryEnrichedDatasetStore store)
    {
        _store = store;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<EnrichedObservation>> ReadPageAsync(
        string instrumentId,
        SourceAdapterType source,
        DateOnly? from,
        DateOnly? to,
        DateOnly? afterDate,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var results = Query(instrumentId, source, from, to, afterDate);
        return Task.FromResult<IReadOnlyList<EnrichedObservation>>(results.Take(pageSize).ToList());
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<EnrichedObservation>> ReadLastPageAsync(
        string instrumentId,
        SourceAdapterType source,
        DateOnly? beforeDate,
        int maxRows,
        CancellationToken cancellationToken = default)
    {
        var results = Query(instrumentId, source, null, null, null)
            .Where(o => !beforeDate.HasValue || o.ObservationDate < beforeDate.Value)
            .OrderByDescending(o => o.ObservationDate)
            .Take(maxRows)
            .Reverse()
            .ToList();

        return Task.FromResult<IReadOnlyList<EnrichedObservation>>(results);
    }

    private IEnumerable<EnrichedObservation> Query(
        string instrumentId,
        SourceAdapterType source,
        DateOnly? from,
        DateOnly? to,
        DateOnly? afterDate)
    {
        // ReadAll() is an internal enumerator over the store's concurrent
        // dictionary, mirroring how the EF reader issues a query against the
        // DbContext. Adding a public API to the store for this would widen its
        // contract; the internal accessor keeps the surface minimal.
        return _store
            .EnumerateAll()
            .Where(kv => kv.Key.InstrumentId == instrumentId)
            .Where(kv => kv.Key.Source == source.ToString())
            .Where(kv => !from.HasValue || kv.Key.Date >= from.Value)
            .Where(kv => !to.HasValue || kv.Key.Date <= to.Value)
            .Where(kv => !afterDate.HasValue || kv.Key.Date > afterDate.Value)
            .OrderBy(kv => kv.Key.Date)
            .Select(kv => kv.Value);
    }
}

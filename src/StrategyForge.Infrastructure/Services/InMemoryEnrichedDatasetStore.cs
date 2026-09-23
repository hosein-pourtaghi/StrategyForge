using System.Collections.Concurrent;
using System.Text.Json;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Interfaces.Providers;
using StrategyForge.Domain.Models;

namespace StrategyForge.Infrastructure.Services;

/// <summary>
/// In-memory implementation of <see cref="IEnrichedDatasetStore"/> for unit testing
/// and development. Mirrors the persistence semantics of the EF Core store:
/// upserts are idempotent on (instrument, source, observation date), same-date rows
/// within a batch are collapsed (last wins), unchanged rows are counted, and reads
/// are chronological. Not suitable for production — data is lost when the process stops.
/// </summary>
public sealed class InMemoryEnrichedDatasetStore : IEnrichedDatasetStore
{
    private sealed record ObservationKey(string InstrumentId, string Source, DateOnly Date);

    private readonly ConcurrentDictionary<ObservationKey, EnrichedObservation> _store = new();

    /// <inheritdoc/>
    public Task<EnrichedUpsertOutcome> UpsertEnrichedAsync(
        string instrumentId,
        SourceAdapterType source,
        IReadOnlyList<EnrichedObservation> observations,
        CancellationToken cancellationToken = default)
    {
        var distinct = observations
            .GroupBy(o => o.ObservationDate)
            .ToDictionary(g => g.Key, g => g.Last());

        var inserted = 0;
        var changed = 0;
        var unchanged = 0;

        foreach (var (date, observation) in distinct)
        {
            var key = new ObservationKey(instrumentId, source.ToString(), date);
            if (_store.TryGetValue(key, out var existing))
            {
                if (SameValues(existing, observation))
                {
                    unchanged++;
                }
                else
                {
                    _store[key] = observation;
                    changed++;
                }
            }
            else
            {
                _store[key] = observation;
                inserted++;
            }
        }

        return Task.FromResult(new EnrichedUpsertOutcome
        {
            Inserted = inserted,
            Changed = changed,
            Unchanged = unchanged,
            DuplicateInBatch = observations.Count - distinct.Count
        });
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<EnrichedObservation>> GetEnrichedAsync(
        string instrumentId,
        SourceAdapterType? source = null,
        DateOnly? from = null,
        DateOnly? to = null,
        int skip = 0,
        int take = 0,
        CancellationToken cancellationToken = default)
    {
        var results = _store
            .Where(kv => kv.Key.InstrumentId == instrumentId)
            .Where(kv => !source.HasValue || kv.Key.Source == source.Value.ToString())
            .Where(kv => !from.HasValue || kv.Key.Date >= from.Value)
            .Where(kv => !to.HasValue || kv.Key.Date <= to.Value)
            .OrderBy(kv => kv.Key.Date)
            .Select(kv => kv.Value)
            .ToList();

        if (skip > 0)
        {
            results = results.Skip(skip).ToList();
        }

        if (take > 0)
        {
            results = results.Take(take).ToList();
        }

        return Task.FromResult<IReadOnlyList<EnrichedObservation>>(results);
    }

    /// <inheritdoc/>
    public Task<int> CountEnrichedAsync(
        string instrumentId,
        SourceAdapterType? source = null,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken cancellationToken = default)
    {
        var count = _store
            .Where(kv => kv.Key.InstrumentId == instrumentId)
            .Where(kv => !source.HasValue || kv.Key.Source == source.Value.ToString())
            .Where(kv => !from.HasValue || kv.Key.Date >= from.Value)
            .Where(kv => !to.HasValue || kv.Key.Date <= to.Value)
            .Count();

        return Task.FromResult(count);
    }

    private static bool SameValues(EnrichedObservation a, EnrichedObservation b)
    {
        return a.Open == b.Open
            && a.High == b.High
            && a.Low == b.Low
            && a.Close == b.Close
            && a.Volume == b.Volume
            && a.Change == b.Change
            && a.ChangePercent == b.ChangePercent
            && DictionaryEquals(a.Indicators, b.Indicators)
            && a.QualityStatus == b.QualityStatus
            && a.WarningCodes.SequenceEqual(b.WarningCodes)
            && string.Equals(a.ProcessedBy, b.ProcessedBy, StringComparison.Ordinal);
    }

    private static bool DictionaryEquals(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, decimal>>? x,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, decimal>>? y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }

        if (x is null || y is null || x.Count != y.Count)
        {
            return false;
        }

        foreach (var (key, xValue) in x)
        {
            if (!y.TryGetValue(key, out var yValue) || !xValue.SequenceEqual(yValue))
            {
                return false;
            }
        }

        return true;
    }
}

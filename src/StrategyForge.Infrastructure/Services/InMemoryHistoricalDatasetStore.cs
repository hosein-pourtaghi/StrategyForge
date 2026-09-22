using System.Collections.Concurrent;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Interfaces.Providers;
using StrategyForge.Domain.Models;

namespace StrategyForge.Infrastructure.Services;

/// <summary>
/// In-memory implementation of <see cref="IHistoricalDatasetStore"/> for unit testing
/// and development. Mirrors the persistence semantics of the EF Core store:
/// upserts are idempotent on (instrument, source, observation date), same-date rows
/// within a batch are collapsed (last wins), and reads are chronological.
/// Not suitable for production use — data is lost when the process stops.
/// </summary>
public sealed class InMemoryHistoricalDatasetStore : IHistoricalDatasetStore
{
    private sealed record ObservationKey(string InstrumentId, string Source, DateOnly Date);

    private readonly ConcurrentDictionary<ObservationKey, Candle> _store = new();

    /// <inheritdoc/>
    public Task<HistoricalDatasetUpsertOutcome> UpsertCandlesAsync(
        InstrumentMapping instrument,
        SourceAdapterType source,
        IReadOnlyList<Candle> candles,
        CancellationToken cancellationToken = default)
    {
        var distinct = candles
            .GroupBy(c => c.Date)
            .ToDictionary(g => g.Key, g => g.Last());

        var inserted = 0;
        var updated = 0;

        foreach (var (date, candle) in distinct)
        {
            var key = new ObservationKey(instrument.InstrumentId, source.ToString(), date);
            if (_store.ContainsKey(key))
            {
                _store[key] = candle;
                updated++;
            }
            else
            {
                _store[key] = candle;
                inserted++;
            }
        }

        return Task.FromResult(new HistoricalDatasetUpsertOutcome
        {
            Inserted = inserted,
            Updated = updated,
            DuplicateInBatch = candles.Count - distinct.Count
        });
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<Candle>> GetCandlesAsync(
        string instrumentId,
        SourceAdapterType? source = null,
        DateOnly? from = null,
        DateOnly? to = null,
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

        return Task.FromResult<IReadOnlyList<Candle>>(results);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<Candle>> GetLatestCandlesAsync(
        string instrumentId,
        int limit,
        SourceAdapterType? source = null,
        CancellationToken cancellationToken = default)
    {
        var results = _store
            .Where(kv => kv.Key.InstrumentId == instrumentId)
            .Where(kv => !source.HasValue || kv.Key.Source == source.Value.ToString())
            .OrderByDescending(kv => kv.Key.Date)
            .Take(limit)
            .Select(kv => kv.Value)
            .ToList();

        return Task.FromResult<IReadOnlyList<Candle>>(results);
    }

    /// <inheritdoc/>
    public Task<int> CountCandlesAsync(
        string instrumentId,
        SourceAdapterType? source = null,
        CancellationToken cancellationToken = default)
    {
        var count = _store
            .Where(kv => kv.Key.InstrumentId == instrumentId)
            .Where(kv => !source.HasValue || kv.Key.Source == source.Value.ToString())
            .Count();

        return Task.FromResult(count);
    }
}

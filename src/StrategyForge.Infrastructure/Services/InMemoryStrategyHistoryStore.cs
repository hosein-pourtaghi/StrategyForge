using System.Collections.Concurrent;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Interfaces.Providers;
using StrategyForge.Domain.Models;

namespace StrategyForge.Infrastructure.Services;

/// <summary>
/// In-memory implementation of IStrategyHistoryStore for unit testing and development.
/// Not suitable for production use — data is lost when the process stops.
/// </summary>
public sealed class InMemoryStrategyHistoryStore : IStrategyHistoryStore
{
    private readonly ConcurrentDictionary<Guid, PersistedStrategy> _store = new();

    /// <inheritdoc/>
    public Task<PersistedStrategy> StoreAsync(
        PersistedStrategy strategy,
        CancellationToken cancellationToken = default)
    {
        _store[strategy.Id] = strategy;
        return Task.FromResult(strategy);
    }

    /// <inheritdoc/>
    public Task<PersistedStrategy?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(id, out var strategy);
        return Task.FromResult(strategy);
    }

    /// <inheritdoc/>
    public Task<PersistedStrategy?> GetLatestByAssetAsync(
        string assetSymbol,
        CancellationToken cancellationToken = default)
    {
        var latest = _store.Values
            .Where(s => s.Asset.Symbol == assetSymbol)
            .OrderByDescending(s => s.GeneratedAt)
            .FirstOrDefault();

        return Task.FromResult(latest);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<PersistedStrategy>> GetByAssetAndDateRangeAsync(
        string assetSymbol,
        DateTimeOffset from,
        DateTimeOffset to,
        int maxResults = 100,
        CancellationToken cancellationToken = default)
    {
        var results = _store.Values
            .Where(s => s.Asset.Symbol == assetSymbol
                && s.GeneratedAt >= from
                && s.GeneratedAt <= to)
            .OrderByDescending(s => s.GeneratedAt)
            .Take(maxResults)
            .ToList();

        return Task.FromResult<IReadOnlyList<PersistedStrategy>>(results);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<PersistedStrategy>> GetByStateAsync(
        PipelineState state,
        int maxResults = 100,
        CancellationToken cancellationToken = default)
    {
        var results = _store.Values
            .Where(s => s.PipelineState == state)
            .OrderByDescending(s => s.GeneratedAt)
            .Take(maxResults)
            .ToList();

        return Task.FromResult<IReadOnlyList<PersistedStrategy>>(results);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<PersistedStrategy>> GetRecentAsync(
        int maxResults = 50,
        CancellationToken cancellationToken = default)
    {
        var results = _store.Values
            .OrderByDescending(s => s.GeneratedAt)
            .Take(maxResults)
            .ToList();

        return Task.FromResult<IReadOnlyList<PersistedStrategy>>(results);
    }

    /// <inheritdoc/>
    public Task<int> CountByAssetAsync(
        string assetSymbol,
        CancellationToken cancellationToken = default)
    {
        var count = _store.Values.Count(s => s.Asset.Symbol == assetSymbol);
        return Task.FromResult(count);
    }
}

using System.Collections.Concurrent;
using StrategyForge.Domain.Interfaces.Providers;
using StrategyForge.Domain.Models;

namespace StrategyForge.Infrastructure.Services;

/// <summary>
/// In-memory implementation of IEvidenceStore for unit testing and development.
/// Not suitable for production use — data is lost when the process stops.
/// </summary>
public sealed class InMemoryEvidenceStore : IEvidenceStore
{
    private readonly ConcurrentDictionary<Guid, PersistedEvidence> _store = new();

    /// <inheritdoc/>
    public Task<PersistedEvidence> StoreAsync(
        PersistedEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        _store[evidence.Id] = evidence;
        return Task.FromResult(evidence);
    }

    /// <inheritdoc/>
    public Task<PersistedEvidence?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(id, out var evidence);
        return Task.FromResult(evidence);
    }

    /// <inheritdoc/>
    public Task<PersistedEvidence?> GetLatestByAssetAsync(
        string assetSymbol,
        CancellationToken cancellationToken = default)
    {
        var latest = _store.Values
            .Where(e => e.Asset.Symbol == assetSymbol)
            .OrderByDescending(e => e.AssembledAt)
            .FirstOrDefault();

        return Task.FromResult(latest);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<PersistedEvidence>> GetByAssetAndDateRangeAsync(
        string assetSymbol,
        DateTimeOffset from,
        DateTimeOffset to,
        int maxResults = 100,
        CancellationToken cancellationToken = default)
    {
        var results = _store.Values
            .Where(e => e.Asset.Symbol == assetSymbol
                && e.AssembledAt >= from
                && e.AssembledAt <= to)
            .OrderByDescending(e => e.AssembledAt)
            .Take(maxResults)
            .ToList();

        return Task.FromResult<IReadOnlyList<PersistedEvidence>>(results);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<PersistedEvidence>> GetRecentAsync(
        int maxResults = 50,
        CancellationToken cancellationToken = default)
    {
        var results = _store.Values
            .OrderByDescending(e => e.AssembledAt)
            .Take(maxResults)
            .ToList();

        return Task.FromResult<IReadOnlyList<PersistedEvidence>>(results);
    }

    /// <inheritdoc/>
    public Task<int> CountByAssetAsync(
        string assetSymbol,
        CancellationToken cancellationToken = default)
    {
        var count = _store.Values.Count(e => e.Asset.Symbol == assetSymbol);
        return Task.FromResult(count);
    }
}

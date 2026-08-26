using System.Collections.Concurrent;
using StrategyForge.Domain.Interfaces.Providers;
using StrategyForge.Domain.Models;

namespace StrategyForge.Infrastructure.Services;

/// <summary>
/// In-memory implementation of IIntelligenceRunStore for unit testing and development.
/// Not suitable for production use — data is lost when the process stops.
/// </summary>
public sealed class InMemoryIntelligenceRunStore : IIntelligenceRunStore
{
    private readonly ConcurrentDictionary<Guid, IntelligenceRun> _store = new();

    /// <inheritdoc/>
    public Task<IntelligenceRun> StoreAsync(
        IntelligenceRun run,
        CancellationToken cancellationToken = default)
    {
        _store[run.Id] = run;
        return Task.FromResult(run);
    }

    /// <inheritdoc/>
    public Task UpdateAsync(
        IntelligenceRun run,
        CancellationToken cancellationToken = default)
    {
        _store[run.Id] = run;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<IntelligenceRun?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(id, out var run);
        return Task.FromResult(run);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<IntelligenceRun>> GetRecentAsync(
        int maxResults = 50,
        CancellationToken cancellationToken = default)
    {
        var results = _store.Values
            .OrderByDescending(r => r.ScheduledAt)
            .Take(maxResults)
            .ToList();

        return Task.FromResult<IReadOnlyList<IntelligenceRun>>(results);
    }
}

using StrategyForge.Domain.Models;

namespace StrategyForge.Domain.Interfaces.Providers;

/// <summary>
/// Repository for persisting and querying intelligence run history.
/// Tracks background intelligence collection and analysis runs.
/// </summary>
public interface IIntelligenceRunStore
{
    /// <summary>
    /// Stores a new intelligence run record.
    /// </summary>
    Task<IntelligenceRun> StoreAsync(
        IntelligenceRun run,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing intelligence run record (e.g., when state changes).
    /// </summary>
    Task UpdateAsync(
        IntelligenceRun run,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a run by its unique identifier.
    /// </summary>
    Task<IntelligenceRun?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves recent intelligence runs ordered by scheduled time (newest first).
    /// </summary>
    Task<IReadOnlyList<IntelligenceRun>> GetRecentAsync(
        int maxResults = 50,
        CancellationToken cancellationToken = default);
}

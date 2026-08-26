using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Models;

namespace StrategyForge.Domain.Interfaces.Providers;

/// <summary>
/// Repository for persisting and querying strategy history.
/// Supports strategy evolution tracking, historical comparison,
/// and intelligence accumulation over time.
/// 
/// Strategy records are immutable once stored.
/// </summary>
public interface IStrategyHistoryStore
{
    /// <summary>
    /// Stores a new strategy record.
    /// </summary>
    /// <param name="strategy">The strategy to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The stored strategy with its assigned ID.</returns>
    Task<PersistedStrategy> StoreAsync(
        PersistedStrategy strategy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a strategy by its unique identifier.
    /// </summary>
    Task<PersistedStrategy?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the most recent strategy for a specific asset.
    /// </summary>
    Task<PersistedStrategy?> GetLatestByAssetAsync(
        string assetSymbol,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves strategies for a specific asset within a date range.
    /// Returns strategies ordered by generation time (newest first).
    /// </summary>
    Task<IReadOnlyList<PersistedStrategy>> GetByAssetAndDateRangeAsync(
        string assetSymbol,
        DateTimeOffset from,
        DateTimeOffset to,
        int maxResults = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves strategies filtered by pipeline state.
    /// </summary>
    Task<IReadOnlyList<PersistedStrategy>> GetByStateAsync(
        PipelineState state,
        int maxResults = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the most recent strategies across all assets.
    /// </summary>
    Task<IReadOnlyList<PersistedStrategy>> GetRecentAsync(
        int maxResults = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts total strategy records for an asset.
    /// </summary>
    Task<int> CountByAssetAsync(
        string assetSymbol,
        CancellationToken cancellationToken = default);
}

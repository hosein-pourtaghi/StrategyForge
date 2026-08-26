using StrategyForge.Domain.Models;

namespace StrategyForge.Domain.Interfaces.Providers;

/// <summary>
/// Repository for persisting and querying analysis evidence.
/// Supports historical evidence comparison, provenance tracking,
/// and intelligence accumulation over time.
/// 
/// Evidence records are immutable once stored.
/// </summary>
public interface IEvidenceStore
{
    /// <summary>
    /// Stores a new evidence record.
    /// </summary>
    /// <param name="evidence">The evidence to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The stored evidence with its assigned ID.</returns>
    Task<PersistedEvidence> StoreAsync(
        PersistedEvidence evidence,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves evidence by its unique identifier.
    /// </summary>
    Task<PersistedEvidence?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the most recent evidence for a specific asset.
    /// </summary>
    Task<PersistedEvidence?> GetLatestByAssetAsync(
        string assetSymbol,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves evidence for a specific asset within a date range.
    /// Returns evidence ordered by assembly time (newest first).
    /// </summary>
    Task<IReadOnlyList<PersistedEvidence>> GetByAssetAndDateRangeAsync(
        string assetSymbol,
        DateTimeOffset from,
        DateTimeOffset to,
        int maxResults = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the most recent evidence records across all assets.
    /// </summary>
    Task<IReadOnlyList<PersistedEvidence>> GetRecentAsync(
        int maxResults = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts total evidence records for an asset.
    /// </summary>
    Task<int> CountByAssetAsync(
        string assetSymbol,
        CancellationToken cancellationToken = default);
}

using StrategyForge.Domain.Models;

namespace StrategyForge.Domain.Interfaces.Providers;

/// <summary>
/// Interface for storing and retrieving assets.
/// Assets can be registered by the user and used throughout the system.
/// </summary>
public interface IAssetRepository
{
    /// <summary>
    /// Gets all registered assets.
    /// </summary>
    Task<IReadOnlyList<Asset>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an asset by its unique identifier.
    /// </summary>
    Task<Asset?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an asset by its symbol.
    /// </summary>
    Task<Asset?> GetBySymbolAsync(string symbol, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new asset to the repository.
    /// </summary>
    Task<Asset> AddAsync(Asset asset, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes an asset from the repository.
    /// </summary>
    Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken = default);
}

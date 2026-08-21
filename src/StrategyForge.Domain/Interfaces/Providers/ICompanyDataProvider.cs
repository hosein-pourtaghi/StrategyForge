using StrategyForge.Domain.Models;

namespace StrategyForge.Domain.Interfaces.Providers;

/// <summary>
/// Interface for providers that supply company fundamental data and financial statements.
/// </summary>
public interface ICompanyDataProvider
{
    /// <summary>Human-readable name of this provider.</summary>
    string Name { get; }

    /// <summary>
    /// Retrieves fundamental company information for a stock.
    /// </summary>
    /// <param name="asset">The stock asset to get company info for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Company information, or null if unavailable.</returns>
    Task<CompanyInfo?> GetCompanyInfoAsync(
        Asset asset,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether this provider has data for the given asset.
    /// </summary>
    bool Supports(Asset asset);
}

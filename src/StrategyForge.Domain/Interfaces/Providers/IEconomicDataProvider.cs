using StrategyForge.Domain.Models;

namespace StrategyForge.Domain.Interfaces.Providers;

/// <summary>
/// Interface for providers that supply macroeconomic indicator data.
/// </summary>
public interface IEconomicDataProvider
{
    /// <summary>Human-readable name of this provider.</summary>
    string Name { get; }

    /// <summary>
    /// Retrieves current economic indicators (inflation, interest rates, etc.).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of economic indicators.</returns>
    Task<IReadOnlyList<EconomicIndicator>> GetIndicatorsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a specific economic indicator by name.
    /// </summary>
    /// <param name="indicatorName">Name of the indicator (e.g., "Inflation Rate").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The indicator value, or null if unavailable.</returns>
    Task<EconomicIndicator?> GetIndicatorAsync(
        string indicatorName,
        CancellationToken cancellationToken = default);
}

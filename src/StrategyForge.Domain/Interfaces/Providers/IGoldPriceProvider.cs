using StrategyForge.Domain.Models;

namespace StrategyForge.Domain.Interfaces.Providers;

/// <summary>
/// Interface for providers that supply gold price data.
/// </summary>
public interface IGoldPriceProvider
{
    /// <summary>Human-readable name of this provider.</summary>
    string Name { get; }

    /// <summary>
    /// Gets the current gold price.
    /// </summary>
    /// <param name="unit">Desired unit (e.g., "USD/oz", "IRR/mithqal"). Null for provider default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The current gold price, or null if unavailable.</returns>
    Task<GoldPrice?> GetCurrentPriceAsync(
        string? unit = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets historical gold prices for a date range.
    /// </summary>
    /// <param name="from">Start date.</param>
    /// <param name="to">End date.</param>
    /// <param name="unit">Desired unit. Null for provider default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of gold prices, ordered oldest to newest.</returns>
    Task<IReadOnlyList<GoldPrice>> GetHistoricalPricesAsync(
        DateOnly from,
        DateOnly to,
        string? unit = null,
        CancellationToken cancellationToken = default);
}

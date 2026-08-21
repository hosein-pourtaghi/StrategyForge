using StrategyForge.Domain.Models;

namespace StrategyForge.Domain.Interfaces.Providers;

/// <summary>
/// Interface for providers that supply currency exchange rate data.
/// </summary>
public interface ICurrencyProvider
{
    /// <summary>Human-readable name of this provider.</summary>
    string Name { get; }

    /// <summary>
    /// Gets the current exchange rate between two currencies.
    /// </summary>
    /// <param name="baseCurrency">Base currency code (e.g., "USD").</param>
    /// <param name="quoteCurrency">Quote currency code (e.g., "IRR").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The exchange rate, or null if unavailable.</returns>
    Task<CurrencyRate?> GetRateAsync(
        string baseCurrency,
        string quoteCurrency,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets historical exchange rates for a date range.
    /// </summary>
    /// <param name="baseCurrency">Base currency code.</param>
    /// <param name="quoteCurrency">Quote currency code.</param>
    /// <param name="from">Start date.</param>
    /// <param name="to">End date.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of currency rates, ordered oldest to newest.</returns>
    Task<IReadOnlyList<CurrencyRate>> GetHistoricalRatesAsync(
        string baseCurrency,
        string quoteCurrency,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);
}

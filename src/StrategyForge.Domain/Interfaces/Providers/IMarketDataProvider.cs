using StrategyForge.Domain.Models;

namespace StrategyForge.Domain.Interfaces.Providers;

/// <summary>
/// Interface for market data providers that supply OHLCV candle data.
/// Implementations normalize external data into the domain Candle model.
/// Multiple providers can be registered for fallback support.
/// </summary>
public interface IMarketDataProvider
{
    /// <summary>Human-readable name of this provider (e.g., "TSETMC", "YahooFinance").</summary>
    string Name { get; }

    /// <summary>
    /// Retrieves historical OHLCV candle data for an asset within a date range.
    /// Returns candles ordered from oldest to newest.
    /// </summary>
    /// <param name="asset">The asset to get data for.</param>
    /// <param name="from">Start date (inclusive).</param>
    /// <param name="to">End date (inclusive).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of candles, or empty if no data available.</returns>
    Task<IReadOnlyList<Candle>> GetHistoricalDataAsync(
        Asset asset,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the most recent candle for an asset.
    /// </summary>
    /// <param name="asset">The asset to get data for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The latest candle, or null if unavailable.</returns>
    Task<Candle?> GetLatestCandleAsync(
        Asset asset,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether this provider can serve data for the given asset.
    /// </summary>
    /// <param name="asset">The asset to check.</param>
    /// <returns>True if this provider supports the asset.</returns>
    bool Supports(Asset asset);
}

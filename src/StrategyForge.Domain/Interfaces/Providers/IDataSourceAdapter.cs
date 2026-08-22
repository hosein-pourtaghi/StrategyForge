using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Models;

namespace StrategyForge.Domain.Interfaces.Providers;

/// <summary>
/// Unified interface for all source adapters in the Data Acquisition Layer.
/// Each adapter wraps an external data source and normalizes its responses
/// into StrategyForge canonical data contracts.
/// 
/// Adapters are replaceable: swapping one adapter for another must not
/// require changes to the rest of the system.
/// </summary>
public interface IDataSourceAdapter
{
    /// <summary>The source adapter type this adapter implements.</summary>
    SourceAdapterType SourceType { get; }

    /// <summary>Human-readable name (e.g., "TSETMC", "TGJU").</summary>
    string Name { get; init; }

    /// <summary>The domain(s) this adapter fetches data from.</summary>
    IReadOnlyList<string> Domains { get; }

    /// <summary>The market data types this adapter can provide.</summary>
    IReadOnlyList<MarketDataType> SupportedCapabilities { get; }

    /// <summary>Whether this adapter is currently enabled.</summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Fetches historical OHLCV candle data for an instrument.
    /// </summary>
    Task<DataResult<IReadOnlyList<Candle>>> GetHistoricalCandlesAsync(
        InstrumentMapping instrument,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches the most recent candle/snapshot for an instrument.
    /// </summary>
    Task<DataResult<Candle>> GetLatestCandleAsync(
        InstrumentMapping instrument,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches order book (depth-of-market) data for an instrument.
    /// Returns UNSUPPORTED_CAPABILITY if this adapter does not provide order books.
    /// </summary>
    Task<DataResult<OrderBook>> GetOrderBookAsync(
        InstrumentMapping instrument,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether this adapter can serve data for the given instrument.
    /// </summary>
    bool Supports(InstrumentMapping instrument);

    /// <summary>
    /// Gets the adapter's current health status.
    /// Used by the registry for fallback decisions.
    /// </summary>
    Task<AdapterHealthStatus> GetHealthAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Health status of a source adapter.
/// Uses a mutable class (not record) so health can be updated over time.
/// </summary>
public class AdapterHealthStatus
{
    /// <summary>Whether the adapter is reachable and functional.</summary>
    public bool IsHealthy { get; set; } = true;

    /// <summary>Last successful request timestamp.</summary>
    public DateTimeOffset? LastSuccessfulRequest { get; set; }

    /// <summary>Last error encountered.</summary>
    public string? LastError { get; set; }

    /// <summary>Number of consecutive failures.</summary>
    public int ConsecutiveFailures { get; set; }

    /// <summary>Average response time over recent requests.</summary>
    public TimeSpan? AverageResponseTime { get; set; }
}

using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Models;

namespace StrategyForge.Domain.Interfaces.Providers;

/// <summary>
/// Manages the collection of registered source adapters.
/// Handles capability-aware adapter selection, fallback logic, and health monitoring.
/// </summary>
public interface IDataSourceRegistry
{
    /// <summary>
    /// Gets all registered adapters.
    /// </summary>
    IReadOnlyList<IDataSourceAdapter> GetAllAdapters();

    /// <summary>
    /// Gets all adapters that support the given instrument.
    /// </summary>
    IReadOnlyList<IDataSourceAdapter> GetAdaptersForInstrument(InstrumentMapping instrument);

    /// <summary>
    /// Gets adapters that support both the given instrument AND the requested data type.
    /// Source selection is deterministic: healthy adapters first, then by source type order.
    /// </summary>
    IReadOnlyList<IDataSourceAdapter> GetAdaptersForCapability(
        InstrumentMapping instrument,
        MarketDataType dataType);

    /// <summary>
    /// Gets the best available adapter for the given instrument, considering health and priority.
    /// </summary>
    IDataSourceAdapter? GetBestAdapter(InstrumentMapping instrument);

    /// <summary>
    /// Gets a specific adapter by source type.
    /// </summary>
    IDataSourceAdapter? GetAdapter(SourceAdapterType sourceType);

    /// <summary>
    /// Fetches historical candles with automatic source selection and fallback.
    /// </summary>
    Task<DataResult<IReadOnlyList<Candle>>> FetchHistoricalCandlesAsync(
        InstrumentMapping instrument,
        DateOnly from,
        DateOnly to,
        SourceAdapterType? preferredSource = null,
        SourceSelectionMode selectionMode = SourceSelectionMode.BestAvailable,
        CandleResolution? resolution = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches the latest candle with automatic source selection and fallback.
    /// </summary>
    Task<DataResult<Candle>> FetchLatestCandleAsync(
        InstrumentMapping instrument,
        SourceAdapterType? preferredSource = null,
        SourceSelectionMode selectionMode = SourceSelectionMode.BestAvailable,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches order book data with automatic source selection and fallback.
    /// Only adapters that support OrderBook capability will be tried.
    /// </summary>
    Task<DataResult<OrderBook>> FetchOrderBookAsync(
        InstrumentMapping instrument,
        SourceAdapterType? preferredSource = null,
        SourceSelectionMode selectionMode = SourceSelectionMode.BestAvailable,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets health status of all registered adapters.
    /// </summary>
    Task<IReadOnlyDictionary<SourceAdapterType, AdapterHealthStatus>> GetAllHealthStatusesAsync(
        CancellationToken cancellationToken = default);
}

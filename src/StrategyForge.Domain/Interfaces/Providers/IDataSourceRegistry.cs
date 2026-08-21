using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Models;

namespace StrategyForge.Domain.Interfaces.Providers;

/// <summary>
/// Manages the collection of registered source adapters.
/// Handles adapter selection, fallback logic, and health monitoring.
/// 
/// The registry is the central coordination point for the Data Acquisition Layer.
/// It decides which adapter to use for a given request and handles fallback
/// when the primary adapter fails.
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
    /// Gets the best available adapter for the given instrument, considering health and priority.
    /// </summary>
    IDataSourceAdapter? GetBestAdapter(InstrumentMapping instrument);

    /// <summary>
    /// Gets a specific adapter by source type.
    /// </summary>
    IDataSourceAdapter? GetAdapter(SourceAdapterType sourceType);

    /// <summary>
    /// Fetches historical candles with automatic source selection and fallback.
    /// Tries the primary source first; if it fails, tries compatible alternatives.
    /// </summary>
    Task<DataResult<IReadOnlyList<Candle>>> FetchHistoricalCandlesAsync(
        InstrumentMapping instrument,
        DateOnly from,
        DateOnly to,
        SourceAdapterType? preferredSource = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches the latest candle with automatic source selection and fallback.
    /// </summary>
    Task<DataResult<Candle>> FetchLatestCandleAsync(
        InstrumentMapping instrument,
        SourceAdapterType? preferredSource = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets health status of all registered adapters.
    /// </summary>
    Task<IReadOnlyDictionary<SourceAdapterType, AdapterHealthStatus>> GetAllHealthStatusesAsync(
        CancellationToken cancellationToken = default);
}

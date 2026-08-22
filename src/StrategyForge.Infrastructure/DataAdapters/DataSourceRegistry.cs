using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Interfaces.Providers;
using StrategyForge.Domain.Models;

namespace StrategyForge.Infrastructure.DataAdapters;

/// <summary>
/// Manages all registered source adapters and handles capability-aware source selection with fallback.
/// </summary>
public sealed class DataSourceRegistry : IDataSourceRegistry
{
    private readonly IReadOnlyList<IDataSourceAdapter> _adapters;
    private readonly ILogger<DataSourceRegistry> _logger;
    private readonly ConcurrentDictionary<SourceAdapterType, AdapterHealthStatus> _healthCache = new();

    public DataSourceRegistry(
        IEnumerable<IDataSourceAdapter> adapters,
        ILogger<DataSourceRegistry> logger)
    {
        _adapters = adapters.ToList().AsReadOnly();
        _logger = logger;

        _logger.LogInformation(
            "DataSourceRegistry initialized with {Count} adapters: {Adapters}",
            _adapters.Count,
            string.Join(", ", _adapters.Select(a => $"{a.Name}({a.SourceType})")));
    }

    public IReadOnlyList<IDataSourceAdapter> GetAllAdapters() => _adapters;

    public IReadOnlyList<IDataSourceAdapter> GetAdaptersForInstrument(InstrumentMapping instrument)
    {
        return _adapters
            .Where(a => a.IsEnabled && a.Supports(instrument))
            .ToList()
            .AsReadOnly();
    }

    public IReadOnlyList<IDataSourceAdapter> GetAdaptersForCapability(
        InstrumentMapping instrument,
        MarketDataType dataType)
    {
        return _adapters
            .Where(a => a.IsEnabled
                && a.Supports(instrument)
                && a.SupportedCapabilities.Contains(dataType))
            .OrderByDescending(a =>
            {
                if (_healthCache.TryGetValue(a.SourceType, out var health) && !health.IsHealthy)
                    return 0;
                return 1;
            })
            .ThenBy(a => a.SourceType)
            .ToList()
            .AsReadOnly();
    }

    public IDataSourceAdapter? GetBestAdapter(InstrumentMapping instrument)
    {
        return GetAdaptersForInstrument(instrument)
            .OrderByDescending(a =>
            {
                if (_healthCache.TryGetValue(a.SourceType, out var health) && !health.IsHealthy)
                    return 0;
                return 1;
            })
            .ThenBy(a => a.SourceType)
            .FirstOrDefault();
    }

    public IDataSourceAdapter? GetAdapter(SourceAdapterType sourceType)
    {
        return _adapters.FirstOrDefault(a => a.SourceType == sourceType && a.IsEnabled);
    }

    public Task<DataResult<IReadOnlyList<Candle>>> FetchHistoricalCandlesAsync(
        InstrumentMapping instrument,
        DateOnly from,
        DateOnly to,
        SourceAdapterType? preferredSource = null,
        SourceSelectionMode selectionMode = SourceSelectionMode.BestAvailable,
        CancellationToken cancellationToken = default)
    {
        return FetchWithFallbackAsync(
            instrument,
            MarketDataType.HistoricalCandles,
            preferredSource,
            selectionMode,
            async adapter =>
            {
                var result = await adapter.GetHistoricalCandlesAsync(instrument, from, to, cancellationToken);
                return new DataResultWrapper<IReadOnlyList<Candle>>
                {
                    Result = result,
                    SourceType = adapter.SourceType,
                    AdapterName = adapter.Name
                };
            },
            cancellationToken);
    }

    public Task<DataResult<Candle>> FetchLatestCandleAsync(
        InstrumentMapping instrument,
        SourceAdapterType? preferredSource = null,
        SourceSelectionMode selectionMode = SourceSelectionMode.BestAvailable,
        CancellationToken cancellationToken = default)
    {
        return FetchWithFallbackAsync(
            instrument,
            MarketDataType.Snapshot,
            preferredSource,
            selectionMode,
            async adapter =>
            {
                var result = await adapter.GetLatestCandleAsync(instrument, cancellationToken);
                return new DataResultWrapper<Candle>
                {
                    Result = result,
                    SourceType = adapter.SourceType,
                    AdapterName = adapter.Name
                };
            },
            cancellationToken);
    }

    public Task<DataResult<OrderBook>> FetchOrderBookAsync(
        InstrumentMapping instrument,
        SourceAdapterType? preferredSource = null,
        SourceSelectionMode selectionMode = SourceSelectionMode.BestAvailable,
        CancellationToken cancellationToken = default)
    {
        return FetchWithFallbackAsync(
            instrument,
            MarketDataType.OrderBook,
            preferredSource,
            selectionMode,
            async adapter =>
            {
                var result = await adapter.GetOrderBookAsync(instrument, cancellationToken);
                return new DataResultWrapper<OrderBook>
                {
                    Result = result,
                    SourceType = adapter.SourceType,
                    AdapterName = adapter.Name
                };
            },
            cancellationToken);
    }

    public async Task<IReadOnlyDictionary<SourceAdapterType, AdapterHealthStatus>> GetAllHealthStatusesAsync(
        CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<SourceAdapterType, AdapterHealthStatus>();

        foreach (var adapter in _adapters)
        {
            try
            {
                var health = await adapter.GetHealthAsync(cancellationToken);
                results[adapter.SourceType] = health;
                _healthCache[adapter.SourceType] = health;
            }
            catch (Exception ex)
            {
                var failedHealth = new AdapterHealthStatus
                {
                    IsHealthy = false,
                    LastError = ex.Message,
                    ConsecutiveFailures = 1
                };
                results[adapter.SourceType] = failedHealth;
                _healthCache[adapter.SourceType] = failedHealth;
            }
        }

        return results;
    }

    // --- Internal helper types and methods ---

    private sealed class DataResultWrapper<T>
    {
        public DataResult<T> Result { get; init; } = default!;
        public SourceAdapterType SourceType { get; init; }
        public string AdapterName { get; init; } = string.Empty;
    }

    /// <summary>
    /// Core fallback logic. Selects adapters based on capability, preferred source, and selection mode,
    /// then tries each in order until one succeeds.
    /// </summary>
    private async Task<DataResult<T>> FetchWithFallbackAsync<T>(
        InstrumentMapping instrument,
        MarketDataType dataType,
        SourceAdapterType? preferredSource,
        SourceSelectionMode selectionMode,
        Func<IDataSourceAdapter, Task<DataResultWrapper<T>>> fetchAction,
        CancellationToken cancellationToken)
    {
        // Get adapters that support this instrument AND this data type
        var compatibleAdapters = GetAdaptersForCapability(instrument, dataType);

        if (compatibleAdapters.Count == 0)
        {
            return DataResult<T>.Failure(new DataCollectionError2
            {
                Code = "NO_COMPATIBLE_SOURCE",
                Message = $"No enabled adapter supports {dataType} for instrument {instrument.Symbol} ({instrument.InstrumentId})",
                Retryable = false
            });
        }

        // Order adapters based on selection mode
        IDataSourceAdapter[] orderedAdapters;
        switch (selectionMode)
        {
            case SourceSelectionMode.PreferredOnly when preferredSource.HasValue:
                orderedAdapters = compatibleAdapters
                    .Where(a => a.SourceType == preferredSource.Value)
                    .ToArray();
                break;

            case SourceSelectionMode.PreferredThenFallback when preferredSource.HasValue:
                var preferred = compatibleAdapters
                    .FirstOrDefault(a => a.SourceType == preferredSource.Value);
                orderedAdapters = preferred != null
                    ? [preferred, .. compatibleAdapters.Where(a => a.SourceType != preferredSource.Value)]
                    : [.. compatibleAdapters];
                break;

            default:
                orderedAdapters = [.. compatibleAdapters];
                break;
        }

        if (orderedAdapters.Length == 0)
        {
            return DataResult<T>.Failure(new DataCollectionError2
            {
                Code = "NO_COMPATIBLE_SOURCE",
                Message = $"No enabled adapter supports {dataType} for {instrument.Symbol} (preferred: {preferredSource})",
                Retryable = false
            });
        }

        // Try each adapter
        DataResult<T>? lastResult = null;
        foreach (var adapter in orderedAdapters)
        {
            _logger.LogDebug(
                "Trying {Source} for {DataType} of {Symbol}",
                adapter.Name, dataType, instrument.Symbol);

            try
            {
                var wrapper = await fetchAction(adapter);

                if (wrapper.Result.Ok)
                {
                    _logger.LogInformation(
                        "Successfully fetched {DataType} from {Source} for {Symbol}",
                        dataType, adapter.Name, instrument.Symbol);
                    return wrapper.Result;
                }

                lastResult = wrapper.Result;
                _logger.LogWarning(
                    "Adapter {Source} failed for {Symbol} {DataType}: {Error}",
                    adapter.Name, instrument.Symbol, dataType, wrapper.Result.Error?.Message);

                // If error is not retryable, stop fallback
                if (wrapper.Result.Error != null && !wrapper.Result.Error.Retryable)
                {
                    // AUTHENTICATION_REQUIRED and similar should stop fallback
                    if (wrapper.Result.Error.Code is "AUTHENTICATION_REQUIRED"
                        or "AUTHENTICATION_FAILED"
                        or "UNSUPPORTED_CAPABILITY")
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Adapter {Source} threw exception for {Symbol} {DataType}",
                    adapter.Name, instrument.Symbol, dataType);

                lastResult = DataResult<T>.Failure(new DataCollectionError2
                {
                    Code = "SOURCE_UNAVAILABLE",
                    Message = $"Exception from {adapter.Name}: {ex.Message}",
                    Retryable = true
                });
            }
        }

        return lastResult ?? DataResult<T>.Failure(new DataCollectionError2
        {
            Code = "SOURCE_UNAVAILABLE",
            Message = $"All compatible adapters failed for {dataType} of {instrument.Symbol}",
            Retryable = false
        });
    }
}

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Interfaces.Providers;
using StrategyForge.Domain.Models;

namespace StrategyForge.Infrastructure.DataAdapters;

/// <summary>
/// Manages all registered source adapters and handles fallback logic.
/// Tries the primary source first; if it fails, tries compatible alternatives.
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

    public IDataSourceAdapter? GetBestAdapter(InstrumentMapping instrument)
    {
        return GetAdaptersForInstrument(instrument)
            .OrderByDescending(a =>
            {
                // Prefer healthy adapters
                if (_healthCache.TryGetValue(a.SourceType, out var health) && !health.IsHealthy)
                    return 0;
                return 1;
            })
            .ThenBy(a => a.SourceType) // Deterministic ordering
            .FirstOrDefault();
    }

    public IDataSourceAdapter? GetAdapter(SourceAdapterType sourceType)
    {
        return _adapters.FirstOrDefault(a => a.SourceType == sourceType && a.IsEnabled);
    }

    public async Task<DataResult<IReadOnlyList<Candle>>> FetchHistoricalCandlesAsync(
        InstrumentMapping instrument,
        DateOnly from,
        DateOnly to,
        SourceAdapterType? preferredSource = null,
        CancellationToken cancellationToken = default)
    {
        var supportedAdapters = GetAdaptersForInstrument(instrument);

        if (supportedAdapters.Count == 0)
        {
            return DataResult<IReadOnlyList<Candle>>.Failure(new DataCollectionError2
            {
                Code = "SOURCE_NOT_SUPPORTED",
                Message = $"No enabled adapters support instrument {instrument.Symbol} ({instrument.InstrumentId})",
                Retryable = false
            });
        }

        // If a preferred source is specified, try it first
        IDataSourceAdapter[] orderedAdapters;
        if (preferredSource.HasValue)
        {
            var preferred = supportedAdapters.FirstOrDefault(a => a.SourceType == preferredSource.Value);
            if (preferred != null)
            {
                orderedAdapters = [preferred, .. supportedAdapters.Where(a => a.SourceType != preferredSource.Value)];
            }
            else
            {
                orderedAdapters = [.. supportedAdapters];
                _logger.LogWarning(
                    "Preferred source {Preferred} not available for {Symbol}, using all available sources",
                    preferredSource, instrument.Symbol);
            }
        }
        else
        {
            orderedAdapters = [.. supportedAdapters];
        }

        // Try each adapter with fallback
        DataResult<IReadOnlyList<Candle>>? lastResult = null;
        foreach (var adapter in orderedAdapters)
        {
            _logger.LogDebug(
                "Trying {Source} for historical candles of {Symbol}",
                adapter.Name, instrument.Symbol);

            var result = await adapter.GetHistoricalCandlesAsync(instrument, from, to, cancellationToken);

            if (result.Ok)
            {
                _logger.LogInformation(
                    "Successfully fetched {Count} candles from {Source} for {Symbol}",
                    result.Data?.Count ?? 0, adapter.Name, instrument.Symbol);
                return result;
            }

            lastResult = result;
            _logger.LogWarning(
                "Adapter {Source} failed for {Symbol}: {Error}",
                adapter.Name, instrument.Symbol, result.Error?.Message);

            // If error is not retryable and not a source failure, don't try fallback
            if (result.Error != null && !result.Error.Retryable &&
                result.Error.Code != "SOURCE_UNAVAILABLE" &&
                result.Error.Code != "TIMEOUT")
            {
                break;
            }
        }

        return lastResult ?? DataResult<IReadOnlyList<Candle>>.Failure(new DataCollectionError2
        {
            Code = "SOURCE_UNAVAILABLE",
            Message = $"All adapters failed for {instrument.Symbol}",
            Retryable = false
        });
    }

    public async Task<DataResult<Candle>> FetchLatestCandleAsync(
        InstrumentMapping instrument,
        SourceAdapterType? preferredSource = null,
        CancellationToken cancellationToken = default)
    {
        var supportedAdapters = GetAdaptersForInstrument(instrument);

        if (supportedAdapters.Count == 0)
        {
            return DataResult<Candle>.Failure(new DataCollectionError2
            {
                Code = "SOURCE_NOT_SUPPORTED",
                Message = $"No enabled adapters support instrument {instrument.Symbol}",
                Retryable = false
            });
        }

        IDataSourceAdapter[] orderedAdapters;
        if (preferredSource.HasValue)
        {
            var preferred = supportedAdapters.FirstOrDefault(a => a.SourceType == preferredSource.Value);
            orderedAdapters = preferred != null
                ? [preferred, .. supportedAdapters.Where(a => a.SourceType != preferredSource.Value)]
                : [.. supportedAdapters];
        }
        else
        {
            orderedAdapters = [.. supportedAdapters];
        }

        DataResult<Candle>? lastResult = null;
        foreach (var adapter in orderedAdapters)
        {
            var result = await adapter.GetLatestCandleAsync(instrument, cancellationToken);

            if (result.Ok)
                return result;

            lastResult = result;

            if (result.Error != null && !result.Error.Retryable &&
                result.Error.Code != "SOURCE_UNAVAILABLE" &&
                result.Error.Code != "TIMEOUT")
            {
                break;
            }
        }

        return lastResult ?? DataResult<Candle>.Failure(new DataCollectionError2
        {
            Code = "SOURCE_UNAVAILABLE",
            Message = $"All adapters failed for {instrument.Symbol}",
            Retryable = false
        });
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
}

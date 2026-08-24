using Microsoft.Extensions.Logging;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Interfaces.Providers;
using StrategyForge.Domain.Models;

namespace StrategyForge.Api.Services;

/// <summary>
/// Orchestrates market data acquisition: source selection, identifier resolution,
/// fallback, pagination, and cross-validation. The single entry point for downstream modules.
/// </summary>
public sealed class EvidenceQueryPipeline
{
    private readonly IDataSourceRegistry _registry;
    private readonly IInstrumentResolver _resolver;
    private readonly ILogger<EvidenceQueryPipeline> _logger;

    public EvidenceQueryPipeline(
        IDataSourceRegistry registry,
        IInstrumentResolver resolver,
        ILogger<EvidenceQueryPipeline> logger)
    {
        _registry = registry;
        _resolver = resolver;
        _logger = logger;
    }

    /// <summary>
    /// Resolves an instrument query to a canonical mapping and fetches historical candles
    /// with deterministic source selection and fallback.
    /// </summary>
    public async Task<DataResult<IReadOnlyList<Candle>>> GetHistoricalCandlesAsync(
        string instrumentQuery,
        DateOnly from,
        DateOnly to,
        SourceAdapterType? preferredSource = null,
        SourceSelectionMode selectionMode = SourceSelectionMode.BestAvailable,
        CandleResolution? resolution = null,
        CancellationToken cancellationToken = default)
    {
        var instrument = await ResolveInstrumentAsync(instrumentQuery, cancellationToken);
        if (instrument == null)
        {
            return DataResult<IReadOnlyList<Candle>>.Failure(new DataCollectionError2
            {
                Code = "INSTRUMENT_NOT_FOUND",
                Message = $"No instrument found for '{instrumentQuery}'",
                Retryable = false
            });
        }

        // Validate date range
        if (from > to)
        {
            return DataResult<IReadOnlyList<Candle>>.Failure(new DataCollectionError2
            {
                Code = "INVALID_DATE_RANGE",
                Message = $"Start date ({from}) must be before or equal to end date ({to})",
                Retryable = false
            });
        }

        return await _registry.FetchHistoricalCandlesAsync(
            instrument, from, to, preferredSource, selectionMode, resolution, cancellationToken);
    }

    /// <summary>
    /// Resolves an instrument query and fetches the latest candle/snapshot.
    /// </summary>
    public async Task<DataResult<Candle>> GetSnapshotAsync(
        string instrumentQuery,
        SourceAdapterType? preferredSource = null,
        SourceSelectionMode selectionMode = SourceSelectionMode.BestAvailable,
        CancellationToken cancellationToken = default)
    {
        var instrument = await ResolveInstrumentAsync(instrumentQuery, cancellationToken);
        if (instrument == null)
        {
            return DataResult<Candle>.Failure(new DataCollectionError2
            {
                Code = "INSTRUMENT_NOT_FOUND",
                Message = $"No instrument found for '{instrumentQuery}'",
                Retryable = false
            });
        }

        return await _registry.FetchLatestCandleAsync(
            instrument, preferredSource, selectionMode, cancellationToken);
    }

    /// <summary>
    /// Resolves an instrument query and fetches order book data.
    /// </summary>
    public async Task<DataResult<OrderBook>> GetOrderBookAsync(
        string instrumentQuery,
        SourceAdapterType? preferredSource = null,
        SourceSelectionMode selectionMode = SourceSelectionMode.BestAvailable,
        CancellationToken cancellationToken = default)
    {
        var instrument = await ResolveInstrumentAsync(instrumentQuery, cancellationToken);
        if (instrument == null)
        {
            return DataResult<OrderBook>.Failure(new DataCollectionError2
            {
                Code = "INSTRUMENT_NOT_FOUND",
                Message = $"No instrument found for '{instrumentQuery}'",
                Retryable = false
            });
        }

        return await _registry.FetchOrderBookAsync(
            instrument, preferredSource, selectionMode, cancellationToken);
    }

    /// <summary>
    /// Returns all adapters that support a given instrument and data type.
    /// Useful for discovering available sources before making a request.
    /// </summary>
    public async Task<IReadOnlyList<AdapterCapabilityInfo>> DiscoverCapabilitiesAsync(
        string instrumentQuery,
        CancellationToken cancellationToken = default)
    {
        var instrument = await ResolveInstrumentAsync(instrumentQuery, cancellationToken);
        if (instrument == null)
        {
            return [];
        }

        var results = new List<AdapterCapabilityInfo>();
        foreach (var adapter in _registry.GetAllAdapters())
        {
            if (adapter.IsEnabled && adapter.Supports(instrument))
            {
                results.Add(new AdapterCapabilityInfo
                {
                    SourceType = adapter.SourceType,
                    Name = adapter.Name,
                    Capabilities = adapter.SupportedCapabilities
                });
            }
        }

        return results.AsReadOnly();
    }

    private async Task<InstrumentMapping?> ResolveInstrumentAsync(
        string query,
        CancellationToken cancellationToken)
    {
        return await _resolver.ResolveAsync(query, cancellationToken);
    }
}

/// <summary>
/// Describes which capabilities a specific adapter offers for a given instrument.
/// </summary>
public sealed record AdapterCapabilityInfo
{
    public SourceAdapterType SourceType { get; init; }
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<MarketDataType> Capabilities { get; init; } = [];
}

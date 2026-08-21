using StrategyForge.Api.Contracts;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Interfaces.Providers;
using StrategyForge.Domain.Models;

namespace StrategyForge.Api.Services;

/// <summary>
/// Application service for market data acquisition.
/// Resolves instruments, selects sources, and delegates to the registry.
/// </summary>
public sealed class MarketDataService
{
    private readonly IInstrumentResolver _resolver;
    private readonly IDataSourceRegistry _registry;

    public MarketDataService(IInstrumentResolver resolver, IDataSourceRegistry registry)
    {
        _resolver = resolver;
        _registry = registry;
    }

    public async Task<DataResultResponse<IReadOnlyList<CandleResponse>>> GetCandlesAsync(
        string instrumentQuery,
        DateOnly from,
        DateOnly to,
        SourceAdapterType? preferredSource = null,
        CancellationToken ct = default)
    {
        var instrument = await _resolver.ResolveAsync(instrumentQuery, ct);
        if (instrument == null)
        {
            return new DataResultResponse<IReadOnlyList<CandleResponse>>
            {
                Ok = false,
                Error = new ErrorDetailResponse { Code = "INSTRUMENT_NOT_FOUND", Message = $"No instrument found for '{instrumentQuery}'", Retryable = false }
            };
        }

        var result = await _registry.FetchHistoricalCandlesAsync(instrument, from, to, preferredSource, ct);
        return MapCandlesResult(result);
    }

    public async Task<DataResultResponse<CandleResponse>> GetSnapshotAsync(
        string instrumentQuery,
        SourceAdapterType? preferredSource = null,
        CancellationToken ct = default)
    {
        var instrument = await _resolver.ResolveAsync(instrumentQuery, ct);
        if (instrument == null)
        {
            return new DataResultResponse<CandleResponse>
            {
                Ok = false,
                Error = new ErrorDetailResponse { Code = "INSTRUMENT_NOT_FOUND", Message = $"No instrument found for '{instrumentQuery}'", Retryable = false }
            };
        }

        var result = await _registry.FetchLatestCandleAsync(instrument, preferredSource, ct);
        return MapSingleCandleResult(result);
    }

    private static DataResultResponse<IReadOnlyList<CandleResponse>> MapCandlesResult(
        DataResult<IReadOnlyList<Candle>> result) => new()
    {
        Ok = result.Ok,
        Data = result.Data?.Select(MapCandle).ToList().AsReadOnly(),
        Summary = result.Summary != null ? new DataMetadataResponse { Count = result.Summary.Count, Description = result.Summary.Description } : null,
        Freshness = result.Freshness != null ? new FreshnessResponse
        {
            FetchedAtUtc = result.Freshness.FetchedAtUtc,
            AgeMs = result.Freshness.AgeMs,
            MaxAllowedAgeMs = result.Freshness.MaxAllowedAgeMs,
            IsFresh = result.Freshness.IsFresh,
            IsCached = result.Freshness.IsCached
        } : null,
        Quality = result.Quality != null ? new QualityResponse
        {
            Score = result.Quality.Score,
            IsComplete = result.Quality.IsComplete,
            Flags = result.Quality.Flags == QualityFlag.None ? null : result.Quality.Flags.ToString()
        } : null,
        Warnings = result.Warnings.Select(w => new WarningResponse { Code = w.Code, Message = w.Message }).ToList().AsReadOnly(),
        Error = result.Error != null ? new ErrorDetailResponse { Code = result.Error.Code, Message = result.Error.Message, Retryable = result.Error.Retryable } : null
    };

    private static DataResultResponse<CandleResponse> MapSingleCandleResult(
        DataResult<Candle> result) => new()
    {
        Ok = result.Ok,
        Data = result.Data != null ? MapCandle(result.Data) : null,
        Freshness = result.Freshness != null ? new FreshnessResponse
        {
            FetchedAtUtc = result.Freshness.FetchedAtUtc,
            AgeMs = result.Freshness.AgeMs,
            MaxAllowedAgeMs = result.Freshness.MaxAllowedAgeMs,
            IsFresh = result.Freshness.IsFresh,
            IsCached = result.Freshness.IsCached
        } : null,
        Quality = result.Quality != null ? new QualityResponse
        {
            Score = result.Quality.Score,
            IsComplete = result.Quality.IsComplete,
            Flags = result.Quality.Flags == QualityFlag.None ? null : result.Quality.Flags.ToString()
        } : null,
        Warnings = result.Warnings.Select(w => new WarningResponse { Code = w.Code, Message = w.Message }).ToList().AsReadOnly(),
        Error = result.Error != null ? new ErrorDetailResponse { Code = result.Error.Code, Message = result.Error.Message, Retryable = result.Error.Retryable } : null
    };

    private static CandleResponse MapCandle(Candle c) => new()
    {
        Date = c.Date,
        Open = c.Open,
        High = c.High,
        Low = c.Low,
        Close = c.Close,
        Volume = c.Volume,
        Value = c.Value,
        TradeCount = c.TradeCount,
        LastPrice = c.LastPrice,
        Change = c.Change,
        ChangePercent = c.ChangePercent,
        MarketTimezone = c.MarketTimezone,
        SourceDate = c.SourceDate,
        SourceCalendar = c.SourceCalendar,
        Adjustment = c.Adjustment != null ? new AdjustmentResponse
        {
            IsAdjusted = c.Adjustment.IsAdjusted,
            Type = c.Adjustment.Type.ToString(),
            AdjustmentSource = c.Adjustment.AdjustmentSource
        } : null,
        Provenance = c.Provenance != null ? new ProvenanceResponse
        {
            Source = c.Provenance.Source.ToString(),
            SourceSymbol = c.Provenance.SourceSymbol,
            SourceInstrumentId = c.Provenance.SourceInstrumentId,
            FetchedAtUtc = c.Provenance.FetchedAtUtc,
            IsCached = c.Provenance.IsCached,
            Endpoint = c.Provenance.Endpoint
        } : null
    };
}

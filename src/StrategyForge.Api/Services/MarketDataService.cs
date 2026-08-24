using Microsoft.AspNetCore.Mvc;
using StrategyForge.Api.Contracts;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Models;

namespace StrategyForge.Api.Services;

/// <summary>
/// Application service for market data acquisition.
/// Delegates to EvidenceQueryPipeline for resolution and source selection,
/// then maps canonical results to API response contracts.
/// </summary>
public sealed class MarketDataService
{
    private readonly EvidenceQueryPipeline _pipeline;

    public MarketDataService(EvidenceQueryPipeline pipeline)
    {
        _pipeline = pipeline;
    }

    public async Task<DataResultResponse<IReadOnlyList<CandleResponse>>> GetCandlesAsync(
        string instrumentQuery,
        DateOnly from,
        DateOnly to,
        SourceAdapterType? preferredSource = null,
        SourceSelectionMode selectionMode = SourceSelectionMode.BestAvailable,
        CandleResolution? resolution = null,
        CancellationToken ct = default)
    {
        var result = await _pipeline.GetHistoricalCandlesAsync(
            instrumentQuery, from, to, preferredSource, selectionMode, resolution, ct);
        return MapCandlesResult(result);
    }

    public async Task<DataResultResponse<CandleResponse>> GetSnapshotAsync(
        string instrumentQuery,
        SourceAdapterType? preferredSource = null,
        SourceSelectionMode selectionMode = SourceSelectionMode.BestAvailable,
        CancellationToken ct = default)
    {
        var result = await _pipeline.GetSnapshotAsync(
            instrumentQuery, preferredSource, selectionMode, ct);
        return MapSingleCandleResult(result);
    }

    public async Task<DataResultResponse<OrderBookResponse>> GetOrderBookAsync(
        string instrumentQuery,
        SourceAdapterType? preferredSource = null,
        SourceSelectionMode selectionMode = SourceSelectionMode.BestAvailable,
        CancellationToken ct = default)
    {
        var result = await _pipeline.GetOrderBookAsync(
            instrumentQuery, preferredSource, selectionMode, ct);
        return MapOrderBookResult(result);
    }

    private static DataResultResponse<IReadOnlyList<CandleResponse>> MapCandlesResult(
        DataResult<IReadOnlyList<Candle>> result) => new()
    {
        Ok = result.Ok,
        Data = result.Data?.Select(c => MapCandle(c)).ToList().AsReadOnly(),
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

    private static CandleResponse MapCandle(Candle c, string? resolution = null) => new()
    {
        Date = c.Date,
        Resolution = resolution ?? c.ExtraFields?.GetValueOrDefault("resolution"),
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

    private static DataResultResponse<OrderBookResponse> MapOrderBookResult(
        DataResult<OrderBook> result) => new()
    {
        Ok = result.Ok,
        Data = result.Data != null ? new OrderBookResponse
        {
            InstrumentId = result.Data.InstrumentId,
            Timestamp = result.Data.Timestamp,
            Bids = result.Data.Bids.Select(b => new OrderBookLevelResponse
            {
                Price = b.Price,
                Quantity = b.Quantity,
                OrderCount = b.OrderCount
            }).ToList().AsReadOnly(),
            Asks = result.Data.Asks.Select(a => new OrderBookLevelResponse
            {
                Price = a.Price,
                Quantity = a.Quantity,
                OrderCount = a.OrderCount
            }).ToList().AsReadOnly(),
            BestBid = result.Data.BestBid,
            BestAsk = result.Data.BestAsk,
            MidPrice = result.Data.MidPrice,
            Spread = result.Data.Spread,
            Provenance = result.Data.Provenance != null ? new ProvenanceResponse
            {
                Source = result.Data.Provenance.Source.ToString(),
                SourceSymbol = result.Data.Provenance.SourceSymbol,
                SourceInstrumentId = result.Data.Provenance.SourceInstrumentId,
                FetchedAtUtc = result.Data.Provenance.FetchedAtUtc,
                IsCached = result.Data.Provenance.IsCached,
                Endpoint = result.Data.Provenance.Endpoint
            } : null
        } : null,
        Summary = null,
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
            Flags = result.Quality.FlagDescriptions.Count > 0 ? string.Join(", ", result.Quality.FlagDescriptions) : null
        } : null,
        Error = result.Error != null ? new ErrorDetailResponse
        {
            Code = result.Error.Code,
            Message = result.Error.Message,
            Retryable = result.Error.Retryable
        } : null,
        Warnings = result.Warnings?.Select(w => new WarningResponse { Code = w.Code, Message = w.Message }).ToList().AsReadOnly() ?? []
    };
}

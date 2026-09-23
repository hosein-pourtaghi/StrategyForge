using StrategyForge.Api.Contracts;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Interfaces.Providers;
using StrategyForge.Domain.Models;
using StrategyForge.Infrastructure.Services;

namespace StrategyForge.Api.Services;

/// <summary>
/// API-facing service for the historical processing pipeline: reads processed
/// (enriched) observations with pagination and triggers processing runs.
/// Instrument inputs accept canonical IDs or resolvable symbols; provider-specific
/// identifiers never appear in the processing API surface.
/// </summary>
public sealed class HistoricalProcessingApiService
{
    private readonly HistoricalProcessingService _processingService;
    private readonly IEnrichedDatasetStore _enrichedStore;
    private readonly IInstrumentResolver _instrumentResolver;

    public HistoricalProcessingApiService(
        HistoricalProcessingService processingService,
        IEnrichedDatasetStore enrichedStore,
        IInstrumentResolver instrumentResolver)
    {
        _processingService = processingService;
        _enrichedStore = enrichedStore;
        _instrumentResolver = instrumentResolver;
    }

    /// <summary>
    /// Reads a paginated, chronologically ordered page of processed observations.
    /// </summary>
    public async Task<EnrichedObservationsPageResponse> GetEnrichedObservationsAsync(
        string instrumentId,
        SourceAdapterType? source,
        DateOnly? from,
        DateOnly? to,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var totalCount = await _enrichedStore.CountEnrichedAsync(instrumentId, source, from, to, cancellationToken);
        var items = await _enrichedStore.GetEnrichedAsync(instrumentId, source, from, to, skip, take, cancellationToken);

        return new EnrichedObservationsPageResponse
        {
            InstrumentId = instrumentId,
            Source = source?.ToString(),
            From = from,
            To = to,
            Skip = skip,
            Take = take,
            TotalCount = totalCount,
            Items = items.Select(MapObservation).ToList()
        };
    }

    /// <summary>
    /// Triggers a processing run for a resolvable instrument query.
    /// </summary>
    public async Task<(HistoricalProcessingResponse? Response, string? Error)> ProcessAsync(
        string instrumentQuery,
        SourceAdapterType source,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var instrument = await _instrumentResolver.ResolveAsync(instrumentQuery, cancellationToken);
        if (instrument is null)
        {
            return (null, "Instrument not found.");
        }

        var request = HistoricalProcessingRequest.Create(
            instrument.InstrumentId, source, from, to);

        var result = await _processingService.ProcessAsync(request, cancellationToken);

        return (MapResult(result), null);
    }

    private static EnrichedObservationResponse MapObservation(EnrichedObservation o) => new()
    {
        InstrumentId = o.InstrumentId,
        Source = o.Source.ToString(),
        ObservationDate = o.ObservationDate,
        Open = o.Open,
        High = o.High,
        Low = o.Low,
        Close = o.Close,
        Volume = o.Volume,
        Change = o.Change,
        ChangePercent = o.ChangePercent,
        Indicators = o.Indicators,
        QualityStatus = o.QualityStatus.ToString(),
        WarningCodes = o.WarningCodes,
        ProcessedBy = o.ProcessedBy,
        ProcessedAtUtc = o.ProcessedAtUtc
    };

    private static HistoricalProcessingResponse MapResult(HistoricalProcessingResult r) => new()
    {
        Ok = r.Ok,
        InstrumentId = r.InstrumentId,
        Source = r.Source.ToString(),
        RequestedFrom = r.RequestedFrom,
        RequestedTo = r.RequestedTo,
        RowsRead = r.RowsRead,
        RowsInScope = r.RowsInScope,
        WarmupRows = r.WarmupRows,
        RowsAccepted = r.RowsAccepted,
        RowsRejected = r.RowsRejected,
        RowsWithWarnings = r.RowsWithWarnings,
        RowsWritten = r.RowsWritten,
        RowsChanged = r.RowsChanged,
        RowsUnchanged = r.RowsUnchanged,
        Duplicates = r.Duplicates,
        ValidationErrors = r.ValidationErrors,
        Warnings = r.Warnings,
        BatchesProcessed = r.BatchesProcessed,
        DurationMs = r.Duration.TotalMilliseconds,
        ReasonCounts = r.ReasonCounts,
        ErrorCode = r.ErrorCode,
        ErrorMessage = r.ErrorMessage
    };
}

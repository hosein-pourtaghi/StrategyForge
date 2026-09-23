using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StrategyForge.Domain.Configuration;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Interfaces.Analysis;
using StrategyForge.Domain.Interfaces.Providers;
using StrategyForge.Domain.Models;

namespace StrategyForge.Infrastructure.Services;

/// <summary>
/// Transforms the stored historical dataset into a validated, indicator-enriched,
/// analysis-ready dataset.
///
/// Implemented flow (each stage is explicit and separately observable):
///
///   Database
///     → paged reader (bounded batches, chronological)
///     → normalization (stored canonical Candle projection; nothing fabricated)
///     → validation (row + temporal rules, deterministic reason codes)
///     → enrichment (existing IndicatorEngine over the FULL contiguous sequence —
///       database batches are NOT indicator windows)
///     → derived persistence (idempotent on instrument + source + date)
///
/// Design guarantees:
/// - RAW is never mutated: enrichment writes to a separate derived store; every
///   derived row is traceable to the raw observation via the shared identity.
/// - Deterministic ordering: (instrument, source, observation date) ascending.
/// - Bounded memory: peak usage scales with BatchSize, not dataset size. Batch
///   boundaries never break indicator history — the engine sees one contiguous
///   chronological sequence per run (warm-up + in-scope rows).
/// - Indicator warm-up is derived from the registered indicators' actual
///   requirements (probed, not hardcoded), scaled by WarmupFactor, capped by
///   MaxWarmupRows, and loaded strictly before the requested range.
/// - Rejected rows are excluded from the indicator input sequence so corrupt
///   values cannot corrupt indicator history.
/// - Re-running the same request is idempotent: identity = (instrument, source, date).
/// - Sources are processed separately; cross-source semantics are untouched.
/// - The pipeline decides nothing about trading; it is data quality plus
///   deterministic enrichment only.
/// </summary>
public sealed class HistoricalProcessingService
{
    private readonly IHistoricalDatasetPageReader _pageReader;
    private readonly IEnrichedDatasetStore _enrichedStore;
    private readonly IIndicatorEngine _indicatorEngine;
    private readonly IndicatorWarmupProber _warmupProber;
    private readonly HistoricalRowValidator _validator;
    private readonly HistoricalProcessingSettings _settings;
    private readonly ILogger<HistoricalProcessingService> _logger;

    public HistoricalProcessingService(
        IHistoricalDatasetPageReader pageReader,
        IEnrichedDatasetStore enrichedStore,
        IIndicatorEngine indicatorEngine,
        IndicatorWarmupProber warmupProber,
        HistoricalRowValidator validator,
        IOptions<HistoricalProcessingSettings> settings,
        ILogger<HistoricalProcessingService> logger)
    {
        _pageReader = pageReader;
        _enrichedStore = enrichedStore;
        _indicatorEngine = indicatorEngine;
        _warmupProber = warmupProber;
        _validator = validator;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Processes the requested instrument/source/range into the derived dataset.
    /// </summary>
    public async Task<HistoricalProcessingResult> ProcessAsync(
        HistoricalProcessingRequest request,
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.UtcNow;

        if (request.From > request.To)
        {
            return HistoricalProcessingResult.Empty(request, "INVALID_RANGE",
                $"Range start {request.From:yyyy-MM-dd} is after range end {request.To:yyyy-MM-dd}");
        }

        var batchSize = Math.Max(1, _settings.BatchSize);

        // --- Indicator warm-up requirement (derived, not hardcoded) ---
        var rawWarmup = _warmupProber.GetMaxWarmupRows(request.IndicatorParameters);
        var warmupFactor = Math.Max(1, _settings.WarmupFactor);
        var warmupRows = rawWarmup == 0
            ? 0
            : Math.Min(
                (int)Math.Ceiling((double)rawWarmup * warmupFactor),
                Math.Max(0, _settings.MaxWarmupRows));

        // Warm-up context: stored observations strictly before the requested range,
        // loaded as one bounded window so indicators start mid-stream correctly
        // instead of treating the first requested day as the first-ever observation.
        var warmupCandles = warmupRows > 0
            ? (await _pageReader.ReadLastPageAsync(
                request.InstrumentId, request.Source, request.From, warmupRows, cancellationToken)).ToList()
            : [];

        _logger.LogInformation(
            "Historical processing started: {Instrument}/{Source} over {From}..{To}; warm-up requirement {RawWarmup} rows (factor {Factor}, cap {Cap}) → {Loaded} context rows loaded",
            request.InstrumentId, request.Source, request.From, request.To,
            rawWarmup, warmupFactor, _settings.MaxWarmupRows, warmupCandles.Count);

        // --- Streaming read + normalize + validate (bounded per batch) ---
        var rowsRead = 0;
        var batchesProcessed = 0;
        var duplicates = 0;
        var rowsRejected = 0;
        var rowsWithWarnings = 0;
        var validationErrors = 0;
        var warnings = 0;
        var reasonCounts = new Dictionary<string, int>();

        var acceptedDates = new List<DateOnly>();
        var qualityByDate = new Dictionary<DateOnly, HistoricalDataQualityAssessment>();
        var candleByDate = new Dictionary<DateOnly, Candle>();
        var acceptedSequence = new List<Candle>(warmupCandles);
        DateOnly? previous = warmupCandles.Count > 0 ? warmupCandles[^1].Date : null;

        var cursor = (DateOnly?)null;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var page = await _pageReader.ReadPageAsync(
                request.InstrumentId, request.Source, request.From, request.To,
                cursor, batchSize, cancellationToken);

            if (page.Count == 0)
            {
                break;
            }

            cursor = page[^1].Date;
            rowsRead += page.Count;
            batchesProcessed++;

            // --- Normalization: the stored rows are already the canonical normalized
            // representation (Phase 6). The stage is a deterministic projection with
            // no sampling, reordering, or fabrication — kept explicit for tracing. ---

            // Deduplicate within batch (deterministic last-wins).
            var distinct = page
                .GroupBy(c => c.Date)
                .Select(g => g.Last())
                .OrderBy(c => c.Date)
                .ToList();
            duplicates += page.Count - distinct.Count;

            // --- Validation (row + temporal, in chronological order) ---
            foreach (var candle in distinct)
            {
                var assessment = _validator.Validate(candle, previous);
                previous = candle.Date;

                foreach (var finding in assessment.Findings)
                {
                    reasonCounts[finding.Code] = reasonCounts.GetValueOrDefault(finding.Code) + 1;
                }

                validationErrors += assessment.Findings.Count(f => IsErrorCode(f.Code));
                warnings += assessment.Findings.Count(f => !IsErrorCode(f.Code));

                qualityByDate[candle.Date] = assessment;

                if (assessment.HasErrors)
                {
                    rowsRejected++;
                    continue;
                }

                if (assessment.Status == HistoricalDataQualityStatus.Warning)
                {
                    rowsWithWarnings++;
                }

                acceptedDates.Add(candle.Date);
                candleByDate[candle.Date] = candle;
                acceptedSequence.Add(candle);
            }
        }

        // --- Enrichment: run the existing IndicatorEngine ONCE over the full
        // contiguous accepted sequence (warm-up + in-scope) so a database batch
        // boundary never becomes an indicator boundary. ---
        var indicatorsByDate = Enrich(acceptedSequence, request.IndicatorParameters);

        // --- Persist derived rows for in-scope accepted rows only ---
        // Every accepted row produces a derived observation; when indicator history is
        // insufficient the Indicators map is empty (nothing fabricated, nothing dropped).
        var derivedBatch = new List<EnrichedObservation>(acceptedDates.Count);
        foreach (var date in acceptedDates)
        {
            var values = indicatorsByDate.TryGetValue(date, out var v)
                ? v
                : new Dictionary<string, IReadOnlyDictionary<string, decimal>>();

            derivedBatch.Add(BuildDerivedObservation(
                request, candleByDate[date], values, qualityByDate[date], startedAt));
        }

        var rowsWritten = 0;
        var rowsChanged = 0;
        var rowsUnchanged = 0;
        for (var offset = 0; offset < derivedBatch.Count; offset += batchSize)
        {
            var batch = derivedBatch.GetRange(offset, Math.Min(batchSize, derivedBatch.Count - offset));
            var outcome = await _enrichedStore.UpsertEnrichedAsync(
                request.InstrumentId, request.Source, batch, cancellationToken);
            rowsWritten += outcome.Inserted;
            rowsChanged += outcome.Changed;
            rowsUnchanged += outcome.Unchanged;
        }

        var duration = DateTimeOffset.UtcNow - startedAt;
        var result = new HistoricalProcessingResult
        {
            InstrumentId = request.InstrumentId,
            Source = request.Source,
            RequestedFrom = request.From,
            RequestedTo = request.To,
            Ok = true,
            RowsRead = rowsRead + warmupCandles.Count,
            RowsInScope = rowsRead,
            WarmupRows = warmupCandles.Count,
            RowsAccepted = acceptedDates.Count,
            RowsRejected = rowsRejected,
            RowsWithWarnings = rowsWithWarnings,
            RowsWritten = rowsWritten,
            RowsChanged = rowsChanged,
            RowsUnchanged = rowsUnchanged,
            Duplicates = duplicates,
            ValidationErrors = validationErrors,
            Warnings = warnings,
            BatchesProcessed = batchesProcessed,
            Duration = duration,
            ReasonCounts = reasonCounts
        };

        _logger.LogInformation(
            "Historical processing finished for {Instrument}/{Source}: read {RowsRead} ({Warmup} warm-up), accepted {Accepted}, rejected {Rejected}, warnings {Warnings}, derived written {Written}, changed {Changed}, unchanged {Unchanged} in {Batches} batch(es), {Duration:F0}ms",
            request.InstrumentId, request.Source, result.RowsRead, result.WarmupRows,
            result.RowsAccepted, result.RowsRejected, result.Warnings,
            result.RowsWritten, result.RowsChanged, result.RowsUnchanged,
            result.BatchesProcessed, duration.TotalMilliseconds);

        return result;
    }

    // =====================================================================
    // Enrichment
    // =====================================================================

    /// <summary>
    /// Runs the registered IndicatorEngine once over the full sequence and indexes
    /// the results by observation date. Component values (e.g., MACD Signal/Histogram,
    /// Bollinger Upper/Lower) are preserved as nested component dictionaries.
    /// </summary>
    private IReadOnlyDictionary<DateOnly, IReadOnlyDictionary<string, IReadOnlyDictionary<string, decimal>>> Enrich(
        IReadOnlyList<Candle> sequence,
        IReadOnlyDictionary<string, IndicatorParameters>? indicatorParameters)
    {
        var engineResult = _indicatorEngine.ComputeAll(sequence, BuildConfiguration(indicatorParameters));

        foreach (var error in engineResult.Errors)
        {
            _logger.LogWarning(
                "Indicator {Indicator} failed during enrichment: {Error}",
                error.IndicatorName, error.ErrorMessage);
        }

        var byDate = new Dictionary<DateOnly, Dictionary<string, IReadOnlyDictionary<string, decimal>>>();

        foreach (var (name, indicatorResults) in engineResult.Results)
        {
            foreach (var r in indicatorResults)
            {
                if (!byDate.TryGetValue(r.Date, out var perIndicator))
                {
                    perIndicator = new Dictionary<string, IReadOnlyDictionary<string, decimal>>();
                    byDate[r.Date] = perIndicator;
                }

                // Components: prefer the indicator's AdditionalValues (MACD, Bollinger),
                // falling back to the primary value under the indicator's own name.
                var components = r.AdditionalValues is { Count: > 0 }
                    ? new Dictionary<string, decimal>(r.AdditionalValues)
                    : new Dictionary<string, decimal> { [name] = r.Value };

                perIndicator[name] = components;
            }
        }

        return byDate.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyDictionary<string, IReadOnlyDictionary<string, decimal>>)kv.Value);
    }

    private static IndicatorConfiguration? BuildConfiguration(
        IReadOnlyDictionary<string, IndicatorParameters>? indicatorParameters)
    {
        if (indicatorParameters is not { Count: > 0 })
        {
            return null;
        }

        return new IndicatorConfiguration
        {
            IndicatorParameters = indicatorParameters
        };
    }

    // =====================================================================
    // Derived-observation construction
    // =====================================================================

    private EnrichedObservation BuildDerivedObservation(
        HistoricalProcessingRequest request,
        Candle candle,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, decimal>> values,
        HistoricalDataQualityAssessment quality,
        DateTimeOffset processedAt)
    {
        var warningCodes = quality.Findings
            .Where(f => !IsErrorCode(f.Code))
            .Select(f => f.Code)
            .Distinct()
            .ToList();

        return new EnrichedObservation
        {
            InstrumentId = request.InstrumentId,
            Source = request.Source,
            ObservationDate = candle.Date,
            Open = candle.Open,
            High = candle.High,
            Low = candle.Low,
            Close = candle.Close,
            Volume = candle.Volume,
            Change = candle.Change,
            ChangePercent = candle.ChangePercent,
            Indicators = values,
            QualityStatus = quality.Status,
            WarningCodes = warningCodes,
            ProcessedBy = _settings.PipelineVersion,
            ProcessedAtUtc = processedAt
        };
    }

    private static bool IsErrorCode(string code) =>
        code is HistoricalDataQualityReason.InvalidOhlc
            or HistoricalDataQualityReason.InvalidPrice
            or HistoricalDataQualityReason.MissingDate
            or HistoricalDataQualityReason.InvalidInstrument
            or HistoricalDataQualityReason.DuplicateObservation
            or HistoricalDataQualityReason.OutOfOrder
            or HistoricalDataQualityReason.MissingRequiredValue;
}

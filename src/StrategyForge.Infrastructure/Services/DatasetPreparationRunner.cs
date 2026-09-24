using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StrategyForge.Domain.Configuration;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Interfaces.Providers;
using StrategyForge.Domain.Models;

namespace StrategyForge.Infrastructure.Services;

/// <summary>
/// Composes the existing Phase 6/7/9 services into one end-to-end dataset
/// preparation run for a canonical instrument:
///
///   Provider → HistoricalIngestionService → HistoricalDatasetStore   (Phase 6)
///     → HistoricalProcessingService → EnrichedDatasetStore           (Phase 7)
///     → DatasetReportsService (coverage + quality)                   (Phase 9)
///     → DatasetPreparationService → JSONL + manifest                 (Phase 9)
///
/// This is orchestration glue only: it never calls providers directly, never
/// recomputes indicators, and never mutates raw or enriched datasets.
/// </summary>
public sealed class DatasetPreparationRunner
{
    private readonly IInstrumentResolver _instrumentResolver;
    private readonly HistoricalIngestionService _ingestionService;
    private readonly HistoricalProcessingService _processingService;
    private readonly DatasetReportsService _reportsService;
    private readonly DatasetPreparationService _preparationService;
    private readonly DatasetPreparationSettings _settings;
    private readonly ILogger<DatasetPreparationRunner> _logger;

    public DatasetPreparationRunner(
        IInstrumentResolver instrumentResolver,
        HistoricalIngestionService ingestionService,
        HistoricalProcessingService processingService,
        DatasetReportsService reportsService,
        DatasetPreparationService preparationService,
        IOptions<DatasetPreparationSettings> settings,
        ILogger<DatasetPreparationRunner> logger)
    {
        _instrumentResolver = instrumentResolver;
        _ingestionService = ingestionService;
        _processingService = processingService;
        _reportsService = reportsService;
        _preparationService = preparationService;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Runs the full pipeline for one instrument/source: ingest from the
    /// provider, process/enrich, build coverage + quality reports, then
    /// project and stream the AI-ready dataset as JSONL.
    /// </summary>
    /// <param name="instrumentIdentifier">Canonical ID, symbol, or source identifier of the instrument.</param>
    /// <param name="source">Provider source. Sources are never merged.</param>
    /// <param name="from">Inclusive observation-date range start.</param>
    /// <param name="to">Inclusive observation-date range end.</param>
    /// <param name="output">Destination stream for the JSONL records.</param>
    /// <param name="manifestOutput">Optional destination for the JSON manifest (written after the records).</param>
    /// <param name="skipIngestion">Skip the provider import (data already stored — idempotent re-runs).</param>
    public async Task<DatasetPreparationRunResult> RunAsync(
        string instrumentIdentifier,
        SourceAdapterType source,
        DateOnly from,
        DateOnly to,
        Stream output,
        Stream? manifestOutput = null,
        bool skipIngestion = false,
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.UtcNow;

        var instrument = await _instrumentResolver.ResolveAsync(instrumentIdentifier, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Instrument '{instrumentIdentifier}' could not be resolved. " +
                "Only instruments that exist in the instrument mapping can be used.");

        // --- Phase 6: provider → registry → ingestion → raw dataset store ---
        HistoricalDatasetImportResult? import = null;
        if (!skipIngestion)
        {
            _logger.LogInformation(
                "Phase 6 ingestion: {Instrument} from {Source} over {From}..{To}",
                instrument.InstrumentId, source, from, to);

            import = await _ingestionService.ImportAsync(new HistoricalDatasetRequest
            {
                Instrument = instrument,
                Source = source,
                From = from,
                To = to
            }, cancellationToken);

            if (!import.Ok)
            {
                _logger.LogWarning(
                    "Ingestion did not complete cleanly: {ErrorCode} {ErrorMessage}",
                    import.ErrorCode, import.ErrorMessage);
            }
        }

        // --- Phase 7: raw → validate → enrich → derived dataset store ---
        _logger.LogInformation("Phase 7 processing: {Instrument} from {Source}", instrument.InstrumentId, source);
        var processed = await _processingService.ProcessAsync(new HistoricalProcessingRequest
        {
            InstrumentId = instrument.InstrumentId,
            Source = source,
            From = from,
            To = to
        }, cancellationToken);

        // --- Phase 9: coverage + quality reports over the stored data ---
        var combinations = new List<(string, SourceAdapterType)> { (instrument.InstrumentId, source) };
        var coverage = await _reportsService.BuildCoverageReportAsync(combinations, from, to, cancellationToken);
        var quality = await _reportsService.BuildQualityReportAsync(combinations, from, to, cancellationToken);

        // Aggregate quality entries; one combination was requested so a summary
        // is built from the per-combination entries (empty-safe for the manifest).
        var qualitySummary = quality.Entries.Count > 0
            ? quality.Entries[0]
            : new DatasetQualitySummary
            {
                InstrumentId = instrument.InstrumentId,
                Source = source,
                From = from,
                To = to,
                TotalRows = 0,
                ValidRows = 0,
                WarningRows = 0,
                InvalidRows = 0,
                InvalidOhlcRows = 0,
                InvalidPriceRows = 0,
                DuplicateRows = 0,
                OutOfOrderRows = 0,
                MissingDateRows = 0,
                DateGapWarnings = 0,
                ZeroVolumeRows = 0,
                MissingOptionalFieldWarnings = 0,
                IncompleteProvenanceWarnings = 0,
                ReasonCounts = new Dictionary<string, int>()
            };

        // --- Phase 9: projection + streaming JSONL export ---
        await using (var stream = output)
        {
            var preparation = await _preparationService.PrepareAsync(
                new AiReadyDatasetRequest
                {
                    InstrumentId = instrument.InstrumentId,
                    Source = source,
                    From = from,
                    To = to
                },
                stream,
                new AiReadyJsonlExporter(),
                new DatasetStatisticsAccumulator(),
                qualitySummary,
                coverage.Entries.Count > 0
                    ? coverage.Entries[0]
                    : new DatasetCoverageEntry
                    {
                        InstrumentId = instrument.InstrumentId,
                        Source = source,
                        FirstObservation = null,
                        LastObservation = null,
                        ObservationCount = 0,
                        GapCount = 0,
                        LargestGapDays = 0,
                        AverageObservationsPerYear = 0m
                    },
                cancellationToken: cancellationToken);

            // --- Manifest (metadata only; never inside data rows) ---
            if (manifestOutput is not null)
            {
                await WriteManifestAsync(manifestOutput, preparation, coverage, cancellationToken);
            }

            var duration = DateTimeOffset.UtcNow - startedAt;

            _logger.LogInformation(
                "Dataset preparation run complete: {DatasetId} — {Rows} AI-ready records in {Duration:F0}ms",
                preparation.DatasetId, preparation.ProjectedRows, duration.TotalMilliseconds);

            return new DatasetPreparationRunResult
            {
                InstrumentId = instrument.InstrumentId,
                Source = source,
                From = from,
                To = to,
                Import = import,
                Processing = processed,
                Coverage = coverage,
                Quality = quality,
                Preparation = preparation,
                Duration = duration
            };
        }
    }

    /// <summary>
    /// Writes the dataset manifest as JSON. Metadata only: identity, scope,
    /// feature policy, temporal configuration, split boundaries, quality.
    /// CreatedAtUtc is export metadata and is deliberately excluded from data rows.
    /// </summary>
    private async Task WriteManifestAsync(
        Stream manifestOutput,
        AiReadyDatasetResult preparation,
        DatasetCoverageReport coverage,
        CancellationToken cancellationToken)
    {
        var manifest = new AiReadyDatasetManifest
        {
            DatasetId = preparation.DatasetId,
            DatasetVersion = preparation.DatasetVersion,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            InstrumentIds = [preparation.InstrumentId],
            Sources = [preparation.Source.ToString()],
            From = preparation.From,
            To = preparation.To,
            Coverage = coverage.Entries,
            RowCount = preparation.ProjectedRows,
            FeatureSet = AiReadyFeaturePolicy.IncludedFeatures,
            ContextWindowObservations = preparation.ContextWindowObservations,
            ForwardHorizons = preparation.ForwardHorizons,
            SplitSegments = preparation.SplitSegments,
            QualitySummary = new DatasetQualityReport
            {
                From = preparation.From,
                To = preparation.To,
                Entries = [preparation.Quality]
            },
            PipelineVersions = new Dictionary<string, string>
            {
                ["processing"] = PipelineVersions.Enriched,
                ["preparation"] = PipelineVersions.Preparation
            }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(
            manifest,
            new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                Converters = { new DeterministicDecimalJsonConverter() }
            });

        var bytes = System.Text.Encoding.UTF8.GetBytes(json + "\n");
        await manifestOutput.WriteAsync(bytes, cancellationToken);
    }
}

/// <summary>Deterministic result of one end-to-end dataset preparation run.</summary>
public sealed record DatasetPreparationRunResult
{
    public required string InstrumentId { get; init; }
    public required SourceAdapterType Source { get; init; }
    public required DateOnly From { get; init; }
    public required DateOnly To { get; init; }

    /// <summary>Phase 6 import result; null when ingestion was skipped.</summary>
    public HistoricalDatasetImportResult? Import { get; init; }

    /// <summary>Phase 7 processing result.</summary>
    public required HistoricalProcessingResult Processing { get; init; }

    public required DatasetCoverageReport Coverage { get; init; }
    public required DatasetQualityReport Quality { get; init; }
    public required AiReadyDatasetResult Preparation { get; init; }
    public required TimeSpan Duration { get; init; }
}

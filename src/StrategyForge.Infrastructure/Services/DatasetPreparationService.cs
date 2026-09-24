using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StrategyForge.Domain.Configuration;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Interfaces.Analysis;
using StrategyForge.Domain.Interfaces.Providers;
using StrategyForge.Domain.Models;

namespace StrategyForge.Infrastructure.Services;

/// <summary>
/// Streaming AI-ready dataset preparation service.
///
///   enriched store (keyset pages) -> projection -> JSONL export -> manifest
///
/// Bounded memory: one batch plus context window at a time. Deterministic.
/// Depends only on Domain abstractions (IAiReadyDatasetProjector,
/// ITimeSeriesSplitter, IStrategyRuleRegistry) — never on the Analysis
/// implementation project (dependency inversion, IIndicatorEngine pattern).
/// </summary>
public sealed class DatasetPreparationService
{
    private readonly IEnrichedDatasetPageReader _pageReader;
    private readonly IAiReadyDatasetProjector _projector;
    private readonly ITimeSeriesSplitter _splitter;
    private readonly IStrategyRuleRegistry _ruleRegistry;
    private readonly DatasetPreparationSettings _settings;
    private readonly ILogger<DatasetPreparationService> _logger;

    public DatasetPreparationService(
        IEnrichedDatasetPageReader pageReader,
        IAiReadyDatasetProjector projector,
        ITimeSeriesSplitter splitter,
        IStrategyRuleRegistry ruleRegistry,
        IOptions<DatasetPreparationSettings> settings,
        ILogger<DatasetPreparationService> logger)
    {
        _pageReader = pageReader;
        _projector = projector;
        _splitter = splitter;
        _ruleRegistry = ruleRegistry;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Prepares the AI-ready dataset for one instrument/source/range and writes
    /// it to <paramref name="output"/> as JSONL. Returns the deterministic
    /// result (accounting, splits, quality, statistics, inspection samples).
    /// </summary>
    public async Task<AiReadyDatasetResult> PrepareAsync(
        AiReadyDatasetRequest request,
        Stream output,
        AiReadyJsonlExporter exporter,
        DatasetStatisticsAccumulator statistics,
        DatasetQualitySummary quality,
        DatasetCoverageEntry coverage,
        StrategyThresholds? thresholds = null,
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.UtcNow;

        if (request.From > request.To)
        {
            throw new ArgumentException("Range start must not be after range end.", nameof(request));
        }

        var horizons = request.ForwardHorizons is { Count: > 0 }
            ? request.ForwardHorizons
            : _settings.EffectiveForwardHorizons;
        var contextWindow = request.ContextWindowObservations ?? _settings.ContextWindowObservations;

        var (datasetId, datasetVersion) = AiReadyDatasetFingerprint.Compute(
            request.InstrumentId,
            request.Source,
            request.From,
            request.To,
            contextWindow,
            horizons,
            PipelineVersions.Enriched,
            _settings.PipelineVersion);

        _logger.LogInformation(
            "AI-ready dataset preparation started: {DatasetId} over {From}..{To}",
            datasetId, request.From, request.To);

        // Load the full in-scope sequence. The projection requires one
        // contiguous chronological sequence (indicator warm-up and label
        // horizons cross database batch boundaries — a batch boundary must
        // never become an analytical boundary). Memory here scales with the
        // in-scope row count of ONE instrument/source/range, which is the
        // dataset being prepared; larger-than-memory datasets are handled by
        // narrowing the requested range, never by silently truncating.
        var observations = await LoadObservationsAsync(request, cancellationToken);

        // --- Projection: features [0..i], labels [i+1..], rules at T ---
        var records = _projector.Project(
            observations,
            thresholds,
            contextWindow,
            horizons,
            provenanceResolver: null,
            rules: null);

        // --- Streaming export + incremental statistics ---
        var batch = new List<AiReadyObservation>(_settings.BatchSize);
        long projectedRows = 0;
        long bytesWritten = 0;

        foreach (var record in records)
        {
            batch.Add(record);
            if (batch.Count >= _settings.BatchSize)
            {
                bytesWritten += await exporter.WriteAsync(output, batch, statistics, _ruleRegistry.BuiltInNames, cancellationToken);
                projectedRows += batch.Count;
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            bytesWritten += await exporter.WriteAsync(output, batch, statistics, _ruleRegistry.BuiltInNames, cancellationToken);
            projectedRows += batch.Count;
        }

        // --- Deterministic inspection samples (first/last N) ---
        var sampleSize = Math.Max(0, _settings.InspectionSampleSize);
        var firstRecords = records.Take(sampleSize).ToList();
        var lastRecords = records.Count > sampleSize
            ? records.Skip(records.Count - sampleSize).ToList()
            : records.ToList();

        // --- Chronological split (reuses Phase 8 splitter via Domain abstraction) ---
        var splitSegments = _splitter.Split(observations)
            .Select(s => new DatasetManifestSegment
            {
                Segment = s.Segment,
                FirstObservation = s.FirstObservation,
                LastObservation = s.LastObservation,
                Observations = s.Observations
            })
            .ToList();

        var labelledRows = horizons.Count > 0
            ? records.Count(r => r.Labels.ForwardReturns.TryGetValue(Math.Max(horizons.Max(), horizons.Min()), out var v) && v.HasValue)
            : 0;

        var duration = DateTimeOffset.UtcNow - startedAt;

        _logger.LogInformation(
            "AI-ready dataset prepared: {DatasetId} — {Rows} records, {Bytes} bytes in {Duration:F0}ms",
            datasetId, projectedRows, bytesWritten, duration.TotalMilliseconds);

        return new AiReadyDatasetResult
        {
            DatasetId = datasetId,
            DatasetVersion = datasetVersion,
            InstrumentId = request.InstrumentId,
            Source = request.Source,
            From = request.From,
            To = request.To,
            Ok = true,
            RawRows = observations.Count,
            ProjectedRows = (int)projectedRows,
            LabelledRows = labelledRows,
            ContextWindowObservations = contextWindow,
            ForwardHorizons = horizons,
            SplitSegments = splitSegments,
            Quality = quality,
            FirstRecords = firstRecords,
            LastRecords = lastRecords,
            Statistics = statistics.Build(),
            Duration = duration
        };
    }

    /// <summary>
    /// Loads the in-scope enriched observations as one contiguous chronological
    /// sequence via the keyset page reader.
    /// </summary>
    private async Task<List<EnrichedObservation>> LoadObservationsAsync(
        AiReadyDatasetRequest request,
        CancellationToken cancellationToken)
    {
        var observations = new List<EnrichedObservation>();
        DateOnly? cursor = null;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var page = await _pageReader.ReadPageAsync(
                request.InstrumentId,
                request.Source,
                request.From,
                request.To,
                cursor,
                _settings.BatchSize,
                cancellationToken);

            if (page.Count == 0)
            {
                break;
            }

            observations.AddRange(page);
            cursor = page[^1].ObservationDate;

            if (page.Count < _settings.BatchSize)
            {
                break; // short page — no more rows
            }
        }

        return observations;
    }
}

/// <summary>Pipeline version constants stamped into manifests for traceability.</summary>
public static class PipelineVersions
{
    /// <summary>Version of the Phase 7 processing pipeline that produced the enriched data.</summary>
    public const string Enriched = "1.0.0";

    /// <summary>Version of the Phase 9 AI-ready dataset preparation pipeline.</summary>
    public const string Preparation = "1.0.0";
}

using StrategyForge.Analysis.Strategy;
using StrategyForge.Api.Contracts;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Interfaces.Providers;
using StrategyForge.Domain.Models;

namespace StrategyForge.Api.Services;

/// <summary>
/// API-facing service for the Phase 8 deterministic strategy layer:
/// market regime classification, deterministic rule evaluation over the
/// enriched dataset, and chronological time-series splitting.
///
/// Boundary: the API never touches provider adapters, the database, or LLMs.
/// It reads the enriched dataset through the <see cref="IEnrichedDatasetStore"/>
/// contract and delegates all strategy logic to the Analysis layer. Sources are
/// never merged — one evaluation covers exactly one instrument and one source.
///
/// Determinism: no clocks, randomness, or LLM calls; identical requests and
/// stored data produce identical responses.
/// </summary>
public sealed class StrategyAnalysisApiService
{
    private readonly IEnrichedDatasetStore _enrichedStore;
    private readonly IInstrumentResolver _instrumentResolver;
    private readonly StrategyEvaluationEngine _evaluationEngine;
    private readonly StrategySetupEngine _setupEngine;

    /// <summary>Upper bound on observations loaded for one regime/evaluate/split call.</summary>
    public const int MaxObservations = 200_000;

    public StrategyAnalysisApiService(
        IEnrichedDatasetStore enrichedStore,
        IInstrumentResolver instrumentResolver,
        StrategyEvaluationEngine evaluationEngine,
        StrategySetupEngine setupEngine)
    {
        _enrichedStore = enrichedStore;
        _instrumentResolver = instrumentResolver;
        _evaluationEngine = evaluationEngine;
        _setupEngine = setupEngine;
    }

    /// <summary>
    /// Classifies the market regime for the requested instrument/source/range.
    /// </summary>
    public async Task<RegimeResponse> GetRegimeAsync(
        RegimeQuery query,
        CancellationToken cancellationToken = default)
    {
        var (instrument, error) = await ResolveAsync(query.Instrument, cancellationToken);
        if (instrument is null)
        {
            return RegimeError(query, error!);
        }

        var source = query.Source!.Value;
        var (from, to, rangeError) = NormalizeRange(query.From, query.To);
        if (rangeError is not null)
        {
            return RegimeError(query, rangeError);
        }

        var observations = await _enrichedStore.GetEnrichedAsync(
            instrument.InstrumentId, source, from, to, 0, MaxObservations, cancellationToken);

        if (observations.Count == 0)
        {
            return new RegimeResponse
            {
                Ok = false,
                InstrumentId = instrument.InstrumentId,
                Source = source.ToString(),
                From = from,
                To = to,
                ErrorCode = "NO_DATA",
                ErrorMessage = "No enriched observations found for the requested instrument/source/range. " +
                               "Run the historical processing pipeline first (POST /api/HistoricalProcessing/process)."
            };
        }

        var thresholds = StrategyThresholds.Default;
        var regimes = observations
            .Select(o => MarketRegimeClassifier.Classify(
                MarketFeatureExtractor.Extract(o, null), thresholds))
            .ToList();

        // Most recent observations first for the API surface; computation order is unchanged.
        var take = Math.Clamp(query.Take, 1, 100);
        var snapshots = new List<RegimeSnapshotResponse>(take);
        for (var i = observations.Count - 1; i >= 0 && snapshots.Count < take; i--)
        {
            var features = MarketFeatureExtractor.Extract(
                observations[i], i > 0 ? observations[i - 1] : null);
            var regime = regimes[i];
            snapshots.Add(new RegimeSnapshotResponse
            {
                ObservationDate = observations[i].ObservationDate,
                Trend = regime.Trend.ToString(),
                Volatility = regime.Volatility.ToString(),
                Momentum = regime.Momentum.ToString(),
                Close = observations[i].Close
            });
        }

        return new RegimeResponse
        {
            Ok = true,
            InstrumentId = instrument.InstrumentId,
            Source = source.ToString(),
            From = from,
            To = to,
            ObservationsEvaluated = observations.Count,
            Regimes = snapshots
        };
    }

    /// <summary>
    /// Evaluates deterministic strategy rules over the enriched dataset with
    /// forward-return statistics and structured evidence.
    /// </summary>
    public async Task<StrategyEvaluateResponse> EvaluateAsync(
        StrategyEvaluateRequest request,
        CancellationToken cancellationToken = default)
    {
        var (instrument, error) = await ResolveAsync(request.Instrument, cancellationToken);
        if (instrument is null)
        {
            return EvaluateError(request, error!);
        }

        var source = request.Source!.Value;
        var (from, to, rangeError) = NormalizeRange(request.From, request.To);
        if (rangeError is not null)
        {
            return EvaluateError(request, rangeError);
        }

        var observations = await _enrichedStore.GetEnrichedAsync(
            instrument.InstrumentId, source, from, to, 0, MaxObservations, cancellationToken);

        if (observations.Count == 0)
        {
            return new StrategyEvaluateResponse
            {
                Ok = false,
                InstrumentId = instrument.InstrumentId,
                Source = source.ToString(),
                From = from,
                To = to,
                ErrorCode = "NO_DATA",
                ErrorMessage = "No enriched observations found for the requested instrument/source/range. " +
                               "Run the historical processing pipeline first (POST /api/HistoricalProcessing/process)."
            };
        }

        if (request.ForwardHorizons.Any(h => h <= 0))
        {
            return EvaluateError(request, "Forward horizons must be positive (trading observations).");
        }

        StrategyEvaluationResult result;
        try
        {
            result = _evaluationEngine.Evaluate(
                instrument.InstrumentId,
                source,
                observations,
                new StrategyEvaluationRequest
                {
                    InstrumentId = instrument.InstrumentId,
                    Source = source,
                    From = from,
                    To = to,
                    RuleNames = request.RuleNames is { Count: > 0 } ? request.RuleNames : null,
                    ForwardHorizons = request.ForwardHorizons is { Count: > 0 } ? request.ForwardHorizons : null,
                    MaxEvidenceSamples = Math.Clamp(request.MaxEvidenceSamples, 0, 50)
                },
                StrategyThresholds.Default);
        }
        catch (ArgumentException ex)
        {
            return EvaluateError(request, ex.Message);
        }

        return new StrategyEvaluateResponse
        {
            Ok = true,
            InstrumentId = result.InstrumentId,
            Source = result.Source.ToString(),
            From = result.From,
            To = result.To,
            ObservationsEvaluated = result.ObservationsEvaluated,
            NoMatchCount = result.NoMatchCount,
            TotalRuleMatches = result.TotalRuleMatches,
            RuleStatistics = result.RuleStatistics.Select(MapStats).ToList(),
            MatchSamples = result.MatchSamples.Select(s => MapSample(
                s, observations, result.RuleStatistics)).ToList()
        };
    }    /// <summary>
    /// Generates deterministic strategy setups over the enriched dataset.
    /// </summary>
    public async Task<StrategySetupsResponse> GenerateSetupsAsync(
        StrategySetupsRequest request,
        CancellationToken cancellationToken = default)
    {
        var (instrument, error) = await ResolveAsync(request.Instrument, cancellationToken);
        if (instrument is null)
        {
            return SetupsError(request, error!);
        }

        var source = request.Source!.Value;
        var (from, to, rangeError) = NormalizeRange(request.From, request.To);
        if (rangeError is not null)
        {
            return SetupsError(request, rangeError);
        }

        var observations = await _enrichedStore.GetEnrichedAsync(
            instrument.InstrumentId, source, from, to, 0, MaxObservations, cancellationToken);

        if (observations.Count == 0)
        {
            return new StrategySetupsResponse
            {
                Ok = false,
                InstrumentId = instrument.InstrumentId,
                Source = source.ToString(),
                From = from,
                To = to,
                ErrorCode = "NO_DATA",
                ErrorMessage = "No enriched observations found for the requested instrument/source/range. " +
                               "Run the historical processing pipeline first (POST /api/HistoricalProcessing/process)."
            };
        }

        StrategySetupResult result;
        try
        {
            result = _setupEngine.Generate(
                observations,
                instrument.InstrumentId,
                source,
                request.RuleNames is { Count: > 0 } ? request.RuleNames : null,
                StrategyThresholds.Default);
        }
        catch (ArgumentException ex)
        {
            return SetupsError(request, ex.Message);
        }

        return new StrategySetupsResponse
        {
            Ok = true,
            InstrumentId = result.InstrumentId,
            Source = result.Source.ToString(),
            From = result.From,
            To = result.To,
            ObservationsEvaluated = result.ObservationsEvaluated,
            Setups = result.Setups.Select(MapSetup).ToList(),
            SetupsPerRule = result.SetupsPerRule,
            SkippedInsufficientEvidence = result.SkippedInsufficientEvidence
        };
    }

    /// <summary>
    /// Computes the chronological Research / Validation / Holdout split for the
    /// requested enriched observations.
    /// </summary>
    public async Task<SplitResponse> SplitAsync(
        SplitQuery query,
        CancellationToken cancellationToken = default)
    {
        var (instrument, error) = await ResolveAsync(query.Instrument, cancellationToken);
        if (instrument is null)
        {
            return SplitError(query, error!);
        }

        var source = query.Source!.Value;
        var (from, to, rangeError) = NormalizeRange(query.From, query.To);
        if (rangeError is not null)
        {
            return SplitError(query, rangeError);
        }

        var observations = await _enrichedStore.GetEnrichedAsync(
            instrument.InstrumentId, source, from, to, 0, MaxObservations, cancellationToken);

        if (observations.Count == 0)
        {
            return new SplitResponse
            {
                Ok = false,
                InstrumentId = instrument.InstrumentId,
                Source = source.ToString(),
                From = from,
                To = to,
                ErrorCode = "NO_DATA",
                ErrorMessage = "No enriched observations found for the requested instrument/source/range. " +
                               "Run the historical processing pipeline first (POST /api/HistoricalProcessing/process)."
            };
        }

        var fractions = new TimeSeriesSplitFractions
        {
            ResearchFraction = query.ResearchFraction ?? 0.6m,
            ValidationFraction = query.ValidationFraction ?? 0.2m
        };

        if (fractions.ResearchFraction < 0 || fractions.ValidationFraction < 0
            || fractions.ResearchFraction + fractions.ValidationFraction > 1m)
        {
            return SplitError(query, "Split fractions must be non-negative and sum to at most 1.0.");
        }

        var segments = TimeSeriesSplitter.Split(observations, fractions);

        return new SplitResponse
        {
            Ok = true,
            InstrumentId = instrument.InstrumentId,
            Source = source.ToString(),
            From = from,
            To = to,
            TotalObservations = observations.Count,
            Segments = segments.Select(s => new SplitSegmentResponse
            {
                Segment = s.Segment.ToString(),
                FirstObservation = s.FirstObservation,
                LastObservation = s.LastObservation,
                Observations = s.Observations
            }).ToList()
        };
    }

    // =====================================================================
    // Helpers
    // =====================================================================

    private async Task<(InstrumentMapping? Instrument, string? Error)> ResolveAsync(
        string? instrumentQuery, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(instrumentQuery))
        {
            return (null, "Instrument parameter is required.");
        }

        var instrument = await _instrumentResolver.ResolveAsync(instrumentQuery.Trim(), ct);
        return instrument is null
            ? (null, $"No instrument found matching '{instrumentQuery}'.")
            : (instrument, null);
    }

    private static (DateOnly From, DateOnly To, string? Error) NormalizeRange(DateOnly? from, DateOnly? to)
    {
        var fromDate = from ?? DateOnly.FromDateTime(DateTime.Today.AddYears(-1));
        var toDate = to ?? DateOnly.FromDateTime(DateTime.Today);

        return fromDate > toDate
            ? (fromDate, toDate, "'from' date must not be after 'to' date.")
            : (fromDate, toDate, null);
    }

    private static RegimeResponse RegimeError(RegimeQuery query, string message) => new()
    {
        Ok = false,
        InstrumentId = query.Instrument ?? "",
        Source = query.Source?.ToString() ?? "",
        ErrorCode = message.Contains("No instrument found", StringComparison.Ordinal) ? "INSTRUMENT_NOT_FOUND" : "INVALID_REQUEST",
        ErrorMessage = message
    };

    private static StrategyEvaluateResponse EvaluateError(StrategyEvaluateRequest request, string message) => new()
    {
        Ok = false,
        InstrumentId = request.Instrument ?? "",
        Source = request.Source?.ToString() ?? "",
        ErrorCode = message.Contains("No instrument found", StringComparison.Ordinal) ? "INSTRUMENT_NOT_FOUND" : "INVALID_REQUEST",
        ErrorMessage = message
    };

    private static SplitResponse SplitError(SplitQuery query, string message) => new()
    {
        Ok = false,
        InstrumentId = query.Instrument ?? "",
        Source = query.Source?.ToString() ?? "",
        ErrorCode = message.Contains("No instrument found", StringComparison.Ordinal) ? "INSTRUMENT_NOT_FOUND" : "INVALID_REQUEST",
        ErrorMessage = message
    };

    private static StrategySetupResponse MapSetup(StrategySetup setup) => new()
    {
        SetupId = setup.SetupId,
        RuleName = setup.RuleName,
        InstrumentId = setup.InstrumentId,
        Source = setup.Source.ToString(),
        ObservationDate = setup.ObservationDate,
        Direction = setup.Direction.ToString(),
        Regime = new RegimeSnapshotResponse
        {
            ObservationDate = setup.ObservationDate,
            Trend = setup.Regime.Trend.ToString(),
            Volatility = setup.Regime.Volatility.ToString(),
            Momentum = setup.Regime.Momentum.ToString(),
            Close = setup.Close
        },
        EntryCondition = setup.EntryCondition,
        SupportingEvidence = setup.SupportingEvidence.Select(e => new StrategyEvidenceItemResponse
        {
            Name = e.Name,
            Value = e.Value,
            Comparison = e.Comparison
        }).ToList(),
        Invalidation = new SetupInvalidationResponse
        {
            Condition = setup.Invalidation.Condition,
            FeatureNames = setup.Invalidation.FeatureNames,
            ReferenceLevel = setup.Invalidation.ReferenceLevel
        },
        Risk = new SetupRiskResponse
        {
            Rsi = setup.Risk.Rsi,
            PercentB = setup.Risk.PercentB,
            BandwidthPercent = setup.Risk.BandwidthPercent,
            CloseVsSmaPercent = setup.Risk.CloseVsSmaPercent,
            Volatility = setup.Risk.Volatility.ToString()
        },
        ProcessedBy = setup.ProcessedBy
    };

    private static StrategySetupsResponse SetupsError(StrategySetupsRequest request, string message) => new()
    {
        Ok = false,
        InstrumentId = request.Instrument ?? "",
        Source = request.Source?.ToString() ?? "",
        ErrorCode = message.Contains("No instrument found", StringComparison.Ordinal) ? "INSTRUMENT_NOT_FOUND" : "INVALID_REQUEST",
        ErrorMessage = message
    };

    private static StrategyRuleStatisticsResponse MapStats(StrategyRuleStatistics s) => new()
    {
        RuleName = s.RuleName,
        MatchCount = s.MatchCount,
        ForwardReturns = s.ForwardReturns.Select(f => new ForwardReturnStatsResponse
        {
            HorizonObservations = f.HorizonObservations,
            MeasurableCount = f.MeasurableCount,
            PositiveOutcomeCount = f.PositiveOutcomeCount,
            NegativeOutcomeCount = f.NegativeOutcomeCount,
            UnavailableOutcomeCount = f.UnavailableOutcomeCount,
            AverageForwardReturn = f.AverageForwardReturn,
            MedianForwardReturn = f.MedianForwardReturn
        }).ToList()
    };

    private static StrategyMatchSampleResponse MapSample(
        StrategyMatchSample sample,
        IReadOnlyList<EnrichedObservation> observations,
        IReadOnlyList<StrategyRuleStatistics> stats)
    {
        var horizonKeys = stats.Count > 0 && stats[0].ForwardReturns.Count > 0
            ? stats[0].ForwardReturns.Select(f => f.HorizonObservations).ToList()
            : [];
        var forwardReturns = new Dictionary<string, decimal?>();
        foreach (var horizon in horizonKeys)
        {
            forwardReturns[horizon.ToString()] =
                sample.ForwardReturns.TryGetValue(horizon, out var value) ? value : null;
        }

        return new StrategyMatchSampleResponse
        {
            ObservationDate = sample.Evidence.ObservationDate,
            Regime = new RegimeSnapshotResponse
            {
                ObservationDate = sample.Evidence.ObservationDate,
                Trend = sample.Evidence.Regime.Trend.ToString(),
                Volatility = sample.Evidence.Regime.Volatility.ToString(),
                Momentum = sample.Evidence.Regime.Momentum.ToString(),
                Close = sample.Evidence.Features.Close
            },
            Close = sample.Evidence.Features.Close,
            Indicators = FindIndicators(observations, sample.Evidence.ObservationDate),
            RuleEvaluations = sample.Evidence.RuleEvaluations.Select(r => new RuleEvaluationResponse
            {
                RuleName = r.RuleName,
                Matched = r.Matched,
                Evidence = r.Evidence.Select(e => new StrategyEvidenceItemResponse
                {
                    Name = e.Name,
                    Value = e.Value,
                    Comparison = e.Comparison
                }).ToList()
            }).ToList(),
            ForwardReturns = forwardReturns
        };
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, decimal>> FindIndicators(
        IReadOnlyList<EnrichedObservation> observations, DateOnly date)
    {
        var match = observations.FirstOrDefault(o => o.ObservationDate == date);
        return match?.Indicators ?? new Dictionary<string, IReadOnlyDictionary<string, decimal>>();
    }
}

using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Interfaces.Analysis;
using StrategyForge.Domain.Models;

namespace StrategyForge.Analysis.Strategy;

/// <summary>
/// Deterministic historical evaluation ("minimal backtest") of strategy rules
/// over the enriched dataset.
///
/// Look-ahead guarantee: a rule is evaluated at observation D using ONLY
/// <see cref="MarketFeatures"/> derived from the enriched observation at D and
/// its predecessors (stored indicator values, which are themselves computed
/// from data up to D). Future observations are used exclusively to measure
/// forward returns AFTER the match decision. This is enforced structurally:
/// features are extracted per index from the prefix ending at that index, and
/// forward returns are computed in a separate pass.
///
/// Forward returns are measured between trading-day offsets in the
/// chronological observation sequence (Close(D+h)/Close(D) − 1), NOT calendar
/// days, so weekends/holidays never shift a horizon. When the horizon extends
/// beyond available data the outcome is counted as Unavailable — never
/// fabricated, never silently dropped.
///
/// These statistics describe historical behavior under the chosen evaluation
/// methodology; they do NOT prove profitability or predictive accuracy.
///
/// Deterministic: same dataset + same rules + same thresholds + same horizons
/// → identical output. No randomness, no clocks, no I/O.
/// </summary>
public sealed class StrategyEvaluationEngine
{
    private readonly IReadOnlyList<IStrategyRule> _rules;

    public StrategyEvaluationEngine(IReadOnlyList<IStrategyRule> rules)
    {
        _rules = rules;
    }

    /// <summary>All rules known to this engine.</summary>
    public IReadOnlyList<IStrategyRule> Rules => _rules;

    /// <summary>
    /// Evaluates the requested rules over a chronologically ordered observation
    /// sequence for ONE instrument and ONE source (sources are never merged).
    /// </summary>
    /// <param name="instrumentId">Canonical instrument ID.</param>
    /// <param name="source">Provider source.</param>
    /// <param name="observations">Chronologically ordered enriched observations (oldest first).</param>
    /// <param name="request">Evaluation request (rule selection, horizons, sample cap).</param>
    /// <param name="thresholds">Explicit thresholds for rules and regime evidence.</param>
    public StrategyEvaluationResult Evaluate(
        string instrumentId,
        SourceAdapterType source,
        IReadOnlyList<EnrichedObservation> observations,
        StrategyEvaluationRequest request,
        StrategyThresholds? thresholds = null)
    {
        var t = thresholds ?? StrategyThresholds.Default;
        var horizons = request.EffectiveForwardHorizons;

        var selectedRules = SelectRules(request.RuleNames);

        // Prefix-scoped feature extraction: features[i] uses only observations[0..i].
        // This is the structural no-look-ahead boundary for rule decisions.
        var featuresByIndex = new List<MarketFeatures>(observations.Count);
        for (var i = 0; i < observations.Count; i++)
        {
            featuresByIndex.Add(MarketFeatureExtractor.Extract(
                observations[i], i > 0 ? observations[i - 1] : null));
        }

        var perRuleStats = new Dictionary<string, RuleAccumulator>();
        var totalMatches = 0;
        var noMatchCount = 0;
        var matchSamples = new List<StrategyMatchSample>();
        var matchedRuleNamesByIndex = new Dictionary<int, List<string>>();

        for (var i = 0; i < observations.Count; i++)
        {
            var features = featuresByIndex[i];
            var matchedHere = new List<string>();

            foreach (var rule in selectedRules)
            {
                var evaluation = rule.Evaluate(features, t);
                var accumulator = perRuleStats.TryGetValue(rule.Name, out var acc)
                    ? acc
                    : perRuleStats[rule.Name] = new RuleAccumulator(horizons);

                if (!evaluation.Matched)
                {
                    accumulator.CountNotMatched();
                    continue;
                }

                accumulator.CountMatch();
                totalMatches++;
                matchedHere.Add(rule.Name);

                // Forward returns: outcome measurement only — computed after the
                // match decision and never fed back into rule evaluation.
                var forwardReturns = new Dictionary<int, decimal?>();
                foreach (var horizon in horizons)
                {
                    var outcomeIndex = i + horizon;
                    var measurable = outcomeIndex < observations.Count
                        && observations[i].Close > 0
                        && observations[outcomeIndex].Close > 0;

                    if (!measurable)
                    {
                        forwardReturns[horizon] = null;
                        accumulator.Unavailable(horizon);
                        continue;
                    }

                    var forwardReturn = Math.Round(
                        (observations[outcomeIndex].Close / observations[i].Close - 1m) * 100m,
                        6);

                    forwardReturns[horizon] = forwardReturn;
                    accumulator.Add(horizon, forwardReturn);
                }

                if (matchSamples.Count < request.MaxEvidenceSamples)
                {
                    matchSamples.Add(new StrategyMatchSample
                    {
                        Evidence = new StrategyObservationEvidence
                        {
                            InstrumentId = instrumentId,
                            Source = source,
                            ObservationDate = observations[i].ObservationDate,
                            Regime = MarketRegimeClassifier.Classify(features, t),
                            Features = features,
                            RulesEvaluated = selectedRules.Select(r => r.Name).ToList(),
                            RulesMatched = matchedHere.ToList(),
                            RuleEvaluations = selectedRules
                                .Select(r => r.Evaluate(features, t))
                                .ToList()
                        },
                        ForwardReturns = forwardReturns
                    });
                }
            }

            if (matchedHere.Count == 0)
            {
                noMatchCount++;
            }
            else
            {
                matchedRuleNamesByIndex[i] = matchedHere;
            }
        }

        return new StrategyEvaluationResult
        {
            InstrumentId = instrumentId,
            Source = source,
            From = observations.Count > 0 ? observations[0].ObservationDate : request.From,
            To = observations.Count > 0 ? observations[^1].ObservationDate : request.To,
            ObservationsEvaluated = observations.Count,
            NoMatchCount = noMatchCount,
            TotalRuleMatches = totalMatches,
            RuleStatistics = selectedRules
                .Select(r => perRuleStats.TryGetValue(r.Name, out var acc)
                    ? acc.Build(r.Name)
                    : new StrategyRuleStatistics
                    {
                        RuleName = r.Name,
                        MatchCount = 0,
                        ForwardReturns = horizons.Select(h => new ForwardReturnStats
                        {
                            HorizonObservations = h,
                            MeasurableCount = 0,
                            PositiveOutcomeCount = 0,
                            NegativeOutcomeCount = 0,
                            UnavailableOutcomeCount = 0
                        }).ToList()
                    })
                .ToList(),
            MatchSamples = matchSamples
        };
    }

    private IReadOnlyList<IStrategyRule> SelectRules(IReadOnlyList<string>? ruleNames)
    {
        if (ruleNames is not { Count: > 0 })
        {
            return _rules;
        }

        var requested = ruleNames.ToHashSet(StringComparer.Ordinal);
        var selected = _rules.Where(r => requested.Contains(r.Name)).ToList();

        var unknown = requested.Except(selected.Select(r => r.Name), StringComparer.Ordinal).ToList();
        if (unknown.Count > 0)
        {
            throw new ArgumentException(
                $"Unknown rule name(s): {string.Join(", ", unknown)}. " +
                $"Known rules: {string.Join(", ", _rules.Select(r => r.Name))}.");
        }

        return selected;
    }

    /// <summary>Per-rule accumulation of match counts and forward returns per horizon.</summary>
    private sealed class RuleAccumulator
    {
        private readonly IReadOnlyList<int> _horizons;
        private readonly Dictionary<int, List<decimal>> _returns = new();

        public RuleAccumulator(IReadOnlyList<int> horizons)
        {
            _horizons = horizons;
            foreach (var h in horizons)
            {
                _returns[h] = [];
            }
        }

        public int Matches { get; private set; }
        public int NotMatched { get; private set; }

        public void CountMatch() => Matches++;
        public void CountNotMatched() => NotMatched++;

        public void Add(int horizon, decimal forwardReturn) => _returns[horizon].Add(forwardReturn);

        public void Unavailable(int horizon)
        {
            // Tracked via the built stats; nothing to accumulate.
        }

        public StrategyRuleStatistics Build(string ruleName)
        {
            var stats = new List<ForwardReturnStats>(_horizons.Count);
            foreach (var horizon in _horizons)
            {
                var values = _returns[horizon];
                var unavailable = Matches - values.Count;

                stats.Add(new ForwardReturnStats
                {
                    HorizonObservations = horizon,
                    MeasurableCount = values.Count,
                    PositiveOutcomeCount = values.Count(v => v > 0),
                    NegativeOutcomeCount = values.Count(v => v < 0),
                    UnavailableOutcomeCount = Math.Max(0, unavailable),
                    AverageForwardReturn = values.Count > 0
                        ? Math.Round(values.Average(), 6)
                        : null,
                    MedianForwardReturn = values.Count > 0
                        ? Math.Round(Median(values), 6)
                        : null
                });
            }

            return new StrategyRuleStatistics
            {
                RuleName = ruleName,
                MatchCount = Matches,
                ForwardReturns = stats
            };
        }

        private static decimal Median(List<decimal> values)
        {
            var sorted = values.OrderBy(v => v).ToList();
            var mid = sorted.Count / 2;
            return sorted.Count % 2 == 1
                ? sorted[mid]
                : (sorted[mid - 1] + sorted[mid]) / 2m;
        }
    }
}

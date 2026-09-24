using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Interfaces.Analysis;
using StrategyForge.Domain.Models;

namespace StrategyForge.Analysis.Strategy;

/// <summary>
/// Deterministic setup generation over the enriched dataset.
///
/// For each observation (chronological order), each selected setup rule is
/// evaluated against <see cref="MarketFeatures"/> derived from the stored
/// indicator values (no recomputation). When a rule matches, the engine
/// materializes the <see cref="StrategySetup"/> with:
///
///   - identity: (instrument, source, rule, observation date) — stable and content-derived
///   - provenance: Phase 7 dataset identity + the enriched observation's ProcessedBy version
///   - risk metadata: actual stored values or null (never fabricated)
///
/// Hard guarantees:
///   - a setup is produced ONLY when the rule reports every required input available
///   - same observations + rules + thresholds → same setups, same identities, same order
///   - ordering: observation date ascending, then rule name (ordinal)
///   - one instrument, one source — sources are never merged
///   - no clocks, no randomness, no LLM calls
/// </summary>
public sealed class StrategySetupEngine
{
    private readonly IReadOnlyList<ISetupRule> _rules;

    public StrategySetupEngine(IReadOnlyList<ISetupRule> rules)
    {
        _rules = rules;
    }

    /// <summary>All setup rules known to this engine.</summary>
    public IReadOnlyList<ISetupRule> Rules => _rules;

    /// <summary>
    /// Generates setups over a chronologically ordered observation sequence for
    /// ONE instrument and ONE source (sources are never merged).
    /// </summary>
    /// <param name="observations">Chronologically ordered enriched observations (oldest first).</param>
    /// <param name="instrumentId">Canonical instrument ID (stamped into setup identity/provenance).</param>
    /// <param name="source">Provider source.</param>
    /// <param name="ruleNames">Optional rule-name filter; null/empty evaluates all registered rules.</param>
    /// <param name="thresholds">Explicit thresholds; defaults when null.</param>
    public StrategySetupResult Generate(
        IReadOnlyList<EnrichedObservation> observations,
        string instrumentId,
        SourceAdapterType source,
        IReadOnlyList<string>? ruleNames = null,
        StrategyThresholds? thresholds = null)
    {
        var t = thresholds ?? StrategyThresholds.Default;
        var selectedRules = SelectRules(ruleNames);

        var setups = new List<StrategySetup>();
        var perRule = new Dictionary<string, int>();
        var skipped = new Dictionary<string, int>();
        var previous = (EnrichedObservation?)null;

        foreach (var observation in observations)
        {
            var features = MarketFeatureExtractor.Extract(observation, previous);
            previous = observation;

            foreach (var rule in selectedRules)
            {
                var evaluation = rule.Evaluate(features, t);

                if (!evaluation.Matched || evaluation.Direction is null
                    || evaluation.EntryCondition is null || evaluation.Invalidation is null)
                {
                    if (evaluation.MissingInputs.Count > 0)
                    {
                        var key = rule.Name;
                        skipped[key] = skipped.GetValueOrDefault(key) + 1;
                    }

                    continue;
                }

                var setup = new StrategySetup
                {
                    SetupId = StrategySetup.ComposeId(instrumentId, source, rule.Name, observation.ObservationDate),
                    RuleName = rule.Name,
                    InstrumentId = instrumentId,
                    Source = source,
                    ObservationDate = observation.ObservationDate,
                    Close = observation.Close,
                    Direction = evaluation.Direction.Value,
                    Regime = evaluation.Regime,
                    EntryCondition = evaluation.EntryCondition,
                    SupportingEvidence = evaluation.Evidence,
                    Invalidation = evaluation.Invalidation,
                    Risk = BuildRisk(features, evaluation.Regime),
                    UnavailableEvidence = [],
                    ProcessedBy = observation.ProcessedBy
                };

                setups.Add(setup);
                perRule[rule.Name] = perRule.GetValueOrDefault(rule.Name) + 1;
            }
        }

        return new StrategySetupResult
        {
            InstrumentId = instrumentId,
            Source = source,
            From = observations.Count > 0 ? observations[0].ObservationDate : default,
            To = observations.Count > 0 ? observations[^1].ObservationDate : default,
            ObservationsEvaluated = observations.Count,
            Setups = setups,
            SetupsPerRule = perRule,
            SkippedInsufficientEvidence = skipped
        };
    }

    /// <summary>
    /// Risk/uncertainty metadata from actual stored values; null when the
    /// underlying value was unavailable — never fabricated.
    /// </summary>
    private static SetupRiskMetadata BuildRisk(MarketFeatures features, MarketRegimeSnapshot regime) => new()
    {
        Rsi = features.Rsi,
        PercentB = features.PercentB,
        BandwidthPercent = features.BandwidthPercent,
        CloseVsSmaPercent = features.Sma is > 0
            ? Math.Round((features.Close / features.Sma.Value - 1m) * 100m, 6)
            : null,
        Volatility = regime.Volatility
    };

    private IReadOnlyList<ISetupRule> SelectRules(IReadOnlyList<string>? ruleNames)
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
                $"Unknown setup rule name(s): {string.Join(", ", unknown)}. " +
                $"Known rules: {string.Join(", ", _rules.Select(r => r.Name))}.");
        }

        return selected;
    }
}

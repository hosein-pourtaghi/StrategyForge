using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Interfaces.Analysis;
using StrategyForge.Domain.Models;

namespace StrategyForge.Analysis.Strategy;

/// <summary>
/// Deterministic projector that transforms validated, indicator-enriched
/// observations into AI-ready records.
///
/// Leakage protection is structural, not conventional:
///   - <see cref="AiReadyObservation.Features"/> for index i is extracted ONLY
///     from observations [0..i] (the Phase 8 extractor itself only ever looks
///     at the current and immediately preceding observation).
///   - <see cref="AiReadyObservation.Labels"/> for index i is computed in a
///     strictly separate pass over [i+1..]; the label pass never touches the
///     feature state of any record.
///
/// Feature selection follows <see cref="AiReadyFeaturePolicy"/>: only the listed
/// fields are projected; unsupplied values remain null — never zero-filled —
/// except volume, where 0 with HasMeaningfulVolume=false is the established
/// Phase 3/7 "provider does not supply volume" semantic.
///
/// Rules are evaluated against the SAME features at T that a decision made at
/// T would have seen — no future information can influence the rule outcome.
///
/// Deterministic: identical input + configuration always produces identical output.
/// </summary>
public sealed class AiReadyProjector : IAiReadyDatasetProjector
{
    /// <summary>
    /// Projects a chronologically ordered sequence (oldest first) of enriched
    /// observations for ONE instrument and ONE source into AI-ready records.
    /// </summary>
    /// <param name="observations">Chronologically ordered enriched observations. Invalid rows must already be excluded.</param>
    /// <param name="thresholds">Deterministic rule/regime thresholds.</param>
    /// <param name="contextWindowObservations">Historical context points per record (0 disables context).</param>
    /// <param name="forwardHorizons">Forward-return horizons in observations (must be non-empty and positive).</param>
    /// <param name="provenanceResolver">Maps an observation to its provenance reference (from raw metadata when available).</param>
    /// <param name="rules">Rules evaluated per record; defaults to the built-in registry.</param>
    public IReadOnlyList<AiReadyObservation> Project(
        IReadOnlyList<EnrichedObservation> observations,
        StrategyThresholds? thresholds = null,
        int contextWindowObservations = 20,
        IReadOnlyList<int>? forwardHorizons = null,
        Func<EnrichedObservation, AiReadyProvenanceReference>? provenanceResolver = null,
        IReadOnlyList<IStrategyRule>? rules = null)
    {
        if (contextWindowObservations < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(contextWindowObservations));
        }

        var horizons = forwardHorizons is { Count: > 0 }
            ? forwardHorizons
            : [1, 5, 10];

        if (horizons.Any(h => h <= 0))
        {
            throw new ArgumentException("Forward horizons must be positive.");
        }

        var t = thresholds ?? StrategyThresholds.Default;
        var ruleSet = rules ?? StrategyRuleRegistry.BuiltIn;
        var maxHorizon = horizons.Max();

        var result = new List<AiReadyObservation>(observations.Count);

        for (var i = 0; i < observations.Count; i++)
        {
            var current = observations[i];

            // --- Features: strictly [0..i] ---
            var features = MarketFeatureExtractor.Extract(current, i > 0 ? observations[i - 1] : null);
            var regime = MarketRegimeClassifier.Classify(features, t);

            var matchedRules = new List<AiReadyRuleEvidence>();
            foreach (var rule in ruleSet)
            {
                var evaluation = rule.Evaluate(features, t);
                if (evaluation.Matched)
                {
                    matchedRules.Add(new AiReadyRuleEvidence
                    {
                        RuleName = rule.Name,
                        Items = evaluation.Evidence
                    });
                }
            }

            // --- Context: strictly earlier observations ---
            var contextStart = Math.Max(0, i - contextWindowObservations);
            var context = new List<AiReadyContextPoint>(contextWindowObservations);
            for (var c = contextStart; c < i; c++)
            {
                context.Add(new AiReadyContextPoint
                {
                    Date = observations[c].ObservationDate,
                    Close = observations[c].Close
                });
            }

            // --- Labels: strictly [i+1..] — separate pass, separate concept ---
            var labels = BuildLabels(observations, i, horizons, maxHorizon);

            result.Add(new AiReadyObservation
            {
                InstrumentId = current.InstrumentId,
                Source = current.Source,
                ObservationDate = current.ObservationDate,
                ProvenanceReference = provenanceResolver?.Invoke(current) ?? new AiReadyProvenanceReference
                {
                    ProcessedBy = current.ProcessedBy
                },
                Features = new AiReadyFeatures
                {
                    Close = features.Close,
                    Volume = features.Volume,
                    HasMeaningfulVolume = features.HasMeaningfulVolume,
                    Sma = features.Sma,
                    Rsi = features.Rsi,
                    Macd = features.MacdLine,
                    MacdSignal = features.MacdSignal,
                    MacdHistogram = features.MacdHistogram,
                    BollingerUpper = features.BollingerUpper,
                    BollingerMiddle = features.BollingerMiddle,
                    BollingerLower = features.BollingerLower,
                    BollingerBandwidthPercent = features.BandwidthPercent,
                    BollingerPercentB = features.PercentB,
                    PriceAboveSma = features.PriceAboveSma,
                    EmaFastAboveSlow = features.EmaFastAboveSlow,
                    MacdAboveSignal = features.MacdAboveSignal,
                    HistogramRising = features.HistogramRising
                },
                TrendRegime = regime.Trend,
                VolatilityRegime = regime.Volatility,
                MomentumRegime = regime.Momentum,
                MatchedRules = matchedRules,
                ContextWindow = context,
                QualityStatus = current.QualityStatus,
                WarningCodes = current.WarningCodes,
                Labels = labels
            });
        }

        return result;
    }

    /// <summary>
    /// Builds future labels for the record at <paramref name="targetIndex"/> from
    /// observations strictly after it. Returns null-valued horizons when the
    /// dataset ends before the horizon completes (never fabricated).
    /// </summary>
    private static AiReadyLabels BuildLabels(
        IReadOnlyList<EnrichedObservation> observations,
        int targetIndex,
        IReadOnlyList<int> horizons,
        int maxHorizon)
    {
        var target = observations[targetIndex];
        var forwardReturns = new Dictionary<int, decimal?>();

        foreach (var horizon in horizons)
        {
            var futureIndex = targetIndex + horizon;
            if (futureIndex < observations.Count)
            {
                var future = observations[futureIndex];
                forwardReturns[horizon] = target.Close == 0
                    ? null // zero close is rejected by validation upstream; defensive only
                    : (future.Close - target.Close) / target.Close;
            }
            else
            {
                forwardReturns[horizon] = null; // horizon beyond dataset — explicit, not fabricated
            }
        }

        // Max forward gain/loss over the largest horizon window.
        decimal? maxGain = null;
        decimal? maxLoss = null;
        if (target.Close > 0)
        {
            var end = Math.Min(observations.Count - 1, targetIndex + maxHorizon);
            if (end > targetIndex)
            {
                var highestHigh = decimal.MinValue;
                var lowestLow = decimal.MaxValue;
                for (var f = targetIndex + 1; f <= end; f++)
                {
                    if (observations[f].High > highestHigh)
                    {
                        highestHigh = observations[f].High;
                    }
                    if (observations[f].Low < lowestLow)
                    {
                        lowestLow = observations[f].Low;
                    }
                }

                maxGain = (highestHigh - target.Close) / target.Close;
                maxLoss = (lowestLow - target.Close) / target.Close;
            }
        }

        return new AiReadyLabels
        {
            ForwardReturns = forwardReturns,
            MaxForwardGain = maxGain,
            MaxForwardLoss = maxLoss
        };
    }
}

/// <summary>
/// Analysis-layer adapter exposing the built-in deterministic rule registry
/// through the Domain <see cref="IStrategyRuleRegistry"/> contract, so the
/// dataset preparation layer can consume rule names without referencing this
/// project (dependency inversion, same pattern as IIndicatorEngine).
/// </summary>
public sealed class BuiltInStrategyRuleRegistry : IStrategyRuleRegistry
{
    /// <inheritdoc/>
    public IReadOnlyList<IStrategyRule> BuiltIn => StrategyRuleRegistry.BuiltIn;

    /// <inheritdoc/>
    public IReadOnlyList<string> BuiltInNames { get; } =
        StrategyRuleRegistry.BuiltIn.Select(r => r.Name).ToList();
}

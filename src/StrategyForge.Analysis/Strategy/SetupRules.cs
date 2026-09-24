using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Interfaces.Analysis;
using StrategyForge.Domain.Models;

namespace StrategyForge.Analysis.Strategy;

// ============================================================
// Phase 8 setup rules — deterministic rules that produce strategy
// setups from stored indicator values via MarketFeatures.
//
// Each rule explicitly defines:
//   - required inputs (checked for availability BEFORE any decision)
//   - regime compatibility (a rule only fires in compatible regimes)
//   - qualifying conditions (entry)
//   - excluded conditions (the setup must NOT be emitted when these hold)
//   - the invalidation condition for any setup it produces
//
// A rule NEVER produces a setup when required evidence is missing:
// Evaluate returns null with the missing items recorded. Unknown is
// never silently converted to false without being reported.
// Rules never compute indicators, never touch providers/HTTP/LLMs,
// and never produce trading instructions.
// ============================================================

/// <summary>Stable names of the built-in setup rules.</summary>
public static class SetupRuleNames
{
    /// <summary>Long continuation in a bullish trend with intact momentum.</summary>
    public const string TrendContinuation = "TrendContinuation";

    /// <summary>Long pullback entry when mean-reversion conditions fire in a non-bearish trend.</summary>
    public const string MeanReversionPullback = "MeanReversionPullback";
}

/// <summary>
/// TrendContinuation (Long): emits a setup when
///
///   regime:   Trend = Bullish AND Momentum ∈ {Strong, Overbought, Neutral}
///   entry:    Close &gt; SMA AND MACD &gt; Signal AND MACD line &gt; 0 AND RSI ≥ TrendFollowingRsiMinimum
///   excluded: MACD histogram falling (momentum deteriorating → no continuation setup)
///   invalid:  MACD line crosses below Signal (defined against the stored MACD components)
///
/// Missing ANY required value → no setup (null) with the missing items listed.
/// </summary>
public sealed class TrendContinuationSetupRule : ISetupRule
{
    public string Name => SetupRuleNames.TrendContinuation;

    public string Description =>
        "Long continuation: Trend=Bullish AND Momentum in {Neutral, Strong, Overbought} AND " +
        "Close > SMA AND MACD > Signal AND MACD > 0 AND RSI >= TrendFollowingRsiMinimum; " +
        "excluded when MACD histogram is falling. Invalidated when MACD crosses below Signal.";

    public SetupRuleEvaluation Evaluate(MarketFeatures features, StrategyThresholds thresholds)
    {
        var missing = CollectMissing(features);

        var regime = MarketRegimeClassifier.Classify(features, thresholds);
        var regimeCompatible = regime.Trend == TrendRegime.Bullish
            && regime.Momentum is MomentumRegime.Neutral
                or MomentumRegime.Strong
                or MomentumRegime.Overbought;

        var entryOk = features.PriceAboveSma == true
            && features.MacdAboveSignal == true
            && features.EmaFastAboveSlow == true
            && features.Rsi.HasValue
            && features.Rsi.Value >= thresholds.TrendFollowingRsiMinimum;

        // Exclusion: a setup must NOT be emitted while the histogram is
        // explicitly falling. A missing histogram does NOT exclude (the
        // exclusion applies only when the evidence exists and is negative).
        var excluded = features.HistogramRising == false;

        var evidence = BuildEvidence(features, thresholds);

        return entryOk && regimeCompatible && !excluded
            ? SetupRuleEvaluation.CreateMatched(
                Name,
                regime,
                SetupDirection.Long,
                "Close > SMA AND MACD > Signal AND MACD > 0 AND RSI >= TrendFollowingRsiMinimum",
                evidence,
                new SetupInvalidation
                {
                    Condition = "MACD line crosses below Signal line",
                    FeatureNames = ["MACD.MACD", "MACD.Signal"],
                    ReferenceLevel = features.MacdSignal
                })
            : SetupRuleEvaluation.CreateNotMatched(Name, regime, evidence, missing);
    }

    private static List<string> CollectMissing(MarketFeatures features)
    {
        var missing = new List<string>();
        if (features.Sma is null) missing.Add("SMA");
        if (features.MacdLine is null) missing.Add("MACD");
        if (features.MacdSignal is null) missing.Add("MACD.Signal");
        if (features.Rsi is null) missing.Add("RSI");
        return missing;
    }

    private static List<StrategyEvidenceItem> BuildEvidence(MarketFeatures features, StrategyThresholds t) =>
    [
        new()
        {
            Name = "Close vs SMA",
            Value = features.Sma,
            Comparison = features.PriceAboveSma.HasValue
                ? $"{features.Close} > SMA({features.Sma}) = {features.PriceAboveSma.Value}"
                : "missing"
        },
        new()
        {
            Name = "MACD vs Signal",
            Value = features.MacdLine,
            Comparison = features.MacdAboveSignal.HasValue
                ? $"{features.MacdLine} > {features.MacdSignal} = {features.MacdAboveSignal.Value}"
                : "missing"
        },
        new()
        {
            Name = "MACD line > 0",
            Value = features.MacdLine,
            Comparison = features.EmaFastAboveSlow.HasValue
                ? $"{features.MacdLine} > 0 = {features.EmaFastAboveSlow.Value}"
                : "missing"
        },
        new()
        {
            Name = "RSI",
            Value = features.Rsi,
            Comparison = features.Rsi.HasValue
                ? TrendFollowingRule.FormatComparison(features.Rsi.Value, ">=", t.TrendFollowingRsiMinimum, features.Rsi.Value >= t.TrendFollowingRsiMinimum)
                : "missing"
        },
        new()
        {
            Name = "MACD Histogram rising",
            Value = features.MacdHistogram,
            Comparison = features.HistogramRising.HasValue
                ? $"HistogramRising={features.HistogramRising.Value} (exclusion applies only when false)"
                : "missing"
        }
    ];
}

/// <summary>
/// MeanReversionPullback (Long): emits a setup when
///
///   regime:   Trend ∈ {Sideways, Bullish} AND Volatility ≠ Unknown
///   entry:    %B ≤ MeanReversionMaxPercentB AND RSI ≤ MeanReversionMaxRsi
///   excluded: Trend = Bearish (never fade a confirmed downtrend)
///   invalid:  Close closes below the stored lower Bollinger band reference
///             (defined against the stored Bollinger components; the reference
///             level is the setup observation's lower band)
///
/// Missing %B or RSI → no setup (null) with the missing items listed.
/// </summary>
public sealed class MeanReversionPullbackSetupRule : ISetupRule
{
    public string Name => SetupRuleNames.MeanReversionPullback;

    public string Description =>
        "Long pullback: Trend in {Sideways, Bullish} AND PercentB <= MeanReversionMaxPercentB AND " +
        "RSI <= MeanReversionMaxRsi; excluded when Trend=Bearish. Invalidated when Close " +
        "closes below the setup observation's lower Bollinger band.";

    public SetupRuleEvaluation Evaluate(MarketFeatures features, StrategyThresholds thresholds)
    {
        var missing = new List<string>();
        if (features.PercentB is null) missing.Add("BollingerBands.PercentB");
        if (features.Rsi is null) missing.Add("RSI");

        var regime = MarketRegimeClassifier.Classify(features, thresholds);
        var regimeCompatible = regime.Trend is TrendRegime.Sideways or TrendRegime.Bullish
            && regime.Volatility != VolatilityRegime.Unknown;

        var entryOk = features.PercentB is not null
            && features.Rsi is not null
            && features.PercentB.Value <= thresholds.MeanReversionMaxPercentB
            && features.Rsi.Value <= thresholds.MeanReversionMaxRsi;

        var evidence = new List<StrategyEvidenceItem>
        {
            new()
            {
                Name = "PercentB",
                Value = features.PercentB,
                Comparison = features.PercentB.HasValue
                    ? TrendFollowingRule.FormatComparison(
                        features.PercentB.Value, "<=", thresholds.MeanReversionMaxPercentB,
                        features.PercentB.Value <= thresholds.MeanReversionMaxPercentB)
                    : "missing"
            },
            new()
            {
                Name = "RSI",
                Value = features.Rsi,
                Comparison = features.Rsi.HasValue
                    ? TrendFollowingRule.FormatComparison(
                        features.Rsi.Value, "<=", thresholds.MeanReversionMaxRsi,
                        features.Rsi.Value <= thresholds.MeanReversionMaxRsi)
                    : "missing"
            }
        };

        return entryOk && regimeCompatible
            ? SetupRuleEvaluation.CreateMatched(
                Name,
                regime,
                SetupDirection.Long,
                "PercentB <= MeanReversionMaxPercentB AND RSI <= MeanReversionMaxRsi",
                evidence,
                new SetupInvalidation
                {
                    Condition = "Close closes below the setup observation's lower Bollinger band",
                    FeatureNames = ["BollingerBands.Lower", "Close"],
                    ReferenceLevel = features.BollingerLower
                })
            : SetupRuleEvaluation.CreateNotMatched(Name, regime, evidence, missing);
    }
}

/// <summary>
/// Registry of the built-in setup rules, in stable evaluation order.
/// Deliberately separate from <see cref="StrategyRuleRegistry"/> (which backs
/// the historical evaluation engine and is pinned by existing tests).
/// </summary>
public static class SetupRuleRegistry
{
    /// <summary>All built-in setup rules in stable evaluation order.</summary>
    public static IReadOnlyList<ISetupRule> BuiltIn { get; } =
    [
        new TrendContinuationSetupRule(),
        new MeanReversionPullbackSetupRule()
    ];
}

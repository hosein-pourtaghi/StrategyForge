using StrategyForge.Domain.Interfaces.Analysis;
using StrategyForge.Domain.Models;

namespace StrategyForge.Analysis.Strategy;

/// <summary>
/// Deterministic strategy rules interpreting stored indicator values via
/// <see cref="MarketFeatures"/>. Rules never compute indicators, never touch
/// providers/HTTP/LLMs, and never produce trading instructions (no orders,
/// entries, stops, or portfolio actions). Every evaluation returns structured
/// evidence containing the actual values used — a rule never returns a bare
/// "true". Missing required values yield Matched = false with the missing
/// condition recorded as evidence; nothing is fabricated.
/// </summary>
public static class StrategyRuleNames
{
    /// <summary>Trend-following: fast EMA above slow EMA (MACD &gt; 0) AND MACD above signal AND RSI &gt;= minimum.</summary>
    public const string TrendFollowing = "TrendFollowing";

    /// <summary>Momentum: MACD histogram rising AND RSI in the configured strong band.</summary>
    public const string Momentum = "Momentum";

    /// <summary>Mean reversion: %B at/below maximum AND RSI at/below maximum (stretched toward/below the lower band).</summary>
    public const string MeanReversion = "MeanReversion";
}

/// <summary>
/// TrendFollowing: EMA(fast) &gt; EMA(slow) — expressed as MACD &gt; 0, the stored
/// single source of truth for that relationship — AND MACD &gt; Signal AND
/// RSI &gt;= <see cref="StrategyThresholds.TrendFollowingRsiMinimum"/>.
/// </summary>
public sealed class TrendFollowingRule : IStrategyRule
{
    public string Name => StrategyRuleNames.TrendFollowing;

    public string Description =>
        $"Fast EMA above slow EMA (MACD > 0) AND MACD above Signal AND RSI >= TrendFollowingRsiMinimum";

    public StrategyRuleEvaluation Evaluate(MarketFeatures features, StrategyThresholds thresholds)
    {
        var macdAboveZero = features.EmaFastAboveSlow;
        var macdAboveSignal = features.MacdAboveSignal;
        var rsi = features.Rsi;
        var rsiOk = rsi.HasValue && rsi.Value >= thresholds.TrendFollowingRsiMinimum;

        var evidence = new List<StrategyEvidenceItem>
        {
            new()
            {
                Name = "MACD",
                Value = features.MacdLine,
                Comparison = macdAboveZero.HasValue
                    ? FormatComparison(features.MacdLine!.Value, ">", 0m, macdAboveZero.Value)
                    : "missing"
            },
            new()
            {
                Name = "MACD vs Signal",
                Value = features.MacdLine,
                Comparison = macdAboveSignal.HasValue && features.MacdSignal.HasValue
                    ? FormatComparison(features.MacdLine!.Value, ">", features.MacdSignal.Value, macdAboveSignal.Value)
                    : "missing"
            },
            new()
            {
                Name = "RSI",
                Value = rsi,
                Comparison = rsiOk
                    ? FormatComparison(rsi!.Value, ">=", thresholds.TrendFollowingRsiMinimum, true)
                    : rsi.HasValue
                        ? FormatComparison(rsi.Value, ">=", thresholds.TrendFollowingRsiMinimum, false)
                        : "missing"
            }
        };

        return new StrategyRuleEvaluation
        {
            RuleName = Name,
            Matched = macdAboveZero == true && macdAboveSignal == true && rsiOk,
            Evidence = evidence
        };
    }

    /// <summary>Shared helper for the evidence comparison strings.</summary>
    internal static string FormatComparison(decimal left, string op, decimal right, bool passed) =>
        $"{left} {op} {right} {(passed ? "✓" : "✗")}";
}

/// <summary>
/// Momentum: MACD histogram rising versus the previous observation AND
/// RSI in the strong band (StrongLower &lt;= RSI &lt; Overbought).
/// </summary>
public sealed class MomentumRule : IStrategyRule
{
    public string Name => StrategyRuleNames.Momentum;

    public string Description =>
        "MACD Histogram rising versus previous observation AND RSI in [StrongLower, Overbought)";

    public StrategyRuleEvaluation Evaluate(MarketFeatures features, StrategyThresholds thresholds)
    {
        var histogramRising = features.HistogramRising;
        var rsi = features.Rsi;
        var rsiOk = rsi.HasValue
            && rsi.Value >= thresholds.RsiStrongLower
            && rsi.Value < thresholds.RsiOverbought;

        var evidence = new List<StrategyEvidenceItem>
        {
            new()
            {
                Name = "MACD Histogram rising",
                Value = features.MacdHistogram,
                Comparison = histogramRising.HasValue
                    ? $"HistogramRising={histogramRising.Value} ✓"
                    : "missing"
            },
            new()
            {
                Name = "RSI",
                Value = rsi,
                Comparison = rsiOk
                    ? $"[{thresholds.RsiStrongLower}, {thresholds.RsiOverbought}) contains {rsi!.Value} ✓"
                    : rsi.HasValue
                        ? $"[{thresholds.RsiStrongLower}, {thresholds.RsiOverbought}) contains {rsi.Value} ✗"
                        : "missing"
            }
        };

        return new StrategyRuleEvaluation
        {
            RuleName = Name,
            Matched = histogramRising == true && rsiOk,
            Evidence = evidence
        };
    }
}

/// <summary>
/// MeanReversion: %B at/below <see cref="StrategyThresholds.MeanReversionMaxPercentB"/>
/// (price stretched toward or below the lower Bollinger band) AND
/// RSI at/below <see cref="StrategyThresholds.MeanReversionMaxRsi"/>.
/// </summary>
public sealed class MeanReversionRule : IStrategyRule
{
    public string Name => StrategyRuleNames.MeanReversion;

    public string Description =>
        $"PercentB <= MeanReversionMaxPercentB AND RSI <= MeanReversionMaxRsi";

    public StrategyRuleEvaluation Evaluate(MarketFeatures features, StrategyThresholds thresholds)
    {
        var percentB = features.PercentB;
        var rsi = features.Rsi;
        var percentBOk = percentB.HasValue && percentB.Value <= thresholds.MeanReversionMaxPercentB;
        var rsiOk = rsi.HasValue && rsi.Value <= thresholds.MeanReversionMaxRsi;

        var evidence = new List<StrategyEvidenceItem>
        {
            new()
            {
                Name = "PercentB",
                Value = percentB,
                Comparison = percentBOk
                    ? TrendFollowingRule.FormatComparison(percentB!.Value, "<=", thresholds.MeanReversionMaxPercentB, true)
                    : percentB.HasValue
                        ? TrendFollowingRule.FormatComparison(percentB.Value, "<=", thresholds.MeanReversionMaxPercentB, false)
                        : "missing"
            },
            new()
            {
                Name = "RSI",
                Value = rsi,
                Comparison = rsiOk
                    ? TrendFollowingRule.FormatComparison(rsi!.Value, "<=", thresholds.MeanReversionMaxRsi, true)
                    : rsi.HasValue
                        ? TrendFollowingRule.FormatComparison(rsi.Value, "<=", thresholds.MeanReversionMaxRsi, false)
                        : "missing"
            }
        };

        return new StrategyRuleEvaluation
        {
            RuleName = Name,
            Matched = percentBOk && rsiOk,
            Evidence = evidence
        };
    }
}

/// <summary>
/// Registry of the built-in deterministic rules. This is a simple, strongly
/// typed list — deliberately NOT a generic rule-engine framework.
/// </summary>
public static class StrategyRuleRegistry
{
    /// <summary>All built-in rules in stable evaluation order.</summary>
    public static IReadOnlyList<IStrategyRule> BuiltIn { get; } =
    [
        new TrendFollowingRule(),
        new MomentumRule(),
        new MeanReversionRule()
    ];
}

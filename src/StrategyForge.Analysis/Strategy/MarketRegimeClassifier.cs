using StrategyForge.Domain.Models;

namespace StrategyForge.Analysis.Strategy;

/// <summary>
/// Deterministic market-regime classifier over market features.
///
/// A regime DESCRIBES measurable market conditions — it is evidence for a later
/// strategy/AI layer, never a trading decision. Classifications are strict
/// threshold comparisons on the stored indicator values (via features);
/// missing inputs yield Unknown, never guesses.
///
/// The default thresholds are conservative starting points for exploration,
/// NOT universal financial definitions. All thresholds are explicit and
/// configurable via <see cref="StrategyThresholds"/>.
///
/// Deterministic: identical features and thresholds always produce identical output.
/// </summary>
public static class MarketRegimeClassifier
{
    /// <summary>
    /// Classifies the full three-dimensional regime for one observation.
    /// </summary>
    public static MarketRegimeSnapshot Classify(MarketFeatures features, StrategyThresholds thresholds)
    {
        return new MarketRegimeSnapshot
        {
            Trend = ClassifyTrend(features),
            Volatility = ClassifyVolatility(features, thresholds),
            Momentum = ClassifyMomentum(features, thresholds)
        };
    }

    /// <summary>
    /// Trend: price vs SMA AND MACD line vs zero. The MACD line equals
    /// EMA(fast) − EMA(slow), so MACD &gt; 0 is exactly "fast EMA above slow EMA"
    /// without recomputing any EMA. Agreement of both signals classifies the
    /// trend; disagreement is Sideways; missing input is Unknown.
    /// </summary>
    public static TrendRegime ClassifyTrend(MarketFeatures features)
    {
        if (features.PriceAboveSma is null || features.EmaFastAboveSlow is null)
        {
            return TrendRegime.Unknown;
        }

        if (features.PriceAboveSma.Value && features.EmaFastAboveSlow.Value)
        {
            return TrendRegime.Bullish;
        }

        if (!features.PriceAboveSma.Value && !features.EmaFastAboveSlow.Value)
        {
            return TrendRegime.Bearish;
        }

        return TrendRegime.Sideways;
    }

    /// <summary>
    /// Volatility: Bollinger bandwidth as a percentage of the middle band,
    /// compared against explicit low/high thresholds.
    /// </summary>
    public static VolatilityRegime ClassifyVolatility(MarketFeatures features, StrategyThresholds thresholds)
    {
        if (features.BandwidthPercent is null)
        {
            return VolatilityRegime.Unknown;
        }

        if (features.BandwidthPercent.Value < thresholds.LowVolatilityBandwidthPercent)
        {
            return VolatilityRegime.Low;
        }

        if (features.BandwidthPercent.Value > thresholds.HighVolatilityBandwidthPercent)
        {
            return VolatilityRegime.High;
        }

        return VolatilityRegime.Normal;
    }

    /// <summary>
    /// Momentum: RSI bands with explicit, configurable thresholds.
    /// Boundaries: RSI &lt; oversold → Oversold; &lt; weakUpper → Weak;
    /// &lt;= strongLower → Neutral; &lt; overbought → Strong; otherwise Overbought.
    /// </summary>
    public static MomentumRegime ClassifyMomentum(MarketFeatures features, StrategyThresholds thresholds)
    {
        if (features.Rsi is null)
        {
            return MomentumRegime.Unknown;
        }

        var rsi = features.Rsi.Value;

        if (rsi < thresholds.RsiOversold)
        {
            return MomentumRegime.Oversold;
        }

        if (rsi < thresholds.RsiWeakUpper)
        {
            return MomentumRegime.Weak;
        }

        if (rsi <= thresholds.RsiStrongLower)
        {
            return MomentumRegime.Neutral;
        }

        if (rsi < thresholds.RsiOverbought)
        {
            return MomentumRegime.Strong;
        }

        return MomentumRegime.Overbought;
    }
}

using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Interfaces.Analysis;

namespace StrategyForge.Domain.Models;

// ============================================================
// Market Regime — evidence about measurable market conditions.
// A regime DESCRIBES the market; it never produces a trading
// decision (no buy/sell). A strategy rule may later interpret it.
// ============================================================

/// <summary>Deterministic trend classification for one observation.</summary>
public enum TrendRegime
{
    /// <summary>Price above SMA and MACD line above zero (fast EMA above slow EMA).</summary>
    Bullish,

    /// <summary>Price below SMA and MACD line below zero (fast EMA below slow EMA).</summary>
    Bearish,

    /// <summary>Trend signals disagree (e.g., price above SMA but MACD below zero).</summary>
    Sideways,

    /// <summary>Required indicator values were unavailable for this observation.</summary>
    Unknown
}

/// <summary>
/// Deterministic volatility classification based on Bollinger bandwidth
/// as a percentage of the middle band. Thresholds are configurable
/// defaults for initial exploration, NOT universal financial definitions.
/// </summary>
public enum VolatilityRegime
{
    /// <summary>Bandwidth percent below the configured low threshold.</summary>
    Low,

    /// <summary>Bandwidth percent between the low and high thresholds.</summary>
    Normal,

    /// <summary>Bandwidth percent above the configured high threshold.</summary>
    High,

    /// <summary>Bollinger Bands values were unavailable for this observation.</summary>
    Unknown
}

/// <summary>
/// Deterministic momentum classification based on RSI bands.
/// Thresholds are configurable defaults for initial exploration,
/// NOT universal financial definitions.
/// </summary>
public enum MomentumRegime
{
    /// <summary>RSI below the oversold threshold (default 30).</summary>
    Oversold,

    /// <summary>RSI between the oversold and weak-upper thresholds.</summary>
    Weak,

    /// <summary>RSI between the weak-upper and strong-lower thresholds.</summary>
    Neutral,

    /// <summary>RSI between the strong-lower and overbought thresholds.</summary>
    Strong,

    /// <summary>RSI at or above the overbought threshold (default 70).</summary>
    Overbought,

    /// <summary>RSI was unavailable for this observation.</summary>
    Unknown
}

/// <summary>
/// The three-dimensional market regime snapshot for one observation.
/// Pure description of measurable conditions — never a trading decision.
/// </summary>
public sealed record MarketRegimeSnapshot
{
    public required TrendRegime Trend { get; init; }
    public required VolatilityRegime Volatility { get; init; }
    public required MomentumRegime Momentum { get; init; }
}

// ============================================================
// Market Features — deterministic projections of the enriched
// dataset. Every feature reads stored indicator values produced by
// the IndicatorEngine (single source of truth); nothing here
// recomputes an indicator.
// ============================================================

/// <summary>
/// Deterministic market features derived from one enriched observation and,
/// where noted, the immediately preceding observation (for rising/falling
/// comparisons). All indicator-derived fields are null when the underlying
/// indicator value was unavailable — never zero-fabricated.
/// </summary>
public sealed record MarketFeatures
{
    public required DateOnly ObservationDate { get; init; }

    /// <summary>Close price of the observation.</summary>
    public required decimal Close { get; init; }

    // --- Trend ---

    /// <summary>Stored SMA value (default period 20).</summary>
    public decimal? Sma { get; init; }

    /// <summary>True when Close &gt; SMA; null when SMA was unavailable.</summary>
    public bool? PriceAboveSma { get; init; }

    /// <summary>
    /// Stored MACD line value. The MACD line equals EMA(fast) − EMA(slow)
    /// (defaults 12/26), so this is the single-source-of-truth value for the
    /// fast/slow EMA relationship — no separate EMA pair is computed.
    /// </summary>
    public decimal? MacdLine { get; init; }

    /// <summary>
    /// True when the MACD line &gt; 0, which is exactly EMA(fast) &gt; EMA(slow);
    /// null when the MACD line was unavailable.
    /// </summary>
    public bool? EmaFastAboveSlow { get; init; }

    // --- Momentum ---

    /// <summary>Stored RSI value (default period 14, Wilder smoothing).</summary>
    public decimal? Rsi { get; init; }

    /// <summary>Stored MACD signal line value.</summary>
    public decimal? MacdSignal { get; init; }

    /// <summary>Stored MACD histogram value (MACD line − signal line).</summary>
    public decimal? MacdHistogram { get; init; }

    /// <summary>True when the histogram is greater than the previous observation's histogram; null when unavailable.</summary>
    public bool? HistogramRising { get; init; }

    /// <summary>True when the MACD line &gt; signal line; null when either was unavailable.</summary>
    public bool? MacdAboveSignal { get; init; }

    // --- Volatility (Bollinger Bands) ---

    public decimal? BollingerUpper { get; init; }
    public decimal? BollingerMiddle { get; init; }
    public decimal? BollingerLower { get; init; }

    /// <summary>
    /// (Upper − Lower) / Middle × 100 — a dimensionless, cross-instrument
    /// comparable volatility measure derived arithmetically from stored
    /// Bollinger components. Null when components or Middle are unavailable.
    /// </summary>
    public decimal? BandwidthPercent { get; init; }

    /// <summary>Stored Bollinger %B: position of Close within the bands.</summary>
    public decimal? PercentB { get; init; }

    // --- Volume ---

    /// <summary>
    /// Raw stored volume. Zero means the provider did not supply volume
    /// (e.g., TGJU) or no trades occurred — it is never treated as real
    /// trading volume and never fabricated (Phase 3/7 semantics).
    /// </summary>
    public long Volume { get; init; }

    /// <summary>True only when the source actually supplied a positive volume.</summary>
    public bool HasMeaningfulVolume { get; init; }
}

// ============================================================
// Thresholds — explicit, configurable, documented. Defaults are
// conservative starting points for exploration, not universal
// financial definitions.
// ============================================================

/// <summary>
/// Explicit thresholds for regime classification and deterministic rules.
/// Deterministic: identical inputs and thresholds always produce identical output.
/// </summary>
public sealed record StrategyThresholds
{
    // --- Momentum regime (RSI bands) ---
    /// <summary>RSI below this is <see cref="MomentumRegime.Oversold"/>. Default 30.</summary>
    public decimal RsiOversold { get; init; } = 30m;

    /// <summary>RSI below this (and at/above oversold) is <see cref="MomentumRegime.Weak"/>. Default 45.</summary>
    public decimal RsiWeakUpper { get; init; } = 45m;

    /// <summary>RSI at/below this is <see cref="MomentumRegime.Neutral"/>. Default 55.</summary>
    public decimal RsiStrongLower { get; init; } = 55m;

    /// <summary>RSI at/above this is <see cref="MomentumRegime.Overbought"/>. Default 70.</summary>
    public decimal RsiOverbought { get; init; } = 70m;

    // --- Volatility regime (Bollinger bandwidth percent) ---
    /// <summary>Bandwidth percent below this is <see cref="VolatilityRegime.Low"/>. Default 1.5%.</summary>
    public decimal LowVolatilityBandwidthPercent { get; init; } = 1.5m;

    /// <summary>Bandwidth percent above this is <see cref="VolatilityRegime.High"/>. Default 5%.</summary>
    public decimal HighVolatilityBandwidthPercent { get; init; } = 5.0m;

    // --- Deterministic rules ---
    /// <summary>Minimum RSI for the TrendFollowing rule. Default 50.</summary>
    public decimal TrendFollowingRsiMinimum { get; init; } = 50m;

    /// <summary>Maximum %B for the MeanReversion rule (price stretched toward/below the lower band). Default 0.2.</summary>
    public decimal MeanReversionMaxPercentB { get; init; } = 0.2m;

    /// <summary>Maximum RSI for the MeanReversion rule. Default 40.</summary>
    public decimal MeanReversionMaxRsi { get; init; } = 40m;

    public static StrategyThresholds Default { get; } = new();
}

// ============================================================
// Rules and evidence — every result is explainable from its input
// observations. Rules never return only "true": matched or not,
// the evaluated values are included.
// ============================================================

/// <summary>One structured evidence value: an actual number plus the comparison that was evaluated.</summary>
public sealed record StrategyEvidenceItem
{
    /// <summary>Feature/indicator name (e.g., "RSI", "MACD Histogram").</summary>
    public required string Name { get; init; }

    /// <summary>The actual value used in the comparison; null when unavailable.</summary>
    public decimal? Value { get; init; }

    /// <summary>Human-readable comparison outcome (e.g., "58.4 > 50 ✓", "missing").</summary>
    public string? Comparison { get; init; }
}

/// <summary>The outcome of evaluating one rule against one observation.</summary>
public sealed record StrategyRuleEvaluation
{
    public required string RuleName { get; init; }
    public required bool Matched { get; init; }

    /// <summary>Evidence for every condition the rule evaluates (actual values included).</summary>
    public IReadOnlyList<StrategyEvidenceItem> Evidence { get; init; } = [];
}

/// <summary>
/// Provider-neutral, structured strategy evidence for one observation —
/// answers "why did this rule match?" without inspecting implementation code.
/// Consumed by the later AI layer as high-quality structured input.
/// </summary>
public sealed record StrategyObservationEvidence
{
    public required string InstrumentId { get; init; }
    public required SourceAdapterType Source { get; init; }
    public required DateOnly ObservationDate { get; init; }

    public required MarketRegimeSnapshot Regime { get; init; }
    public required MarketFeatures Features { get; init; }

    public required IReadOnlyList<string> RulesEvaluated { get; init; }
    public required IReadOnlyList<string> RulesMatched { get; init; }

    /// <summary>Full per-rule evaluations with actual values.</summary>
    public required IReadOnlyList<StrategyRuleEvaluation> RuleEvaluations { get; init; }
}

// ============================================================
// Historical evaluation — deterministic forward-return statistics.
// These describe historical behavior under the chosen evaluation
// methodology; they do NOT prove profitability or predictive accuracy.
// ============================================================

/// <summary>Request for deterministic historical evaluation of strategy rules.</summary>
public sealed record StrategyEvaluationRequest
{
    public required string InstrumentId { get; init; }
    public required SourceAdapterType Source { get; init; }

    /// <summary>Inclusive start of the evaluation range (Gregorian).</summary>
    public required DateOnly From { get; init; }

    /// <summary>Inclusive end of the evaluation range (Gregorian).</summary>
    public required DateOnly To { get; init; }

    /// <summary>Rules to evaluate; null or empty evaluates all registered rules.</summary>
    public IReadOnlyList<string>? RuleNames { get; init; }

    /// <summary>Forward-return horizons in subsequent observations (trading days). Default 1/5/10.</summary>
    public IReadOnlyList<int>? ForwardHorizons { get; init; }

    /// <summary>Maximum number of matched-observation evidence samples returned. Default 5.</summary>
    public int MaxEvidenceSamples { get; init; } = 5;

    public IReadOnlyList<int> EffectiveForwardHorizons =>
        ForwardHorizons is { Count: > 0 } ? ForwardHorizons : [1, 5, 10];
}

/// <summary>
/// Forward-return statistics for one horizon. "Observations" are trading-day
/// offsets in the chronological sequence, not calendar days (holidays and
/// weekends must not shift the measurement).
/// </summary>
public sealed record ForwardReturnStats
{
    /// <summary>Horizon in subsequent observations.</summary>
    public required int HorizonObservations { get; init; }

    /// <summary>Matches whose forward return could be measured.</summary>
    public required int MeasurableCount { get; init; }

    /// <summary>Matches with a strictly positive forward return.</summary>
    public required int PositiveOutcomeCount { get; init; }

    /// <summary>Matches with a strictly negative forward return.</summary>
    public required int NegativeOutcomeCount { get; init; }

    /// <summary>Matches where the horizon extended beyond available data (never fabricated).</summary>
    public required int UnavailableOutcomeCount { get; init; }

    /// <summary>Mean of measurable forward returns; null when none were measurable.</summary>
    public decimal? AverageForwardReturn { get; init; }

    /// <summary>Median of measurable forward returns; null when none were measurable.</summary>
    public decimal? MedianForwardReturn { get; init; }
}

/// <summary>Deterministic evaluation statistics for one rule.</summary>
public sealed record StrategyRuleStatistics
{
    public required string RuleName { get; init; }
    public required int MatchCount { get; init; }

    /// <summary>One entry per requested horizon, in request order.</summary>
    public required IReadOnlyList<ForwardReturnStats> ForwardReturns { get; init; }
}

/// <summary>A matched observation with full evidence and its forward returns per horizon.</summary>
public sealed record StrategyMatchSample
{
    public required StrategyObservationEvidence Evidence { get; init; }

    /// <summary>Horizon → forward return; null when the horizon was unavailable.</summary>
    public required IReadOnlyDictionary<int, decimal?> ForwardReturns { get; init; }
}

/// <summary>Deterministic result of one historical evaluation run.</summary>
public sealed record StrategyEvaluationResult
{
    public required string InstrumentId { get; init; }
    public required SourceAdapterType Source { get; init; }
    public required DateOnly From { get; init; }
    public required DateOnly To { get; init; }

    /// <summary>Observations the rules were evaluated against.</summary>
    public required int ObservationsEvaluated { get; init; }

    /// <summary>Observations where no rule matched.</summary>
    public required int NoMatchCount { get; init; }

    /// <summary>Total rule matches across all evaluated rules (an observation can match multiple rules).</summary>
    public required int TotalRuleMatches { get; init; }

    public required IReadOnlyList<StrategyRuleStatistics> RuleStatistics { get; init; }

    /// <summary>Deterministic sample of matched observations with full evidence (bounded by MaxEvidenceSamples).</summary>
    public IReadOnlyList<StrategyMatchSample> MatchSamples { get; init; } = [];

    public static StrategyEvaluationResult Empty(
        StrategyEvaluationRequest request) => new()
    {
        InstrumentId = request.InstrumentId,
        Source = request.Source,
        From = request.From,
        To = request.To,
        ObservationsEvaluated = 0,
        NoMatchCount = 0,
        TotalRuleMatches = 0,
        RuleStatistics = []
    };
}

// ============================================================
// Chronological time-series splitting — past → future only.
// Random shuffling of time-series data is never performed.
// ============================================================

/// <summary>Chronological evaluation segments for training/validation/holdout separation.</summary>
public enum TimeSeriesSegment
{
    /// <summary>Earliest period — training/research.</summary>
    Research,

    /// <summary>Middle period — validation.</summary>
    Validation,

    /// <summary>Latest period — test/holdout.</summary>
    Holdout
}

/// <summary>
/// One chronological segment of the dataset. Dates are actual observation
/// dates from the dataset, never assumed calendar years.
/// </summary>
public sealed record TimeSeriesSplitSegment
{
    public required TimeSeriesSegment Segment { get; init; }
    public required DateOnly From { get; init; }

    /// <summary>Actual first observation date in the segment.</summary>
    public required DateOnly FirstObservation { get; init; }

    /// <summary>Actual last observation date in the segment.</summary>
    public required DateOnly LastObservation { get; init; }

    public required int Observations { get; init; }
}

/// <summary>Chronological split fractions. No shuffling is ever performed.</summary>
public sealed record TimeSeriesSplitFractions
{
    /// <summary>Fraction of observations assigned to Research. Default 0.6.</summary>
    public decimal ResearchFraction { get; init; } = 0.6m;

    /// <summary>Fraction assigned to Validation. Default 0.2; the remainder is Holdout.</summary>
    public decimal ValidationFraction { get; init; } = 0.2m;

    public static TimeSeriesSplitFractions Default { get; } = new();
}

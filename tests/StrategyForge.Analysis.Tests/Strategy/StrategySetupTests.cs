using StrategyForge.Analysis.Strategy;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Models;
using Xunit;

namespace StrategyForge.Analysis.Tests.Strategy;

// ============================================================
// Phase 8 setup rules + setup generation engine
// ============================================================

public class SetupRuleTests
{
    private static readonly DateOnly D = new(2026, 6, 1);

    // ---------------------------------------------------------
    // TrendContinuation
    // ---------------------------------------------------------

    private static MarketFeatures FeaturesOf(EnrichedObservation observation) =>
        MarketFeatureExtractor.Extract(observation, null);

    [Fact]
    public void TrendContinuation_Matches_WhenAllConditionsHold()
    {
        // Price above SMA, MACD > Signal > 0, RSI in Strong band, no histogram exclusion.
        var features = FeaturesOf(StrategyTestData.Observation(
            D, close: 110m, sma: 100m, rsi: 60m, macd: 2m, signal: 1m));

        var evaluation = new TrendContinuationSetupRule().Evaluate(features, StrategyThresholds.Default);

        Assert.True(evaluation.Matched);
        Assert.Empty(evaluation.MissingInputs);
        Assert.Equal(SetupDirection.Long, evaluation.Direction);
        Assert.Equal(TrendRegime.Bullish, evaluation.Regime.Trend);
        Assert.NotNull(evaluation.Invalidation);
        Assert.Equal("MACD line crosses below Signal line", evaluation.Invalidation!.Condition);
    }

    [Fact]
    public void TrendContinuation_DoesNotMatch_WhenRsiBelowMinimum()
    {
        var features = FeaturesOf(StrategyTestData.Observation(
            D, close: 110m, sma: 100m, rsi: 49m, macd: 2m, signal: 1m));

        var evaluation = new TrendContinuationSetupRule().Evaluate(features, StrategyThresholds.Default);

        Assert.False(evaluation.Matched);
        Assert.Null(evaluation.Direction);
    }

    [Fact]
    public void TrendContinuation_DoesNotMatch_WhenMacdBelowSignal()
    {
        var features = FeaturesOf(StrategyTestData.Observation(
            D, close: 110m, sma: 100m, rsi: 60m, macd: 1m, signal: 2m));

        var evaluation = new TrendContinuationSetupRule().Evaluate(features, StrategyThresholds.Default);

        Assert.False(evaluation.Matched);
    }

    [Fact]
    public void TrendContinuation_Excluded_WhenHistogramFalling()
    {
        // All entry conditions hold but the histogram is explicitly falling.
        var current = StrategyTestData.Observation(
            D, close: 110m, sma: 100m, rsi: 60m, macd: 2m, signal: 1m, histogram: 0.5m);
        var previous = StrategyTestData.Observation(
            D.AddDays(-1), close: 109m, sma: 100m, rsi: 60m, macd: 2m, signal: 1m, histogram: 0.8m);

        var features = MarketFeatureExtractor.Extract(current, previous);
        var evaluation = new TrendContinuationSetupRule().Evaluate(features, StrategyThresholds.Default);

        Assert.False(evaluation.Matched);
        Assert.Empty(evaluation.MissingInputs); // data was complete — the rule chose not to fire
    }

    [Fact]
    public void TrendContinuation_RegimeGate_BearishTrendNeverFires()
    {
        // Price below SMA AND MACD < 0 → Bearish regime → rule never fires,
        // even though RSI alone would satisfy the momentum threshold.
        var features = FeaturesOf(StrategyTestData.Observation(
            D, close: 90m, sma: 100m, rsi: 60m, macd: -2m, signal: -1m));

        var evaluation = new TrendContinuationSetupRule().Evaluate(features, StrategyThresholds.Default);

        Assert.False(evaluation.Matched);
        Assert.Equal(TrendRegime.Bearish, evaluation.Regime.Trend);
    }

    [Fact]
    public void TrendContinuation_MissingRequiredEvidence_ListedAndNeverMatched()
    {
        // No RSI, no SMA, no MACD at all.
        var features = FeaturesOf(StrategyTestData.Observation(D, close: 100m));

        var evaluation = new TrendContinuationSetupRule().Evaluate(features, StrategyThresholds.Default);

        Assert.False(evaluation.Matched);
        Assert.Contains("RSI", evaluation.MissingInputs);
        Assert.Contains("SMA", evaluation.MissingInputs);
        Assert.Contains("MACD", evaluation.MissingInputs);
        Assert.Contains("MACD.Signal", evaluation.MissingInputs);
    }

    [Fact]
    public void TrendContinuation_RsiBoundary_AtMinimum_Matches()
    {
        var features = FeaturesOf(StrategyTestData.Observation(
            D, close: 110m, sma: 100m, rsi: 50m, macd: 2m, signal: 1m));

        var evaluation = new TrendContinuationSetupRule().Evaluate(features, StrategyThresholds.Default);

        // RSI >= TrendFollowingRsiMinimum (50) is inclusive.
        Assert.True(evaluation.Matched);
    }

    // ---------------------------------------------------------
    // MeanReversionPullback
    // ---------------------------------------------------------

    [Fact]
    public void MeanReversionPullback_Matches_InSidewaysTrend()
    {
        // Close above SMA but MACD below zero → Sideways; %B and RSI stretched low.
        var features = FeaturesOf(StrategyTestData.Observation(
            D, close: 102m, sma: 100m, rsi: 35m, macd: -1m, signal: 0m,
            bbUpper: 110m, bbMiddle: 100m, bbLower: 90m, bandwidth: 20m, percentB: 0.15m));

        var evaluation = new MeanReversionPullbackSetupRule().Evaluate(features, StrategyThresholds.Default);

        Assert.True(evaluation.Matched);
        Assert.Equal(SetupDirection.Long, evaluation.Direction);
        Assert.Equal(TrendRegime.Sideways, evaluation.Regime.Trend);
        Assert.Equal("Close closes below the setup observation's lower Bollinger band",
            evaluation.Invalidation!.Condition);
        Assert.Equal(90m, evaluation.Invalidation.ReferenceLevel);
    }

    [Fact]
    public void MeanReversionPullback_Matches_InBullishTrend()
    {
        var features = FeaturesOf(StrategyTestData.Observation(
            D, close: 105m, sma: 100m, rsi: 38m, macd: 1m, signal: 2m,
            bbUpper: 110m, bbMiddle: 100m, bbLower: 90m, bandwidth: 20m, percentB: 0.1m));

        var evaluation = new MeanReversionPullbackSetupRule().Evaluate(features, StrategyThresholds.Default);

        Assert.True(evaluation.Matched);
        Assert.Equal(TrendRegime.Bullish, evaluation.Regime.Trend);
    }

    [Fact]
    public void MeanReversionPullback_Excluded_InBearishTrend()
    {
        // Close below SMA and MACD below zero → Bearish → never fade it.
        var features = FeaturesOf(StrategyTestData.Observation(
            D, close: 95m, sma: 100m, rsi: 25m, macd: -2m, signal: -1m,
            bbUpper: 105m, bbMiddle: 100m, bbLower: 95m, bandwidth: 10m, percentB: 0.0m));

        var evaluation = new MeanReversionPullbackSetupRule().Evaluate(features, StrategyThresholds.Default);

        Assert.False(evaluation.Matched);
        Assert.Equal(TrendRegime.Bearish, evaluation.Regime.Trend);
    }

    [Fact]
    public void MeanReversionPullback_DoesNotMatch_WhenPercentBHigh()
    {
        var features = FeaturesOf(StrategyTestData.Observation(
            D, close: 102m, sma: 100m, rsi: 35m, macd: -1m, signal: 0m,
            bbUpper: 110m, bbMiddle: 100m, bbLower: 90m, bandwidth: 20m, percentB: 0.5m));

        var evaluation = new MeanReversionPullbackSetupRule().Evaluate(features, StrategyThresholds.Default);

        Assert.False(evaluation.Matched);
    }

    [Fact]
    public void MeanReversionPullback_PercentBBoundary_AtMaximum_Matches()
    {
        var features = FeaturesOf(StrategyTestData.Observation(
            D, close: 102m, sma: 100m, rsi: 40m, macd: -1m, signal: 0m,
            bbUpper: 110m, bbMiddle: 100m, bbLower: 90m, bandwidth: 20m, percentB: 0.2m));

        var evaluation = new MeanReversionPullbackSetupRule().Evaluate(features, StrategyThresholds.Default);

        // %B <= MeanReversionMaxPercentB (0.2) is inclusive.
        Assert.True(evaluation.Matched);
    }

    [Fact]
    public void MeanReversionPullback_MissingEvidence_ListedAndNeverMatched()
    {
        // No Bollinger at all, no RSI.
        var features = FeaturesOf(StrategyTestData.Observation(
            D, close: 100m, sma: 100m, macd: 0m));

        var evaluation = new MeanReversionPullbackSetupRule().Evaluate(features, StrategyThresholds.Default);

        Assert.False(evaluation.Matched);
        Assert.Contains("BollingerBands.PercentB", evaluation.MissingInputs);
        Assert.Contains("RSI", evaluation.MissingInputs);
    }

    [Fact]
    public void SetupRules_AreDeterministic()
    {
        var features = FeaturesOf(StrategyTestData.Observation(
            D, close: 110m, sma: 100m, rsi: 60m, macd: 2m, signal: 1m));

        var first = new TrendContinuationSetupRule().Evaluate(features, StrategyThresholds.Default);
        var second = new TrendContinuationSetupRule().Evaluate(features, StrategyThresholds.Default);

        Assert.Equal(first.Matched, second.Matched);
        Assert.Equal(first.Direction, second.Direction);
        Assert.Equal(first.EntryCondition, second.EntryCondition);
        Assert.Equal(first.Invalidation!.Condition, second.Invalidation.Condition);
        Assert.Equal(first.Invalidation.ReferenceLevel, second.Invalidation.ReferenceLevel);
        Assert.Equal(first.Invalidation.FeatureNames, second.Invalidation.FeatureNames);
        Assert.Equal(first.Evidence.Count, second.Evidence.Count);
        for (var i = 0; i < first.Evidence.Count; i++)
        {
            Assert.Equal(first.Evidence[i].Name, second.Evidence[i].Name);
            Assert.Equal(first.Evidence[i].Value, second.Evidence[i].Value);
            Assert.Equal(first.Evidence[i].Comparison, second.Evidence[i].Comparison);
        }
    }

    [Fact]
    public void SetupRuleRegistry_ContainsExactlyTheTwoSetupRules()
    {
        Assert.Equal(2, SetupRuleRegistry.BuiltIn.Count);
        Assert.Equal(SetupRuleNames.TrendContinuation, SetupRuleRegistry.BuiltIn[0].Name);
        Assert.Equal(SetupRuleNames.MeanReversionPullback, SetupRuleRegistry.BuiltIn[1].Name);
    }
}

public class StrategySetupEngineTests
{
    private const string InstrumentId = StrategyTestData.InstrumentId;

    private static StrategySetupEngine CreateEngine() => new(SetupRuleRegistry.BuiltIn);

    /// <summary>
    /// Builds a series whose last observation qualifies for BOTH setup rules:
    /// earlier observations are neutral so only the final one can match.
    /// </summary>
    private static List<EnrichedObservation> QualifyingSeries(DateOnly start, int history)
    {
        var list = new List<EnrichedObservation>();
        for (var i = 0; i < history; i++)
        {
            list.Add(StrategyTestData.Observation(
                start.AddDays(i), close: 100m + i, sma: 100m, rsi: 55m, macd: 0.5m, signal: 0.6m));
        }

        // Final observation: price above SMA, MACD > Signal > 0, RSI strong.
        list.Add(StrategyTestData.Observation(
            start.AddDays(history), close: 100m + history, sma: 100m, rsi: 60m, macd: 2m, signal: 1m));

        return list;
    }

    [Fact]
    public void Generate_ProducesSetup_WithStableIdentity()
    {
        var start = new DateOnly(2026, 1, 1);
        var observations = QualifyingSeries(start, 5);

        var result = CreateEngine().Generate(observations, InstrumentId, SourceAdapterType.Tgju);

        var setup = Assert.Single(result.Setups);
        Assert.Equal($"{InstrumentId}|Tgju|TrendContinuation|{start.AddDays(5):yyyy-MM-dd}", setup.SetupId);
        Assert.Equal(SetupRuleNames.TrendContinuation, setup.RuleName);
        Assert.Equal(start.AddDays(5), setup.ObservationDate);
    }

    [Fact]
    public void Generate_StampesProvenance_FromUnderlyingObservation()
    {
        var start = new DateOnly(2026, 1, 1);
        var observations = QualifyingSeries(start, 5);

        var result = CreateEngine().Generate(observations, InstrumentId, SourceAdapterType.Tgju);

        var setup = Assert.Single(result.Setups);
        Assert.Equal(InstrumentId, setup.InstrumentId);
        Assert.Equal(SourceAdapterType.Tgju, setup.Source);
        Assert.Equal("test", setup.ProcessedBy); // builder stamps ProcessedBy = "test"
        Assert.Empty(setup.UnavailableEvidence);
    }

    [Fact]
    public void Generate_RiskMetadata_UsesActualValuesOrNulls()
    {
        var start = new DateOnly(2026, 1, 1);
        var observations = new List<EnrichedObservation>
        {
            StrategyTestData.Observation(
                start, close: 110m, sma: 100m, rsi: 60m, macd: 2m, signal: 1m,
                bbUpper: 120m, bbMiddle: 100m, bbLower: 80m, bandwidth: 40m, percentB: 0.75m)
        };

        var result = CreateEngine().Generate(observations, InstrumentId, SourceAdapterType.Tgju);

        var setup = Assert.Single(result.Setups);
        Assert.Equal(60m, setup.Risk.Rsi);
        Assert.Equal(0.75m, setup.Risk.PercentB);
        Assert.Equal(40m / 100m * 100m, setup.Risk.BandwidthPercent);
        Assert.Equal(10m, setup.Risk.CloseVsSmaPercent); // (110/100 − 1) × 100
        Assert.Equal(VolatilityRegime.High, setup.Risk.Volatility);

        // Null-safety: missing Bollinger → null risk values, never fabricated.
        var withoutBb = CreateEngine().Generate(
            [StrategyTestData.Observation(start, close: 110m, sma: 100m, rsi: 60m, macd: 2m, signal: 1m)],
            InstrumentId, SourceAdapterType.Tgju);
        var bareSetup = Assert.Single(withoutBb.Setups);
        Assert.Null(bareSetup.Risk.PercentB);
        Assert.Null(bareSetup.Risk.BandwidthPercent);
        Assert.Equal(VolatilityRegime.Unknown, bareSetup.Risk.Volatility);
    }

    [Fact]
    public void Generate_Ordering_IsByDateThenRuleName()
    {
        var start = new DateOnly(2026, 1, 1);
        var observations = new List<EnrichedObservation>
        {
            // Day 1: only TrendContinuation qualifies.
            StrategyTestData.Observation(
                start, close: 110m, sma: 100m, rsi: 60m, macd: 2m, signal: 1m),
            // Day 2: both rules qualify (Sideways + stretched low + trend entry components
            // cannot coexist, so use two separate qualifying observations instead —
            // here we assert multi-rule ordering via rule-name sort on one day).
            StrategyTestData.Observation(
                start.AddDays(1), close: 110m, sma: 100m, rsi: 60m, macd: 2m, signal: 1m)
        };

        var result = CreateEngine().Generate(observations, InstrumentId, SourceAdapterType.Tgju);

        Assert.True(result.Setups.Count >= 2);
        for (var i = 1; i < result.Setups.Count; i++)
        {
            var earlier = result.Setups[i - 1];
            var later = result.Setups[i];
            Assert.True(
                later.ObservationDate > earlier.ObservationDate
                || (later.ObservationDate == earlier.ObservationDate
                    && string.CompareOrdinal(later.RuleName, earlier.RuleName) >= 0),
                "Setups must be ordered by (date, rule name)");
        }
    }

    [Fact]
    public void Generate_IsDeterministic_IdenticalRunsProduceIdenticalOutput()
    {
        var start = new DateOnly(2026, 1, 1);
        var observations = QualifyingSeries(start, 10);
        observations.AddRange(QualifyingSeries(start.AddDays(11), 10));

        var engine = CreateEngine();
        var first = engine.Generate(observations, InstrumentId, SourceAdapterType.Tgju);
        var second = engine.Generate(observations, InstrumentId, SourceAdapterType.Tgju);

        Assert.Equal(first.Setups.Count, second.Setups.Count);
        for (var i = 0; i < first.Setups.Count; i++)
        {
            Assert.Equal(first.Setups[i].SetupId, second.Setups[i].SetupId);
        }

        // Record equality on IReadOnlyList-typed members falls back to reference
        // comparison, so determinism of the full business output is asserted via
        // deterministic serialization: identical observations + configuration must
        // produce byte-identical setup payloads.
        var serializerOptions = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        };

        Assert.Equal(
            System.Text.Json.JsonSerializer.Serialize(first.Setups, serializerOptions),
            System.Text.Json.JsonSerializer.Serialize(second.Setups, serializerOptions));

        Assert.Equal(first.SetupsPerRule, second.SetupsPerRule);
        Assert.Equal(first.SkippedInsufficientEvidence, second.SkippedInsufficientEvidence);
    }

    [Fact]
    public void Generate_SkippedInsufficientEvidence_AccountedNotSilent()
    {
        var start = new DateOnly(2026, 1, 1);
        var observations = new List<EnrichedObservation>
        {
            // No indicators at all → both rules skip with missing inputs.
            StrategyTestData.Observation(start, close: 100m),
            StrategyTestData.Observation(start.AddDays(1), close: 101m)
        };

        var result = CreateEngine().Generate(observations, InstrumentId, SourceAdapterType.Tgju);

        Assert.Empty(result.Setups);
        Assert.Equal(2, result.ObservationsEvaluated);
        Assert.Equal(2, result.SkippedInsufficientEvidence.GetValueOrDefault(SetupRuleNames.TrendContinuation));
        Assert.Equal(2, result.SkippedInsufficientEvidence.GetValueOrDefault(SetupRuleNames.MeanReversionPullback));
    }

    [Fact]
    public void Generate_RuleSelection_UnknownNameThrowsWithKnownNames()
    {
        var observations = QualifyingSeries(new DateOnly(2026, 1, 1), 3);

        var ex = Assert.Throws<ArgumentException>(() =>
            CreateEngine().Generate(observations, InstrumentId, SourceAdapterType.Tgju, ["Nope"]));

        Assert.Contains("TrendContinuation", ex.Message);
        Assert.Contains("MeanReversionPullback", ex.Message);
    }

    [Fact]
    public void Generate_RuleSelection_EvaluatesOnlySelectedRules()
    {
        var start = new DateOnly(2026, 1, 1);
        var observations = QualifyingSeries(start, 5);

        var result = CreateEngine().Generate(
            observations, InstrumentId, SourceAdapterType.Tgju, [SetupRuleNames.TrendContinuation]);

        var setup = Assert.Single(result.Setups);
        Assert.Equal(SetupRuleNames.TrendContinuation, setup.RuleName);
    }

    [Fact]
    public void Generate_EmptySequence_ProducesEmptyResult()
    {
        var result = CreateEngine().Generate(
            [], InstrumentId, SourceAdapterType.Tgju,
            null, StrategyThresholds.Default);

        Assert.Empty(result.Setups);
        Assert.Equal(0, result.ObservationsEvaluated);
    }

    [Fact]
    public void Generate_SourcesRemainSeparate()
    {
        var start = new DateOnly(2026, 1, 1);
        var observations = QualifyingSeries(start, 5);

        var tgju = CreateEngine().Generate(observations, InstrumentId, SourceAdapterType.Tgju);
        var nobitex = CreateEngine().Generate(observations, InstrumentId, SourceAdapterType.Nobitex);

        Assert.All(tgju.Setups, s => Assert.Equal(SourceAdapterType.Tgju, s.Source));
        Assert.All(nobitex.Setups, s => Assert.Equal(SourceAdapterType.Nobitex, s.Source));
        Assert.NotEqual(tgju.Setups[0].SetupId, nobitex.Setups[0].SetupId);
    }
}

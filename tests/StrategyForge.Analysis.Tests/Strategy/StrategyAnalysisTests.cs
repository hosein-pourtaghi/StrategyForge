using StrategyForge.Analysis.Strategy;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Models;
using Xunit;

namespace StrategyForge.Analysis.Tests.Strategy;

// ============================================================
// Test-data builders
// ============================================================

/// <summary>
/// Builds enriched observations with explicit indicator values so every test
/// expectation is precisely computable. These builders never recompute
/// indicators — they simulate what the Phase 7 enrichment pipeline stores.
/// </summary>
public static class StrategyTestData
{
    public const string InstrumentId = "test-usd-irr";

    public static EnrichedObservation Observation(
        DateOnly date,
        decimal close,
        decimal? sma = null,
        decimal? rsi = null,
        decimal? macd = null,
        decimal? signal = null,
        decimal? histogram = null,
        decimal? bbUpper = null,
        decimal? bbMiddle = null,
        decimal? bbLower = null,
        decimal? bandwidth = null,
        decimal? percentB = null,
        long volume = 0,
        string source = "Tgju")
    {
        var indicators = new Dictionary<string, IReadOnlyDictionary<string, decimal>>();

        if (sma.HasValue)
        {
            indicators["SMA"] = new Dictionary<string, decimal> { ["SMA"] = sma.Value };
        }

        if (rsi.HasValue)
        {
            indicators["RSI"] = new Dictionary<string, decimal> { ["RSI"] = rsi.Value };
        }

        if (macd.HasValue)
        {
            var macdComponents = new Dictionary<string, decimal> { ["MACD"] = macd.Value };
            if (signal.HasValue)
            {
                macdComponents["Signal"] = signal.Value;
            }
            if (histogram.HasValue)
            {
                macdComponents["Histogram"] = histogram.Value;
            }
            indicators["MACD"] = macdComponents;
        }

        if (bbUpper.HasValue)
        {
            var bb = new Dictionary<string, decimal>
            {
                ["Upper"] = bbUpper.Value,
                ["Middle"] = bbMiddle ?? 0m,
                ["Lower"] = bbLower ?? 0m,
                ["Bandwidth"] = bandwidth ?? 0m,
                ["PercentB"] = percentB ?? 0m
            };
            indicators["BollingerBands"] = bb;
        }

        return new EnrichedObservation
        {
            InstrumentId = InstrumentId,
            Source = Enum.Parse<SourceAdapterType>(source),
            ObservationDate = date,
            Open = close - 1,
            High = close + 1,
            Low = close - 2,
            Close = close,
            Volume = volume,
            Indicators = indicators,
            QualityStatus = HistoricalDataQualityStatus.Valid,
            ProcessedBy = "test"
        };
    }
}

// ============================================================
// Feature extraction tests
// ============================================================

public class MarketFeatureExtractorTests
{
    [Fact]
    public void Extract_PriceAboveSma_ComputedFromStoredSma()
    {
        var o = StrategyTestData.Observation(
            new DateOnly(2026, 1, 5), close: 110m, sma: 100m);

        var f = MarketFeatureExtractor.Extract(o, null);

        Assert.Equal(110m, f.Close);
        Assert.Equal(100m, f.Sma);
        Assert.True(f.PriceAboveSma);
    }

    [Fact]
    public void Extract_PriceBelowSma()
    {
        var o = StrategyTestData.Observation(
            new DateOnly(2026, 1, 5), close: 90m, sma: 100m);

        var f = MarketFeatureExtractor.Extract(o, null);

        Assert.False(f.PriceAboveSma);
    }

    [Fact]
    public void Extract_MissingSma_PriceAboveSmaIsNull()
    {
        var o = StrategyTestData.Observation(new DateOnly(2026, 1, 5), close: 100m);

        var f = MarketFeatureExtractor.Extract(o, null);

        Assert.Null(f.Sma);
        Assert.Null(f.PriceAboveSma);
    }

    [Fact]
    public void Extract_MacdComponents_MappedToFeatures()
    {
        var o = StrategyTestData.Observation(
            new DateOnly(2026, 1, 5), close: 100m,
            macd: 12.4m, signal: 8.7m, histogram: 3.7m);

        var f = MarketFeatureExtractor.Extract(o, null);

        Assert.Equal(12.4m, f.MacdLine);
        Assert.Equal(8.7m, f.MacdSignal);
        Assert.Equal(3.7m, f.MacdHistogram);
        Assert.True(f.MacdAboveSignal);
        // MACD > 0 is exactly "EMA fast above EMA slow" via the stored MACD line.
        Assert.True(f.EmaFastAboveSlow);
    }

    [Fact]
    public void Extract_MacdNegative_EmaFastBelowSlow()
    {
        var o = StrategyTestData.Observation(
            new DateOnly(2026, 1, 5), close: 100m,
            macd: -3.2m, signal: -1.0m, histogram: -2.2m);

        var f = MarketFeatureExtractor.Extract(o, null);

        Assert.False(f.EmaFastAboveSlow);
        Assert.False(f.MacdAboveSignal);
    }

    [Fact]
    public void Extract_HistogramRising_ComparedAgainstPreviousObservation()
    {
        var date = new DateOnly(2026, 1, 5);
        var prev = StrategyTestData.Observation(date.AddDays(-1), close: 100m,
            macd: 1m, signal: 2m, histogram: -1m);
        var current = StrategyTestData.Observation(date, close: 101m,
            macd: 2m, signal: 2m, histogram: 0m);

        var f = MarketFeatureExtractor.Extract(current, prev);

        Assert.True(f.HistogramRising);
    }

    [Fact]
    public void Extract_HistogramFalling()
    {
        var date = new DateOnly(2026, 1, 5);
        var prev = StrategyTestData.Observation(date.AddDays(-1), close: 100m,
            macd: 2m, signal: 1m, histogram: 1m);
        var current = StrategyTestData.Observation(date, close: 101m,
            macd: 1.5m, signal: 1m, histogram: 0.5m);

        var f = MarketFeatureExtractor.Extract(current, prev);

        Assert.False(f.HistogramRising);
    }

    [Fact]
    public void Extract_FirstObservation_HistogramRisingIsNull()
    {
        var o = StrategyTestData.Observation(
            new DateOnly(2026, 1, 5), close: 100m, macd: 1m, signal: 0m, histogram: 5m);

        var f = MarketFeatureExtractor.Extract(o, null);

        Assert.Null(f.HistogramRising);
    }

    [Fact]
    public void Extract_BandwidthPercent_DerivedArithmeticallyFromStoredComponents()
    {
        // Bandwidth 5 on Middle 100 → 5%.
        var o = StrategyTestData.Observation(
            new DateOnly(2026, 1, 5), close: 100m,
            bbUpper: 102.5m, bbMiddle: 100m, bbLower: 97.5m,
            bandwidth: 5m, percentB: 0.5m);

        var f = MarketFeatureExtractor.Extract(o, null);

        Assert.Equal(5m, f.BandwidthPercent);
        Assert.Equal(0.5m, f.PercentB);
    }

    [Fact]
    public void Extract_ZeroMiddleBand_BandwidthPercentIsNull_NoDivisionByZero()
    {
        var o = StrategyTestData.Observation(
            new DateOnly(2026, 1, 5), close: 100m,
            bbUpper: 2m, bbMiddle: 0m, bbLower: -2m, bandwidth: 4m);

        var f = MarketFeatureExtractor.Extract(o, null);

        Assert.Null(f.BandwidthPercent);
    }

    [Fact]
    public void Extract_VolumeSemantics_TgjuZeroVolumeIsNotMeaningful()
    {
        // TGJU does not supply volume (Phase 3): Volume = 0 must not count as real volume.
        var o = StrategyTestData.Observation(new DateOnly(2026, 1, 5), close: 100m, volume: 0);

        var f = MarketFeatureExtractor.Extract(o, null);

        Assert.Equal(0, f.Volume);
        Assert.False(f.HasMeaningfulVolume);
    }

    [Fact]
    public void Extract_PositiveVolume_IsMeaningful()
    {
        var o = StrategyTestData.Observation(new DateOnly(2026, 1, 5), close: 100m, volume: 1000);

        var f = MarketFeatureExtractor.Extract(o, null);

        Assert.True(f.HasMeaningfulVolume);
    }

    [Fact]
    public void Extract_IsDeterministic()
    {
        var prev = StrategyTestData.Observation(new DateOnly(2026, 1, 4), close: 100m,
            macd: 1m, signal: 0m, histogram: 1m);
        var current = StrategyTestData.Observation(new DateOnly(2026, 1, 5), close: 101m,
            sma: 99m, rsi: 55m, macd: 2m, signal: 1m, histogram: 1m,
            bbUpper: 103m, bbMiddle: 100m, bbLower: 97m, bandwidth: 6m, percentB: 0.6m);

        var a = MarketFeatureExtractor.Extract(current, prev);
        var b = MarketFeatureExtractor.Extract(current, prev);

        Assert.Equal(a, b);
    }
}

// ============================================================
// Regime classification tests
// ============================================================

public class MarketRegimeClassifierTests
{
    private readonly StrategyThresholds _t = StrategyThresholds.Default;

    [Fact]
    public void Trend_Bullish_WhenPriceAboveSmaAndMacdPositive()
    {
        var f = MarketFeatureExtractor.Extract(
            StrategyTestData.Observation(new DateOnly(2026, 1, 5), close: 110m, sma: 100m, macd: 2m), null);

        Assert.Equal(TrendRegime.Bullish, MarketRegimeClassifier.ClassifyTrend(f));
    }

    [Fact]
    public void Trend_Bearish_WhenPriceBelowSmaAndMacdNegative()
    {
        var f = MarketFeatureExtractor.Extract(
            StrategyTestData.Observation(new DateOnly(2026, 1, 5), close: 90m, sma: 100m, macd: -2m), null);

        Assert.Equal(TrendRegime.Bearish, MarketRegimeClassifier.ClassifyTrend(f));
    }

    [Fact]
    public void Trend_Sideways_WhenSignalsDisagree()
    {
        var f = MarketFeatureExtractor.Extract(
            StrategyTestData.Observation(new DateOnly(2026, 1, 5), close: 110m, sma: 100m, macd: -2m), null);

        Assert.Equal(TrendRegime.Sideways, MarketRegimeClassifier.ClassifyTrend(f));
    }

    [Fact]
    public void Trend_Unknown_WhenIndicatorsMissing()
    {
        var f = MarketFeatureExtractor.Extract(
            StrategyTestData.Observation(new DateOnly(2026, 1, 5), close: 100m), null);

        Assert.Equal(TrendRegime.Unknown, MarketRegimeClassifier.ClassifyTrend(f));
    }

    [Theory]
    [InlineData(1.0, VolatilityRegime.Low)]     // BandwidthPercent 1% < 1.5
    [InlineData(3.0, VolatilityRegime.Normal)]  // 1.5 ≤ 3 ≤ 5
    [InlineData(6.0, VolatilityRegime.High)]    // > 5
    public void Volatility_ThresholdsAreExplicitAndRespected(decimal bandwidthPercent, VolatilityRegime expected)
    {
        // Middle = 100 → stored Bandwidth = BandwidthPercent × 1.
        var f = MarketFeatureExtractor.Extract(
            StrategyTestData.Observation(
                new DateOnly(2026, 1, 5), close: 100m,
                bbUpper: 101m, bbMiddle: 100m, bbLower: 99m, bandwidth: bandwidthPercent), null);

        Assert.Equal(expected, MarketRegimeClassifier.ClassifyVolatility(f, _t));
    }

    [Fact]
    public void Volatility_Unknown_WhenBollingerMissing()
    {
        var f = MarketFeatureExtractor.Extract(
            StrategyTestData.Observation(new DateOnly(2026, 1, 5), close: 100m), null);

        Assert.Equal(VolatilityRegime.Unknown, MarketRegimeClassifier.ClassifyVolatility(f, _t));
    }

    [Theory]
    [InlineData(25, MomentumRegime.Oversold)]  // < 30
    [InlineData(35, MomentumRegime.Weak)]      // [30, 45)
    [InlineData(50, MomentumRegime.Neutral)]   // [45, 55]
    [InlineData(60, MomentumRegime.Strong)]    // (55, 70)
    [InlineData(75, MomentumRegime.Overbought)] // >= 70
    public void Momentum_ThresholdsAreExplicitAndRespected(decimal rsi, MomentumRegime expected)
    {
        var f = MarketFeatureExtractor.Extract(
            StrategyTestData.Observation(new DateOnly(2026, 1, 5), close: 100m, rsi: rsi), null);

        Assert.Equal(expected, MarketRegimeClassifier.ClassifyMomentum(f, _t));
    }

    [Fact]
    public void Momentum_CustomThresholds_AreRespected()
    {
        var f = MarketFeatureExtractor.Extract(
            StrategyTestData.Observation(new DateOnly(2026, 1, 5), close: 100m, rsi: 40m), null);

        var custom = new StrategyThresholds { RsiOversold = 20m, RsiWeakUpper = 35m };

        Assert.Equal(MomentumRegime.Neutral, MarketRegimeClassifier.ClassifyMomentum(f, custom));
    }

    [Fact]
    public void Momentum_Unknown_WhenRsiMissing()
    {
        var f = MarketFeatureExtractor.Extract(
            StrategyTestData.Observation(new DateOnly(2026, 1, 5), close: 100m), null);

        Assert.Equal(MomentumRegime.Unknown, MarketRegimeClassifier.ClassifyMomentum(f, _t));
    }
}

// ============================================================
// Rule evaluation tests
// ============================================================

public class DeterministicRuleTests
{
    private readonly StrategyThresholds _t = StrategyThresholds.Default;

    [Fact]
    public void TrendFollowing_Matches_WhenAllConditionsHold()
    {
        // close 110 > SMA 100; MACD 2 > Signal 1 and > 0; RSI 55 >= 50.
        var f = MarketFeatureExtractor.Extract(
            StrategyTestData.Observation(
                new DateOnly(2026, 1, 5), close: 110m, sma: 100m,
                rsi: 55m, macd: 2m, signal: 1m, histogram: 1m), null);

        var result = new TrendFollowingRule().Evaluate(f, _t);

        Assert.True(result.Matched);
        Assert.Equal(3, result.Evidence.Count);
        // Evidence contains actual values, not descriptions.
        Assert.Contains(result.Evidence, e => e.Name == "RSI" && e.Value == 55m);
        Assert.Contains(result.Evidence, e => e.Name == "MACD" && e.Value == 2m);
    }

    [Fact]
    public void TrendFollowing_DoesNotMatch_WhenRsiBelowMinimum()
    {
        var f = MarketFeatureExtractor.Extract(
            StrategyTestData.Observation(
                new DateOnly(2026, 1, 5), close: 110m, sma: 100m,
                rsi: 45m, macd: 2m, signal: 1m, histogram: 1m), null);

        var result = new TrendFollowingRule().Evaluate(f, _t);

        Assert.False(result.Matched);
        // Evidence still explains WHY it did not match.
        Assert.Contains(result.Evidence, e => e.Name == "RSI" && e.Comparison!.Contains("✗"));
    }

    [Fact]
    public void TrendFollowing_DoesNotMatch_WhenMacdBelowSignal()
    {
        var f = MarketFeatureExtractor.Extract(
            StrategyTestData.Observation(
                new DateOnly(2026, 1, 5), close: 110m, sma: 100m,
                rsi: 55m, macd: 1m, signal: 2m, histogram: -1m), null);

        var result = new TrendFollowingRule().Evaluate(f, _t);

        Assert.False(result.Matched);
    }

    [Fact]
    public void TrendFollowing_MissingRsi_DoesNotMatchAndExplains()
    {
        var f = MarketFeatureExtractor.Extract(
            StrategyTestData.Observation(
                new DateOnly(2026, 1, 5), close: 110m, sma: 100m,
                macd: 2m, signal: 1m, histogram: 1m), null);

        var result = new TrendFollowingRule().Evaluate(f, _t);

        Assert.False(result.Matched);
        Assert.Contains(result.Evidence, e => e.Name == "RSI" && e.Value is null && e.Comparison == "missing");
    }

    [Fact]
    public void Momentum_Matches_WhenHistogramRisingAndRsiInStrongBand()
    {
        var date = new DateOnly(2026, 1, 5);
        var prev = StrategyTestData.Observation(date.AddDays(-1), close: 100m,
            macd: 1m, signal: 1.5m, histogram: -0.5m);
        var current = StrategyTestData.Observation(date, close: 101m,
            rsi: 60m, macd: 2m, signal: 1m, histogram: 1m);

        var f = MarketFeatureExtractor.Extract(current, prev);

        var result = new MomentumRule().Evaluate(f, _t);

        Assert.True(result.Matched);
    }

    [Fact]
    public void Momentum_DoesNotMatch_WhenHistogramFalling()
    {
        var date = new DateOnly(2026, 1, 5);
        var prev = StrategyTestData.Observation(date.AddDays(-1), close: 100m,
            macd: 3m, signal: 1m, histogram: 2m);
        var current = StrategyTestData.Observation(date, close: 101m,
            rsi: 60m, macd: 2m, signal: 1m, histogram: 1m);

        var f = MarketFeatureExtractor.Extract(current, prev);

        Assert.False(new MomentumRule().Evaluate(f, _t).Matched);
    }

    [Fact]
    public void Momentum_DoesNotMatch_WhenRsiOverbought()
    {
        var date = new DateOnly(2026, 1, 5);
        var prev = StrategyTestData.Observation(date.AddDays(-1), close: 100m,
            macd: 1m, signal: 1.5m, histogram: -0.5m);
        var current = StrategyTestData.Observation(date, close: 101m,
            rsi: 72m, macd: 2m, signal: 1m, histogram: 1m);

        var f = MarketFeatureExtractor.Extract(current, prev);

        Assert.False(new MomentumRule().Evaluate(f, _t).Matched);
    }

    [Fact]
    public void MeanReversion_Matches_WhenPercentBAndRsiAreLow()
    {
        var f = MarketFeatureExtractor.Extract(
            StrategyTestData.Observation(
                new DateOnly(2026, 1, 5), close: 97m,
                rsi: 28m,
                bbUpper: 101m, bbMiddle: 100m, bbLower: 99m,
                bandwidth: 2m, percentB: 0.1m), null);

        var result = new MeanReversionRule().Evaluate(f, _t);

        Assert.True(result.Matched);
    }

    [Fact]
    public void MeanReversion_DoesNotMatch_WhenPercentBHigh()
    {
        var f = MarketFeatureExtractor.Extract(
            StrategyTestData.Observation(
                new DateOnly(2026, 1, 5), close: 100m,
                rsi: 28m,
                bbUpper: 101m, bbMiddle: 100m, bbLower: 99m,
                bandwidth: 2m, percentB: 0.5m), null);

        Assert.False(new MeanReversionRule().Evaluate(f, _t).Matched);
    }

    [Fact]
    public void Rules_AreDeterministic()
    {
        var f = MarketFeatureExtractor.Extract(
            StrategyTestData.Observation(
                new DateOnly(2026, 1, 5), close: 110m, sma: 100m,
                rsi: 55m, macd: 2m, signal: 1m, histogram: 1m,
                bbUpper: 112m, bbMiddle: 100m, bbLower: 88m, bandwidth: 24m, percentB: 0.8m), null);

        foreach (var rule in StrategyRuleRegistry.BuiltIn)
        {
            var a = rule.Evaluate(f, _t);
            var b = rule.Evaluate(f, _t);

            Assert.Equal(a.RuleName, b.RuleName);
            Assert.Equal(a.Matched, b.Matched);
            Assert.Equal(a.Evidence.Count, b.Evidence.Count);
            for (var i = 0; i < a.Evidence.Count; i++)
            {
                Assert.Equal(a.Evidence[i].Name, b.Evidence[i].Name);
                Assert.Equal(a.Evidence[i].Value, b.Evidence[i].Value);
                Assert.Equal(a.Evidence[i].Comparison, b.Evidence[i].Comparison);
            }
        }
    }

    [Fact]
    public void Rules_NeverReturnEvidenceFreeResults()
    {
        // Even with everything missing, evidence explains what was missing.
        var f = MarketFeatureExtractor.Extract(
            StrategyTestData.Observation(new DateOnly(2026, 1, 5), close: 100m), null);

        foreach (var rule in StrategyRuleRegistry.BuiltIn)
        {
            var result = rule.Evaluate(f, _t);
            Assert.False(result.Matched);
            Assert.NotEmpty(result.Evidence);
            Assert.All(result.Evidence, e => Assert.Equal("missing", e.Comparison));
        }
    }

    [Fact]
    public void Registry_ContainsExactlyTheThreeInitialRules()
    {
        Assert.Equal(
            new[] { "TrendFollowing", "Momentum", "MeanReversion" },
            StrategyRuleRegistry.BuiltIn.Select(r => r.Name).ToArray());
    }
}

// ============================================================
// Time-series split tests
// ============================================================

public class TimeSeriesSplitterTests
{
    private static IReadOnlyList<EnrichedObservation> Series(int count, DateOnly start) =>
        Enumerable.Range(0, count)
            .Select(i => StrategyTestData.Observation(start.AddDays(i), close: 100m + i))
            .ToList();

    [Fact]
    public void Split_DefaultFractions_AreChronological()
    {
        var observations = Series(100, new DateOnly(2026, 1, 1));

        var segments = TimeSeriesSplitter.Split(observations);

        Assert.Equal(3, segments.Count);
        Assert.Equal(TimeSeriesSegment.Research, segments[0].Segment);
        Assert.Equal(TimeSeriesSegment.Validation, segments[1].Segment);
        Assert.Equal(TimeSeriesSegment.Holdout, segments[2].Segment);

        Assert.Equal(60, segments[0].Observations);
        Assert.Equal(20, segments[1].Observations);
        Assert.Equal(20, segments[2].Observations);

        // Strict past → future ordering, no overlap.
        Assert.True(segments[0].LastObservation < segments[1].FirstObservation);
        Assert.True(segments[1].LastObservation < segments[2].FirstObservation);
    }

    [Fact]
    public void Split_CustomFractions_Respected()
    {
        var observations = Series(100, new DateOnly(2026, 1, 1));

        var segments = TimeSeriesSplitter.Split(observations, new TimeSeriesSplitFractions
        {
            ResearchFraction = 0.5m,
            ValidationFraction = 0.3m
        });

        Assert.Equal(50, segments[0].Observations);
        Assert.Equal(30, segments[1].Observations);
        Assert.Equal(20, segments[2].Observations);
    }

    [Fact]
    public void Split_NoFutureLeakage_EarlierSegmentsNeverContainLaterDates()
    {
        var observations = Series(90, new DateOnly(2026, 1, 1));

        var segments = TimeSeriesSplitter.Split(observations);

        var research = segments[0];
        var validation = segments[1];
        var holdout = segments[2];

        Assert.All(
            observations.Take(research.Observations),
            o => Assert.True(o.ObservationDate <= research.LastObservation));
        Assert.All(
            observations.Skip(research.Observations).Take(validation.Observations),
            o => Assert.True(o.ObservationDate > research.LastObservation));
        Assert.All(
            observations.Skip(research.Observations + validation.Observations),
            o => Assert.True(o.ObservationDate > validation.LastObservation));
    }

    [Fact]
    public void Split_BoundariesAreActualObservationDates()
    {
        // Deliberate gap: 10 observations in January, then 10 in March.
        var january = Series(10, new DateOnly(2026, 1, 1));
        var march = Series(10, new DateOnly(2026, 3, 1));
        var observations = january.Concat(march).ToList();

        var segments = TimeSeriesSplitter.Split(observations, new TimeSeriesSplitFractions
        {
            ResearchFraction = 0.5m,
            ValidationFraction = 0.25m
        });

        Assert.Equal(new DateOnly(2026, 1, 10), segments[0].LastObservation);
        // Boundaries are ACTUAL observation dates — no Jan 11 exists in the data.
        Assert.Equal(new DateOnly(2026, 3, 1), segments[1].FirstObservation);
        Assert.Equal(new DateOnly(2026, 3, 6), segments[2].FirstObservation);
    }

    [Fact]
    public void Split_EmptyInput_ReturnsEmpty()
    {
        Assert.Empty(TimeSeriesSplitter.Split([]));
    }

    [Fact]
    public void Split_ZeroObservationCounts_SmallSeriesHandled()
    {
        // 3 observations at default fractions → 1 research, 0 validation, 2 holdout.
        var segments = TimeSeriesSplitter.Split(Series(3, new DateOnly(2026, 1, 1)));

        Assert.Equal(2, segments.Count);
        Assert.Equal(1, segments[0].Observations);
        Assert.Equal(2, segments[1].Observations);
        Assert.Equal(TimeSeriesSegment.Research, segments[0].Segment);
        Assert.Equal(TimeSeriesSegment.Holdout, segments[1].Segment);
    }
}

// ============================================================
// Historical evaluation tests — forward returns, no look-ahead, determinism
// ============================================================

public class StrategyEvaluationEngineTests
{
    private static StrategyEvaluationEngine CreateEngine() =>
        new(StrategyRuleRegistry.BuiltIn);

    private static StrategyEvaluationRequest Request(
        DateOnly from, DateOnly to,
        IReadOnlyList<string>? rules = null,
        IReadOnlyList<int>? horizons = null,
        int maxSamples = 5) =>
        new()
        {
            InstrumentId = StrategyTestData.InstrumentId,
            Source = SourceAdapterType.Tgju,
            From = from,
            To = to,
            RuleNames = rules,
            ForwardHorizons = horizons,
            MaxEvidenceSamples = maxSamples
        };

    /// <summary>
    /// Builds an observation sequence with explicit indicator values per index,
    /// so matches can be placed deterministically at chosen indices.
    /// </summary>
    private static List<EnrichedObservation> SeriesWithMatches(
        int count,
        DateOnly start,
        Func<int, bool> matchAt)
    {
        var observations = new List<EnrichedObservation>(count);
        for (var i = 0; i < count; i++)
        {
            var match = matchAt(i);
            observations.Add(StrategyTestData.Observation(
                start.AddDays(i),
                close: 100m + i,
                sma: match ? 90m : 110m,              // match → price above SMA
                rsi: match ? 55m : 45m,               // match → RSI >= 50
                macd: match ? 2m : -2m,               // match → fast EMA above slow
                signal: match ? 1m : 0m,
                histogram: match ? 1m : 0m));
        }
        return observations;
    }

    [Fact]
    public void Evaluate_ForwardReturns_ComputedFromSubsequentCloses()
    {
        // Match at index 0 (close 100). 1-day horizon → close[1]=101 → +1%.
        var observations = SeriesWithMatches(5, new DateOnly(2026, 1, 1), i => i == 0);

        var result = CreateEngine().Evaluate(
            StrategyTestData.InstrumentId, SourceAdapterType.Tgju,
            observations, Request(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 5),
                rules: ["TrendFollowing"], horizons: [1]));

        var stats = result.RuleStatistics.Single();
        Assert.Equal(1, stats.MatchCount);
        var h1 = stats.ForwardReturns.Single(f => f.HorizonObservations == 1);
        Assert.Equal(1, h1.MeasurableCount);
        Assert.Equal(1m, h1.AverageForwardReturn);
        Assert.Equal(1m, h1.MedianForwardReturn);
        Assert.Equal(1, h1.PositiveOutcomeCount);
        Assert.Equal(0, h1.NegativeOutcomeCount);
        Assert.Equal(0, h1.UnavailableOutcomeCount);
    }

    [Fact]
    public void Evaluate_HorizonBeyondData_CountedAsUnavailable()
    {
        // Match at the LAST index: forward horizon cannot exist.
        var observations = SeriesWithMatches(5, new DateOnly(2026, 1, 1), i => i == 4);

        var result = CreateEngine().Evaluate(
            StrategyTestData.InstrumentId, SourceAdapterType.Tgju,
            observations, Request(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 5),
                rules: ["TrendFollowing"], horizons: [1, 5]));

        var stats = result.RuleStatistics.Single();
        Assert.Equal(1, stats.MatchCount);
        var h1 = stats.ForwardReturns.Single(f => f.HorizonObservations == 1);
        Assert.Equal(0, h1.MeasurableCount);
        Assert.Equal(1, h1.UnavailableOutcomeCount);
        Assert.Null(h1.AverageForwardReturn);
        Assert.Null(h1.MedianForwardReturn);

        var h5 = stats.ForwardReturns.Single(f => f.HorizonObservations == 5);
        Assert.Equal(1, h5.UnavailableOutcomeCount);
    }

    [Fact]
    public void Evaluate_NegativeAndPositiveOutcomes_Counted()
    {
        // Rising series with NO matches, then a falling series whose first
        // observation matches: the 1-day forward return is strictly negative.
        var rising = SeriesWithMatches(10, new DateOnly(2026, 1, 1), i => false);
        var falling = Enumerable.Range(0, 5).Select(i =>
            StrategyTestData.Observation(
                new DateOnly(2026, 1, 11).AddDays(i),
                close: 109m - i,
                sma: i == 0 ? 90m : 110m,
                rsi: i == 0 ? 55m : 45m,
                macd: i == 0 ? 2m : -2m,
                signal: i == 0 ? 1m : 0m,
                histogram: i == 0 ? 1m : 0m));
        var observations = rising.Concat(falling).ToList();

        var result = CreateEngine().Evaluate(
            StrategyTestData.InstrumentId, SourceAdapterType.Tgju,
            observations, Request(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 15),
                rules: ["TrendFollowing"], horizons: [1]));

        var h1 = result.RuleStatistics.Single().ForwardReturns.Single();
        Assert.Equal(1, h1.MeasurableCount);
        Assert.Equal(0, h1.PositiveOutcomeCount);
        Assert.Equal(1, h1.NegativeOutcomeCount);
        Assert.True(h1.AverageForwardReturn < 0);
    }

    [Fact]
    public void Evaluate_MultipleRules_TrackedSeparately()
    {
        // All three rules match on the same feature set at index 0:
        // close 110 > SMA 100; MACD 2 > Signal 1 and > 0; RSI 55 ∈ [55, 70);
        // percentB 0.1 ≤ 0.2; RSI 55... wait — MeanReversion needs RSI ≤ 40.
        // Use two observations: one TrendFollowing+Momentum match and one MeanReversion match.
        var trendMatch = StrategyTestData.Observation(
            new DateOnly(2026, 1, 1), close: 110m,
            sma: 100m, rsi: 55m, macd: 2m, signal: 1m, histogram: 1m);
        var meanReversionMatch = StrategyTestData.Observation(
            new DateOnly(2026, 1, 2), close: 97m,
            sma: 100m, rsi: 28m,
            macd: -2m, signal: 0m, histogram: 0m,
            bbUpper: 101m, bbMiddle: 100m, bbLower: 99m, bandwidth: 2m, percentB: 0.1m);
        var rest = Enumerable.Range(0, 3).Select(i =>
            StrategyTestData.Observation(new DateOnly(2026, 1, 3).AddDays(i), close: 100m + i));

        var observations = new[] { trendMatch, meanReversionMatch }.Concat(rest).ToList();

        var result = CreateEngine().Evaluate(
            StrategyTestData.InstrumentId, SourceAdapterType.Tgju,
            observations, Request(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 5),
                horizons: [1]));

        // Day 1 matches TrendFollowing (close 110 > SMA 100, MACD 2 > Signal 1, RSI 55 ≥ 50).
        // Day 2 matches MeanReversion (percentB 0.1 ≤ 0.2, RSI 28 ≤ 40).
        // Momentum matches nowhere (no rising-histogram predecessor in this fixture).
        var byName = result.RuleStatistics.ToDictionary(s => s.RuleName);
        Assert.Equal(1, byName["TrendFollowing"].MatchCount);
        Assert.Equal(0, byName["Momentum"].MatchCount);
        Assert.Equal(1, byName["MeanReversion"].MatchCount);
        Assert.Equal(2, result.TotalRuleMatches);
        Assert.Equal(3, result.NoMatchCount); // 5 observations, 2 matched
    }

    [Fact]
    public void Evaluate_NoLookAhead_RuleDecisionUsesOnlyDataUpToMatchDate()
    {
        // The match features at index i are derived from observations up to i.
        // Here: a match at index 2 whose forward returns are strongly negative —
        // if future data leaked into the decision, future rules/engines would
        // have "avoided" matching. The engine must still report the match.
        var observations = SeriesWithMatches(10, new DateOnly(2026, 1, 1), i => i == 2);

        var result = CreateEngine().Evaluate(
            StrategyTestData.InstrumentId, SourceAdapterType.Tgju,
            observations, Request(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 10),
                rules: ["TrendFollowing"], horizons: [1, 5]));

        var stats = result.RuleStatistics.Single();
        Assert.Equal(1, stats.MatchCount); // match decision unaffected by future data

        // Forward returns measured AFTER the decision (structural separation):
        var h1 = stats.ForwardReturns.Single(f => f.HorizonObservations == 1);
        Assert.Equal(1, h1.MeasurableCount); // outcome measured, decision already made

        // The match sample's regime/features come from the observation at the
        // match date only — the sample is the evidence of no look-ahead.
        var sample = result.MatchSamples.Single();
        Assert.Equal(new DateOnly(2026, 1, 3), sample.Evidence.ObservationDate);
        Assert.Equal(102m, sample.Evidence.Features.Close);
        Assert.Equal(TrendRegime.Bullish, sample.Evidence.Regime.Trend);
    }

    [Fact]
    public void Evaluate_MatchSample_ContainsActualIndicatorValues()
    {
        var observations = SeriesWithMatches(3, new DateOnly(2026, 1, 1), i => i == 0);

        var result = CreateEngine().Evaluate(
            StrategyTestData.InstrumentId, SourceAdapterType.Tgju,
            observations, Request(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 3),
                rules: ["TrendFollowing"], horizons: [1]));

        var sample = result.MatchSamples.Single();
        Assert.Equal(55m, sample.Evidence.Features.Rsi);
        Assert.Equal(2m, sample.Evidence.Features.MacdLine);
        Assert.Contains(
            sample.Evidence.RuleEvaluations.Single(r => r.RuleName == "TrendFollowing").Evidence,
            e => e.Name == "RSI" && e.Value == 55m);
    }

    [Fact]
    public void Evaluate_IsDeterministic()
    {
        var observations = SeriesWithMatches(20, new DateOnly(2026, 1, 1), i => i % 5 == 0);

        var request = Request(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 20),
            horizons: [1, 5], maxSamples: 3);

        var engine = CreateEngine();
        var a = engine.Evaluate(
            StrategyTestData.InstrumentId, SourceAdapterType.Tgju, observations, request);
        var b = engine.Evaluate(
            StrategyTestData.InstrumentId, SourceAdapterType.Tgju, observations, request);

        Assert.Equal(a.TotalRuleMatches, b.TotalRuleMatches);
        Assert.Equal(a.NoMatchCount, b.NoMatchCount);
        Assert.Equal(a.RuleStatistics.Count, b.RuleStatistics.Count);
        for (var i = 0; i < a.RuleStatistics.Count; i++)
        {
            Assert.Equal(a.RuleStatistics[i].RuleName, b.RuleStatistics[i].RuleName);
            Assert.Equal(a.RuleStatistics[i].MatchCount, b.RuleStatistics[i].MatchCount);
            for (var j = 0; j < a.RuleStatistics[i].ForwardReturns.Count; j++)
            {
                Assert.Equal(
                    a.RuleStatistics[i].ForwardReturns[j].AverageForwardReturn,
                    b.RuleStatistics[i].ForwardReturns[j].AverageForwardReturn);
                Assert.Equal(
                    a.RuleStatistics[i].ForwardReturns[j].MedianForwardReturn,
                    b.RuleStatistics[i].ForwardReturns[j].MedianForwardReturn);
                Assert.Equal(
                    a.RuleStatistics[i].ForwardReturns[j].MeasurableCount,
                    b.RuleStatistics[i].ForwardReturns[j].MeasurableCount);
            }
        }

        Assert.Equal(a.MatchSamples.Count, b.MatchSamples.Count);
        for (var i = 0; i < a.MatchSamples.Count; i++)
        {
            Assert.Equal(
                a.MatchSamples[i].Evidence.ObservationDate,
                b.MatchSamples[i].Evidence.ObservationDate);
            Assert.Equal(
                a.MatchSamples[i].Evidence.Features.Rsi,
                b.MatchSamples[i].Evidence.Features.Rsi);
            Assert.Equal(
                a.MatchSamples[i].ForwardReturns[1],
                b.MatchSamples[i].ForwardReturns[1]);
        }
    }

    [Fact]
    public void Evaluate_UnknownRuleName_ThrowsWithKnownNames()
    {
        var observations = SeriesWithMatches(3, new DateOnly(2026, 1, 1), _ => true);

        var ex = Assert.Throws<ArgumentException>(() => CreateEngine().Evaluate(
            StrategyTestData.InstrumentId, SourceAdapterType.Tgju,
            observations, Request(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 3),
                rules: ["NoSuchRule"])));

        Assert.Contains("NoSuchRule", ex.Message);
        Assert.Contains("TrendFollowing", ex.Message);
    }

    [Fact]
    public void Evaluate_EmptySequence_ProducesZeroedResult()
    {
        var result = CreateEngine().Evaluate(
            StrategyTestData.InstrumentId, SourceAdapterType.Tgju,
            [], Request(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31)));

        Assert.Equal(0, result.ObservationsEvaluated);
        // Per-rule zeroed statistics remain informative (the rules exist); no matches, no samples.
        Assert.All(result.RuleStatistics, s => Assert.Equal(0, s.MatchCount));
        Assert.All(result.RuleStatistics,
            s => Assert.All(s.ForwardReturns, f => Assert.Equal(0, f.MeasurableCount)));
        Assert.Empty(result.MatchSamples);
    }
}

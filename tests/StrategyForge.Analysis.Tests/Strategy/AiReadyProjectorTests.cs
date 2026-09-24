using StrategyForge.Analysis.Strategy;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Interfaces.Analysis;
using StrategyForge.Domain.Models;

namespace StrategyForge.Analysis.Tests.Strategy;

/// <summary>
/// Phase 9 tests: AI-ready dataset projection — feature selection, temporal
/// context, forward-return labels, look-ahead leakage protection, and the
/// deterministic dataset fingerprint.
/// </summary>
public class AiReadyProjectorTests
{
    private const string InstrumentId = "test-instrument";
    private static readonly SourceAdapterType Source = SourceAdapterType.Tgju;

    /// <summary>
    /// Builds enriched observations with explicit indicator values so feature
    /// extraction is fully deterministic (no real indicator computation needed —
    /// the projector consumes stored values only).
    /// </summary>
    private static List<EnrichedObservation> CreateObservations(
        int count,
        decimal startClose = 100m,
        decimal step = 1m,
        bool withIndicators = true,
        bool withVolume = false)
    {
        var result = new List<EnrichedObservation>(count);

        for (var i = 0; i < count; i++)
        {
            var close = startClose + step * i;
            var indicators = new Dictionary<string, IReadOnlyDictionary<string, decimal>>();

            if (withIndicators)
            {
                indicators["SMA"] = new Dictionary<string, decimal> { ["SMA"] = close - 5m };
                indicators["RSI"] = new Dictionary<string, decimal> { ["RSI"] = 55m };
                indicators["MACD"] = new Dictionary<string, decimal>
                {
                    ["MACD"] = 1m,
                    ["Signal"] = 0.5m,
                    ["Histogram"] = 0.5m
                };
                indicators["BollingerBands"] = new Dictionary<string, decimal>
                {
                    ["Upper"] = close + 2m,
                    ["Middle"] = close,
                    ["Lower"] = close - 2m,
                    ["Bandwidth"] = 4m,
                    ["PercentB"] = 0.5m
                };
            }

            result.Add(new EnrichedObservation
            {
                InstrumentId = InstrumentId,
                Source = Source,
                ObservationDate = new DateOnly(2026, 1, 1).AddDays(i),
                Open = close - 1m,
                High = close + 1m,
                Low = close - 2m,
                Close = close,
                Volume = withVolume ? 1000 + i : 0,
                Indicators = indicators,
                QualityStatus = HistoricalDataQualityStatus.Valid,
                WarningCodes = [],
                ProcessedBy = "test-pipeline-1.0.0",
                ProcessedAtUtc = DateTimeOffset.UtcNow
            });
        }

        return result;
    }

    // ============================================================
    // Feature selection (Step 11)
    // ============================================================

    [Fact]
    public void Project_IncludesExpectedFeatureFields()
    {
        var observations = CreateObservations(30);
        var projector = new AiReadyProjector();

        var records = projector.Project(observations, forwardHorizons: [1, 5]);

        // One record per observation date (deterministic per-date identity).
        Assert.All(records.GroupBy(r => r.ObservationDate), g => Assert.Single(g));
        var last = records[^1];

        Assert.Equal(100m + 29m, last.Features.Close);
        Assert.NotNull(last.Features.Sma);
        Assert.NotNull(last.Features.Rsi);
        Assert.NotNull(last.Features.Macd);
        Assert.NotNull(last.Features.MacdSignal);
        Assert.NotNull(last.Features.MacdHistogram);
        Assert.NotNull(last.Features.BollingerBandwidthPercent);
        Assert.NotNull(last.Features.BollingerPercentB);
    }

    [Fact]
    public void Project_MissingIndicators_RemainNull_NotFabricated()
    {
        var observations = CreateObservations(25, withIndicators: false);
        var projector = new AiReadyProjector();

        var records = projector.Project(observations, forwardHorizons: [1]);

        Assert.All(records, r =>
        {
            Assert.Null(r.Features.Sma);
            Assert.Null(r.Features.Rsi);
            Assert.Null(r.Features.Macd);
            Assert.Null(r.Features.MacdSignal);
            Assert.Null(r.Features.MacdHistogram);
            Assert.Null(r.Features.BollingerBandwidthPercent);
            Assert.Null(r.Features.BollingerPercentB);
            Assert.Null(r.Features.PriceAboveSma);
            Assert.Null(r.Features.EmaFastAboveSlow);
        });
    }

    [Fact]
    public void Project_ZeroVolume_HasMeaningfulVolumeIsFalse()
    {
        var observations = CreateObservations(5, withVolume: false);
        var projector = new AiReadyProjector();

        var records = projector.Project(observations, forwardHorizons: [1]);

        Assert.All(records, r =>
        {
            Assert.Equal(0, r.Features.Volume);
            Assert.False(r.Features.HasMeaningfulVolume);
        });
    }

    [Fact]
    public void Project_PositiveVolume_HasMeaningfulVolumeIsTrue()
    {
        var observations = CreateObservations(5, withVolume: true);
        var projector = new AiReadyProjector();

        var records = projector.Project(observations, forwardHorizons: [1]);

        Assert.All(records, r => Assert.True(r.Features.HasMeaningfulVolume));
    }

    // ============================================================
    // Temporal context (Step 12)
    // ============================================================

    [Fact]
    public void Project_ContextWindow_ContainsOnlyStrictlyEarlierObservations()
    {
        var observations = CreateObservations(30);
        var projector = new AiReadyProjector();

        var records = projector.Project(observations, contextWindowObservations: 5, forwardHorizons: [1]);

        var last = records[^1];
        Assert.Equal(5, last.ContextWindow.Count);

        // Context is the 5 observations immediately before the target, oldest first.
        Assert.Equal(observations[^6].ObservationDate, last.ContextWindow[0].Date);
        Assert.Equal(observations[^2].ObservationDate, last.ContextWindow[^1].Date);
        Assert.All(last.ContextWindow, c => Assert.True(c.Date < last.ObservationDate));
    }

    [Fact]
    public void Project_ContextWindow_TruncatesAtDatasetStart()
    {
        var observations = CreateObservations(10);
        var projector = new AiReadyProjector();

        var records = projector.Project(observations, contextWindowObservations: 20, forwardHorizons: [1]);

        // The first record has no earlier observations; record i has i context points.
        Assert.Empty(records[0].ContextWindow);
        Assert.Equal(3, records[3].ContextWindow.Count);
    }

    [Fact]
    public void Project_ZeroContextWindow_NoContextPoints()
    {
        var observations = CreateObservations(10);
        var projector = new AiReadyProjector();

        var records = projector.Project(observations, contextWindowObservations: 0, forwardHorizons: [1]);

        Assert.All(records, r => Assert.Empty(r.ContextWindow));
    }

    // ============================================================
    // Labels and leakage protection (Steps 13, 22)
    // ============================================================

    [Fact]
    public void Project_ForwardReturns_ComputedFromStrictlyFutureObservations()
    {
        // Deterministic series: close(T) = 100 + T, so close(T+h) is exactly +h.
        var observations = CreateObservations(10, startClose: 100m, step: 1m);
        var projector = new AiReadyProjector();

        var records = projector.Project(observations, forwardHorizons: [1, 2]);

        // Record at index 0: close 100; horizon 1 → close 101 → return 0.01.
        Assert.Equal(101m / 100m - 1m, records[0].Labels.ForwardReturns[1]);
        Assert.Equal(102m / 100m - 1m, records[0].Labels.ForwardReturns[2]);

        // Record at index 5: close 105; horizon 1 → close 106 → 106/105 − 1.
        Assert.Equal(106m / 105m - 1m, records[5].Labels.ForwardReturns[1]);
    }

    [Fact]
    public void Project_HorizonBeyondDataset_LabelIsNullNotFabricated()
    {
        var observations = CreateObservations(5);
        var projector = new AiReadyProjector();

        var records = projector.Project(observations, forwardHorizons: [1, 10]);

        // Last record cannot have any forward observation.
        Assert.Null(records[^1].Labels.ForwardReturns[1]);
        Assert.Null(records[^1].Labels.ForwardReturns[10]);
        Assert.Null(records[^1].Labels.MaxForwardGain);
        Assert.Null(records[^1].Labels.MaxForwardLoss);

        // Second-to-last has horizon 1 but not 10.
        Assert.NotNull(records[^2].Labels.ForwardReturns[1]);
        Assert.Null(records[^2].Labels.ForwardReturns[10]);
    }

    [Fact]
    public void Project_MaxForwardGainLoss_ComputedOverLargestHorizonWindow()
    {
        var observations = CreateObservations(10, startClose: 100m, step: 1m);
        var projector = new AiReadyProjector();

        var records = projector.Project(observations, forwardHorizons: [1, 5]);

        // Record 0: close 100; window covers observations 1..5 (highs 102..106 → max 106).
        Assert.Equal(106m / 100m - 1m, records[0].Labels.MaxForwardGain);
        // Lows in the window are 99..103 → min 99 (all above target close → positive loss value).
        Assert.Equal(99m / 100m - 1m, records[0].Labels.MaxForwardLoss);
    }

    [Fact]
    public void Project_LabelsAreStructurallySeparateFromFeatures()
    {
        var observations = CreateObservations(8);
        var projector = new AiReadyProjector();

        var records = projector.Project(observations, forwardHorizons: [1]);

        // Leakage protection is structural: Features and Labels are distinct
        // record types; the labels dictionary keys are horizons, not features.
        Assert.All(records, r =>
        {
            Assert.NotNull(r.Features);
            Assert.NotNull(r.Labels);
            Assert.NotSame(r.Features, r.Labels);
        });
    }

    // ============================================================
    // Determinism (Step 17) and fingerprint (Step 19)
    // ============================================================

    [Fact]
    public void Project_IdenticalInput_ProducesIdenticalOutput()
    {
        var observationsA = CreateObservations(15);
        var observationsB = CreateObservations(15);
        var projector = new AiReadyProjector();

        var a = projector.Project(observationsA, forwardHorizons: [1, 5]);
        var b = projector.Project(observationsB, forwardHorizons: [1, 5]);

        Assert.Equal(a.Count, b.Count);
        for (var i = 0; i < a.Count; i++)
        {
            Assert.Equal(a[i].ObservationDate, b[i].ObservationDate);
            Assert.Equal(a[i].Features.Close, b[i].Features.Close);
            Assert.Equal(a[i].Features.Sma, b[i].Features.Sma);
            Assert.Equal(a[i].Labels.ForwardReturns[1], b[i].Labels.ForwardReturns[1]);
            Assert.Equal(a[i].TrendRegime, b[i].TrendRegime);
            Assert.Equal(a[i].MomentumRegime, b[i].MomentumRegime);
        }
    }

    [Fact]
    public void Fingerprint_SameConfiguration_SameIdentity()
    {
        var a = AiReadyDatasetFingerprint.Compute(
            InstrumentId, Source, new DateOnly(2024, 1, 1), new DateOnly(2026, 9, 1),
            20, [1, 5, 10], "1.0.0", "1.0.0");
        var b = AiReadyDatasetFingerprint.Compute(
            InstrumentId, Source, new DateOnly(2024, 1, 1), new DateOnly(2026, 9, 1),
            20, [1, 5, 10], "1.0.0", "1.0.0");

        Assert.Equal(a.DatasetId, b.DatasetId);
        Assert.Equal(a.DatasetVersion, b.DatasetVersion);
    }

    [Fact]
    public void Fingerprint_DifferentConfiguration_DifferentIdentity()
    {
        var baseFingerprint = AiReadyDatasetFingerprint.Compute(
            InstrumentId, Source, new DateOnly(2024, 1, 1), new DateOnly(2026, 9, 1),
            20, [1, 5, 10], "1.0.0", "1.0.0");

        var differentWindow = AiReadyDatasetFingerprint.Compute(
            InstrumentId, Source, new DateOnly(2024, 1, 1), new DateOnly(2026, 9, 1),
            50, [1, 5, 10], "1.0.0", "1.0.0");

        var differentRange = AiReadyDatasetFingerprint.Compute(
            InstrumentId, Source, new DateOnly(2024, 1, 2), new DateOnly(2026, 9, 1),
            20, [1, 5, 10], "1.0.0", "1.0.0");

        Assert.NotEqual(baseFingerprint.DatasetVersion, differentWindow.DatasetVersion);
        Assert.NotEqual(baseFingerprint.DatasetVersion, differentRange.DatasetVersion);
    }

    // ============================================================
    // Lineage (Step 7)
    // ============================================================

    [Fact]
    public void Project_RecordsRemainTraceableToSourceObservations()
    {
        var observations = CreateObservations(6);
        var projector = new AiReadyProjector();

        var records = projector.Project(
            observations,
            forwardHorizons: [1],
            provenanceResolver: o => new AiReadyProvenanceReference
            {
                SourceInstrumentId = "price_dollar_rl",
                SourceSymbol = "دلار",
                Endpoint = "history",
                ProcessedBy = o.ProcessedBy
            });

        Assert.All(records, r =>
        {
            Assert.Equal(InstrumentId, r.InstrumentId);
            Assert.Equal(Source, r.Source);
            Assert.Equal("price_dollar_rl", r.ProvenanceReference.SourceInstrumentId);
            Assert.Equal("test-pipeline-1.0.0", r.ProvenanceReference.ProcessedBy);
        });
    }

    // ============================================================
    // Regimes (Step 3/8 semantics)
    // ============================================================

    [Fact]
    public void Project_RegimesReflectStoredIndicatorValues()
    {
        // Close > SMA (close - 5) → price above SMA; MACD 1 > 0 → EMA fast above slow.
        var observations = CreateObservations(4);
        var projector = new AiReadyProjector();

        var records = projector.Project(observations, forwardHorizons: [1]);

        Assert.Equal(TrendRegime.Bullish, records[0].TrendRegime);
        // RSI 55 → Neutral (between 45 and 55 inclusive per classifier).
        Assert.Equal(MomentumRegime.Neutral, records[0].MomentumRegime);
    }
}

/// <summary>
/// Phase 9 tests for the Domain-abstraction adapters: the splitter adapter must
/// reuse the one Phase 8 splitting algorithm; the rule registry adapter must
/// expose the built-in rule names in stable order.
/// </summary>
public class Phase9AnalysisAdapterTests
{
    [Fact]
    public void TimeSeriesSplitterAdapter_SplitsChronologically()
    {
        var observations = CreateEnriched(20);
        var adapter = new TimeSeriesSplitterAdapter();

        var segments = adapter.Split(observations);

        Assert.Equal(3, segments.Count);
        Assert.Equal(TimeSeriesSegment.Research, segments[0].Segment);
        Assert.Equal(TimeSeriesSegment.Validation, segments[1].Segment);
        Assert.Equal(TimeSeriesSegment.Holdout, segments[2].Segment);

        // Chronological, disjoint, in order.
        Assert.True(segments[0].LastObservation < segments[1].FirstObservation);
        Assert.True(segments[1].LastObservation < segments[2].FirstObservation);

        // Sum of segment observations equals the dataset.
        Assert.Equal(20, segments.Sum(s => s.Observations));
    }

    [Fact]
    public void TimeSeriesSplitterAdapter_NeverShuffles()
    {
        var observations = CreateEnriched(12);
        var adapter = new TimeSeriesSplitterAdapter();

        var segments = adapter.Split(observations);

        var allDates = segments.SelectMany(s => Enumerable.Range(0, s.Observations))
            .Select((_, i) => observations[i].ObservationDate)
            .ToList();

        // Segment first dates must appear in non-decreasing chronological order.
        var seen = new List<DateOnly>();
        foreach (var segment in segments)
        {
            seen.Add(segment.FirstObservation);
            seen.Add(segment.LastObservation);
        }

        Assert.Equal(seen.OrderBy(d => d), seen);
    }

    [Fact]
    public void BuiltInStrategyRuleRegistry_ExposesStableRuleNames()
    {
        IStrategyRuleRegistry registry = new BuiltInStrategyRuleRegistry();

        Assert.NotEmpty(registry.BuiltIn);
        Assert.Equal(registry.BuiltIn.Count, registry.BuiltInNames.Count);
        Assert.All(registry.BuiltIn, r => Assert.Contains(r.Name, registry.BuiltInNames));
    }

    private static List<EnrichedObservation> CreateEnriched(int count) =>
        Enumerable.Range(0, count).Select(i => new EnrichedObservation
        {
            InstrumentId = "split-test",
            Source = SourceAdapterType.Tgju,
            ObservationDate = new DateOnly(2026, 1, 1).AddDays(i),
            Open = 100m,
            High = 101m,
            Low = 99m,
            Close = 100m + i,
            Indicators = new Dictionary<string, IReadOnlyDictionary<string, decimal>>(),
            ProcessedBy = "test"
        }).ToList();
}

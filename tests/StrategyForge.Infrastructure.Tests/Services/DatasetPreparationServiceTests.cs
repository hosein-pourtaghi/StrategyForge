using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StrategyForge.Analysis.Strategy;
using StrategyForge.Domain.Configuration;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Interfaces.Analysis;
using StrategyForge.Domain.Interfaces.Providers;
using StrategyForge.Domain.Models;
using StrategyForge.Infrastructure.Services;
using Xunit;

namespace StrategyForge.Infrastructure.Tests.Services;

/// <summary>
/// Phase 9 tests: AI-ready JSONL export — validity, determinism, and bounded
/// incremental writing.
/// </summary>
public class AiReadyJsonlExporterTests
{
    private static AiReadyObservation CreateRecord(DateOnly date, decimal close) => new()
    {
        InstrumentId = "test",
        Source = SourceAdapterType.Tgju,
        ObservationDate = date,
        ProvenanceReference = new AiReadyProvenanceReference { ProcessedBy = "test-1.0.0" },
        Features = new AiReadyFeatures
        {
            Close = close,
            Volume = 0,
            HasMeaningfulVolume = false,
            Rsi = 55m,
            Macd = 1m,
            MacdSignal = 0.5m,
            MacdHistogram = 0.5m
        },
        TrendRegime = TrendRegime.Bullish,
        VolatilityRegime = VolatilityRegime.Normal,
        MomentumRegime = MomentumRegime.Neutral,
        MatchedRules = [],
        ContextWindow = [],
        QualityStatus = HistoricalDataQualityStatus.Valid,
        WarningCodes = [],
        Labels = new AiReadyLabels
        {
            ForwardReturns = new Dictionary<int, decimal?> { [1] = 0.01m, [5] = null }
        }
    };

    [Fact]
    public async Task WriteAsync_ProducesValidJsonl()
    {
        var exporter = new AiReadyJsonlExporter();
        var records = new List<AiReadyObservation>
        {
            CreateRecord(new DateOnly(2026, 1, 1), 100m),
            CreateRecord(new DateOnly(2026, 1, 2), 101m)
        };
        using var stream = new MemoryStream();
        var statistics = new DatasetStatisticsAccumulator();

        await exporter.WriteAsync(stream, records, statistics, ["RuleA", "RuleB"]);

        stream.Position = 0;
        using var reader = new StreamReader(stream);
        var lines = (await reader.ReadToEndAsync()).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(2, lines.Length);
        // Each line must be valid JSON with the expected camelCase fields.
        using var doc = System.Text.Json.JsonDocument.Parse(lines[0]);
        Assert.True(doc.RootElement.TryGetProperty("features", out var features));
        Assert.True(features.TryGetProperty("close", out var close));
        Assert.Equal(100m, close.GetDecimal());
        Assert.True(doc.RootElement.TryGetProperty("labels", out var labels));
        Assert.True(doc.RootElement.TryGetProperty("provenanceReference", out _));
    }

    [Fact]
    public async Task WriteAsync_NullFeaturesStayNull_NotZero()
    {
        var exporter = new AiReadyJsonlExporter();
        var record = CreateRecord(new DateOnly(2026, 1, 1), 100m);
        var records = new List<AiReadyObservation> { record };
        using var stream = new MemoryStream();

        await exporter.WriteAsync(stream, records, new DatasetStatisticsAccumulator(), []);

        stream.Position = 0;
        using var reader = new StreamReader(stream);
        var line = (await reader.ReadToEndAsync()).Split('\n')[0];
        using var doc = System.Text.Json.JsonDocument.Parse(line);

        Assert.True(doc.RootElement.TryGetProperty("features", out var features));
        Assert.True(features.TryGetProperty("sma", out var sma));
        Assert.Equal(System.Text.Json.JsonValueKind.Null, sma.ValueKind);
        Assert.True(features.TryGetProperty("volume", out var volume));
        Assert.Equal(0, volume.GetInt64());
    }

    [Fact]
    public async Task WriteAsync_SameRecordsSameOrder_ProducesIdenticalBytes()
    {
        var exporter = new AiReadyJsonlExporter();
        var records = Enumerable.Range(0, 10)
            .Select(i => CreateRecord(new DateOnly(2026, 1, 1).AddDays(i), 100m + i))
            .ToList();

        var streamA = new MemoryStream();
        var streamB = new MemoryStream();

        await exporter.WriteAsync(streamA, records, new DatasetStatisticsAccumulator(), ["RuleA"]);
        await exporter.WriteAsync(streamB, records, new DatasetStatisticsAccumulator(), ["RuleA"]);

        Assert.Equal(streamA.ToArray(), streamB.ToArray());
    }

    [Fact]
    public async Task WriteAsync_UnavailableLabel_NullInJson()
    {
        var exporter = new AiReadyJsonlExporter();
        var records = new List<AiReadyObservation> { CreateRecord(new DateOnly(2026, 1, 1), 100m) };
        using var stream = new MemoryStream();

        await exporter.WriteAsync(stream, records, new DatasetStatisticsAccumulator(), []);

        stream.Position = 0;
        using var reader = new StreamReader(stream);
        var line = (await reader.ReadToEndAsync()).Split('\n')[0];
        using var doc = System.Text.Json.JsonDocument.Parse(line);

        var labels = doc.RootElement.GetProperty("labels");
        var forwardReturns = labels.GetProperty("forwardReturns");
        Assert.Equal(0.01m, forwardReturns.GetProperty("1").GetDecimal());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, forwardReturns.GetProperty("5").ValueKind);
    }
}

/// <summary>
/// Phase 9 tests: in-memory enriched page reader (keyset cursor, chronological
/// ordering) — the paged-read contract that streaming depends on.
/// </summary>
public class InMemoryEnrichedDatasetPageReaderTests
{
    private static InMemoryEnrichedDatasetStore CreateStore(int count, string instrumentId = "test")
    {
        var store = new InMemoryEnrichedDatasetStore();
        var batch = Enumerable.Range(0, count).Select(i => new EnrichedObservation
        {
            InstrumentId = instrumentId,
            Source = SourceAdapterType.Tgju,
            ObservationDate = new DateOnly(2026, 1, 1).AddDays(i),
            Open = 100m,
            High = 101m,
            Low = 99m,
            Close = 100m + i,
            Indicators = new Dictionary<string, IReadOnlyDictionary<string, decimal>>(),
            ProcessedBy = "test"
        }).ToList();

        store.UpsertEnrichedAsync(instrumentId, SourceAdapterType.Tgju, batch).GetAwaiter().GetResult();
        return store;
    }

    [Fact]
    public async Task ReadPageAsync_PaginatesChronologicallyWithoutOverlap()
    {
        var store = CreateStore(25);
        var reader = new InMemoryEnrichedDatasetPageReader(store);

        var page1 = await reader.ReadPageAsync("test", SourceAdapterType.Tgju, null, null, null, 10);
        var page2 = await reader.ReadPageAsync("test", SourceAdapterType.Tgju, null, null, page1[^1].ObservationDate, 10);
        var page3 = await reader.ReadPageAsync("test", SourceAdapterType.Tgju, null, null, page2[^1].ObservationDate, 10);
        var page4 = await reader.ReadPageAsync("test", SourceAdapterType.Tgju, null, null, page3[^1].ObservationDate, 10);

        Assert.Equal(10, page1.Count);
        Assert.Equal(10, page2.Count);
        Assert.Equal(5, page3.Count);
        Assert.Empty(page4);

        var all = page1.Concat(page2).Concat(page3).ToList();
        Assert.Equal(all.Select(o => o.ObservationDate), all.Select(o => o.ObservationDate).OrderBy(d => d));
        Assert.Equal(25, all.Count);
    }

    [Fact]
    public async Task ReadPageAsync_RespectsDateRangeAndInstrumentFilters()
    {
        var store = CreateStore(20);
        CreateStore(5, "other");
        var reader = new InMemoryEnrichedDatasetPageReader(store);

        var from = new DateOnly(2026, 1, 5);
        var to = new DateOnly(2026, 1, 10);
        var page = await reader.ReadPageAsync("test", SourceAdapterType.Tgju, from, to, null, 100);

        Assert.Equal(6, page.Count);
        Assert.All(page, o => Assert.InRange(o.ObservationDate, from, to));
    }

    [Fact]
    public async Task ReadLastPageAsync_ReturnsAscendingWarmupContext()
    {
        var store = CreateStore(15);
        var reader = new InMemoryEnrichedDatasetPageReader(store);

        var page = await reader.ReadLastPageAsync("test", SourceAdapterType.Tgju, new DateOnly(2026, 1, 10), 5);

        Assert.Equal(5, page.Count);
        Assert.Equal(page.Select(o => o.ObservationDate), page.Select(o => o.ObservationDate).OrderBy(d => d));
        Assert.All(page, o => Assert.True(o.ObservationDate < new DateOnly(2026, 1, 10)));
    }
}

/// <summary>
/// Phase 9 tests: DatasetPreparationService end-to-end over the in-memory
/// enriched dataset — projection, chronological splits, statistics, inspection
/// samples, and idempotency of a repeated preparation.
/// </summary>
public class DatasetPreparationServiceTests
{
    private const string InstrumentId = "prep-test";
    private static readonly SourceAdapterType Source = SourceAdapterType.Tgju;

    private static (InMemoryEnrichedDatasetStore Store, DatasetPreparationService Service) Create(
        int observationCount,
        int contextWindow = 5)
    {
        var store = new InMemoryEnrichedDatasetStore();
        var batch = Enumerable.Range(0, observationCount).Select(i =>
        {
            var close = 100m + i;
            return new EnrichedObservation
            {
                InstrumentId = InstrumentId,
                Source = Source,
                ObservationDate = new DateOnly(2026, 1, 1).AddDays(i),
                Open = close - 1m,
                High = close + 1m,
                Low = close - 2m,
                Close = close,
                Indicators = new Dictionary<string, IReadOnlyDictionary<string, decimal>>
                {
                    ["SMA"] = new Dictionary<string, decimal> { ["SMA"] = close - 5m },
                    ["RSI"] = new Dictionary<string, decimal> { ["RSI"] = 55m },
                    ["MACD"] = new Dictionary<string, decimal> { ["MACD"] = 1m, ["Signal"] = 0.5m, ["Histogram"] = 0.5m },
                    ["BollingerBands"] = new Dictionary<string, decimal> { ["Upper"] = close + 2m, ["Middle"] = close, ["Lower"] = close - 2m, ["Bandwidth"] = 4m, ["PercentB"] = 0.5m }
                },
                QualityStatus = HistoricalDataQualityStatus.Valid,
                WarningCodes = [],
                ProcessedBy = "test-1.0.0"
            };
        }).ToList();

        store.UpsertEnrichedAsync(InstrumentId, Source, batch).GetAwaiter().GetResult();

        var service = new DatasetPreparationService(
            new InMemoryEnrichedDatasetPageReader(store),
            new AiReadyProjector(),
            new TimeSeriesSplitterAdapter(),
            new BuiltInStrategyRuleRegistry(),
            Options.Create(new DatasetPreparationSettings { BatchSize = 4, ContextWindowObservations = contextWindow }),
            NullLogger<DatasetPreparationService>.Instance);

        return (store, service);
    }

    private static DatasetQualitySummary QualitySummary() => new()
    {
        InstrumentId = InstrumentId,
        Source = Source,
        From = new DateOnly(2026, 1, 1),
        To = new DateOnly(2026, 12, 31),
        TotalRows = 40,
        ValidRows = 40,
        WarningRows = 0,
        InvalidRows = 0,
        InvalidOhlcRows = 0,
        InvalidPriceRows = 0,
        DuplicateRows = 0,
        OutOfOrderRows = 0,
        MissingDateRows = 0,
        DateGapWarnings = 0,
        ZeroVolumeRows = 40,
        MissingOptionalFieldWarnings = 0,
        IncompleteProvenanceWarnings = 0,
        ReasonCounts = new Dictionary<string, int>()
    };

    private static DatasetCoverageEntry CoverageEntry(int count) => new()
    {
        InstrumentId = InstrumentId,
        Source = Source,
        FirstObservation = new DateOnly(2026, 1, 1),
        LastObservation = new DateOnly(2026, 1, 1).AddDays(count - 1),
        ObservationCount = count,
        GapCount = 0,
        LargestGapDays = 0,
        AverageObservationsPerYear = 365m
    };

    [Fact]
    public async Task PrepareAsync_ProjectsAllRowsWithDeterministicIdentity()
    {
        var (_, service) = Create(40);
        using var output = new MemoryStream();

        var result = await service.PrepareAsync(
            new AiReadyDatasetRequest
            {
                InstrumentId = InstrumentId,
                Source = Source,
                From = new DateOnly(2026, 1, 1),
                To = new DateOnly(2026, 12, 31)
            },
            output,
            new AiReadyJsonlExporter(),
            new DatasetStatisticsAccumulator(),
            QualitySummary(),
            CoverageEntry(40));

        Assert.True(result.Ok);
        Assert.Equal(40, result.RawRows);
        Assert.Equal(40, result.ProjectedRows);
        Assert.Equal("ai-ready-prep-test-tgju-20260101-20261231", result.DatasetId);

        var allDates = result.FirstRecords.Select(r => r.ObservationDate)
            .Concat(result.LastRecords.Select(r => r.ObservationDate))
            .Distinct()
            .ToList();
        Assert.All(allDates, d => Assert.InRange(d, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)));
    }

    [Fact]
    public async Task PrepareAsync_SplitsAreChronologicalAndDisjoint()
    {
        var (_, service) = Create(40);
        using var output = new MemoryStream();

        var result = await service.PrepareAsync(
            new AiReadyDatasetRequest
            {
                InstrumentId = InstrumentId,
                Source = Source,
                From = new DateOnly(2026, 1, 1),
                To = new DateOnly(2026, 12, 31)
            },
            output,
            new AiReadyJsonlExporter(),
            new DatasetStatisticsAccumulator(),
            QualitySummary(),
            CoverageEntry(40));

        Assert.Equal(3, result.SplitSegments.Count);
        Assert.Equal(TimeSeriesSegment.Research, result.SplitSegments[0].Segment);
        Assert.Equal(TimeSeriesSegment.Validation, result.SplitSegments[1].Segment);
        Assert.Equal(TimeSeriesSegment.Holdout, result.SplitSegments[2].Segment);
        Assert.True(result.SplitSegments[0].LastObservation < result.SplitSegments[1].FirstObservation);
        Assert.True(result.SplitSegments[1].LastObservation < result.SplitSegments[2].FirstObservation);
        Assert.Equal(40, result.SplitSegments.Sum(s => s.Observations));
    }

    [Fact]
    public async Task PrepareAsync_StreamsAllRowsToOutput()
    {
        var (_, service) = Create(40);
        using var output = new MemoryStream();

        await service.PrepareAsync(
            new AiReadyDatasetRequest
            {
                InstrumentId = InstrumentId,
                Source = Source,
                From = new DateOnly(2026, 1, 1),
                To = new DateOnly(2026, 12, 31)
            },
            output,
            new AiReadyJsonlExporter(),
            new DatasetStatisticsAccumulator(),
            QualitySummary(),
            CoverageEntry(40));

        output.Position = 0;
        using var reader = new StreamReader(output);
        var lines = (await reader.ReadToEndAsync()).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(40, lines.Length);
    }

    [Fact]
    public async Task PrepareAsync_IdempotentRepeat_ProducesEquivalentLogicalOutput()
    {
        var (_, service) = Create(40);
        using var output1 = new MemoryStream();
        using var output2 = new MemoryStream();

        var result1 = await service.PrepareAsync(
            new AiReadyDatasetRequest
            {
                InstrumentId = InstrumentId,
                Source = Source,
                From = new DateOnly(2026, 1, 1),
                To = new DateOnly(2026, 12, 31)
            },
            output1,
            new AiReadyJsonlExporter(),
            new DatasetStatisticsAccumulator(),
            QualitySummary(),
            CoverageEntry(40));

        var result2 = await service.PrepareAsync(
            new AiReadyDatasetRequest
            {
                InstrumentId = InstrumentId,
                Source = Source,
                From = new DateOnly(2026, 1, 1),
                To = new DateOnly(2026, 12, 31)
            },
            output2,
            new AiReadyJsonlExporter(),
            new DatasetStatisticsAccumulator(),
            QualitySummary(),
            CoverageEntry(40));

        // Same dataset identity, row count, statistics, and record content.
        Assert.Equal(result1.DatasetId, result2.DatasetId);
        Assert.Equal(result1.DatasetVersion, result2.DatasetVersion);
        Assert.Equal(result1.ProjectedRows, result2.ProjectedRows);
        Assert.Equal(result1.Statistics.Records, result2.Statistics.Records);
        Assert.Equal(result1.FirstRecords.Count, result2.FirstRecords.Count);
        Assert.Equal(result1.LastRecords.Count, result2.LastRecords.Count);

        output1.Position = 0;
        output2.Position = 0;
        Assert.Equal(output1.ToArray(), output2.ToArray());
    }

    [Fact]
    public async Task PrepareAsync_StatisticsAccumulateRuleAndRegimeDistributions()
    {
        var (_, service) = Create(40);
        using var output = new MemoryStream();
        var statistics = new DatasetStatisticsAccumulator();

        var result = await service.PrepareAsync(
            new AiReadyDatasetRequest
            {
                InstrumentId = InstrumentId,
                Source = Source,
                From = new DateOnly(2026, 1, 1),
                To = new DateOnly(2026, 12, 31)
            },
            output,
            new AiReadyJsonlExporter(),
            statistics,
            QualitySummary(),
            CoverageEntry(40));

        Assert.Equal(40, result.Statistics.Records);
        // Every observation has RSI 55 + MACD 1 → TrendFollowing matches (RSI >= 50, bullish).
        Assert.True(result.Statistics.RuleMatchDistribution.Values.Sum() > 0);
        Assert.Equal(40, result.Statistics.TrendRegimeDistribution.Values.Sum());
        Assert.True(result.Statistics.ForwardReturnDistributions.Count > 0);
    }

    [Fact]
    public async Task PrepareAsync_InvalidRange_Throws()
    {
        var (_, service) = Create(5);
        using var output = new MemoryStream();

        await Assert.ThrowsAsync<ArgumentException>(() => service.PrepareAsync(
            new AiReadyDatasetRequest
            {
                InstrumentId = InstrumentId,
                Source = Source,
                From = new DateOnly(2026, 12, 31),
                To = new DateOnly(2026, 1, 1)
            },
            output,
            new AiReadyJsonlExporter(),
            new DatasetStatisticsAccumulator(),
            QualitySummary(),
            CoverageEntry(5)));
    }

    [Fact]
    public async Task PrepareAsync_NoRowsInStore_ProducesEmptyDataset()
    {
        var (_, service) = Create(0);
        using var output = new MemoryStream();

        var result = await service.PrepareAsync(
            new AiReadyDatasetRequest
            {
                InstrumentId = "missing-instrument",
                Source = Source,
                From = new DateOnly(2026, 1, 1),
                To = new DateOnly(2026, 12, 31)
            },
            output,
            new AiReadyJsonlExporter(),
            new DatasetStatisticsAccumulator(),
            QualitySummary(),
            CoverageEntry(0));

        Assert.True(result.Ok);
        Assert.Equal(0, result.RawRows);
        Assert.Equal(0, result.ProjectedRows);
        Assert.Empty(result.SplitSegments);

        output.Position = 0;
        Assert.Equal(0, output.Length);
    }
}

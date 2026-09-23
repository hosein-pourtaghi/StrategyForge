using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StrategyForge.Domain.Configuration;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Interfaces.Analysis;
using StrategyForge.Domain.Interfaces.Providers;
using StrategyForge.Domain.Models;
using StrategyForge.Infrastructure.Services;
using Xunit;

namespace StrategyForge.Infrastructure.Tests.Services;

// ============================================================
// IndicatorWarmupProber Tests — warm-up derived from real indicators
// ============================================================

public class IndicatorWarmupProberTests
{
    private static IndicatorWarmupProber CreateProber()
    {
        var indicators = new List<IIndicator>
        {
            new StrategyForge.Analysis.Indicators.SmIndicator(),
            new StrategyForge.Analysis.Indicators.EmaIndicator(),
            new StrategyForge.Analysis.Indicators.RsiIndicator(),
            new StrategyForge.Analysis.Indicators.MacdIndicator(),
            new StrategyForge.Analysis.Indicators.BollingerBandsIndicator()
        };
        var engine = new StrategyForge.Analysis.IndicatorEngine(indicators);
        return new IndicatorWarmupProber(engine);
    }

    [Fact]
    public void GetMaxWarmupRows_Defaults_MeetsMacdRequirement()
    {
        var prober = CreateProber();

        var warmup = prober.GetMaxWarmupRows();

        // MACD (fast 12, slow 26, signal 9) needs 26 + 9 = 35 candles before its first
        // emission; the prober must discover this from the real implementations, not
        // from a hardcoded constant.
        Assert.Equal(35, warmup);
    }

    [Fact]
    public void GetMaxWarmupRows_WithCustomSmaPeriod_TracksParameter()
    {
        // SMA emits its first value once `period` candles are available.
        var prober = CreateProber();

        var warmup = prober.GetMaxWarmupRows(new Dictionary<string, IndicatorParameters>
        {
            ["SMA"] = new IndicatorParameters { Period = 50 }
        });

        Assert.Equal(50, warmup);
    }

    [Fact]
    public void GetMaxWarmupRows_IsDeterministic()
    {
        var prober = CreateProber();

        Assert.Equal(prober.GetMaxWarmupRows(), prober.GetMaxWarmupRows());
    }
}

// ============================================================
// HistoricalRowValidator Tests — data quality reason codes
// ============================================================

public class HistoricalRowValidatorTests
{
    private readonly HistoricalRowValidator _validator = new();

    private static Candle CreateCandle(DateOnly date, decimal open = 100m, decimal close = 100m) => new()
    {
        Date = date,
        Open = open,
        High = Math.Max(open, close) + 2m,
        Low = Math.Min(open, close) - 2m,
        Close = close,
        Volume = 1000,
        Change = 1m,
        ChangePercent = 1m,
        Provenance = new DataProvenance
        {
            Source = SourceAdapterType.Tgju,
            SourceInstrumentId = "geram18",
            FetchedAtUtc = DateTimeOffset.UtcNow,
            IsCached = false
        }
    };

    [Fact]
    public void Validate_ValidOhlc_ReturnsValid()
    {
        var assessment = _validator.Validate(CreateCandle(new DateOnly(2026, 1, 5)), null);

        Assert.Equal(HistoricalDataQualityStatus.Valid, assessment.Status);
        Assert.Empty(assessment.Findings);
    }

    [Fact]
    public void Validate_HighBelowLow_ReturnsInvalidOhlc()
    {
        var candle = CreateCandle(new DateOnly(2026, 1, 5));
        candle = candle with { High = candle.Low - 1 };

        var assessment = _validator.Validate(candle, null);

        Assert.Equal(HistoricalDataQualityStatus.Invalid, assessment.Status);
        Assert.Contains(assessment.Findings, f => f.Code == HistoricalDataQualityReason.InvalidOhlc);
    }

    [Fact]
    public void Validate_NonPositiveClose_ReturnsInvalidPrice()
    {
        var candle = CreateCandle(new DateOnly(2026, 1, 5)) with { Close = 0m };

        var assessment = _validator.Validate(candle, null);

        Assert.Equal(HistoricalDataQualityStatus.Invalid, assessment.Status);
    }

    [Fact]
    public void Validate_MissingDate_ReturnsMissingDate()
    {
        var candle = CreateCandle(default);

        var assessment = _validator.Validate(candle, null);

        Assert.Contains(assessment.Findings, f => f.Code == HistoricalDataQualityReason.MissingDate);
        Assert.Equal(HistoricalDataQualityStatus.Invalid, assessment.Status);
    }

    [Fact]
    public void Validate_SameDateAsPrevious_ReturnsDuplicateObservation()
    {
        var date = new DateOnly(2026, 1, 5);

        var assessment = _validator.Validate(CreateCandle(date), date);

        Assert.Contains(assessment.Findings, f => f.Code == HistoricalDataQualityReason.DuplicateObservation);
        Assert.Equal(HistoricalDataQualityStatus.Invalid, assessment.Status);
    }

    [Fact]
    public void Validate_EarlierDateThanPrevious_ReturnsOutOfOrder()
    {
        var assessment = _validator.Validate(
            CreateCandle(new DateOnly(2026, 1, 4)), new DateOnly(2026, 1, 6));

        Assert.Contains(assessment.Findings, f => f.Code == HistoricalDataQualityReason.OutOfOrder);
        Assert.Equal(HistoricalDataQualityStatus.Invalid, assessment.Status);
    }

    [Fact]
    public void Validate_TwoDayGap_ReturnsDateGapWarningNotError()
    {
        // 2026-01-05 (Mon) → 2026-01-09 (Fri): a weekend gap is an observation, not an error.
        var assessment = _validator.Validate(
            CreateCandle(new DateOnly(2026, 1, 9)), new DateOnly(2026, 1, 5));

        Assert.Contains(assessment.Findings, f => f.Code == HistoricalDataQualityReason.DateGap);
        Assert.Equal(HistoricalDataQualityStatus.Warning, assessment.Status);
    }

    [Fact]
    public void Validate_ZeroVolume_ReturnsZeroVolumeWarning()
    {
        var candle = CreateCandle(new DateOnly(2026, 1, 5)) with { Volume = 0 };

        var assessment = _validator.Validate(candle, null);

        Assert.Contains(assessment.Findings, f => f.Code == HistoricalDataQualityReason.ZeroVolume);
        Assert.Equal(HistoricalDataQualityStatus.Warning, assessment.Status);
    }

    [Fact]
    public void Validate_MissingChange_ReturnsMissingOptionalFieldWarning()
    {
        var candle = CreateCandle(new DateOnly(2026, 1, 5)) with { Change = null, ChangePercent = null };

        var assessment = _validator.Validate(candle, null);

        Assert.Contains(assessment.Findings, f => f.Code == HistoricalDataQualityReason.MissingOptionalField);
        Assert.Equal(HistoricalDataQualityStatus.Warning, assessment.Status);
    }

    [Fact]
    public void Validate_MissingProvenance_ReturnsSourceMetadataIncompleteWarning()
    {
        var candle = CreateCandle(new DateOnly(2026, 1, 5)) with { Provenance = null };

        var assessment = _validator.Validate(candle, null);

        Assert.Contains(assessment.Findings, f => f.Code == HistoricalDataQualityReason.SourceMetadataIncomplete);
        Assert.Equal(HistoricalDataQualityStatus.Warning, assessment.Status);
    }
}

// ============================================================
// HistoricalProcessingService Tests — end-to-end pipeline
// ============================================================

public class HistoricalProcessingServiceTests
{
    private const string InstrumentId = "test-usd-irr";

    private readonly InMemoryHistoricalDatasetStore _rawStore = new();
    private readonly InMemoryEnrichedDatasetStore _enrichedStore = new();

    /// <summary>
    /// Registers RSI (14) and MACD (12/26/9) only, so the derived warm-up requirement
    /// is exactly 35 rows and every expectation in this class is precisely computable.
    /// </summary>
    private HistoricalProcessingService CreateService(
        int batchSize = 1000,
        int warmupFactor = 1,
        int? maxWarmupRows = null,
        IHistoricalDatasetPageReader? pageReader = null)
    {
        var indicators = new List<IIndicator>
        {
            new StrategyForge.Analysis.Indicators.RsiIndicator(),
            new StrategyForge.Analysis.Indicators.MacdIndicator()
        };
        var engine = new StrategyForge.Analysis.IndicatorEngine(indicators);

        return new HistoricalProcessingService(
            pageReader ?? _rawStore,
            _enrichedStore,
            engine,
            new IndicatorWarmupProber(engine),
            new HistoricalRowValidator(),
            Options.Create(new HistoricalProcessingSettings
            {
                BatchSize = batchSize,
                WarmupFactor = warmupFactor,
                MaxWarmupRows = maxWarmupRows ?? 5000
            }),
            Mock.Of<ILogger<HistoricalProcessingService>>());
    }

    private async Task SeedAsync(params Candle[] candles)
    {
        var instrument = new InstrumentMapping
        {
            InstrumentId = InstrumentId,
            Symbol = "TEST",
            DisplayName = "Test Instrument",
            AssetClass = AssetType.Currency,
            Exchange = "free_market",
            QuoteCurrency = "IRR",
            SourceIdentifiers = new Dictionary<SourceAdapterType, SourceIdentifier>()
        };
        await _rawStore.UpsertCandlesAsync(
            instrument, SourceAdapterType.Tgju, candles, CancellationToken.None);
    }

    private static Candle CreateCandle(DateOnly date, decimal close) => new()
    {
        Date = date,
        Open = close - 1,
        High = close + 1,
        Low = close - 2,
        Close = close,
        Volume = 0,
        Provenance = new DataProvenance
        {
            Source = SourceAdapterType.Tgju,
            SourceInstrumentId = "usd-irr",
            FetchedAtUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            IsCached = false
        }
    };

    private static List<Candle> CreateSeries(int count, DateOnly start) =>
        Enumerable.Range(0, count)
            .Select(i => CreateCandle(start.AddDays(i), 100m + i))
            .ToList();

    [Fact]
    public async Task ProcessAsync_PersistsEnrichedRowsWithIndicators()
    {
        // 60 daily candles: 2026-01-01 .. 2026-03-01 (31 + 28 + 1 days).
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 3, 1);
        await SeedAsync(CreateSeries(60, from).ToArray());

        var service = CreateService();
        var result = await service.ProcessAsync(
            HistoricalProcessingRequest.Create(InstrumentId, SourceAdapterType.Tgju, from, to));

        Assert.True(result.Ok);
        Assert.Equal(60, result.RowsRead);
        Assert.Equal(60, result.RowsAccepted);
        Assert.Equal(0, result.RowsRejected);
        Assert.Equal(60, result.RowsWritten);
        Assert.Equal(1, result.BatchesProcessed);

        var stored = await _enrichedStore.GetEnrichedAsync(
            InstrumentId, SourceAdapterType.Tgju, from: from, to: to);
        Assert.Equal(60, stored.Count);

        var last = stored[^1];
        Assert.Equal(to, last.ObservationDate);
        Assert.Contains("RSI", last.Indicators.Keys);
        Assert.Contains("MACD", last.Indicators.Keys);
        // MACD preserves its components
        Assert.Contains("Signal", last.Indicators["MACD"].Keys);
        Assert.Contains("Histogram", last.Indicators["MACD"].Keys);
    }

    [Fact]
    public async Task ProcessAsync_RespectsRequestedRange()
    {
        // 90 rows from 2025-11-01 → 2025-11-01 .. 2026-01-29.
        // Range [2026-01-01, 2026-01-31] → 29 in-scope rows; 61 rows exist before the
        // range, of which exactly the required 35 (MACD 26+9) load as warm-up context.
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 1, 31);
        await SeedAsync(CreateSeries(90, new DateOnly(2025, 11, 1)).ToArray());

        var service = CreateService();
        var result = await service.ProcessAsync(
            HistoricalProcessingRequest.Create(InstrumentId, SourceAdapterType.Tgju, from, to));

        Assert.True(result.Ok);
        Assert.Equal(29, result.RowsInScope);
        Assert.Equal(35, result.WarmupRows);
        Assert.Equal(64, result.RowsRead);

        var stored = await _enrichedStore.GetEnrichedAsync(
            InstrumentId, SourceAdapterType.Tgju);
        Assert.Equal(29, stored.Count);
        Assert.All(stored, o =>
        {
            Assert.True(o.ObservationDate >= from);
            Assert.True(o.ObservationDate <= to);
        });
    }

    [Fact]
    public async Task ProcessAsync_WarmupEnablesIndicatorsInsideRange()
    {
        // 80 rows exist BEFORE the range (2025-10-01 .. 2025-12-19) and 40 inside
        // (2026-01-01 .. 2026-02-09). MACD (35) would starve inside the range alone;
        // the warm-up context must carry indicator history across the range boundary.
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 2, 9);
        var warmup = CreateSeries(80, new DateOnly(2025, 10, 1));
        var inRange = CreateSeries(40, from);
        await SeedAsync(warmup.Concat(inRange).ToArray());

        var service = CreateService();
        var result = await service.ProcessAsync(
            HistoricalProcessingRequest.Create(InstrumentId, SourceAdapterType.Tgju, from, to));

        Assert.True(result.Ok);
        Assert.Equal(40, result.RowsInScope);
        Assert.Equal(35, result.WarmupRows); // the requirement, not "everything available"

        var stored = await _enrichedStore.GetEnrichedAsync(
            InstrumentId, SourceAdapterType.Tgju, from: from, to: to);
        Assert.Equal(40, stored.Count);
        Assert.All(stored, o => Assert.Contains("MACD", o.Indicators.Keys));
    }

    [Fact]
    public async Task ProcessAsync_InvalidRowsRejected_ButValidRowsStillEnriched()
    {
        // 40 rows seeded from Jan 1; the range bounds to Jan 31 → 31 in-scope rows.
        // Row 10 (2026-01-11) is corrupted (High < Low): rejected, while the other 30
        // rows are still validated, enriched, and persisted.
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 1, 31);
        var series = CreateSeries(40, from);
        series[10] = series[10] with { High = series[10].Low - 5 };

        await SeedAsync(series.ToArray());

        var service = CreateService();
        var result = await service.ProcessAsync(
            HistoricalProcessingRequest.Create(InstrumentId, SourceAdapterType.Tgju, from, to));

        Assert.Equal(1, result.RowsRejected);
        Assert.Equal(30, result.RowsAccepted);
        Assert.Equal(1, result.ValidationErrors);
        Assert.True(result.ReasonCounts.ContainsKey(HistoricalDataQualityReason.InvalidOhlc));

        var stored = await _enrichedStore.GetEnrichedAsync(
            InstrumentId, SourceAdapterType.Tgju, from: from, to: to);
        Assert.Equal(30, stored.Count);
        Assert.DoesNotContain(stored, o => o.ObservationDate == series[10].Date);
    }

    [Fact]
    public async Task ProcessAsync_IdempotentSecondRun_UpdatesNotInserts()
    {
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 3, 1);
        await SeedAsync(CreateSeries(60, from).ToArray());

        var service = CreateService();
        var request = HistoricalProcessingRequest.Create(InstrumentId, SourceAdapterType.Tgju, from, to);

        var first = await service.ProcessAsync(request);
        var second = await service.ProcessAsync(request);

        Assert.True(first.Ok);
        Assert.True(second.Ok);
        Assert.Equal(60, first.RowsWritten);
        Assert.Equal(0, second.RowsWritten);
        Assert.Equal(0, second.RowsChanged);
        Assert.Equal(first.RowsAccepted, second.RowsUnchanged);
    }

    [Fact]
    public async Task ProcessAsync_SourcesRemainSeparate()
    {
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 3, 1);
        var instrument = new InstrumentMapping
        {
            InstrumentId = InstrumentId,
            Symbol = "TEST",
            DisplayName = "Test Instrument",
            AssetClass = AssetType.Currency,
            Exchange = "free_market",
            QuoteCurrency = "IRR",
            SourceIdentifiers = new Dictionary<SourceAdapterType, SourceIdentifier>()
        };
        var candles = CreateSeries(60, from).ToArray();

        await _rawStore.UpsertCandlesAsync(instrument, SourceAdapterType.Tgju, candles, CancellationToken.None);
        // Second source: different price levels, same dates.
        await _rawStore.UpsertCandlesAsync(
            instrument, SourceAdapterType.Nobitex,
            candles.Select(c => c with { Close = c.Close * 1000, Open = c.Open * 1000 }).ToArray(),
            CancellationToken.None);

        var service = CreateService();
        await service.ProcessAsync(
            HistoricalProcessingRequest.Create(InstrumentId, SourceAdapterType.Tgju, from, to));

        var tgju = await _enrichedStore.GetEnrichedAsync(InstrumentId, SourceAdapterType.Tgju);
        var nobitex = await _enrichedStore.GetEnrichedAsync(InstrumentId, SourceAdapterType.Nobitex);

        Assert.Equal(60, tgju.Count);
        Assert.Empty(nobitex); // Only the requested source was processed.

        // TGJU values are preserved as-is (never overwritten by the other source).
        Assert.All(tgju, o => Assert.True(o.Close < 200m));
    }

    [Fact]
    public async Task ProcessAsync_BatchesBoundedAndOrdered()
    {
        // 250 rows with BatchSize = 100 → 3 batches; ordering must remain chronological.
        var from = new DateOnly(2026, 1, 1);
        var to = from.AddDays(249);
        await SeedAsync(CreateSeries(250, from).ToArray());

        var service = CreateService(batchSize: 100);
        var result = await service.ProcessAsync(
            HistoricalProcessingRequest.Create(InstrumentId, SourceAdapterType.Tgju, from, to));

        Assert.Equal(3, result.BatchesProcessed);
        Assert.Equal(250, result.RowsRead);

        var stored = await _enrichedStore.GetEnrichedAsync(
            InstrumentId, SourceAdapterType.Tgju, from: from, to: to);
        Assert.Equal(250, stored.Count);
        for (var i = 1; i < stored.Count; i++)
        {
            Assert.True(stored[i].ObservationDate > stored[i - 1].ObservationDate);
        }
    }

    [Fact]
    public async Task ProcessAsync_EmptyRange_FailsFast()
    {
        var service = CreateService();

        var result = await service.ProcessAsync(
            HistoricalProcessingRequest.Create(
                InstrumentId, SourceAdapterType.Tgju,
                new DateOnly(2026, 1, 31), new DateOnly(2026, 1, 1)));

        Assert.False(result.Ok);
        Assert.Equal("INVALID_RANGE", result.ErrorCode);
    }

    [Fact]
    public async Task ProcessAsync_DuplicateDatesInBatch_LastWins()
    {
        // Store identity is unique per (instrument, source, date), so a same-date
        // duplicate can only appear inside a reader page — inject it via a stub
        // reader to make the in-batch collapse observable.
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 2, 9);
        var series = CreateSeries(40, from);
        // OHLC-consistent alternative row for the same date (High must cover Close).
        var duplicated = series[20] with { Close = 999m, High = 1000m };
        await SeedAsync(series.ToArray());

        var service = CreateService(pageReader: new SinglePageReader(series.Concat([duplicated]).ToList()));
        var result = await service.ProcessAsync(
            HistoricalProcessingRequest.Create(InstrumentId, SourceAdapterType.Tgju, from, to));

        Assert.Equal(1, result.Duplicates);
        Assert.Equal(40, result.RowsAccepted); // 40 distinct dates; the duplicate collapses.

        var stored = await _enrichedStore.GetEnrichedAsync(
            InstrumentId, SourceAdapterType.Tgju, from: from, to: to);
        Assert.Equal(40, stored.Count);
        Assert.Equal(999m, stored.Single(o => o.ObservationDate == series[20].Date).Close);
    }

    [Fact]
    public async Task ProcessAsync_InsufficientIndicatorHistory_PersistsDerivedRowsWithoutIndicators()
    {
        // 10 rows cannot satisfy RSI (14) or MACD (35): accepted rows must still reach
        // the derived dataset — with empty indicator maps, never fabricated values.
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 1, 10);
        await SeedAsync(CreateSeries(10, from).ToArray());

        var service = CreateService();
        var result = await service.ProcessAsync(
            HistoricalProcessingRequest.Create(InstrumentId, SourceAdapterType.Tgju, from, to));

        Assert.True(result.Ok);
        Assert.Equal(10, result.RowsAccepted);
        Assert.Equal(10, result.RowsWritten);

        var stored = await _enrichedStore.GetEnrichedAsync(
            InstrumentId, SourceAdapterType.Tgju, from: from, to: to);
        Assert.Equal(10, stored.Count);
        Assert.All(stored, o => Assert.Empty(o.Indicators));
    }
}

/// <summary>
/// Deterministic single-source page reader over a fixed row list. Unlike the store,
/// it can serve the same date twice within one page, which is what the in-batch
/// duplicate-collapse behavior needs to be observable.
/// </summary>
internal sealed class SinglePageReader : IHistoricalDatasetPageReader
{
    private readonly IReadOnlyList<Candle> _rows;

    public SinglePageReader(IReadOnlyList<Candle> rows) => _rows = rows;

    public Task<IReadOnlyList<Candle>> ReadPageAsync(
        string instrumentId,
        SourceAdapterType source,
        DateOnly? from,
        DateOnly? to,
        DateOnly? afterDate,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var lowerExclusive = afterDate ?? (from.HasValue ? from.Value.AddDays(-1) : (DateOnly?)null);

        var page = _rows
            .Where(c => !lowerExclusive.HasValue || c.Date > lowerExclusive.Value)
            .Where(c => !to.HasValue || c.Date <= to.Value)
            .OrderBy(c => c.Date)
            .Take(pageSize)
            .ToList();

        return Task.FromResult<IReadOnlyList<Candle>>(page);
    }

    public Task<IReadOnlyList<Candle>> ReadLastPageAsync(
        string instrumentId,
        SourceAdapterType source,
        DateOnly? beforeDate,
        int maxRows,
        CancellationToken cancellationToken = default)
    {
        var results = _rows
            .Where(c => !beforeDate.HasValue || c.Date < beforeDate.Value)
            .OrderByDescending(c => c.Date)
            .Take(maxRows)
            .OrderBy(c => c.Date)
            .ToList();

        return Task.FromResult<IReadOnlyList<Candle>>(results);
    }
}

// ============================================================
// InMemory enriched store Tests — idempotent derived upsert
// ============================================================

public class InMemoryEnrichedDatasetStoreTests
{
    private const string InstrumentId = "test-usd-irr";

    private static EnrichedObservation CreateObservation(DateOnly date, decimal close = 100m) => new()
    {
        InstrumentId = InstrumentId,
        Source = SourceAdapterType.Tgju,
        ObservationDate = date,
        Open = close - 1,
        High = close + 1,
        Low = close - 2,
        Close = close,
        Volume = 0,
        Indicators = new Dictionary<string, IReadOnlyDictionary<string, decimal>>
        {
            ["RSI"] = new Dictionary<string, decimal> { ["RSI"] = 55.5m }
        },
        QualityStatus = HistoricalDataQualityStatus.Valid,
        ProcessedBy = "1.0.0"
    };

    [Fact]
    public async Task UpsertEnrichedAsync_InsertsThenCountsUnchanged()
    {
        var store = new InMemoryEnrichedDatasetStore();
        var date = new DateOnly(2026, 1, 5);

        var first = await store.UpsertEnrichedAsync(
            InstrumentId, SourceAdapterType.Tgju, [CreateObservation(date)]);
        var second = await store.UpsertEnrichedAsync(
            InstrumentId, SourceAdapterType.Tgju, [CreateObservation(date)]);

        Assert.Equal(1, first.Inserted);
        Assert.Equal(0, second.Inserted);
        Assert.Equal(0, second.Changed);
        Assert.Equal(1, second.Unchanged);
    }

    [Fact]
    public async Task UpsertEnrichedAsync_ValueChange_CountsChanged()
    {
        var store = new InMemoryEnrichedDatasetStore();
        var date = new DateOnly(2026, 1, 5);

        await store.UpsertEnrichedAsync(
            InstrumentId, SourceAdapterType.Tgju, [CreateObservation(date, close: 100m)]);
        var outcome = await store.UpsertEnrichedAsync(
            InstrumentId, SourceAdapterType.Tgju, [CreateObservation(date, close: 105m)]);

        Assert.Equal(1, outcome.Changed);
        Assert.Equal(0, outcome.Unchanged);

        var stored = await store.GetEnrichedAsync(InstrumentId, SourceAdapterType.Tgju);
        Assert.Equal(105m, stored[0].Close);
    }

    [Fact]
    public async Task GetEnrichedAsync_PaginationAndOrdering()
    {
        var store = new InMemoryEnrichedDatasetStore();
        var dates = Enumerable.Range(0, 10)
            .Select(i => new DateOnly(2026, 1, 1).AddDays(i))
            .ToList();
        await store.UpsertEnrichedAsync(
            InstrumentId, SourceAdapterType.Tgju,
            dates.Select(d => CreateObservation(d)).ToList());

        var page = await store.GetEnrichedAsync(
            InstrumentId, SourceAdapterType.Tgju, skip: 2, take: 3);

        Assert.Equal(3, page.Count);
        Assert.Equal(dates[2], page[0].ObservationDate);
        Assert.Equal(dates[4], page[2].ObservationDate);
    }
}

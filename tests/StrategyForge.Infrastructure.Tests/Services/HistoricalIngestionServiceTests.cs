using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using StrategyForge.Domain.Configuration;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Interfaces.Providers;
using StrategyForge.Domain.Models;
using StrategyForge.Infrastructure.Services;
using Xunit;

namespace StrategyForge.Infrastructure.Tests.Services;

/// <summary>
/// Phase 6 tests: historical dataset ingestion — deterministic chunking, bounded retries,
/// validation, range filtering, and idempotent upsert behavior.
/// </summary>
public class HistoricalIngestionServiceTests
{
    private static readonly DateOnly From = new(2024, 1, 1);
    private static readonly DateOnly To = new(2024, 1, 31);

    private static InstrumentMapping CreateInstrument() => new()
    {
        InstrumentId = "iran-equity-foolad-4439113430858354",
        Symbol = "\u0641\u0648\u0644\u0627\u062f",
        LatinSymbol = "Foolad",
        DisplayName = "Foolad Mobarakeh",
        AssetClass = AssetType.Stock,
        Exchange = "TSE",
        QuoteCurrency = "IRR",
        SourceIdentifiers = new Dictionary<SourceAdapterType, SourceIdentifier>
        {
            [SourceAdapterType.Tsetmc] = new SourceIdentifier { Id = "4439113430858354" }
        }
    };

    private static Candle CreateCandle(DateOnly date, decimal close) => new()
    {
        Date = date,
        Open = close - 1,
        High = close + 2,
        Low = close - 2,
        Close = close,
        Volume = 1000,
        Provenance = new DataProvenance
        {
            Source = SourceAdapterType.Tsetmc,
            SourceInstrumentId = "4439113430858354",
            FetchedAtUtc = DateTimeOffset.UtcNow,
            IsCached = false
        }
    };

    private static HistoricalDatasetRequest CreateRequest(SourceAdapterType source = SourceAdapterType.Tsetmc) => new()
    {
        Instrument = CreateInstrument(),
        Source = source,
        From = From,
        To = To
    };

    private static HistoricalIngestionSettings CreateSettings(
        Func<HistoricalIngestionSettings, HistoricalIngestionSettings>? configure = null)
    {
        var settings = new HistoricalIngestionSettings
        {
            ChunkDays = 90,
            MaxChunkRetries = 0,
            ChunkRetryBaseDelayMs = 0,
            MaxFailedChunks = 3
        };

        return configure != null ? configure(settings) : settings;
    }

    private static HistoricalIngestionService CreateService(
        Mock<IDataSourceRegistry> registryMock,
        IHistoricalDatasetStore? store = null,
        HistoricalIngestionSettings? settings = null)
    {
        return new HistoricalIngestionService(
            registryMock.Object,
            store ?? new InMemoryHistoricalDatasetStore(),
            Options.Create(settings ?? CreateSettings()),
            NullLogger<HistoricalIngestionService>.Instance);
    }

    // ============================================================
    // Chunking
    // ============================================================

    [Fact]
    public void BuildChunks_SingleChunk_WhenRangeFitsChunkSize()
    {
        var chunks = HistoricalIngestionService.BuildChunks(From, To, 90);

        var chunk = Assert.Single(chunks);
        Assert.Equal(From, chunk.From);
        Assert.Equal(To, chunk.To);
    }

    [Fact]
    public void BuildChunks_SplitsIntoContiguousNonOverlappingWindows()
    {
        // Jan 1 → Mar 31 = 91 days; 30-day chunks → 4 windows (last is a 1-day remainder).
        var chunks = HistoricalIngestionService.BuildChunks(
            new DateOnly(2024, 1, 1), new DateOnly(2024, 3, 31), 30);

        Assert.Equal(4, chunks.Count);
        Assert.Equal(new DateOnly(2024, 1, 1), chunks[0].From);
        Assert.Equal(new DateOnly(2024, 1, 30), chunks[0].To);
        Assert.Equal(new DateOnly(2024, 1, 31), chunks[1].From);
        Assert.Equal(new DateOnly(2024, 2, 29), chunks[1].To);
        Assert.Equal(new DateOnly(2024, 3, 1), chunks[2].From);
        Assert.Equal(new DateOnly(2024, 3, 30), chunks[2].To);
        Assert.Equal(new DateOnly(2024, 3, 31), chunks[3].From);
        Assert.Equal(new DateOnly(2024, 3, 31), chunks[3].To);
    }

    [Fact]
    public void BuildChunks_SingleDayRange_ProducesOneChunk()
    {
        var chunks = HistoricalIngestionService.BuildChunks(From, From, 90);

        var chunk = Assert.Single(chunks);
        Assert.Equal(From, chunk.From);
        Assert.Equal(From, chunk.To);
    }

    // ============================================================
    // Fetch + persist behavior
    // ============================================================

    [Fact]
    public async Task ImportAsync_FetchesThroughRegistryWithPreferredSourceOnly()
    {
        var registryMock = new Mock<IDataSourceRegistry>();
        registryMock
            .Setup(r => r.FetchHistoricalCandlesAsync(
                It.IsAny<InstrumentMapping>(),
                It.IsAny<DateOnly>(),
                It.IsAny<DateOnly>(),
                It.IsAny<SourceAdapterType?>(),
                It.IsAny<SourceSelectionMode>(),
                It.IsAny<CandleResolution?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(DataResult<IReadOnlyList<Candle>>.Success(
                new[] { CreateCandle(From, 100m) }.AsReadOnly()));

        var service = CreateService(registryMock);
        var result = await service.ImportAsync(CreateRequest());

        Assert.True(result.Ok);
        registryMock.Verify(
            r => r.FetchHistoricalCandlesAsync(
                It.IsAny<InstrumentMapping>(),
                From,
                To,
                SourceAdapterType.Tsetmc,
                SourceSelectionMode.PreferredOnly,
                It.IsAny<CandleResolution?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ImportAsync_PersistsFetchedCandles()
    {
        var registryMock = new Mock<IDataSourceRegistry>();
        registryMock
            .Setup(r => r.FetchHistoricalCandlesAsync(
                It.IsAny<InstrumentMapping>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                It.IsAny<SourceAdapterType?>(), It.IsAny<SourceSelectionMode>(),
                It.IsAny<CandleResolution?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DataResult<IReadOnlyList<Candle>>.Success(
                new[] { CreateCandle(From, 100m), CreateCandle(From.AddDays(1), 101m) }.AsReadOnly()));

        var store = new InMemoryHistoricalDatasetStore();
        var service = CreateService(registryMock, store);
        var result = await service.ImportAsync(CreateRequest());

        Assert.True(result.Ok);
        Assert.Equal(2, result.Inserted);
        Assert.Equal(0, result.Updated);
        Assert.Equal(2, await store.CountCandlesAsync(CreateInstrument().InstrumentId));
    }

    // ============================================================
    // Idempotency
    // ============================================================

    [Fact]
    public async Task ImportAsync_ReRun_DoesNotDuplicateObservations()
    {
        var registryMock = new Mock<IDataSourceRegistry>();
        registryMock
            .Setup(r => r.FetchHistoricalCandlesAsync(
                It.IsAny<InstrumentMapping>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                It.IsAny<SourceAdapterType?>(), It.IsAny<SourceSelectionMode>(),
                It.IsAny<CandleResolution?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DataResult<IReadOnlyList<Candle>>.Success(
                new[] { CreateCandle(From, 100m), CreateCandle(From.AddDays(1), 101m) }.AsReadOnly()));

        var store = new InMemoryHistoricalDatasetStore();
        var service = CreateService(registryMock, store);
        var request = CreateRequest();

        var first = await service.ImportAsync(request);
        var second = await service.ImportAsync(request);

        Assert.True(first.Ok);
        Assert.True(second.Ok);
        Assert.Equal(2, first.Inserted);
        Assert.Equal(0, second.Inserted);
        Assert.Equal(2, second.Updated);
        Assert.Equal(2, await store.CountCandlesAsync(CreateInstrument().InstrumentId));
    }

    [Fact]
    public async Task UpsertCandlesAsync_DuplicateDatesInBatch_CollapsesToLastWins()
    {
        var store = new InMemoryHistoricalDatasetStore();
        var instrument = CreateInstrument();

        var outcome = await store.UpsertCandlesAsync(
            instrument,
            SourceAdapterType.Tsetmc,
            new[]
            {
                CreateCandle(From, 100m),
                CreateCandle(From, 110m) // same date, later row wins
            });

        Assert.Equal(1, outcome.Inserted);
        Assert.Equal(1, outcome.DuplicateInBatch);

        var stored = await store.GetCandlesAsync(instrument.InstrumentId);
        var candle = Assert.Single(stored);
        Assert.Equal(110m, candle.Close);
    }

    [Fact]
    public async Task UpsertCandlesAsync_SameDateDifferentSource_AreDistinctObservations()
    {
        var store = new InMemoryHistoricalDatasetStore();
        var instrument = CreateInstrument();
        var candle = CreateCandle(From, 100m);

        await store.UpsertCandlesAsync(instrument, SourceAdapterType.Tsetmc, new[] { candle });
        await store.UpsertCandlesAsync(instrument, SourceAdapterType.Tgju, new[] { candle with { Volume = 0 } });

        // The identity includes the provider: two sources are two rows.
        Assert.Equal(2, await store.CountCandlesAsync(instrument.InstrumentId));
        Assert.Equal(1, await store.CountCandlesAsync(instrument.InstrumentId, SourceAdapterType.Tsetmc));
        Assert.Equal(1, await store.CountCandlesAsync(instrument.InstrumentId, SourceAdapterType.Tgju));
    }

    // ============================================================
    // Validation and range filtering
    // ============================================================

    [Fact]
    public async Task ImportAsync_InvalidCandlesRejectedAndCounted()
    {
        var invalid = CreateCandle(From, 100m) with { High = 50m, Low = 200m };
        var registryMock = new Mock<IDataSourceRegistry>();
        registryMock
            .Setup(r => r.FetchHistoricalCandlesAsync(
                It.IsAny<InstrumentMapping>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                It.IsAny<SourceAdapterType?>(), It.IsAny<SourceSelectionMode>(),
                It.IsAny<CandleResolution?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DataResult<IReadOnlyList<Candle>>.Success(
                new[] { CreateCandle(From, 100m), invalid }.AsReadOnly()));

        var store = new InMemoryHistoricalDatasetStore();
        var service = CreateService(registryMock, store);
        var result = await service.ImportAsync(CreateRequest());

        Assert.True(result.Ok);
        Assert.Equal(1, result.Rejected);
        Assert.Equal(1, result.Inserted);
        Assert.Equal(1, await store.CountCandlesAsync(CreateInstrument().InstrumentId));
    }

    [Fact]
    public async Task ImportAsync_OutOfRangeCandlesCountedAndSkipped()
    {
        var registryMock = new Mock<IDataSourceRegistry>();
        registryMock
            .Setup(r => r.FetchHistoricalCandlesAsync(
                It.IsAny<InstrumentMapping>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                It.IsAny<SourceAdapterType?>(), It.IsAny<SourceSelectionMode>(),
                It.IsAny<CandleResolution?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DataResult<IReadOnlyList<Candle>>.Success(
                new[]
                {
                    CreateCandle(From, 100m),
                    CreateCandle(To.AddDays(5), 105m) // outside requested range
                }.AsReadOnly()));

        var store = new InMemoryHistoricalDatasetStore();
        var service = CreateService(registryMock, store);
        var result = await service.ImportAsync(CreateRequest());

        Assert.True(result.Ok);
        Assert.Equal(1, result.OutOfRange);
        Assert.Equal(1, result.Inserted);
    }

    // ============================================================
    // Bounded retries and failure limits
    // ============================================================

    [Fact]
    public async Task ImportAsync_RetriableChunkFailure_RetriesUpToBoundedAttempts()
    {
        var registryMock = new Mock<IDataSourceRegistry>();
        registryMock
            .Setup(r => r.FetchHistoricalCandlesAsync(
                It.IsAny<InstrumentMapping>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                It.IsAny<SourceAdapterType?>(), It.IsAny<SourceSelectionMode>(),
                It.IsAny<CandleResolution?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DataResult<IReadOnlyList<Candle>>.Failure(new DataCollectionError2
            {
                Code = "SOURCE_UNAVAILABLE",
                Message = "Transient provider outage",
                Retryable = true
            }));

        var service = CreateService(registryMock, settings: CreateSettings(baseSettings => baseSettings with
        {
            MaxChunkRetries = 2,
            ChunkRetryBaseDelayMs = 0,
            MaxFailedChunks = 1
        }));
        var result = await service.ImportAsync(CreateRequest());

        Assert.False(result.Ok);
        Assert.Equal("CHUNK_FAILURE_LIMIT_REACHED", result.ErrorCode);
        registryMock.Verify(
            r => r.FetchHistoricalCandlesAsync(
                It.IsAny<InstrumentMapping>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                It.IsAny<SourceAdapterType?>(), It.IsAny<SourceSelectionMode>(),
                It.IsAny<CandleResolution?>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3)); // 1 initial + 2 retries, then terminal
    }

    [Fact]
    public async Task ImportAsync_NonRetriableChunkFailure_DoesNotRetry()
    {
        var registryMock = new Mock<IDataSourceRegistry>();
        registryMock
            .Setup(r => r.FetchHistoricalCandlesAsync(
                It.IsAny<InstrumentMapping>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                It.IsAny<SourceAdapterType?>(), It.IsAny<SourceSelectionMode>(),
                It.IsAny<CandleResolution?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DataResult<IReadOnlyList<Candle>>.Failure(new DataCollectionError2
            {
                Code = "UNSUPPORTED_CAPABILITY",
                Message = "Source cannot serve this instrument",
                Retryable = false
            }));

        var service = CreateService(registryMock, settings: CreateSettings(baseSettings => baseSettings with
        {
            MaxChunkRetries = 5,
            ChunkRetryBaseDelayMs = 0,
            MaxFailedChunks = 1
        }));
        var result = await service.ImportAsync(CreateRequest());

        Assert.False(result.Ok);
        registryMock.Verify(
            r => r.FetchHistoricalCandlesAsync(
                It.IsAny<InstrumentMapping>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                It.IsAny<SourceAdapterType?>(), It.IsAny<SourceSelectionMode>(),
                It.IsAny<CandleResolution?>(), It.IsAny<CancellationToken>()),
            Times.Once); // terminal error — no retry loop
    }

    [Fact]
    public async Task ImportAsync_SingleChunkFailureBelowLimit_ImportStillSucceeds()
    {
        var registryMock = new Mock<IDataSourceRegistry>();
        var callCount = 0;
        registryMock
            .Setup(r => r.FetchHistoricalCandlesAsync(
                It.IsAny<InstrumentMapping>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                It.IsAny<SourceAdapterType?>(), It.IsAny<SourceSelectionMode>(),
                It.IsAny<CandleResolution?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InstrumentMapping _, DateOnly from, DateOnly to,
                SourceAdapterType? _, SourceSelectionMode _, CandleResolution? _, CancellationToken _) =>
            {
                callCount++;
                if (callCount == 1)
                {
                    return DataResult<IReadOnlyList<Candle>>.Failure(new DataCollectionError2
                    {
                        Code = "SOURCE_UNAVAILABLE",
                        Message = "Transient failure",
                        Retryable = false
                    });
                }
                return DataResult<IReadOnlyList<Candle>>.Success(
                    new[] { CreateCandle(from, 100m) }.AsReadOnly());
            });

        // 31 daily chunks over January; only the first fails terminally.
        var service = CreateService(registryMock, settings: CreateSettings(baseSettings => baseSettings with
        {
            ChunkDays = 1,
            MaxFailedChunks = 2
        }));
        var result = await service.ImportAsync(CreateRequest());

        Assert.True(result.Ok);
        Assert.Equal(1, result.ChunksFailed);
        Assert.Equal(31, result.ChunksFetched);
        Assert.NotEmpty(result.Warnings);
        Assert.Equal(30, result.Inserted);
    }

    [Fact]
    public async Task ImportAsync_EmptyChunkResponse_PersistsNothingButSucceeds()
    {
        var registryMock = new Mock<IDataSourceRegistry>();
        registryMock
            .Setup(r => r.FetchHistoricalCandlesAsync(
                It.IsAny<InstrumentMapping>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                It.IsAny<SourceAdapterType?>(), It.IsAny<SourceSelectionMode>(),
                It.IsAny<CandleResolution?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DataResult<IReadOnlyList<Candle>>.Success(
                Array.Empty<Candle>().AsReadOnly()));

        var service = CreateService(registryMock);
        var result = await service.ImportAsync(CreateRequest());

        Assert.True(result.Ok);
        Assert.Equal(0, result.Inserted);
        Assert.Equal(0, result.Updated);
    }

    [Fact]
    public async Task ImportAsync_InvertedRange_FailsWithoutCallingProvider()
    {
        var registryMock = new Mock<IDataSourceRegistry>();
        var service = CreateService(registryMock);
        var request = CreateRequest() with { From = To, To = From };

        var result = await service.ImportAsync(request);

        Assert.False(result.Ok);
        Assert.Equal("INVALID_RANGE", result.ErrorCode);
        registryMock.Verify(
            r => r.FetchHistoricalCandlesAsync(
                It.IsAny<InstrumentMapping>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                It.IsAny<SourceAdapterType?>(), It.IsAny<SourceSelectionMode>(),
                It.IsAny<CandleResolution?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ImportAsync_ResultCarriesRangeAndDuration()
    {
        var registryMock = new Mock<IDataSourceRegistry>();
        registryMock
            .Setup(r => r.FetchHistoricalCandlesAsync(
                It.IsAny<InstrumentMapping>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                It.IsAny<SourceAdapterType?>(), It.IsAny<SourceSelectionMode>(),
                It.IsAny<CandleResolution?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DataResult<IReadOnlyList<Candle>>.Success(
                new[] { CreateCandle(From, 100m) }.AsReadOnly()));

        var service = CreateService(registryMock);
        var result = await service.ImportAsync(CreateRequest());

        Assert.Equal(From, result.RequestedFrom);
        Assert.Equal(To, result.RequestedTo);
        Assert.Equal(From, result.EffectiveFrom);
        Assert.Equal(To, result.EffectiveTo);
        Assert.Equal(1, result.ChunksFetched);
        Assert.Equal(0, result.ChunksFailed);
        Assert.True(result.Duration >= TimeSpan.Zero);
        Assert.Null(result.ErrorCode);
    }
}

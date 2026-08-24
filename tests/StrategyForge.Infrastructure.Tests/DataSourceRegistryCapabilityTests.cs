using Microsoft.Extensions.Logging;
using Moq;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Interfaces.Providers;
using StrategyForge.Domain.Models;
using StrategyForge.Infrastructure.DataAdapters;
using Xunit;

namespace StrategyForge.Infrastructure.Tests;

public class DataSourceRegistryCapabilityTests
{
    private readonly Mock<ILogger<DataSourceRegistry>> _loggerMock = new();

    private static readonly InstrumentMapping Foolad = new()
    {
        InstrumentId = "foolad-tse",
        Symbol = "فولاد",
        DisplayName = "Foolad",
        AssetClass = AssetType.Stock,
        Exchange = "TSE",
        QuoteCurrency = "IRR",
        SourceIdentifiers = new Dictionary<SourceAdapterType, SourceIdentifier>
        {
            [SourceAdapterType.Tsetmc] = new SourceIdentifier { Id = "4439113430858354" },
            [SourceAdapterType.BrsApi] = new SourceIdentifier { Id = "4439113430858354" }
        }
    };

    private static IDataSourceAdapter MakeAdapter(
        SourceAdapterType src, string name, bool enabled,
        MarketDataType[] caps, bool supports)
    {
        var m = new Mock<IDataSourceAdapter>();
        m.Setup(a => a.SourceType).Returns(src);
        m.Setup(a => a.Name).Returns(name);
        m.Setup(a => a.IsEnabled).Returns(enabled);
        m.Setup(a => a.SupportedCapabilities).Returns(caps);
        m.Setup(a => a.Supports(It.IsAny<InstrumentMapping>())).Returns(supports);
        m.Setup(a => a.GetHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdapterHealthStatus { IsHealthy = true });
        return m.Object;
    }

    [Fact]
    public void GetAdaptersForCapability_FiltersByCapability()
    {
        var tsetmc = MakeAdapter(SourceAdapterType.Tsetmc, "TSETMC", true,
            [MarketDataType.HistoricalCandles, MarketDataType.Snapshot], true);
        var tgju = MakeAdapter(SourceAdapterType.Tgju, "TGJU", true,
            [MarketDataType.FreeMarketFxRate, MarketDataType.Snapshot], true);

        var reg = new DataSourceRegistry([tsetmc, tgju], _loggerMock.Object);
        var list = reg.GetAdaptersForCapability(Foolad, MarketDataType.HistoricalCandles);

        Assert.Single(list);
        Assert.Equal(SourceAdapterType.Tsetmc, list[0].SourceType);
    }

    [Fact]
    public void GetAdaptersForCapability_FiltersDisabled()
    {
        var on = MakeAdapter(SourceAdapterType.Tsetmc, "TSETMC", true, [MarketDataType.HistoricalCandles], true);
        var off = MakeAdapter(SourceAdapterType.BrsApi, "BRSAPI", false, [MarketDataType.HistoricalCandles], true);

        var reg = new DataSourceRegistry([on, off], _loggerMock.Object);
        Assert.Single(reg.GetAdaptersForCapability(Foolad, MarketDataType.HistoricalCandles));
    }

    [Fact]
    public void GetAdaptersForCapability_EmptyForUnsupported()
    {
        var tsetmc = MakeAdapter(SourceAdapterType.Tsetmc, "TSETMC", true,
            [MarketDataType.HistoricalCandles], true);
        var reg = new DataSourceRegistry([tsetmc], _loggerMock.Object);
        Assert.Empty(reg.GetAdaptersForCapability(Foolad, MarketDataType.OrderBook));
    }

    [Fact]
    public async Task FetchCandles_BestAvailable_FallbackOnFailure()
    {
        var order = new List<SourceAdapterType>();
        var t1 = new Mock<IDataSourceAdapter>();
        t1.Setup(a => a.SourceType).Returns(SourceAdapterType.Tsetmc);
        t1.Setup(a => a.Name).Returns("TSETMC");
        t1.Setup(a => a.IsEnabled).Returns(true);
        t1.Setup(a => a.SupportedCapabilities).Returns(new[] { MarketDataType.HistoricalCandles });
        t1.Setup(a => a.Supports(It.IsAny<InstrumentMapping>())).Returns(true);
        t1.Setup(a => a.GetHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdapterHealthStatus { IsHealthy = true });
        t1.Setup(a => a.GetHistoricalCandlesAsync(It.IsAny<InstrumentMapping>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CandleResolution?>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add(SourceAdapterType.Tsetmc))
            .ReturnsAsync(DataResult<IReadOnlyList<Candle>>.Failure(new DataCollectionError2 { Code = "SOURCE_UNAVAILABLE", Message = "Down", Retryable = true }));

        var t2 = new Mock<IDataSourceAdapter>();
        t2.Setup(a => a.SourceType).Returns(SourceAdapterType.BrsApi);
        t2.Setup(a => a.Name).Returns("BRSAPI");
        t2.Setup(a => a.IsEnabled).Returns(true);
        t2.Setup(a => a.SupportedCapabilities).Returns(new[] { MarketDataType.HistoricalCandles });
        t2.Setup(a => a.Supports(It.IsAny<InstrumentMapping>())).Returns(true);
        t2.Setup(a => a.GetHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdapterHealthStatus { IsHealthy = true });
        var candles = new List<Candle> { new() { Date = DateOnly.FromDateTime(DateTime.Today), Open = 100, High = 110, Low = 90, Close = 105, Volume = 1000 } }.AsReadOnly();
        t2.Setup(a => a.GetHistoricalCandlesAsync(It.IsAny<InstrumentMapping>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CandleResolution?>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add(SourceAdapterType.BrsApi))
            .ReturnsAsync(DataResult<IReadOnlyList<Candle>>.Success(candles));

        var reg = new DataSourceRegistry([t2.Object, t1.Object], _loggerMock.Object);
        var result = await reg.FetchHistoricalCandlesAsync(
            Foolad, DateOnly.FromDateTime(DateTime.Today.AddDays(-30)), DateOnly.FromDateTime(DateTime.Today));

        Assert.True(result.Ok);
        Assert.Single(result.Data!);
        Assert.Equal(SourceAdapterType.Tsetmc, order[0]);
        Assert.Equal(SourceAdapterType.BrsApi, order[1]);
    }

    [Fact]
    public async Task FetchCandles_PreferredOnly_NoFallback()
    {
        var t1 = new Mock<IDataSourceAdapter>();
        t1.Setup(a => a.SourceType).Returns(SourceAdapterType.Tsetmc);
        t1.Setup(a => a.Name).Returns("TSETMC");
        t1.Setup(a => a.IsEnabled).Returns(true);
        t1.Setup(a => a.SupportedCapabilities).Returns(new[] { MarketDataType.HistoricalCandles });
        t1.Setup(a => a.Supports(It.IsAny<InstrumentMapping>())).Returns(true);
        t1.Setup(a => a.GetHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdapterHealthStatus { IsHealthy = true });
        t1.Setup(a => a.GetHistoricalCandlesAsync(It.IsAny<InstrumentMapping>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CandleResolution?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DataResult<IReadOnlyList<Candle>>.Failure(new DataCollectionError2 { Code = "SOURCE_UNAVAILABLE", Message = "Down", Retryable = true }));

        var t2 = new Mock<IDataSourceAdapter>();
        t2.Setup(a => a.SourceType).Returns(SourceAdapterType.BrsApi);
        t2.Setup(a => a.Name).Returns("BRSAPI");
        t2.Setup(a => a.IsEnabled).Returns(true);
        t2.Setup(a => a.SupportedCapabilities).Returns(new[] { MarketDataType.HistoricalCandles });
        t2.Setup(a => a.Supports(It.IsAny<InstrumentMapping>())).Returns(true);
        t2.Setup(a => a.GetHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdapterHealthStatus { IsHealthy = true });
        t2.Setup(a => a.GetHistoricalCandlesAsync(It.IsAny<InstrumentMapping>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CandleResolution?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DataResult<IReadOnlyList<Candle>>.Success(new List<Candle> { new() { Date = DateOnly.FromDateTime(DateTime.Today), Open = 100, High = 110, Low = 90, Close = 105, Volume = 1000 } }.AsReadOnly()));

        var reg = new DataSourceRegistry([t2.Object, t1.Object], _loggerMock.Object);
        var result = await reg.FetchHistoricalCandlesAsync(
            Foolad,
            DateOnly.FromDateTime(DateTime.Today.AddDays(-30)),
            DateOnly.FromDateTime(DateTime.Today),
            preferredSource: SourceAdapterType.Tsetmc,
            selectionMode: SourceSelectionMode.PreferredOnly);

        Assert.False(result.Ok);
        Assert.Equal("SOURCE_UNAVAILABLE", result.Error!.Code);
    }

    [Fact]
    public async Task FetchCandles_PreferredThenFallback()
    {
        var order = new List<SourceAdapterType>();
        var t1 = new Mock<IDataSourceAdapter>();
        t1.Setup(a => a.SourceType).Returns(SourceAdapterType.Tsetmc);
        t1.Setup(a => a.Name).Returns("TSETMC");
        t1.Setup(a => a.IsEnabled).Returns(true);
        t1.Setup(a => a.SupportedCapabilities).Returns(new[] { MarketDataType.HistoricalCandles });
        t1.Setup(a => a.Supports(It.IsAny<InstrumentMapping>())).Returns(true);
        t1.Setup(a => a.GetHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdapterHealthStatus { IsHealthy = true });
        t1.Setup(a => a.GetHistoricalCandlesAsync(It.IsAny<InstrumentMapping>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CandleResolution?>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add(SourceAdapterType.Tsetmc))
            .ReturnsAsync(DataResult<IReadOnlyList<Candle>>.Failure(new DataCollectionError2 { Code = "SOURCE_UNAVAILABLE", Message = "Down", Retryable = true }));

        var t2 = new Mock<IDataSourceAdapter>();
        t2.Setup(a => a.SourceType).Returns(SourceAdapterType.BrsApi);
        t2.Setup(a => a.Name).Returns("BRSAPI");
        t2.Setup(a => a.IsEnabled).Returns(true);
        t2.Setup(a => a.SupportedCapabilities).Returns(new[] { MarketDataType.HistoricalCandles });
        t2.Setup(a => a.Supports(It.IsAny<InstrumentMapping>())).Returns(true);
        t2.Setup(a => a.GetHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdapterHealthStatus { IsHealthy = true });
        t2.Setup(a => a.GetHistoricalCandlesAsync(It.IsAny<InstrumentMapping>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CandleResolution?>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add(SourceAdapterType.BrsApi))
            .ReturnsAsync(DataResult<IReadOnlyList<Candle>>.Success(new List<Candle> { new() { Date = DateOnly.FromDateTime(DateTime.Today), Open = 100, High = 110, Low = 90, Close = 105, Volume = 1000 } }.AsReadOnly()));

        var reg = new DataSourceRegistry([t2.Object, t1.Object], _loggerMock.Object);
        var result = await reg.FetchHistoricalCandlesAsync(
            Foolad,
            DateOnly.FromDateTime(DateTime.Today.AddDays(-30)),
            DateOnly.FromDateTime(DateTime.Today),
            preferredSource: SourceAdapterType.Tsetmc,
            selectionMode: SourceSelectionMode.PreferredThenFallback);

        Assert.True(result.Ok);
        Assert.Equal(SourceAdapterType.Tsetmc, order[0]);
        Assert.Equal(SourceAdapterType.BrsApi, order[1]);
    }

    [Fact]
    public async Task FetchCandles_NoCompatibleSource()
    {
        var tgju = MakeAdapter(SourceAdapterType.Tgju, "TGJU", true, [MarketDataType.FreeMarketFxRate], true);
        var reg = new DataSourceRegistry([tgju], _loggerMock.Object);
        var result = await reg.FetchHistoricalCandlesAsync(
            Foolad, DateOnly.FromDateTime(DateTime.Today.AddDays(-30)), DateOnly.FromDateTime(DateTime.Today));

        Assert.False(result.Ok);
        Assert.Equal("NO_COMPATIBLE_SOURCE", result.Error!.Code);
    }

    [Fact]
    public void GetAllAdapters_ReturnsAll()
    {
        var a = MakeAdapter(SourceAdapterType.Tsetmc, "TSETMC", true, [MarketDataType.HistoricalCandles], true);
        var b = MakeAdapter(SourceAdapterType.Tgju, "TGJU", true, [MarketDataType.FreeMarketFxRate], true);
        var reg = new DataSourceRegistry([a, b], _loggerMock.Object);
        Assert.Equal(2, reg.GetAllAdapters().Count);
    }

    [Fact]
    public void GetBestAdapter_ReturnsNonNullable()
    {
        var a = MakeAdapter(SourceAdapterType.Tsetmc, "TSETMC", true, [MarketDataType.HistoricalCandles], true);
        var reg = new DataSourceRegistry([a], _loggerMock.Object);
        Assert.NotNull(reg.GetBestAdapter(Foolad));
    }
}

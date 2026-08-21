using Microsoft.AspNetCore.Mvc;
using Moq;
using StrategyForge.Api.Contracts;
using StrategyForge.Api.Controllers;
using StrategyForge.Api.Services;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Interfaces.Providers;
using StrategyForge.Domain.Models;
using Xunit;

namespace StrategyForge.Api.Tests.Controllers;

public class MarketDataControllerTests
{
    private readonly Mock<IInstrumentResolver> _resolverMock;
    private readonly Mock<IDataSourceRegistry> _registryMock;
    private readonly MarketDataController _controller;

    public MarketDataControllerTests()
    {
        _resolverMock = new Mock<IInstrumentResolver>();
        _registryMock = new Mock<IDataSourceRegistry>();
        var service = new MarketDataService(_resolverMock.Object, _registryMock.Object);
        _controller = new MarketDataController(service);
    }

    [Fact]
    public async Task GetCandles_NullInstrument_ReturnsBadRequest()
    {
        var result = await _controller.GetCandles(null, null, null, null, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetCandles_EmptyInstrument_ReturnsBadRequest()
    {
        var result = await _controller.GetCandles("", null, null, null, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetCandles_FromAfterTo_ReturnsBadRequest()
    {
        var result = await _controller.GetCandles(
            "فولاد",
            new DateOnly(2025, 12, 31),
            new DateOnly(2025, 1, 1),
            null,
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetCandles_InstrumentNotFound_ReturnsNotFound()
    {
        _resolverMock.Setup(r => r.ResolveAsync("unknown", It.IsAny<CancellationToken>()))
            .ReturnsAsync((InstrumentMapping?)null);

        var result = await _controller.GetCandles("unknown", null, null, null, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetCandles_ValidRequest_ReturnsOk()
    {
        var instrument = CreateFoolad();
        _resolverMock.Setup(r => r.ResolveAsync("فولاد", It.IsAny<CancellationToken>()))
            .ReturnsAsync(instrument);
        _registryMock.Setup(r => r.FetchHistoricalCandlesAsync(
                instrument,
                It.IsAny<DateOnly>(),
                It.IsAny<DateOnly>(),
                It.IsAny<SourceAdapterType?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(DataResult<IReadOnlyList<Candle>>.Success(
                new List<Candle>
                {
                    new() { Date = new DateOnly(2024, 1, 1), Open = 100, High = 110, Low = 90, Close = 105, Volume = 1000 }
                }.AsReadOnly()));

        var result = await _controller.GetCandles("فولاد", new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 31), null, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetSnapshot_NullInstrument_ReturnsBadRequest()
    {
        var result = await _controller.GetSnapshot(null, null, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetSnapshot_InstrumentNotFound_ReturnsNotFound()
    {
        _resolverMock.Setup(r => r.ResolveAsync("unknown", It.IsAny<CancellationToken>()))
            .ReturnsAsync((InstrumentMapping?)null);

        var result = await _controller.GetSnapshot("unknown", null, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    private static InstrumentMapping CreateFoolad() => new()
    {
        InstrumentId = "iran-equity-foolad-4439113430858354",
        Symbol = "فولاد",
        LatinSymbol = "Foolad",
        DisplayName = "Foolad Mobarakeh Steel",
        AssetClass = AssetType.Stock,
        Exchange = "TSE",
        QuoteCurrency = "IRR",
        SourceIdentifiers = new Dictionary<SourceAdapterType, SourceIdentifier>
        {
            [SourceAdapterType.Tsetmc] = new() { Id = "4439113430858354", SourceSymbol = "فولاد" }
        }
    };
}

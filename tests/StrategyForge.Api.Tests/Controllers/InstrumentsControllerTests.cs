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

public class InstrumentsControllerTests
{
    private readonly Mock<IInstrumentResolver> _resolverMock;
    private readonly InstrumentsController _controller;

    public InstrumentsControllerTests()
    {
        _resolverMock = new Mock<IInstrumentResolver>();
        var service = new InstrumentService(_resolverMock.Object);
        _controller = new InstrumentsController(service);
    }

    // --- Resolve ---

    [Fact]
    public async Task Resolve_ValidQuery_ReturnsOk()
    {
        _resolverMock.Setup(r => r.ResolveAsync("فولاد", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateFoolad());

        var result = await _controller.Resolve("فولاد", CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var ok = (OkObjectResult)result;
        Assert.IsType<InstrumentResponse>(ok.Value);
    }

    [Fact]
    public async Task Resolve_NullQuery_ReturnsBadRequest()
    {
        var result = await _controller.Resolve(null, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Resolve_EmptyQuery_ReturnsBadRequest()
    {
        var result = await _controller.Resolve("", CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Resolve_NotFound_ReturnsNotFound()
    {
        _resolverMock.Setup(r => r.ResolveAsync("unknown", It.IsAny<CancellationToken>()))
            .ReturnsAsync((InstrumentMapping?)null);

        var result = await _controller.Resolve("unknown", CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    // --- GetById ---

    [Fact]
    public async Task GetById_ValidId_ReturnsOk()
    {
        _resolverMock.Setup(r => r.ResolveAsync("iran-equity-foolad-4439113430858354", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateFoolad());

        var result = await _controller.GetById("iran-equity-foolad-4439113430858354", CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetById_NotFound_ReturnsNotFound()
    {
        _resolverMock.Setup(r => r.ResolveAsync("missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((InstrumentMapping?)null);

        var result = await _controller.GetById("missing", CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    // --- Search ---

    [Fact]
    public async Task Search_ValidQuery_ReturnsOk()
    {
        _resolverMock.Setup(r => r.SearchAsync("Fool", 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InstrumentMapping> { CreateFoolad() }.AsReadOnly());

        var result = await _controller.Search("Fool", 10, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Search_EmptyQuery_ReturnsEmptyArray()
    {
        var result = await _controller.Search("", 10, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Empty((InstrumentResponse[])ok.Value!);
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

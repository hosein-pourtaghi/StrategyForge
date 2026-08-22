using System.Text;
using System.Net;
using Moq;
using StrategyForge.Domain.Configuration;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Models;
using StrategyForge.Infrastructure.Authentication;
using Xunit;

namespace StrategyForge.Infrastructure.Tests.Adapters;

public class TseWebGatewayAdapterTests
{
    [Fact]
    public async Task GetLatestCandle_OrderBookAndInfo_ParsesCorrectly()
    {
        var orderBookJson = TestInfrastructure.CreateTseWebGatewayOrderBookJson();
        var infoJson = TestInfrastructure.CreateTseWebGatewayInstrumentInfoJson();
        var callCount = 0;

        var handler = new MockHttpMessageHandler((req, ct) =>
        {
            callCount++;
            var json = req.RequestUri!.PathAndQuery.Contains("BestLimits")
                ? orderBookJson
                : infoJson;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"))
            });
        });

        var adapter = TestInfrastructure.CreateTseWebGatewayAdapter(handler);
        var instrument = TestInfrastructure.CreateFooladInstrument();

        var result = await adapter.GetLatestCandleAsync(instrument, CancellationToken.None);

        Assert.True(result.Ok);
        Assert.NotNull(result.Data);
        // Best bid from order book: pd=4497 (level 1 buy)
        // Best ask from order book: po=4499 (level 1 sell)
        Assert.Equal(4497m, result.Data.BidPrice);
        Assert.Equal(4499m, result.Data.AskPrice);
        // Mid price: (4497 + 4499) / 2 = 4498
        Assert.Equal(4498m, result.Data.Close);
        Assert.Equal(2, callCount); // Both endpoints called
    }

    [Fact]
    public async Task GetLatestCandle_ProvenanceSource_IsTseWebGateway()
    {
        var orderBookJson = TestInfrastructure.CreateTseWebGatewayOrderBookJson();
        var infoJson = TestInfrastructure.CreateTseWebGatewayInstrumentInfoJson();

        var handler = new MockHttpMessageHandler((req, ct) =>
        {
            var json = req.RequestUri!.PathAndQuery.Contains("BestLimits")
                ? orderBookJson
                : infoJson;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"))
            });
        });

        var adapter = TestInfrastructure.CreateTseWebGatewayAdapter(handler);
        var instrument = TestInfrastructure.CreateFooladInstrument();

        var result = await adapter.GetLatestCandleAsync(instrument, CancellationToken.None);

        Assert.True(result.Ok);
        Assert.Equal(SourceAdapterType.TseWebGateway, result.Data!.Provenance!.Source);
        Assert.Equal("4439113430858354", result.Data.Provenance.SourceInstrumentId);
    }

    [Fact]
    public async Task GetLatestCandle_InstrumentInfoExtracted()
    {
        var orderBookJson = TestInfrastructure.CreateTseWebGatewayOrderBookJson();
        var infoJson = TestInfrastructure.CreateTseWebGatewayInstrumentInfoJson();

        var handler = new MockHttpMessageHandler((req, ct) =>
        {
            var json = req.RequestUri!.PathAndQuery.Contains("BestLimits")
                ? orderBookJson
                : infoJson;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"))
            });
        });

        var adapter = TestInfrastructure.CreateTseWebGatewayAdapter(handler);
        var instrument = TestInfrastructure.CreateFooladInstrument();

        var result = await adapter.GetLatestCandleAsync(instrument, CancellationToken.None);

        Assert.True(result.Ok);
        Assert.Equal("IRO1FOLD0001", result.Data!.ExtraFields!["isin"]);
        Assert.Equal(" metals", result.Data.ExtraFields["sector"]);
    }

    [Fact]
    public async Task GetLatestCandle_InstrumentNotInSource_ReturnsFailure()
    {
        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));

        var adapter = TestInfrastructure.CreateTseWebGatewayAdapter(handler);
        var instrument = new InstrumentMapping
        {
            InstrumentId = "test",
            Symbol = "Test",
            DisplayName = "Test",
            AssetClass = AssetType.Stock,
            Exchange = "TSE",
            QuoteCurrency = "IRR",
            SourceIdentifiers = new Dictionary<SourceAdapterType, SourceIdentifier>()
        };

        var result = await adapter.GetLatestCandleAsync(instrument, CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Equal("INSTRUMENT_NOT_FOUND", result.Error?.Code);
    }

    [Fact]
    public async Task GetLatestCandle_CacheHit_UsesCachedData()
    {
        var orderBookJson = TestInfrastructure.CreateTseWebGatewayOrderBookJson();
        var infoJson = TestInfrastructure.CreateTseWebGatewayInstrumentInfoJson();
        var callCount = 0;

        var handler = new MockHttpMessageHandler((req, ct) =>
        {
            callCount++;
            var json = req.RequestUri!.PathAndQuery.Contains("BestLimits")
                ? orderBookJson
                : infoJson;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"))
            });
        });

        var adapter = TestInfrastructure.CreateTseWebGatewayAdapter(handler);
        var instrument = TestInfrastructure.CreateFooladInstrument();

        var result1 = await adapter.GetLatestCandleAsync(instrument, CancellationToken.None);
        Assert.True(result1.Ok);

        var result2 = await adapter.GetLatestCandleAsync(instrument, CancellationToken.None);
        Assert.True(result2.Ok);
        Assert.True(result2.Freshness!.IsCached);
        Assert.Equal(2, callCount); // 2 endpoints on first call, 0 on second (cached)
    }

    [Fact]
    public void Supports_StockInstrument_ReturnsTrue()
    {
        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var adapter = TestInfrastructure.CreateTseWebGatewayAdapter(handler);
        var instrument = TestInfrastructure.CreateFooladInstrument();

        Assert.True(adapter.Supports(instrument));
    }

    [Fact]
    public void Supports_CurrencyInstrument_ReturnsFalse()
    {
        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var adapter = TestInfrastructure.CreateTseWebGatewayAdapter(handler);
        var instrument = TestInfrastructure.CreateUsdIrrInstrument();

        Assert.False(adapter.Supports(instrument));
    }

    [Fact]
    public async Task GetHistoricalCandles_AlwaysReturnsEmpty()
    {
        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"))
            }));

        var adapter = TestInfrastructure.CreateTseWebGatewayAdapter(handler);
        var instrument = TestInfrastructure.CreateFooladInstrument();

        var result = await adapter.GetHistoricalCandlesAsync(
            instrument,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            CancellationToken.None);

        Assert.True(result.Ok);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public void SourceType_ReturnsTseWebGateway()
    {
        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var adapter = TestInfrastructure.CreateTseWebGatewayAdapter(handler);

        Assert.Equal(SourceAdapterType.TseWebGateway, adapter.SourceType);
    }

    [Fact]
    public async Task GetLatestCandle_ProvenanceNotLeaked()
    {
        var orderBookJson = TestInfrastructure.CreateTseWebGatewayOrderBookJson();
        var infoJson = TestInfrastructure.CreateTseWebGatewayInstrumentInfoJson();

        var handler = new MockHttpMessageHandler((req, ct) =>
        {
            var json = req.RequestUri!.PathAndQuery.Contains("BestLimits")
                ? orderBookJson
                : infoJson;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"))
            });
        });

        var adapter = TestInfrastructure.CreateTseWebGatewayAdapter(handler);
        var instrument = TestInfrastructure.CreateFooladInstrument();

        var result = await adapter.GetLatestCandleAsync(instrument, CancellationToken.None);

        Assert.True(result.Ok);
        var provenanceStr = System.Text.Json.JsonSerializer.Serialize(result.Data!.Provenance);
        Assert.DoesNotContain("password", provenanceStr, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("apikey", provenanceStr, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetLatestCandle_FreshnessAndQuality_Present()
    {
        var orderBookJson = TestInfrastructure.CreateTseWebGatewayOrderBookJson();
        var infoJson = TestInfrastructure.CreateTseWebGatewayInstrumentInfoJson();

        var handler = new MockHttpMessageHandler((req, ct) =>
        {
            var json = req.RequestUri!.PathAndQuery.Contains("BestLimits")
                ? orderBookJson
                : infoJson;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"))
            });
        });

        var adapter = TestInfrastructure.CreateTseWebGatewayAdapter(handler);
        var instrument = TestInfrastructure.CreateFooladInstrument();

        var result = await adapter.GetLatestCandleAsync(instrument, CancellationToken.None);

        Assert.True(result.Ok);
        Assert.NotNull(result.Freshness);
        Assert.NotNull(result.Quality);
    }
}

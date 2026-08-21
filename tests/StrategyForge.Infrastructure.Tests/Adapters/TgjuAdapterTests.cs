using System.Text;
using System.Net;
using Moq;
using StrategyForge.Domain.Configuration;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Models;
using StrategyForge.Infrastructure.Authentication;
using Xunit;

namespace StrategyForge.Infrastructure.Tests.Adapters;

public class TgjuAdapterTests
{
    [Fact]
    public async Task GetLatestCandle_ValidResponse_ParsesRateCorrectly()
    {
        var json = TestInfrastructure.CreateTgjuLatestJson(585000m, "price_dollar_rl");
        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"))
            }));

        var adapter = TestInfrastructure.CreateTgjuAdapter(handler);
        var instrument = TestInfrastructure.CreateUsdIrrInstrument();

        var result = await adapter.GetLatestCandleAsync(instrument, CancellationToken.None);

        Assert.True(result.Ok);
        Assert.NotNull(result.Data);
        Assert.Equal(585000m, result.Data.Close);
        Assert.Equal(585000m, result.Data.Open);
        Assert.Equal("Asia/Tehran", result.Data.MarketTimezone);
    }

    [Fact]
    public async Task GetLatestCandle_FreeMarketRateType_InExtraFields()
    {
        var json = TestInfrastructure.CreateTgjuLatestJson(585000m, "price_dollar_rl");
        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"))
            }));

        var adapter = TestInfrastructure.CreateTgjuAdapter(handler);
        var instrument = TestInfrastructure.CreateUsdIrrInstrument();

        var result = await adapter.GetLatestCandleAsync(instrument, CancellationToken.None);

        Assert.True(result.Ok);
        Assert.Equal("free_market", result.Data!.ExtraFields["rateType"]);
    }

    [Fact]
    public async Task GetLatestCandle_ProvenanceSource_IsTgju()
    {
        var json = TestInfrastructure.CreateTgjuLatestJson(585000m, "price_dollar_rl");
        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"))
            }));

        var adapter = TestInfrastructure.CreateTgjuAdapter(handler);
        var instrument = TestInfrastructure.CreateUsdIrrInstrument();

        var result = await adapter.GetLatestCandleAsync(instrument, CancellationToken.None);

        Assert.True(result.Ok);
        Assert.NotNull(result.Data!.Provenance);
        Assert.Equal(SourceAdapterType.Tgju, result.Data.Provenance.Source);
    }

    [Fact]
    public async Task GetLatestCandle_InvalidResponse_ReturnsFailure()
    {
        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            }));

        var adapter = TestInfrastructure.CreateTgjuAdapter(handler);
        var instrument = TestInfrastructure.CreateUsdIrrInstrument();

        var result = await adapter.GetLatestCandleAsync(instrument, CancellationToken.None);

        // Invalid response should fail with DATA_VALIDATION_FAILED
        Assert.False(result.Ok);
        Assert.Equal("DATA_VALIDATION_FAILED", result.Error?.Code);
    }

    [Fact]
    public async Task GetLatestCandle_InstrumentWithoutTgjuId_ReturnsNotFound()
    {
        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        var adapter = TestInfrastructure.CreateTgjuAdapter(handler);
        var instrument = new InstrumentMapping
        {
            InstrumentId = "test",
            Symbol = "Test",
            LatinSymbol = "Test",
            DisplayName = "Test",
            AssetClass = AssetType.Currency,
            Exchange = "OTC",
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
        var json = TestInfrastructure.CreateTgjuLatestJson(585000m, "price_dollar_rl");
        var callCount = 0;
        var handler = new MockHttpMessageHandler((req, ct) =>
        {
            callCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"))
            });
        });

        var adapter = TestInfrastructure.CreateTgjuAdapter(handler);
        var instrument = TestInfrastructure.CreateUsdIrrInstrument();

        var result1 = await adapter.GetLatestCandleAsync(instrument, CancellationToken.None);
        Assert.True(result1.Ok);

        var result2 = await adapter.GetLatestCandleAsync(instrument, CancellationToken.None);
        Assert.True(result2.Ok);
        Assert.True(result2.Freshness!.IsCached);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void Supports_CurrencyInstrument_ReturnsTrue()
    {
        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var adapter = TestInfrastructure.CreateTgjuAdapter(handler);
        var instrument = TestInfrastructure.CreateUsdIrrInstrument();

        Assert.True(adapter.Supports(instrument));
    }

    [Fact]
    public void Supports_StockInstrument_ReturnsFalse()
    {
        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var adapter = TestInfrastructure.CreateTgjuAdapter(handler);
        var instrument = TestInfrastructure.CreateFooladInstrument();

        Assert.False(adapter.Supports(instrument));
    }

    [Fact]
    public async Task GetLatestCandle_FreshnessAndQuality_Present()
    {
        var json = TestInfrastructure.CreateTgjuLatestJson(585000m, "price_dollar_rl");
        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"))
            }));

        var adapter = TestInfrastructure.CreateTgjuAdapter(handler);
        var instrument = TestInfrastructure.CreateUsdIrrInstrument();

        var result = await adapter.GetLatestCandleAsync(instrument, CancellationToken.None);

        Assert.True(result.Ok);
        Assert.NotNull(result.Freshness);
        Assert.NotNull(result.Quality);
        Assert.True(result.Quality.Score > 0);
    }

    [Fact]
    public void SourceType_ReturnsTgju()
    {
        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var adapter = TestInfrastructure.CreateTgjuAdapter(handler);

        Assert.Equal(SourceAdapterType.Tgju, adapter.SourceType);
    }
}

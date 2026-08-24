using System.Text;
using System.Net;
using Moq;
using StrategyForge.Domain.Configuration;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Models;
using StrategyForge.Infrastructure.Authentication;
using Xunit;

namespace StrategyForge.Infrastructure.Tests.Adapters;

public class CbiAdapterTests
{
    [Fact]
    public async Task GetLatestCandle_ValidResponse_ParsesOfficialRate()
    {
        var json = TestInfrastructure.CreateCbiRateJson("USD", 42000m, 41900m, 42100m);
        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"))
            }));

        var adapter = TestInfrastructure.CreateCbiAdapter(handler);
        var instrument = TestInfrastructure.CreateUsdIrrInstrument();

        var result = await adapter.GetLatestCandleAsync(instrument, CancellationToken.None);

        Assert.True(result.Ok);
        Assert.NotNull(result.Data);
        Assert.Equal(42000m, result.Data.Close);
        Assert.Equal(42000m, result.Data.Open);
        Assert.Equal(42100m, result.Data.High); // sellRate
        Assert.Equal(41900m, result.Data.Low); // buyRate
    }

    [Fact]
    public async Task GetLatestCandle_OfficialRateType_InExtraFields()
    {
        var json = TestInfrastructure.CreateCbiRateJson("USD", 42000m, null, null);
        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"))
            }));

        var adapter = TestInfrastructure.CreateCbiAdapter(handler);
        var instrument = TestInfrastructure.CreateUsdIrrInstrument();

        var result = await adapter.GetLatestCandleAsync(instrument, CancellationToken.None);

        Assert.True(result.Ok);
        Assert.Equal("official", result.Data!.ExtraFields["rateType"]);
        Assert.Equal("cbi", result.Data.ExtraFields["source"]);
    }

    [Fact]
    public async Task GetLatestCandle_ProvenanceSource_IsCbi()
    {
        var json = TestInfrastructure.CreateCbiRateJson("USD", 42000m, null, null);
        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"))
            }));

        var adapter = TestInfrastructure.CreateCbiAdapter(handler);
        var instrument = TestInfrastructure.CreateUsdIrrInstrument();

        var result = await adapter.GetLatestCandleAsync(instrument, CancellationToken.None);

        Assert.True(result.Ok);
        Assert.NotNull(result.Data!.Provenance);
        Assert.Equal(SourceAdapterType.Cbi, result.Data.Provenance.Source);
    }

    [Fact]
    public async Task GetLatestCandle_CurrencyNotFound_ReturnsFailure()
    {
        var json = TestInfrastructure.CreateCbiRateJson("EUR", 48000m, null, null);
        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"))
            }));

        var adapter = TestInfrastructure.CreateCbiAdapter(handler);
        var instrument = TestInfrastructure.CreateUsdIrrInstrument(); // Looking for USD, but only EUR available

        var result = await adapter.GetLatestCandleAsync(instrument, CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Equal("DATA_VALIDATION_FAILED", result.Error?.Code);
    }

    [Fact]
    public async Task GetLatestCandle_EmptyResponse_ReturnsFailure()
    {
        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", Encoding.UTF8, "application/json")
            }));

        var adapter = TestInfrastructure.CreateCbiAdapter(handler);
        var instrument = TestInfrastructure.CreateUsdIrrInstrument();

        var result = await adapter.GetLatestCandleAsync(instrument, CancellationToken.None);

        Assert.False(result.Ok);
    }

    [Fact]
    public async Task GetHistoricalCandles_AlwaysReturnsEmpty()
    {
        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", Encoding.UTF8, "application/json")
            }));

        var adapter = TestInfrastructure.CreateCbiAdapter(handler);
        var instrument = TestInfrastructure.CreateUsdIrrInstrument();

        var result = await adapter.GetHistoricalCandlesAsync(
            instrument,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            null, CancellationToken.None);

        Assert.True(result.Ok);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task GetLatestCandle_CacheHit_UsesCachedData()
    {
        var json = TestInfrastructure.CreateCbiRateJson("USD", 42000m, null, null);
        var callCount = 0;
        var handler = new MockHttpMessageHandler((req, ct) =>
        {
            callCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"))
            });
        });

        var adapter = TestInfrastructure.CreateCbiAdapter(handler);
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
        var adapter = TestInfrastructure.CreateCbiAdapter(handler);
        var instrument = TestInfrastructure.CreateUsdIrrInstrument();

        Assert.True(adapter.Supports(instrument));
    }

    [Fact]
    public void Supports_StockInstrument_ReturnsFalse()
    {
        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var adapter = TestInfrastructure.CreateCbiAdapter(handler);
        var instrument = TestInfrastructure.CreateFooladInstrument();

        Assert.False(adapter.Supports(instrument));
    }

    [Fact]
    public void SourceType_ReturnsCbi()
    {
        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var adapter = TestInfrastructure.CreateCbiAdapter(handler);

        Assert.Equal(SourceAdapterType.Cbi, adapter.SourceType);
    }
}

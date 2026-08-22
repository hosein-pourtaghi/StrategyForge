using System.Text;
using System.Net;
using Moq;
using StrategyForge.Domain.Configuration;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Models;
using StrategyForge.Infrastructure.Authentication;
using Xunit;

namespace StrategyForge.Infrastructure.Tests.Adapters;

public class BrsApiAdapterTests
{
    [Fact]
    public async Task GetLatestCandle_ValidResponse_ParsesCorrectly()
    {
        var json = TestInfrastructure.CreateBrsApiAllSymbolsJson(
            "4439113430858354", "فولاد", 4500m, 4499m, 4529m, 4470m, 4594m, 174743191L, 5953L);

        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"))
            }));

        var adapter = TestInfrastructure.CreateBrsApiAdapter(handler);
        var instrument = TestInfrastructure.CreateBrsApiFooladInstrument();

        var result = await adapter.GetLatestCandleAsync(instrument, CancellationToken.None);

        Assert.True(result.Ok);
        Assert.NotNull(result.Data);
        Assert.Equal(4529m, result.Data.Close); // pc (closing price)
        Assert.Equal(4500m, result.Data.Open);  // pf (first price)
        Assert.Equal(4594m, result.Data.High);  // pmax
        Assert.Equal(4470m, result.Data.Low);   // pmin
        Assert.Equal(4499m, result.Data.LastPrice); // pl (last price)
    }

    [Fact]
    public async Task GetLatestCandle_ProvenanceSource_IsBrsApi()
    {
        var json = TestInfrastructure.CreateBrsApiAllSymbolsJson(
            "4439113430858354", "فولاد", 4500m, 4499m, 4529m, 4470m, 4594m, 100000L, 1000L);

        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"))
            }));

        var adapter = TestInfrastructure.CreateBrsApiAdapter(handler);
        var instrument = TestInfrastructure.CreateBrsApiFooladInstrument();

        var result = await adapter.GetLatestCandleAsync(instrument, CancellationToken.None);

        Assert.True(result.Ok);
        Assert.Equal(SourceAdapterType.BrsApi, result.Data!.Provenance!.Source);
        Assert.Equal("4439113430858354", result.Data.Provenance.SourceInstrumentId);
    }

    [Fact]
    public async Task GetLatestCandle_AuthenticationRequired_FailsWithoutCredentials()
    {
        var json = TestInfrastructure.CreateBrsApiAllSymbolsJson(
            "4439113430858354", "فولاد", 4500m, 4499m, 4529m, 4470m, 4594m, 100000L, 1000L);

        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"))
            }));

        // Use a failing authenticator
        var failingAuth = new Mock<IDataSourceAuthenticator>();
        failingAuth.Setup(a => a.AuthenticateAsync(
                It.IsAny<HttpRequestMessage>(),
                It.IsAny<AuthenticationSettings>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthenticationResult.Failed(
                AuthenticationMode.ApiKey, "AUTHENTICATION_REQUIRED",
                "API key not configured"));

        var adapter = TestInfrastructure.CreateBrsApiAdapter(handler, authenticator: failingAuth.Object);
        var instrument = TestInfrastructure.CreateBrsApiFooladInstrument();

        var result = await adapter.GetLatestCandleAsync(instrument, CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Equal("AUTHENTICATION_REQUIRED", result.Error?.Code);
    }

    [Fact]
    public async Task GetHistoricalCandles_AlwaysReturnsEmpty()
    {
        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"))
            }));

        var adapter = TestInfrastructure.CreateBrsApiAdapter(handler);
        var instrument = TestInfrastructure.CreateBrsApiFooladInstrument();

        var result = await adapter.GetHistoricalCandlesAsync(
            instrument,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            CancellationToken.None);

        Assert.True(result.Ok);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task GetLatestCandle_InstrumentNotInResponse_ReturnsFailure()
    {
        var json = TestInfrastructure.CreateBrsApiAllSymbolsJson(
            "99999", "OTHER", 100m, 110m, 105m, 95m, 115m, 500000L, 500L);

        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"))
            }));

        var adapter = TestInfrastructure.CreateBrsApiAdapter(handler);
        var instrument = TestInfrastructure.CreateBrsApiFooladInstrument();

        var result = await adapter.GetLatestCandleAsync(instrument, CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Equal("DATA_VALIDATION_FAILED", result.Error?.Code);
    }

    [Fact]
    public async Task GetLatestCandle_CacheHit_UsesCachedData()
    {
        var json = TestInfrastructure.CreateBrsApiAllSymbolsJson(
            "4439113430858354", "فولاد", 4500m, 4499m, 4529m, 4470m, 4594m, 100000L, 1000L);
        var callCount = 0;
        var handler = new MockHttpMessageHandler((req, ct) =>
        {
            callCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"))
            });
        });

        var adapter = TestInfrastructure.CreateBrsApiAdapter(handler);
        var instrument = TestInfrastructure.CreateBrsApiFooladInstrument();

        var result1 = await adapter.GetLatestCandleAsync(instrument, CancellationToken.None);
        Assert.True(result1.Ok);

        var result2 = await adapter.GetLatestCandleAsync(instrument, CancellationToken.None);
        Assert.True(result2.Ok);
        Assert.True(result2.Freshness!.IsCached);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void Supports_StockInstrument_ReturnsTrue()
    {
        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var adapter = TestInfrastructure.CreateBrsApiAdapter(handler);
        var instrument = TestInfrastructure.CreateBrsApiFooladInstrument();

        Assert.True(adapter.Supports(instrument));
    }

    [Fact]
    public void Supports_CryptoInstrument_ReturnsFalse()
    {
        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var adapter = TestInfrastructure.CreateBrsApiAdapter(handler);
        var instrument = TestInfrastructure.CreateUsdtIrrInstrument();

        Assert.False(adapter.Supports(instrument));
    }

    [Fact]
    public void SourceType_ReturnsBrsApi()
    {
        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var adapter = TestInfrastructure.CreateBrsApiAdapter(handler);

        Assert.Equal(SourceAdapterType.BrsApi, adapter.SourceType);
    }

    [Fact]
    public async Task GetLatestCandle_ProvenanceNotLeaked()
    {
        var json = TestInfrastructure.CreateBrsApiAllSymbolsJson(
            "4439113430858354", "فولاد", 4500m, 4499m, 4529m, 4470m, 4594m, 100000L, 1000L);
        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"))
            }));

        var adapter = TestInfrastructure.CreateBrsApiAdapter(handler);
        var instrument = TestInfrastructure.CreateBrsApiFooladInstrument();

        var result = await adapter.GetLatestCandleAsync(instrument, CancellationToken.None);

        Assert.True(result.Ok);
        var provenanceStr = System.Text.Json.JsonSerializer.Serialize(result.Data!.Provenance);
        Assert.DoesNotContain("password", provenanceStr, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("apikey", provenanceStr, StringComparison.OrdinalIgnoreCase);
    }
}

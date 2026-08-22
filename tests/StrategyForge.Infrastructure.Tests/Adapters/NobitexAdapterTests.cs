using System.Text;
using System.Net;
using StrategyForge.Domain.Configuration;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Models;
using StrategyForge.Infrastructure.Authentication;
using Xunit;

namespace StrategyForge.Infrastructure.Tests.Adapters;

public class NobitexAdapterTests
{
    [Fact]
    public async Task GetHistoricalCandles_OhlcResponse_ParsesCandlesCorrectly()
    {
        // Use a timestamp guaranteed to be within a wide date range
        // 2026-08-15 ~ 1786771200
        var timestamp = DateTimeOffset.UtcNow.AddDays(-5).ToUnixTimeSeconds();
        var timestamps = new long[] { timestamp };
        var json = TestInfrastructure.CreateNobitexOhlcJson(
            timestamps,
            [185000m], [190000m], [182000m], [188000m], [500.5m]);

        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"))
            }));

        var adapter = TestInfrastructure.CreateNobitexAdapter(handler);
        var instrument = TestInfrastructure.CreateUsdtIrrInstrument();

        var result = await adapter.GetHistoricalCandlesAsync(
            instrument,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            CancellationToken.None);

        Assert.True(result.Ok);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data);

        var candle = result.Data[0];
        Assert.Equal(185000m, candle.Open);
        Assert.Equal(190000m, candle.High);
        Assert.Equal(182000m, candle.Low);
        Assert.Equal(188000m, candle.Close);
        Assert.Equal(500L, candle.Volume);
        Assert.Equal("Asia/Tehran", candle.MarketTimezone);
    }

    [Fact]
    public async Task GetHistoricalCandles_ProvenanceSource_IsNobitex()
    {
        var timestamp = DateTimeOffset.UtcNow.AddDays(-5).ToUnixTimeSeconds();
        var timestamps = new long[] { timestamp };
        var json = TestInfrastructure.CreateNobitexOhlcJson(
            timestamps, [185000m], [190000m], [182000m], [188000m], [500m]);

        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"))
            }));

        var adapter = TestInfrastructure.CreateNobitexAdapter(handler);
        var instrument = TestInfrastructure.CreateUsdtIrrInstrument();

        var result = await adapter.GetHistoricalCandlesAsync(
            instrument,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            CancellationToken.None);

        Assert.True(result.Ok);
        var candle = result.Data![0];
        Assert.NotNull(candle.Provenance);
        Assert.Equal(SourceAdapterType.Nobitex, candle.Provenance.Source);
        Assert.Equal("USDTIRT", candle.Provenance.SourceSymbol);
        Assert.Equal("market/udf/history", candle.Provenance.Endpoint);
    }

    [Fact]
    public async Task GetHistoricalCandles_ErrorStatus_ReturnsEmpty()
    {
        var json = "{ \"s\": \"error\", \"errmsg\": \"Invalid resolution!\" }";
        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"))
            }));

        var adapter = TestInfrastructure.CreateNobitexAdapter(handler);
        var instrument = TestInfrastructure.CreateUsdtIrrInstrument();

        var result = await adapter.GetHistoricalCandlesAsync(
            instrument,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            CancellationToken.None);

        Assert.True(result.Ok); // Adapter returns empty list on error status
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task GetLatestCandle_StatsResponse_ParsesCorrectly()
    {
        var json = TestInfrastructure.CreateNobitexStatsJson(
            "usdt-rls", 188125m, 187000m, 190000m, 185000m, 188200m, 188050m);

        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"))
            }));

        var adapter = TestInfrastructure.CreateNobitexAdapter(handler);
        var instrument = TestInfrastructure.CreateUsdtIrrInstrument();

        var result = await adapter.GetLatestCandleAsync(instrument, CancellationToken.None);

        Assert.True(result.Ok);
        Assert.NotNull(result.Data);
        Assert.Equal(188125m, result.Data.Close);
        Assert.Equal(187000m, result.Data.Open);
        Assert.Equal(190000m, result.Data.High);
        Assert.Equal(185000m, result.Data.Low);
        Assert.Equal(188050m, result.Data.BidPrice);
        Assert.Equal(188200m, result.Data.AskPrice);
    }

    [Fact]
    public async Task GetLatestCandle_ProvenanceSource_IsNobitex()
    {
        var json = TestInfrastructure.CreateNobitexStatsJson(
            "usdt-rls", 188125m, null, null, null, null, null);

        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"))
            }));

        var adapter = TestInfrastructure.CreateNobitexAdapter(handler);
        var instrument = TestInfrastructure.CreateUsdtIrrInstrument();

        var result = await adapter.GetLatestCandleAsync(instrument, CancellationToken.None);

        Assert.True(result.Ok);
        Assert.Equal(SourceAdapterType.Nobitex, result.Data!.Provenance!.Source);
        Assert.Equal("USDTIRT", result.Data.Provenance.SourceSymbol);
        Assert.Equal("market/stats", result.Data.Provenance.Endpoint);
    }

    [Fact]
    public async Task GetLatestCandle_RateType_IsExchange()
    {
        var json = TestInfrastructure.CreateNobitexStatsJson(
            "usdt-rls", 188125m, null, null, null, null, null);

        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"))
            }));

        var adapter = TestInfrastructure.CreateNobitexAdapter(handler);
        var instrument = TestInfrastructure.CreateUsdtIrrInstrument();

        var result = await adapter.GetLatestCandleAsync(instrument, CancellationToken.None);

        Assert.True(result.Ok);
        Assert.Equal("exchange", result.Data!.ExtraFields!["rateType"]);
    }

    [Fact]
    public async Task GetLatestCandle_InvalidResponse_ReturnsFailure()
    {
        var json = "{ \"status\": \"error\" }";
        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"))
            }));

        var adapter = TestInfrastructure.CreateNobitexAdapter(handler);
        var instrument = TestInfrastructure.CreateUsdtIrrInstrument();

        var result = await adapter.GetLatestCandleAsync(instrument, CancellationToken.None);

        Assert.False(result.Ok);
    }

    [Fact]
    public async Task GetLatestCandle_InstrumentWithoutNobitexId_ReturnsNotFound()
    {
        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        var adapter = TestInfrastructure.CreateNobitexAdapter(handler);
        var instrument = new InstrumentMapping
        {
            InstrumentId = "test",
            Symbol = "Test",
            DisplayName = "Test",
            AssetClass = AssetType.Crypto,
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
        var json = TestInfrastructure.CreateNobitexStatsJson(
            "usdt-rls", 188125m, null, null, null, null, null);
        var callCount = 0;
        var handler = new MockHttpMessageHandler((req, ct) =>
        {
            callCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"))
            });
        });

        var adapter = TestInfrastructure.CreateNobitexAdapter(handler);
        var instrument = TestInfrastructure.CreateUsdtIrrInstrument();

        var result1 = await adapter.GetLatestCandleAsync(instrument, CancellationToken.None);
        Assert.True(result1.Ok);

        var result2 = await adapter.GetLatestCandleAsync(instrument, CancellationToken.None);
        Assert.True(result2.Ok);
        Assert.True(result2.Freshness!.IsCached);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void Supports_CryptoInstrument_ReturnsTrue()
    {
        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var adapter = TestInfrastructure.CreateNobitexAdapter(handler);
        var instrument = TestInfrastructure.CreateUsdtIrrInstrument();

        Assert.True(adapter.Supports(instrument));
    }

    [Fact]
    public void Supports_StockInstrument_ReturnsFalse()
    {
        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var adapter = TestInfrastructure.CreateNobitexAdapter(handler);
        var instrument = TestInfrastructure.CreateFooladInstrument();

        Assert.False(adapter.Supports(instrument));
    }

    [Fact]
    public async Task GetHistoricalCandles_FreshnessAndQuality_Present()
    {
        var timestamp = DateTimeOffset.UtcNow.AddDays(-5).ToUnixTimeSeconds();
        var timestamps = new long[] { timestamp };
        var json = TestInfrastructure.CreateNobitexOhlcJson(
            timestamps, [185000m], [190000m], [182000m], [188000m], [500m]);

        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"))
            }));

        var adapter = TestInfrastructure.CreateNobitexAdapter(handler);
        var instrument = TestInfrastructure.CreateUsdtIrrInstrument();

        var result = await adapter.GetHistoricalCandlesAsync(
            instrument,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            CancellationToken.None);

        Assert.True(result.Ok);
        Assert.NotNull(result.Freshness);
        Assert.False(result.Freshness.IsCached);
        Assert.NotNull(result.Quality);
        Assert.True(result.Quality.Score > 0);
    }

    [Fact]
    public void SourceType_ReturnsNobitex()
    {
        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var adapter = TestInfrastructure.CreateNobitexAdapter(handler);

        Assert.Equal(SourceAdapterType.Nobitex, adapter.SourceType);
    }

    [Fact]
    public async Task GetLatestCandle_ProvenanceNotLeaked()
    {
        var json = TestInfrastructure.CreateNobitexStatsJson(
            "usdt-rls", 188125m, null, null, null, null, null);
        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"))
            }));

        var adapter = TestInfrastructure.CreateNobitexAdapter(handler);
        var instrument = TestInfrastructure.CreateUsdtIrrInstrument();

        var result = await adapter.GetLatestCandleAsync(instrument, CancellationToken.None);

        Assert.True(result.Ok);
        var provenanceStr = System.Text.Json.JsonSerializer.Serialize(result.Data!.Provenance);
        Assert.DoesNotContain("password", provenanceStr, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("apikey", provenanceStr, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", provenanceStr, StringComparison.OrdinalIgnoreCase);
    }
}

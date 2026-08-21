using System.Text;
using System.Net;
using Moq;
using StrategyForge.Domain.Configuration;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Models;
using StrategyForge.Infrastructure.Authentication;
using Xunit;

namespace StrategyForge.Infrastructure.Tests.Adapters;

public class TsetmcAdapterTests
{
    [Fact]
    public async Task GetHistoricalCandles_ValidResponse_ParsesCandlesCorrectly()
    {
        // Jalali date 14050530 = 2026-08-21 Gregorian
        var json = TestInfrastructure.CreateTsetmcCandleJson(14050530, 100000m, 98000m, 105000m, 97000m, 1500000L);

        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"))
            }));

        var adapter = TestInfrastructure.CreateTsetmcAdapter(handler);
        var instrument = TestInfrastructure.CreateFooladInstrument();

        var result = await adapter.GetHistoricalCandlesAsync(
            instrument,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            CancellationToken.None);

        Assert.True(result.Ok);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data);

        var candle = result.Data[0];
        Assert.Equal(100000m, candle.Close);
        Assert.Equal(98000m, candle.Open);
        Assert.Equal(105000m, candle.High);
        Assert.Equal(97000m, candle.Low);
        Assert.Equal(1500000L, candle.Volume);
        Assert.Equal("Asia/Tehran", candle.MarketTimezone);
        Assert.Equal("jalali", candle.SourceCalendar);

        // Verify provenance
        Assert.NotNull(candle.Provenance);
        Assert.Equal(SourceAdapterType.Tsetmc, candle.Provenance.Source);
        Assert.Equal("4439113430858354", candle.Provenance.SourceInstrumentId);
    }

    [Fact]
    public async Task GetHistoricalCandles_SetsUnadjustedProvenance()
    {
        var json = TestInfrastructure.CreateTsetmcCandleJson(14050530, 100000m, 98000m, 105000m, 97000m, 1500000L);
        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"))
            }));

        var adapter = TestInfrastructure.CreateTsetmcAdapter(handler);
        var instrument = TestInfrastructure.CreateFooladInstrument();

        var result = await adapter.GetHistoricalCandlesAsync(
            instrument,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            CancellationToken.None);

        Assert.True(result.Ok);
        var candle = result.Data![0];
        Assert.NotNull(candle.Adjustment);
        Assert.Equal(DataAdjustmentType.None, candle.Adjustment.Type);
    }

    [Fact]
    public async Task GetHistoricalCandles_InvalidResponse_ReturnsEmptyCandles()
    {
        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            }));

        var adapter = TestInfrastructure.CreateTsetmcAdapter(handler);
        var instrument = TestInfrastructure.CreateFooladInstrument();

        var result = await adapter.GetHistoricalCandlesAsync(
            instrument,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            CancellationToken.None);

        Assert.True(result.Ok);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
    }

    [Fact]
    public async Task GetHistoricalCandles_InstrumentWithoutTsetmcId_ReturnsNotFound()
    {
        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        var adapter = TestInfrastructure.CreateTsetmcAdapter(handler);
        var instrument = new InstrumentMapping
        {
            InstrumentId = "test",
            Symbol = "Test",
            LatinSymbol = "Test",
            DisplayName = "Test",
            AssetClass = AssetType.Stock,
            Exchange = "TSE",
            QuoteCurrency = "IRR",
            SourceIdentifiers = new Dictionary<SourceAdapterType, SourceIdentifier>()
            // No TSETMC ID
        };

        var result = await adapter.GetHistoricalCandlesAsync(
            instrument,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            CancellationToken.None);

        Assert.False(result.Ok);
        Assert.NotNull(result.Error);
        Assert.Equal("INSTRUMENT_NOT_FOUND", result.Error.Code);
    }

    [Fact]
    public async Task GetHistoricalCandles_HttpError_Retryable()
    {
        var callCount = 0;
        var handler = new MockHttpMessageHandler((req, ct) =>
        {
            callCount++;
            throw new HttpRequestException("Connection refused");
        });

        var adapter = TestInfrastructure.CreateTsetmcAdapter(handler);
        var instrument = TestInfrastructure.CreateFooladInstrument();

        var result = await adapter.GetHistoricalCandlesAsync(
            instrument,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            CancellationToken.None);

        // With retry, callCount should be 1 (maxRetries=0) but the resilience wrapper catches it
        Assert.False(result.Ok);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task GetHistoricalCandles_ReturnsFreshnessAndQuality()
    {
        var json = TestInfrastructure.CreateTsetmcCandleJson(14050530, 100000m, 98000m, 105000m, 97000m, 1500000L);
        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"))
            }));

        var adapter = TestInfrastructure.CreateTsetmcAdapter(handler);
        var instrument = TestInfrastructure.CreateFooladInstrument();

        var result = await adapter.GetHistoricalCandlesAsync(
            instrument,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            CancellationToken.None);

        Assert.True(result.Ok);
        Assert.NotNull(result.Freshness);
        Assert.False(result.Freshness.IsCached); // First fetch, not cached
        Assert.NotNull(result.Quality);
        Assert.True(result.Quality.Score > 0);
    }

    [Fact]
    public async Task GetHistoricalCandles_CacheHit_ReturnsCachedData()
    {
        var json = TestInfrastructure.CreateTsetmcCandleJson(14050530, 100000m, 98000m, 105000m, 97000m, 1500000L);
        var callCount = 0;
        var handler = new MockHttpMessageHandler((req, ct) =>
        {
            callCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"))
            });
        });

        var adapter = TestInfrastructure.CreateTsetmcAdapter(handler);
        var instrument = TestInfrastructure.CreateFooladInstrument();

        var from = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        var to = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

        // First call: fetches from HTTP
        var result1 = await adapter.GetHistoricalCandlesAsync(instrument, from, to, CancellationToken.None);
        Assert.True(result1.Ok);

        // Second call: should come from cache
        var result2 = await adapter.GetHistoricalCandlesAsync(instrument, from, to, CancellationToken.None);
        Assert.True(result2.Ok);
        Assert.True(result2.Freshness!.IsCached);
        Assert.Equal(1, callCount); // Only one HTTP call made
    }

    [Fact]
    public void Supports_StockInstrument_ReturnsTrue()
    {
        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var adapter = TestInfrastructure.CreateTsetmcAdapter(handler);
        var instrument = TestInfrastructure.CreateFooladInstrument();

        Assert.True(adapter.Supports(instrument));
    }

    [Fact]
    public void Supports_CurrencyInstrument_ReturnsFalse()
    {
        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var adapter = TestInfrastructure.CreateTsetmcAdapter(handler);
        var instrument = TestInfrastructure.CreateUsdIrrInstrument();

        Assert.False(adapter.Supports(instrument));
    }

    [Fact]
    public async Task GetHistoricalCandles_ProvenanceNotLeaked_IntoResult()
    {
        var json = TestInfrastructure.CreateTsetmcCandleJson(14050530, 100000m, 98000m, 105000m, 97000m, 1500000L);
        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"))
            }));

        var adapter = TestInfrastructure.CreateTsetmcAdapter(handler);
        var instrument = TestInfrastructure.CreateFooladInstrument();

        var result = await adapter.GetHistoricalCandlesAsync(
            instrument,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            CancellationToken.None);

        Assert.True(result.Ok);
        // Verify that provenance contains source info but not auth secrets
        var provenance = result.Data![0].Provenance;
        Assert.NotNull(provenance);
        Assert.Equal(SourceAdapterType.Tsetmc, provenance.Source);
        // No API keys, no passwords in provenance
        var provenanceStr = System.Text.Json.JsonSerializer.Serialize(provenance);
        Assert.DoesNotContain("password", provenanceStr, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("apikey", provenanceStr, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetHistoricalCandles_CandleProvenancePreserved()
    {
        var json = TestInfrastructure.CreateTsetmcCandleJson(14050530, 100000m, 98000m, 105000m, 97000m, 1500000L);
        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"))
            }));

        var adapter = TestInfrastructure.CreateTsetmcAdapter(handler);
        var instrument = TestInfrastructure.CreateFooladInstrument();

        var result = await adapter.GetHistoricalCandlesAsync(
            instrument,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            CancellationToken.None);

        Assert.True(result.Ok);
        var candle = result.Data![0];
        Assert.Equal("4439113430858354", candle.ExtraFields["tsetmcInsCode"]);
    }

    [Fact]
    public void SourceType_ReturnsTsetmc()
    {
        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var adapter = TestInfrastructure.CreateTsetmcAdapter(handler);

        Assert.Equal(SourceAdapterType.Tsetmc, adapter.SourceType);
    }
}

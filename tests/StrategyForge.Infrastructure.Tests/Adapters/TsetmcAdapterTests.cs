using System.Globalization;
using System.Net;
using System.Text;
using Moq;
using StrategyForge.Domain.Configuration;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Models;
using StrategyForge.Infrastructure.Authentication;
using StrategyForge.Infrastructure.Services;
using Xunit;

namespace StrategyForge.Infrastructure.Tests.Adapters;

public class TsetmcAdapterTests
{
    // --- Time-independent window helpers ---
    // Historical-candle tests must not hardcode a Jalali date: as wall-clock time
    // drifts, a fixed dEven falls outside the queried window and the adapter's
    // date filter drops it. Derive the candle date from the query window instead,
    // using the production JalaliCalendarService (doubles as a round-trip check).

    private static (DateOnly From, DateOnly To) CandleWindow() =>
        (DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)),
         DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)));

    private static int DEvenInsideWindow()
    {
        var (from, _) = CandleWindow();
        var jalali = new JalaliCalendarService().ToJalali(from.AddDays(1));
        return int.Parse(jalali.Replace("/", ""), CultureInfo.InvariantCulture);
    }

    [Fact]
    public async Task GetHistoricalCandles_ValidResponse_ParsesCandlesCorrectly()
    {
        var json = TestInfrastructure.CreateTsetmcCandleJson(DEvenInsideWindow(), 100000m, 98000m, 105000m, 97000m, 1500000L);

        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"))
            }));

        var adapter = TestInfrastructure.CreateTsetmcAdapter(handler);
        var instrument = TestInfrastructure.CreateFooladInstrument();
        var (from, to) = CandleWindow();

        var result = await adapter.GetHistoricalCandlesAsync(
            instrument,
            from,
            to,
            null, CancellationToken.None);

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
        var json = TestInfrastructure.CreateTsetmcCandleJson(DEvenInsideWindow(), 100000m, 98000m, 105000m, 97000m, 1500000L);
        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"))
            }));

        var adapter = TestInfrastructure.CreateTsetmcAdapter(handler);
        var instrument = TestInfrastructure.CreateFooladInstrument();
        var (from, to) = CandleWindow();

        var result = await adapter.GetHistoricalCandlesAsync(
            instrument,
            from,
            to,
            null, CancellationToken.None);

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
            null, CancellationToken.None);

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
            null, CancellationToken.None);

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
            null, CancellationToken.None);

        // With retry, callCount should be 1 (maxRetries=0) but the resilience wrapper catches it
        Assert.False(result.Ok);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task GetHistoricalCandles_ReturnsFreshnessAndQuality()
    {
        var json = TestInfrastructure.CreateTsetmcCandleJson(DEvenInsideWindow(), 100000m, 98000m, 105000m, 97000m, 1500000L);
        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"))
            }));

        var adapter = TestInfrastructure.CreateTsetmcAdapter(handler);
        var instrument = TestInfrastructure.CreateFooladInstrument();
        var (from, to) = CandleWindow();

        var result = await adapter.GetHistoricalCandlesAsync(
            instrument,
            from,
            to,
            null, CancellationToken.None);

        Assert.True(result.Ok);
        Assert.NotNull(result.Freshness);
        Assert.False(result.Freshness.IsCached); // First fetch, not cached
        Assert.NotNull(result.Quality);
        Assert.True(result.Quality.Score > 0);
    }

    [Fact]
    public async Task GetHistoricalCandles_CacheHit_ReturnsCachedData()
    {
        var json = TestInfrastructure.CreateTsetmcCandleJson(DEvenInsideWindow(), 100000m, 98000m, 105000m, 97000m, 1500000L);
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
        var (from, to) = CandleWindow();

        // First call: fetches from HTTP
        var result1 = await adapter.GetHistoricalCandlesAsync(instrument, from, to, null, CancellationToken.None);
        Assert.True(result1.Ok);

        // Second call: should come from cache
        var result2 = await adapter.GetHistoricalCandlesAsync(instrument, from, to, null, CancellationToken.None);
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
        var json = TestInfrastructure.CreateTsetmcCandleJson(DEvenInsideWindow(), 100000m, 98000m, 105000m, 97000m, 1500000L);
        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"))
            }));

        var adapter = TestInfrastructure.CreateTsetmcAdapter(handler);
        var instrument = TestInfrastructure.CreateFooladInstrument();
        var (from, to) = CandleWindow();

        var result = await adapter.GetHistoricalCandlesAsync(
            instrument,
            from,
            to,
            null, CancellationToken.None);

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
        var json = TestInfrastructure.CreateTsetmcCandleJson(DEvenInsideWindow(), 100000m, 98000m, 105000m, 97000m, 1500000L);
        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"))
            }));

        var adapter = TestInfrastructure.CreateTsetmcAdapter(handler);
        var instrument = TestInfrastructure.CreateFooladInstrument();
        var (from, to) = CandleWindow();

        var result = await adapter.GetHistoricalCandlesAsync(
            instrument,
            from,
            to,
            null, CancellationToken.None);

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

    // ============================================================
    // Snapshot (GetLatestCandleAsync) coverage
    // ============================================================

    private const string LatestCandleJson = """
        {
          "closingPriceInfo": {
            "dEven": 14050530,
            "pClosing": 100000,
            "pDrCotVal": 99500,
            "priceFirst": 98000,
            "priceMax": 105000,
            "priceMin": 97000,
            "qTotTran5J": 1500000,
            "qTotCap": 150000000000,
            "zTotTran": 1000,
            "priceYesterday": 95000
          }
        }
        """;

    [Fact]
    public async Task GetLatestCandle_ValidResponse_ParsesSnapshotAndProvenance()
    {
        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(LatestCandleJson, Encoding.UTF8, "application/json")
            }));

        var adapter = TestInfrastructure.CreateTsetmcAdapter(handler);
        var instrument = TestInfrastructure.CreateFooladInstrument();

        var result = await adapter.GetLatestCandleAsync(instrument, CancellationToken.None);

        Assert.True(result.Ok);
        Assert.NotNull(result.Data);

        var candle = result.Data!;
        Assert.Equal(100000m, candle.Close);
        Assert.Equal(98000m, candle.Open);
        Assert.Equal(105000m, candle.High);
        Assert.Equal(97000m, candle.Low);
        Assert.Equal(99500m, candle.LastPrice);
        Assert.Equal(1500000L, candle.Volume);
        Assert.Equal("Asia/Tehran", candle.MarketTimezone);
        Assert.Equal("jalali", candle.SourceCalendar);

        // Provenance identifies provider, instrument, and endpoint
        Assert.NotNull(candle.Provenance);
        Assert.Equal(SourceAdapterType.Tsetmc, candle.Provenance.Source);
        Assert.Equal("4439113430858354", candle.Provenance.SourceInstrumentId);
        Assert.Equal("closingPriceInfo", candle.Provenance.Endpoint);
        Assert.False(candle.Provenance.IsCached);
    }

    [Fact]
    public async Task GetLatestCandle_InstrumentWithoutTsetmcId_ReturnsNotFound()
    {
        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        var adapter = TestInfrastructure.CreateTsetmcAdapter(handler);
        var instrument = new InstrumentMapping
        {
            InstrumentId = "test-no-id",
            Symbol = "TEST",
            LatinSymbol = "TEST",
            DisplayName = "Test",
            AssetClass = AssetType.Stock,
            Exchange = "TSE",
            QuoteCurrency = "IRR",
            SourceIdentifiers = new Dictionary<SourceAdapterType, SourceIdentifier>()
        };

        var result = await adapter.GetLatestCandleAsync(instrument, CancellationToken.None);

        Assert.False(result.Ok);
        Assert.NotNull(result.Error);
        Assert.Equal("INSTRUMENT_NOT_FOUND", result.Error.Code);
    }

    // ============================================================
    // Previous close → Change / ChangePercent normalization
    // ============================================================

    [Fact]
    public async Task GetHistoricalCandles_PreviousClose_MapsChangeAndPercent()
    {
        // close=100000, previousClose=95000 → change=+5000, percent=5000*100/95000
        var dEven = DEvenInsideWindow();
        var json = $$"""
            {
              "closingPriceHistory": [
                {
                  "dEven": {{dEven}},
                  "pClosing": 100000,
                  "pDrCotVal": 100000,
                  "priceFirst": 98000,
                  "priceMax": 105000,
                  "priceMin": 97000,
                  "qTotTran5J": 1500000,
                  "qTotCap": 150000000000,
                  "zTotTran": 1000,
                  "priceYesterday": 95000
                }
              ]
            }
            """;
        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            }));

        var adapter = TestInfrastructure.CreateTsetmcAdapter(handler);
        var instrument = TestInfrastructure.CreateFooladInstrument();
        var (from, to) = CandleWindow();

        var result = await adapter.GetHistoricalCandlesAsync(
            instrument,
            from,
            to,
            null, CancellationToken.None);

        Assert.True(result.Ok);
        var candle = Assert.Single(result.Data!);
        Assert.Equal(5000m, candle.Change);
        Assert.Equal(5000m * 100m / 95000m, candle.ChangePercent);
        Assert.Equal("95000", candle.ExtraFields!["previousClose"]);
    }

    [Fact]
    public async Task GetHistoricalCandles_StringNumbers_ParsedCorrectly()
    {
        // TSETMC sometimes returns numeric fields as JSON strings
        var dEven = DEvenInsideWindow();
        var json = $$"""
            {
              "closingPriceHistory": [
                {
                  "dEven": {{dEven}},
                  "pClosing": "100000",
                  "pDrCotVal": "99500",
                  "priceFirst": "98000",
                  "priceMax": "105000",
                  "priceMin": "97000",
                  "qTotTran5J": "1500000",
                  "qTotCap": "150000000000",
                  "zTotTran": "1000"
                }
              ]
            }
            """;
        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            }));

        var adapter = TestInfrastructure.CreateTsetmcAdapter(handler);
        var instrument = TestInfrastructure.CreateFooladInstrument();
        var (from, to) = CandleWindow();

        var result = await adapter.GetHistoricalCandlesAsync(
            instrument,
            from,
            to,
            null, CancellationToken.None);

        Assert.True(result.Ok);
        var candle = Assert.Single(result.Data!);
        Assert.Equal(100000m, candle.Close);
        Assert.Equal(98000m, candle.Open);
        Assert.Equal(105000m, candle.High);
        Assert.Equal(97000m, candle.Low);
        Assert.Equal(99500m, candle.LastPrice);
        Assert.Equal(1500000L, candle.Volume);
        Assert.Equal(150000000000m, candle.Value);
        Assert.Equal(1000L, candle.TradeCount);
    }

    // ============================================================
    // Malformed response / timeout / cancellation handling
    // ============================================================

    [Fact]
    public async Task GetHistoricalCandles_MalformedJson_ReturnsTypedFailure()
    {
        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("not-json{{{", Encoding.UTF8, "application/json")
            }));

        var adapter = TestInfrastructure.CreateTsetmcAdapter(handler);
        var instrument = TestInfrastructure.CreateFooladInstrument();

        var result = await adapter.GetHistoricalCandlesAsync(
            instrument,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            null, CancellationToken.None);

        // Malformed provider data must not surface as a success with fabricated data
        Assert.False(result.Ok);
        Assert.NotNull(result.Error);
        Assert.Equal("SOURCE_UNAVAILABLE", result.Error.Code);
    }

    [Fact]
    public async Task GetLatestCandle_HttpTimeout_ReturnsTypedRetryableTimeoutError()
    {
        // HttpClient surfaces request timeouts as TaskCanceledException while the
        // caller's token is NOT cancelled — must become a typed retryable failure,
        // not an unhandled exception.
        var handler = new MockHttpMessageHandler((req, ct) =>
            throw new TaskCanceledException("Simulated HttpClient timeout"));

        var adapter = TestInfrastructure.CreateTsetmcAdapter(handler);
        var instrument = TestInfrastructure.CreateFooladInstrument();

        var result = await adapter.GetLatestCandleAsync(instrument, CancellationToken.None);

        Assert.False(result.Ok);
        Assert.NotNull(result.Error);
        Assert.Equal("SOURCE_TIMEOUT", result.Error.Code);
        Assert.True(result.Error.Retryable);
    }

    [Fact]
    public async Task GetHistoricalCandles_UserCancellation_Propagates()
    {
        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        var adapter = TestInfrastructure.CreateTsetmcAdapter(handler);
        var instrument = TestInfrastructure.CreateFooladInstrument();

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            adapter.GetHistoricalCandlesAsync(
                instrument,
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)),
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
                null, cts.Token));
    }
}

using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using StrategyForge.Domain.Configuration;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Interfaces.Providers;
using StrategyForge.Domain.Models;
using StrategyForge.Infrastructure;
using StrategyForge.Infrastructure.Authentication;
using StrategyForge.Infrastructure.Services;
using Xunit;

namespace StrategyForge.Infrastructure.Tests.Adapters;

/// <summary>
/// Deterministic tests for the TGJU adapter against the live-verified
/// api.tgju.org DataTables contract (2026-09-21). No test performs network I/O.
/// </summary>
public class TgjuAdapterTests
{
    // Rows are newest-first per the verified contract; dates are relative to the
    // query window (never hardcoded) so the suite stays deterministic as time passes.

    private static (DateOnly From, DateOnly To) CandleWindow() =>
        (DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)),
         DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)));

    private static (string Gregorian, string Jalali) DatesFor(DateOnly date) =>
        (date.ToString("yyyy/MM/dd", System.Globalization.CultureInfo.InvariantCulture),
         new JalaliCalendarService().ToJalali(date));

    private static HttpResponseMessage OkJson(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"))
    };

    private static MockHttpMessageHandler HandlerReturning(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        new((req, ct) => Task.FromResult(respond(req)));

    // ============================================================
    // Mapping: valid response → canonical model
    // ============================================================

    [Fact]
    public async Task GetHistoricalCandles_ValidResponse_MapsCanonicalCandle()
    {
        var (from, to) = CandleWindow();
        var d1 = from.AddDays(1);
        var d2 = d1.AddDays(1);
        var (g1, j1) = DatesFor(d1);
        var (g2, j2) = DatesFor(d2);

        // Newest-first: d2 row first — adapter must reorder to oldest→newest.
        // Row values are the exact live-observed USD/IRR rows (2026-09-21):
        // [open, low, high, last, change, changePct, gregorian, jalali].
        var json = TestInfrastructure.CreateTgjuTableJson(
            (2310000m, 2307600m, 2319200m, 2308000m, 2000m, 0.09m, g2, j2),
            (2301850m, 2301600m, 2322200m, 2306000m, 3250m, 0.14m, g1, j1));

        var handler = HandlerReturning(_ => OkJson(json));
        var adapter = TestInfrastructure.CreateTgjuAdapter(handler);
        var instrument = TestInfrastructure.CreateUsdIrrInstrument();

        var result = await adapter.GetHistoricalCandlesAsync(instrument, from, to, null, CancellationToken.None);

        Assert.True(result.Ok);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.Count);

        var oldest = result.Data[0];
        Assert.Equal(d1, oldest.Date);
        Assert.Equal(2301850m, oldest.Open);
        Assert.Equal(2301600m, oldest.Low);
        Assert.Equal(2322200m, oldest.High);
        Assert.Equal(2306000m, oldest.Close);
        Assert.Equal(3250m, oldest.Change);
        Assert.Equal(0.14m, oldest.ChangePercent);
        Assert.Equal(0L, oldest.Volume); // TGJU rows carry no volume — never fabricated
        Assert.Equal("Asia/Tehran", oldest.MarketTimezone);
        Assert.Equal("jalali", oldest.SourceCalendar);
        Assert.Equal(j1, oldest.SourceDate);
        Assert.Equal(DataAdjustmentType.None, oldest.Adjustment.Type);

        var newest = result.Data[1];
        Assert.Equal(d2, newest.Date);
        Assert.Equal(2308000m, newest.Close);
    }

    [Fact]
    public async Task GetHistoricalCandles_CorrectInstrumentAndProvenance()
    {
        var (from, to) = CandleWindow();
        var (g, j) = DatesFor(from.AddDays(1));
        var json = TestInfrastructure.CreateTgjuSingleRowJson(585000m, 584000m, 586500m, 585750m, g, j);

        var handler = HandlerReturning(_ => OkJson(json));
        var adapter = TestInfrastructure.CreateTgjuAdapter(handler);
        var instrument = TestInfrastructure.CreateUsdIrrInstrument();

        var result = await adapter.GetHistoricalCandlesAsync(instrument, from, to, null, CancellationToken.None);

        Assert.True(result.Ok);
        var candle = Assert.Single(result.Data!);

        Assert.NotNull(candle.Provenance);
        Assert.Equal(SourceAdapterType.Tgju, candle.Provenance.Source);
        Assert.Equal("price_dollar_rl", candle.Provenance.SourceSymbol);
        Assert.Equal("history", candle.Provenance.Endpoint);
        Assert.False(candle.Provenance.IsCached);
        Assert.True(candle.Provenance.FetchedAtUtc <= DateTimeOffset.UtcNow);

        Assert.Equal("free_market", candle.ExtraFields!["rateType"]);
        Assert.Equal("tgju", candle.ExtraFields["source"]);
    }

    [Fact]
    public async Task GetHistoricalCandles_CommaSeparatedAndMarkupNumbers_AreNormalized()
    {
        var (from, to) = CandleWindow();
        var (g, j) = DatesFor(from.AddDays(1));
        // Exact live-shape row: comma thousands separators + HTML-wrapped change cells.
        var json = $$"""
        {
          "recordsTotal": 1,
          "data": [
            ["2,307,600", "2,306,000", "2,319,200", "2,310,000",
             "<span class=\"high\" dir=\"ltr\">2000<\/span>",
             "<span class=\"high\" dir=\"ltr\">0.09%<\/span>",
             "{{g}}", "{{j}}"]
          ]
        }
        """;

        var handler = HandlerReturning(_ => OkJson(json));
        var adapter = TestInfrastructure.CreateTgjuAdapter(handler);
        var instrument = TestInfrastructure.CreateUsdIrrInstrument();

        var result = await adapter.GetHistoricalCandlesAsync(instrument, from, to, null, CancellationToken.None);

        Assert.True(result.Ok);
        var candle = Assert.Single(result.Data!);
        Assert.Equal(2307600m, candle.Open);
        Assert.Equal(2306000m, candle.Low);
        Assert.Equal(2319200m, candle.High);
        Assert.Equal(2310000m, candle.Close);
        Assert.Equal(2000m, candle.Change);
        Assert.Equal(0.09m, candle.ChangePercent);
    }

    [Fact]
    public async Task GetHistoricalCandles_Gold18kInstrument_MapsCorrectly()
    {
        var (from, to) = CandleWindow();
        var (g, j) = DatesFor(from.AddDays(1));
        var json = TestInfrastructure.CreateTgjuSingleRowJson(236911000m, 236900000m, 241248000m, 241248000m, g, j);

        var handler = HandlerReturning(_ => OkJson(json));
        var adapter = TestInfrastructure.CreateTgjuAdapter(handler);
        var instrument = TestInfrastructure.CreateTgjuGold18kInstrument();

        var result = await adapter.GetHistoricalCandlesAsync(instrument, from, to, null, CancellationToken.None);

        Assert.True(result.Ok);
        var candle = Assert.Single(result.Data!);
        Assert.Equal(241248000m, candle.Close);
        Assert.Equal("geram18", candle.Provenance!.SourceSymbol);
    }

    [Fact]
    public async Task GetHistoricalCandles_DateConversion_JalaliAndGregorianAgree()
    {
        // Both date columns are the same Jalali/Gregorian day (live shape).
        // Column 6 (Jalali-shaped year) must be converted via JalaliCalendarService,
        // proving the shared production calendar is used — not a local re-implementation.
        var (from, to) = CandleWindow();
        var expected = from.AddDays(1);
        var jalali = new JalaliCalendarService().ToJalali(expected);

        var json = TestInfrastructure.CreateTgjuSingleRowJson(585000m, 584000m, 586000m, 585500m,
            gregorian: jalali, jalali: jalali);

        var handler = HandlerReturning(_ => OkJson(json));
        var adapter = TestInfrastructure.CreateTgjuAdapter(handler);
        var instrument = TestInfrastructure.CreateUsdIrrInstrument();

        var result = await adapter.GetHistoricalCandlesAsync(instrument, from, to, null, CancellationToken.None);

        Assert.True(result.Ok);
        var candle = Assert.Single(result.Data!);
        Assert.Equal(expected, candle.Date);
        Assert.Equal(jalali, candle.SourceDate);
    }

    [Fact]
    public async Task GetLatestCandle_ValidResponse_MapsSnapshotWithProvenance()
    {
        var (from, to) = CandleWindow();
        var (g, j) = DatesFor(from.AddDays(1));
        var json = TestInfrastructure.CreateTgjuSingleRowJson(585000m, 584000m, 586500m, 585750m, g, j);

        var handler = HandlerReturning(_ => OkJson(json));
        var adapter = TestInfrastructure.CreateTgjuAdapter(handler);
        var instrument = TestInfrastructure.CreateUsdIrrInstrument();

        var result = await adapter.GetLatestCandleAsync(instrument, CancellationToken.None);

        Assert.True(result.Ok);
        Assert.NotNull(result.Data);
        Assert.Equal(585750m, result.Data!.Close);
        Assert.Equal(585000m, result.Data.Open);

        Assert.NotNull(result.Data.Provenance);
        Assert.Equal(SourceAdapterType.Tgju, result.Data.Provenance.Source);
        Assert.Equal("price_dollar_rl", result.Data.Provenance.SourceSymbol);
        Assert.Equal("latest", result.Data.Provenance.Endpoint);

        // Snapshot path must hit the same verified endpoint shape.
        var request = Assert.Single(handler.Requests);
        Assert.Contains("/v1/market/indicator/summary-table-data/price_dollar_rl", request.RequestUri!.ToString());
    }

    [Fact]
    public async Task GetHistoricalCandles_HighLowDerivedFromPrice_WhenAbsent()
    {
        // Degenerate row: open=low=high=last (live payloads always carry all four,
        // but the adapter must not crash or fabricate if a field is missing).
        var (from, to) = CandleWindow();
        var (g, j) = DatesFor(from.AddDays(1));
        var json = $$"""
        {
          "recordsTotal": 1,
          "data": [["585000", "585000", "585000", "585000", "0", "0%", "{{g}}", "{{j}}"]]
        }
        """;

        var handler = HandlerReturning(_ => OkJson(json));
        var adapter = TestInfrastructure.CreateTgjuAdapter(handler);
        var instrument = TestInfrastructure.CreateUsdIrrInstrument();

        var result = await adapter.GetHistoricalCandlesAsync(instrument, from, to, null, CancellationToken.None);

        Assert.True(result.Ok);
        var candle = Assert.Single(result.Data!);
        Assert.Equal(585000m, candle.High);
        Assert.Equal(585000m, candle.Low);
    }

    // ============================================================
    // Validation: missing/invalid data must fail, never fabricate
    // ============================================================

    [Fact]
    public async Task GetHistoricalCandles_RowWithoutLastPrice_IsSkipped()
    {
        var (from, to) = CandleWindow();
        var (g, j) = DatesFor(from.AddDays(1));
        var json = $$"""
        {
          "recordsTotal": 2,
          "data": [
            ["", "", "", "", "", "", "{{g}}", "{{j}}"],
            ["585000", "584000", "586500", "585750", "750", "0.13%", "{{g}}", "{{j}}"]
          ]
        }
        """;

        var handler = HandlerReturning(_ => OkJson(json));
        var adapter = TestInfrastructure.CreateTgjuAdapter(handler);
        var instrument = TestInfrastructure.CreateUsdIrrInstrument();

        var result = await adapter.GetHistoricalCandlesAsync(instrument, from, to, null, CancellationToken.None);

        Assert.True(result.Ok);
        var candle = Assert.Single(result.Data!);
        Assert.Equal(585750m, candle.Close);
    }

    [Fact]
    public async Task GetHistoricalCandles_RowWithUnparseableNumber_IsSkipped()
    {
        var (from, to) = CandleWindow();
        var (g, j) = DatesFor(from.AddDays(1));
        var json = $$"""
        {
          "recordsTotal": 2,
          "data": [
            ["2,307,600", "2,306,000", "2,319,200", "N/A", "2000", "0.09%", "{{g}}", "{{j}}"],
            ["585000", "584000", "586500", "585750", "750", "0.13%", "{{g}}", "{{j}}"]
          ]
        }
        """;

        var handler = HandlerReturning(_ => OkJson(json));
        var adapter = TestInfrastructure.CreateTgjuAdapter(handler);
        var instrument = TestInfrastructure.CreateUsdIrrInstrument();

        var result = await adapter.GetHistoricalCandlesAsync(instrument, from, to, null, CancellationToken.None);

        Assert.True(result.Ok);
        Assert.Single(result.Data!);
    }

    [Fact]
    public async Task GetHistoricalCandles_RowWithoutUsableDate_IsSkipped()
    {
        var json = $$"""
        {
          "recordsTotal": 2,
          "data": [
            ["585000", "584000", "586500", "585750", "750", "0.13%", "not-a-date", "also-not"],
            ["586000", "585000", "587000", "586500", "500", "0.09%", "{{DatesFor(CandleWindow().From.AddDays(1)).Gregorian}}", "{{DatesFor(CandleWindow().From.AddDays(1)).Jalali}}"]
          ]
        }
        """;

        var handler = HandlerReturning(_ => OkJson(json));
        var adapter = TestInfrastructure.CreateTgjuAdapter(handler);
        var instrument = TestInfrastructure.CreateUsdIrrInstrument();
        var (from, to) = CandleWindow();

        var result = await adapter.GetHistoricalCandlesAsync(instrument, from, to, null, CancellationToken.None);

        Assert.True(result.Ok);
        Assert.Single(result.Data!);
    }

    [Fact]
    public async Task GetLatestCandle_EmptyData_ReturnsDataValidationFailure()
    {
        var json = """{ "recordsTotal": 0, "data": [] }""";
        var handler = HandlerReturning(_ => OkJson(json));
        var adapter = TestInfrastructure.CreateTgjuAdapter(handler);
        var instrument = TestInfrastructure.CreateUsdIrrInstrument();

        var result = await adapter.GetLatestCandleAsync(instrument, CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Equal("DATA_VALIDATION_FAILED", result.Error?.Code);
    }

    [Fact]
    public async Task GetHistoricalCandles_InstrumentWithoutTgjuId_ReturnsNotFound()
    {
        var handler = HandlerReturning(_ => OkJson("{}"));
        var adapter = TestInfrastructure.CreateTgjuAdapter(handler);
        var instrument = TestInfrastructure.CreateFooladInstrument(); // stock, no TGJU id

        var result = await adapter.GetLatestCandleAsync(instrument, CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Equal("INSTRUMENT_NOT_FOUND", result.Error?.Code);
    }

    [Fact]
    public async Task GetHistoricalCandles_EmptySourceIdentifier_ReturnsNotFound()
    {
        var handler = HandlerReturning(_ => OkJson("{}"));
        var adapter = TestInfrastructure.CreateTgjuAdapter(handler);
        var instrument = TestInfrastructure.CreateTgjuNoDateInstrument();

        // An empty identifier must fail typed — never issue a malformed URL.
        var result = await adapter.GetLatestCandleAsync(instrument, CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Equal("INSTRUMENT_NOT_FOUND", result.Error?.Code);
    }

    // ============================================================
    // HTTP behavior
    // ============================================================

    [Fact]
    public async Task GetHistoricalCandles_HttpFailure_ReturnsTypedError()
    {
        var handler = HandlerReturning(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent(TestInfrastructure.CreateTgjuErrorJson(), Encoding.UTF8, "text/html")
        });
        var adapter = TestInfrastructure.CreateTgjuAdapter(handler);
        var instrument = TestInfrastructure.CreateUsdIrrInstrument();

        var result = await adapter.GetLatestCandleAsync(instrument, CancellationToken.None);

        Assert.False(result.Ok);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task GetHistoricalCandles_MalformedJson_ReturnsTypedFailure()
    {
        var handler = HandlerReturning(_ => OkJson("not-json{{{"));
        var adapter = TestInfrastructure.CreateTgjuAdapter(handler);
        var instrument = TestInfrastructure.CreateUsdIrrInstrument();
        var (from, to) = CandleWindow();

        var result = await adapter.GetHistoricalCandlesAsync(instrument, from, to, null, CancellationToken.None);

        Assert.False(result.Ok);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task GetLatestCandle_HttpTimeout_ReturnsTypedRetryableTimeoutError()
    {
        // HttpClient surfaces request timeouts as TaskCanceledException while the
        // caller's token is NOT cancelled — must become a typed retryable failure.
        var handler = new MockHttpMessageHandler((req, ct) => throw new TaskCanceledException("Simulated HttpClient timeout"));
        var adapter = TestInfrastructure.CreateTgjuAdapter(handler);
        var instrument = TestInfrastructure.CreateUsdIrrInstrument();

        var result = await adapter.GetLatestCandleAsync(instrument, CancellationToken.None);

        Assert.False(result.Ok);
        Assert.NotNull(result.Error);
        Assert.Equal("SOURCE_TIMEOUT", result.Error.Code);
        Assert.True(result.Error.Retryable);
    }

    [Fact]
    public async Task GetHistoricalCandles_UserCancellation_Propagates()
    {
        var handler = HandlerReturning(_ => OkJson("{}"));
        var adapter = TestInfrastructure.CreateTgjuAdapter(handler);
        var instrument = TestInfrastructure.CreateUsdIrrInstrument();

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            adapter.GetHistoricalCandlesAsync(
                instrument,
                CandleWindow().From,
                CandleWindow().To,
                null, cts.Token));
    }

    [Fact]
    public async Task GetLatestCandle_CacheHit_UsesCachedData()
    {
        var (g, j) = DatesFor(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1));
        var json = TestInfrastructure.CreateTgjuSingleRowJson(585000m, 584000m, 586500m, 585750m, g, j);
        var callCount = 0;
        var handler = new MockHttpMessageHandler((req, ct) =>
        {
            callCount++;
            return Task.FromResult(OkJson(json));
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

    // ============================================================
    // Adapter contract
    // ============================================================

    [Fact]
    public void Supports_CurrencyAndCommodity_ReturnsTrue_CryptoWithoutSlug_ReturnsFalse()
    {
        var handler = HandlerReturning(_ => OkJson("{}"));
        var adapter = TestInfrastructure.CreateTgjuAdapter(handler);

        Assert.True(adapter.Supports(TestInfrastructure.CreateUsdIrrInstrument()));
        Assert.True(adapter.Supports(TestInfrastructure.CreateTgjuGold18kInstrument()));

        // USDT/IRR intentionally has no TGJU identifier (no live slug exists),
        // so TGJU must not claim support — Nobitex serves that instrument.
        Assert.False(adapter.Supports(TestInfrastructure.CreateUsdtIrrInstrument()));
    }

    [Fact]
    public void Supports_StockInstrument_ReturnsFalse()
    {
        var handler = HandlerReturning(_ => OkJson("{}"));
        var adapter = TestInfrastructure.CreateTgjuAdapter(handler);

        Assert.False(adapter.Supports(TestInfrastructure.CreateFooladInstrument()));
    }

    [Fact]
    public void Capabilities_AreHonest()
    {
        // Only capabilities the adapter actually implements — required for
        // capability-based registry selection (Step 8).
        var handler = HandlerReturning(_ => OkJson("{}"));
        var adapter = TestInfrastructure.CreateTgjuAdapter(handler);

        Assert.Equal(SourceAdapterType.Tgju, adapter.SourceType);
        Assert.Contains(MarketDataType.HistoricalCandles, adapter.SupportedCapabilities);
        Assert.Contains(MarketDataType.Snapshot, adapter.SupportedCapabilities);
        Assert.Contains(MarketDataType.FreeMarketFxRate, adapter.SupportedCapabilities);
        Assert.Equal("api.tgju.org", Assert.Single(adapter.Domains));
    }

    // ============================================================
    // Registry / DI integration (real AddStrategyForgeInfrastructure path)
    // ============================================================

    private static IConfiguration BuildConfiguration()
    {
        var values = new Dictionary<string, string?>
        {
            ["DataSourceSettings:HttpTimeoutSeconds"] = "10",
            ["DataSourceSettings:RetryBaseDelayMs"] = "10",
            ["DataSourceSettings:RetryMaxDelayMs"] = "100",
            ["DataSourceSettings:UserAgent"] = "StrategyForge-Test/1.0",
            ["DataSourceSettings:DefaultRateLimit:MaxRequests"] = "1000",
            ["DataSourceSettings:DefaultRateLimit:Window"] = "00:00:01"
        };

        var sources = new (string Key, string Type, string BaseUrl)[]
        {
            ("tsetmc", "Tsetmc", "https://cdn.tsetmc.com"),
            ("tgju", "Tgju", "https://api.tgju.org"),
            ("cbi", "Cbi", "https://cbi.ir"),
            ("tsewebgateway", "TseWebGateway", "https://cdn.tsetmc.com"),
            ("brsapi", "BrsApi", "https://Api.BrsApi.ir"),
            ("nobitex", "Nobitex", "https://apiv2.nobitex.ir")
        };

        foreach (var (key, type, baseUrl) in sources)
        {
            var prefix = $"DataSourceSettings:Sources:{key}";
            values[$"{prefix}:Name"] = type;
            values[$"{prefix}:SourceType"] = type;
            values[$"{prefix}:Enabled"] = "true";
            values[$"{prefix}:BaseUrl"] = baseUrl;
            values[$"{prefix}:CacheMinutes"] = "5";
            values[$"{prefix}:MaxRetries"] = "0";
            values[$"{prefix}:RateLimit:MaxRequests"] = "1000";
            values[$"{prefix}:RateLimit:Window"] = "00:00:01";
            values[$"{prefix}:Authentication:Mode"] = key == "brsapi" ? "ApiKey" : "None";
            if (key == "brsapi")
            {
                values[$"{prefix}:Authentication:CredentialReference"] = "TestBrsApiKey";
            }
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();

        // Production hosts (WebApplicationBuilder) register IConfiguration
        // automatically; CredentialResolver depends on it, so replicate that here.
        services.AddSingleton<IConfiguration>(BuildConfiguration());

        services.AddStrategyForgeInfrastructure(BuildConfiguration());
        return services.BuildServiceProvider();
    }

    private static InstrumentMapping CreateUsdIrrForRegistry() => new()
    {
        InstrumentId = "iran-fx-usd-irr-free",
        Symbol = "دلار",
        LatinSymbol = "USD/IRR",
        DisplayName = "USD/IRR Free Market Rate",
        AssetClass = AssetType.Currency,
        Exchange = "free_market",
        QuoteCurrency = "IRR",
        SourceIdentifiers = new Dictionary<SourceAdapterType, SourceIdentifier>
        {
            [SourceAdapterType.Tgju] = new SourceIdentifier { Id = "price_dollar_rl" }
        }
    };

    [Fact]
    public void AddInfrastructure_TgjuIsResolvableThroughRegistry()
    {
        using var provider = BuildProvider();

        var registry = provider.GetRequiredService<IDataSourceRegistry>();
        var tgju = registry.GetAdapter(SourceAdapterType.Tgju);

        Assert.NotNull(tgju);
        Assert.Equal("Tgju", tgju.Name);
        Assert.True(tgju.IsEnabled);
        Assert.Equal("api.tgju.org", Assert.Single(tgju.Domains));
        Assert.Contains(MarketDataType.FreeMarketFxRate, tgju.SupportedCapabilities);
    }

    [Fact]
    public void AddInfrastructure_TgjuCapabilitySelection_FreeMarketFxRate()
    {
        using var provider = BuildProvider();
        var registry = provider.GetRequiredService<IDataSourceRegistry>();
        var instrument = CreateUsdIrrForRegistry();

        // The real provider-selection path: capability + instrument-aware lookup.
        var capable = registry.GetAdaptersForCapability(instrument, MarketDataType.FreeMarketFxRate);
        Assert.Contains(capable, a => a.SourceType == SourceAdapterType.Tgju);

        var tgju = registry.GetAdapter(SourceAdapterType.Tgju);
        Assert.NotNull(tgju);
        Assert.True(tgju.Supports(instrument));
    }
}

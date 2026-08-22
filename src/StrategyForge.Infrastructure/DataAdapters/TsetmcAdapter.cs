using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StrategyForge.Domain.Configuration;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Interfaces.Providers;
using StrategyForge.Domain.Models;
using StrategyForge.Infrastructure.Authentication;
using StrategyForge.Infrastructure.Services;

namespace StrategyForge.Infrastructure.DataAdapters;

/// <summary>
/// Data source adapter for TSETMC (Tehran Securities Exchange Technology Management Co.).
/// Fetches Iranian equity market data from publicly accessible TSETMC CDN endpoints.
/// 
/// TSETMC CDN public endpoints: Authentication = None
/// TSETMC authenticated web service: Authentication = ProviderCredentials (future)
/// </summary>
public sealed class TsetmcAdapter : BaseDataSourceAdapter
{
    private readonly JalaliCalendarService _jalali;

    public override SourceAdapterType SourceType => SourceAdapterType.Tsetmc;
    public override IReadOnlyList<string> Domains { get; } = ["cdn.tsetmc.com"];
    public override IReadOnlyList<MarketDataType> SupportedCapabilities { get; } = [MarketDataType.HistoricalCandles, MarketDataType.Snapshot];

    public TsetmcAdapter(
        HttpClient httpClient,
        IOptions<DataSourceSettings> settings,
        ILogger<TsetmcAdapter> logger,
        RateLimiter rateLimiter,
        InMemoryDataCache cache,
        DataQualityValidator qualityValidator,
        IDataSourceAuthenticator authenticator,
        JalaliCalendarService jalali)
        : base(httpClient, settings, logger, rateLimiter, cache, qualityValidator, authenticator, "tsetmc")
    {
        _jalali = jalali;
    }

    protected override bool CanSupportInstrument(InstrumentMapping instrument) =>
        instrument.AssetClass == AssetType.Stock ||
        instrument.AssetClass == AssetType.ETF ||
        instrument.AssetClass == AssetType.Index;

    protected override async Task<IReadOnlyList<Candle>> FetchCandlesFromSourceAsync(
        string sourceInstrumentId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var daysBack = (to.ToDateTime(TimeOnly.MinValue) - from.ToDateTime(TimeOnly.MinValue)).Days;
        if (daysBack <= 0) daysBack = 1;

        var url = $"/api/ClosingPrice/GetClosingPriceHistory/{sourceInstrumentId}/{daysBack}";

        Logger.LogDebug("Fetching TSETMC historical data: {Url}", url);

        // Authenticate the request
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        var authError = await AuthenticateRequestAsync<IReadOnlyList<Candle>>(request, cancellationToken);
        if (authError != null)
        {
            Logger.LogWarning("TSETMC authentication failed: {Code}", authError.Error?.Code);
            // CDN is public, so authentication failure shouldn't block; proceed without auth headers
        }

        var response = await HttpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var json = JsonDocument.Parse(content);

        var candles = ParseTsetmcCandles(json, sourceInstrumentId);

        return candles
            .Where(c => c.Date >= from && c.Date <= to)
            .OrderBy(c => c.Date)
            .ToList()
            .AsReadOnly();
    }

    protected override async Task<Candle?> FetchLatestCandleFromSourceAsync(
        string sourceInstrumentId,
        CancellationToken cancellationToken)
    {
        var url = $"/api/ClosingPrice/GetClosingPriceInfo/{sourceInstrumentId}";

        Logger.LogDebug("Fetching TSETMC latest price: {Url}", url);

        var response = await HttpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var json = JsonDocument.Parse(content);

        return ParseTsetmcLatestCandle(json, sourceInstrumentId);
    }

    private IReadOnlyList<Candle> ParseTsetmcCandles(JsonDocument json, string insCode)
    {
        var candles = new List<Candle>();

        if (json.RootElement.ValueKind != JsonValueKind.Object)
            return candles;

        if (!json.RootElement.TryGetProperty("closingPriceHistory", out var history) &&
            !json.RootElement.TryGetProperty("closingPriceDaily", out history))
        {
            if (json.RootElement.ValueKind == JsonValueKind.Array)
                history = json.RootElement;
            else
            {
                Logger.LogWarning("Unexpected TSETMC response format");
                return candles;
            }
        }

        if (history.ValueKind != JsonValueKind.Array)
            return candles;

        foreach (var item in history.EnumerateArray())
        {
            try
            {
                var candle = ParseTsetmcCandleItem(item, insCode);
                if (candle != null)
                    candles.Add(candle);
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Failed to parse TSETMC candle item");
            }
        }

        return candles.AsReadOnly();
    }

    private Candle? ParseTsetmcCandleItem(JsonElement item, string insCode)
    {
        if (!item.TryGetProperty("dEven", out var dEvenProp))
            return null;

        var dEven = dEvenProp.GetInt32();
        if (dEven == 0) return null;

        var jalaliYear = dEven / 10000;
        var jalaliMonth = (dEven % 10000) / 100;
        var jalaliDay = dEven % 100;

        DateOnly gregorianDate;
        try
        {
            gregorianDate = _jalali.ToGregorian(jalaliYear, jalaliMonth, jalaliDay);
        }
        catch
        {
            Logger.LogDebug("Failed to parse Jalali date {JalaliDate}", dEven);
            return null;
        }

        var close = GetDecimal(item, "pClosing");
        var lastPrice = GetDecimal(item, "pDrCotVal");
        var open = GetDecimal(item, "priceFirst");
        var high = GetDecimal(item, "priceMax");
        var low = GetDecimal(item, "priceMin");
        var volume = GetLong(item, "qTotTran5J");
        var value = GetDecimal(item, "qTotCap");
        var tradeCount = GetLong(item, "zTotTran");

        if (open <= 0 && close <= 0)
            return null;

        if (open <= 0) open = close > 0 ? close : lastPrice;
        if (high <= 0) high = Math.Max(open, close);
        if (low <= 0) low = Math.Min(open, close);
        if (high < low) (high, low) = (low, high);

        var provenance = new DataProvenance
        {
            Source = SourceAdapterType.Tsetmc,
            SourceInstrumentId = insCode,
            FetchedAtUtc = DateTimeOffset.UtcNow,
            IsCached = false,
            Endpoint = "closingPriceHistory"
        };

        return new Candle
        {
            Date = gregorianDate,
            Open = open,
            High = high,
            Low = low,
            Close = close > 0 ? close : lastPrice,
            Volume = volume,
            Value = value,
            TradeCount = tradeCount,
            LastPrice = lastPrice,
            MarketTimezone = "Asia/Tehran",
            SourceDate = $"{jalaliYear}/{jalaliMonth:D2}/{jalaliDay:D2}",
            SourceCalendar = "jalali",
            Adjustment = DataAdjustment.Unadjusted,
            Provenance = provenance,
            ExtraFields = new Dictionary<string, string>
            {
                ["tsetmcInsCode"] = insCode,
                ["dEven"] = dEven.ToString()
            }
        };
    }

    private Candle? ParseTsetmcLatestCandle(JsonDocument json, string insCode)
    {
        if (!json.RootElement.TryGetProperty("closingPriceInfo", out var info))
        {
            if (json.RootElement.TryGetProperty("closingPrice", out info) == false)
                return null;
        }

        return ParseTsetmcCandleItem(info, insCode);
    }

    private static decimal GetDecimal(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            return prop.ValueKind switch
            {
                JsonValueKind.Number => prop.GetDecimal(),
                JsonValueKind.String => decimal.TryParse(prop.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var val) ? val : 0,
                _ => 0
            };
        }
        return 0;
    }

    private static long GetLong(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            return prop.ValueKind switch
            {
                JsonValueKind.Number => prop.GetInt64(),
                JsonValueKind.String => long.TryParse(prop.GetString(), out var val) ? val : 0,
                _ => 0
            };
        }
        return 0;
    }
}

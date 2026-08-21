using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StrategyForge.Domain.Configuration;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Interfaces.Providers;
using StrategyForge.Domain.Models;
using StrategyForge.Infrastructure.Services;

namespace StrategyForge.Infrastructure.DataAdapters;

/// <summary>
/// Data source adapter for TSETMC (Tehran Securities Exchange Technology Management Co.).
/// Fetches Iranian equity market data from publicly accessible TSETMC CDN endpoints.
/// 
/// TSETMC provides:
/// - Historical OHLCV candle data
/// - Intraday market data
/// - Market snapshots
/// - Volume and value data
/// 
/// This adapter uses TSETMC's public CDN API (cdn.tsetmc.com).
/// All requests are rate-limited to respect the source.
/// </summary>
public sealed class TsetmcAdapter : BaseDataSourceAdapter
{
    private readonly JalaliCalendarService _jalali;

    public override SourceAdapterType SourceType => SourceAdapterType.Tsetmc;
    public override IReadOnlyList<string> Domains { get; } = ["cdn.tsetmc.com"];

    public TsetmcAdapter(
        HttpClient httpClient,
        IOptions<DataSourceSettings> settings,
        ILogger<TsetmcAdapter> logger,
        RateLimiter rateLimiter,
        InMemoryDataCache cache,
        DataQualityValidator qualityValidator,
        JalaliCalendarService jalali)
        : base(httpClient, settings, logger, rateLimiter, cache, qualityValidator, "tsetmc")
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
        // TSETMC historical data endpoint
        // Format: /api/ClosingPrice/GetClosingPriceHistory/{InsCode}/{daysBack}
        var daysBack = (to.ToDateTime(TimeOnly.MinValue) - from.ToDateTime(TimeOnly.MinValue)).Days;
        if (daysBack <= 0) daysBack = 1;

        var url = $"/api/ClosingPrice/GetClosingPriceHistory/{sourceInstrumentId}/{daysBack}";

        Logger.LogDebug("Fetching TSETMC historical data: {Url}", url);

        var response = await HttpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var json = JsonDocument.Parse(content);

        var candles = ParseTsetmcCandles(json, sourceInstrumentId);

        // Filter to requested date range
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
        // TSETMC latest price endpoint
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

        // TSETMC returns { "closingPriceHistory": [...] }
        if (!json.RootElement.TryGetProperty("closingPriceHistory", out var history) &&
            !json.RootElement.TryGetProperty("closingPriceDaily", out history))
        {
            // Try the array format some endpoints return
            if (json.RootElement.ValueKind == JsonValueKind.Array)
            {
                history = json.RootElement;
            }
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
        // TSETMC closingPrice fields:
        // dEven: date as YYYYMMDD integer (Jalali)
        // pClosing: close price
        // pDrCotVal: last price
        // zTotTran: number of trades
        // qTotTran5J: volume
        // qTotCap: total value
        // pClosingAdj: adjusted close
        // priceMin: low
        // priceMax: high
        // priceFirst: open

        if (!item.TryGetProperty("dEven", out var dEvenProp))
            return null;

        var dEven = dEvenProp.GetInt32();
        if (dEven == 0) return null;

        // Parse Jalali date from YYYYMMDD format
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

        // Parse prices
        var close = GetDecimal(item, "pClosing");
        var lastPrice = GetDecimal(item, "pDrCotVal");
        var open = GetDecimal(item, "priceFirst");
        var high = GetDecimal(item, "priceMax");
        var low = GetDecimal(item, "priceMin");
        var volume = GetLong(item, "qTotTran5J");
        var value = GetDecimal(item, "qTotCap");
        var tradeCount = GetLong(item, "zTotTran");

        // Validate OHLC
        if (open <= 0 && close <= 0)
            return null;

        // If open/high/low not provided, derive from close
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

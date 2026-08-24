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
/// Data source adapter for TGJU (tgju.org) — free-market FX rates and gold prices.
/// 
/// TGJU public endpoints: Authentication = None
/// TGJU authenticated Web Service: Authentication = ApiKey (future)
/// 
/// Free-market rates must always be explicitly labeled as free-market rates
/// and must never be represented as official government rates.
/// </summary>
public sealed class TgjuAdapter : BaseDataSourceAdapter
{
    public override SourceAdapterType SourceType => SourceAdapterType.Tgju;
    public override IReadOnlyList<string> Domains { get; } = ["tgju.org"];
    public override IReadOnlyList<MarketDataType> SupportedCapabilities { get; } = [MarketDataType.HistoricalCandles, MarketDataType.Snapshot, MarketDataType.FreeMarketFxRate, MarketDataType.MarketStatistics];

    private static readonly Dictionary<string, string> TgjuSymbols = new(StringComparer.OrdinalIgnoreCase)
    {
        ["USD-IRR"] = "price_dollar_rl",
        ["EUR-IRR"] = "price_euro",
        ["GBP-IRR"] = "price_gbp",
        ["GOLD_18K"] = "price_sekee",
        ["GOLD_MESGHAL"] = "price_mesghal",
        ["GOLD_24K"] = "price_gold",
        ["USDT-IRR"] = "price_tether"
    };

    public TgjuAdapter(
        HttpClient httpClient,
        IOptions<DataSourceSettings> settings,
        ILogger<TgjuAdapter> logger,
        RateLimiter rateLimiter,
        InMemoryDataCache cache,
        DataQualityValidator qualityValidator,
        IDataSourceAuthenticator authenticator)
        : base(httpClient, settings, logger, rateLimiter, cache, qualityValidator, authenticator, "tgju")
    {
    }

    protected override bool CanSupportInstrument(InstrumentMapping instrument) =>
        instrument.AssetClass == AssetType.Currency ||
        instrument.AssetClass == AssetType.Commodity ||
        instrument.AssetClass == AssetType.Crypto;

    protected override async Task<IReadOnlyList<Candle>> FetchCandlesFromSourceAsync(
        string sourceInstrumentId,
        DateOnly from,
        DateOnly to,
        CandleResolution? resolution,
        CancellationToken cancellationToken)
    {
        var url = $"/market/{sourceInstrumentId}";

        Logger.LogDebug("Fetching TGJU historical data: {Url}", url);

        var response = await HttpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var json = JsonDocument.Parse(content);

        return ParseTgjuCandles(json, sourceInstrumentId, from, to);
    }

    protected override async Task<Candle?> FetchLatestCandleFromSourceAsync(
        string sourceInstrumentId,
        CancellationToken cancellationToken)
    {
        var url = $"/market/{sourceInstrumentId}";

        Logger.LogDebug("Fetching TGJU latest rate: {Url}", url);

        var response = await HttpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var json = JsonDocument.Parse(content);

        return ParseTgjuLatestCandle(json, sourceInstrumentId);
    }

    public bool SupportsTgjuSymbol(string symbol) =>
        TgjuSymbols.ContainsKey(symbol) || TgjuSymbols.ContainsValue(symbol);

    public string? GetTgjuSymbol(string symbol) =>
        TgjuSymbols.TryGetValue(symbol, out var tgju) ? tgju : symbol;

    private IReadOnlyList<Candle> ParseTgjuCandles(JsonDocument json, string symbol, DateOnly from, DateOnly to)
    {
        var candles = new List<Candle>();

        if (!json.RootElement.TryGetProperty("items", out var items) &&
            !json.RootElement.TryGetProperty("data", out items))
        {
            if (json.RootElement.TryGetProperty("price", out var priceProp) ||
                json.RootElement.TryGetProperty("p", out priceProp))
            {
                var latest = ParseTgjuItem(json.RootElement, symbol);
                if (latest != null)
                    candles.Add(latest);
            }
            return candles.AsReadOnly();
        }

        if (items.ValueKind != JsonValueKind.Array)
            return candles.AsReadOnly();

        foreach (var item in items.EnumerateArray())
        {
            try
            {
                var candle = ParseTgjuItem(item, symbol);
                if (candle != null && candle.Date >= from && candle.Date <= to)
                    candles.Add(candle);
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Failed to parse TGJU item");
            }
        }

        return candles.OrderBy(c => c.Date).ToList().AsReadOnly();
    }

    private Candle? ParseTgjuLatestCandle(JsonDocument json, string symbol)
    {
        return ParseTgjuItem(json.RootElement, symbol);
    }

    private Candle? ParseTgjuItem(JsonElement item, string symbol)
    {
        var price = GetDecimal(item, "p") ?? GetDecimal(item, "price");
        if (price == null || price <= 0)
            return null;

        var high = GetDecimal(item, "h") ?? price;
        var low = GetDecimal(item, "l") ?? price;
        var timeStr = GetString(item, "d") ?? GetString(item, "time") ?? GetString(item, "t");

        DateOnly date;
        if (timeStr != null)
        {
            date = ParseDate(timeStr);
        }
        else
        {
            date = DateOnly.FromDateTime(DateTime.UtcNow);
        }

        return new Candle
        {
            Date = date,
            Open = price.Value,
            High = high.Value,
            Low = low.Value,
            Close = price.Value,
            Volume = 0,
            MarketTimezone = "Asia/Tehran",
            Adjustment = DataAdjustment.Unadjusted,
            Provenance = new DataProvenance
            {
                Source = SourceAdapterType.Tgju,
                SourceSymbol = symbol,
                FetchedAtUtc = DateTimeOffset.UtcNow,
                IsCached = false,
                Endpoint = "market_rate"
            },
            ExtraFields = new Dictionary<string, string>
            {
                ["rateType"] = "free_market",
                ["source"] = "tgju"
            }
        };
    }

    private static DateOnly ParseDate(string dateStr)
    {
        if (DateOnly.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date;

        if (long.TryParse(dateStr, out var timestamp))
        {
            var dt = DateTimeOffset.FromUnixTimeSeconds(timestamp);
            return DateOnly.FromDateTime(dt.DateTime);
        }

        return DateOnly.FromDateTime(DateTime.UtcNow);
    }

    private static decimal? GetDecimal(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            return prop.ValueKind switch
            {
                JsonValueKind.Number => prop.GetDecimal(),
                JsonValueKind.String => decimal.TryParse(prop.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var val) ? val : null,
                _ => null
            };
        }
        return null;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            return prop.ValueKind switch
            {
                JsonValueKind.String => prop.GetString(),
                JsonValueKind.Number => prop.GetRawText(),
                _ => null
            };
        }
        return null;
    }
}

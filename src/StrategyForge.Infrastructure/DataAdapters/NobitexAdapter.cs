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
/// Data source adapter for Nobitex — Iran's largest cryptocurrency exchange.
/// Provides public market data for crypto pairs (USDT/IRR, BTC/IRR, etc.).
///
/// Nobitex public endpoints: Authentication = None
/// Nobitex private/account APIs: NOT implemented (outside StrategyForge scope).
///
/// Verified endpoints:
///   GET /v3/orderbook/{SYMBOL}         — Order book (300 req/min)
///   GET /market/stats                   — Market stats snapshot (20 req/min)
///   GET /market/udf/history             — OHLC history (500 candles max)
///   GET /v2/trades/{SYMBOL}             — Recent trades (60 req/min)
///
/// Symbol format: {ASSET}IRT (e.g., USDTIRT, BTCIRT)
/// USDT/IRR is a crypto instrument — never normalized to USD/IRR.
/// </summary>
public sealed class NobitexAdapter : BaseDataSourceAdapter
{
    public override SourceAdapterType SourceType => SourceAdapterType.Nobitex;
    public override IReadOnlyList<string> Domains { get; } = ["apiv2.nobitex.ir"];
    public override IReadOnlyList<MarketDataType> SupportedCapabilities { get; } = [MarketDataType.HistoricalCandles, MarketDataType.Snapshot, MarketDataType.MarketStatistics];

    /// <summary>Map from StrategyForge symbol to Nobitex symbol format.</summary>
    private static readonly Dictionary<string, string> NobitexSymbols = new(StringComparer.OrdinalIgnoreCase)
    {
        ["USDT-IRR"] = "USDTIRT",
        ["BTC-IRR"] = "BTCIRT",
        ["ETH-IRR"] = "ETHIRT",
        ["LTC-IRR"] = "LTCIRT",
        ["XRP-IRR"] = "XRPIRT",
        ["BCH-IRR"] = "BCHIRT",
        ["DOGE-IRR"] = "DOGEIRT",
        ["TRX-IRR"] = "TRXIRT"
    };

    public NobitexAdapter(
        HttpClient httpClient,
        IOptions<DataSourceSettings> settings,
        ILogger<NobitexAdapter> logger,
        RateLimiter rateLimiter,
        InMemoryDataCache cache,
        DataQualityValidator qualityValidator,
        IDataSourceAuthenticator authenticator)
        : base(httpClient, settings, logger, rateLimiter, cache, qualityValidator, authenticator, "nobitex")
    {
    }

    protected override bool CanSupportInstrument(InstrumentMapping instrument) =>
        instrument.AssetClass == AssetType.Crypto;

    protected override async Task<IReadOnlyList<Candle>> FetchCandlesFromSourceAsync(
        string sourceInstrumentId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        // Nobitex OHLC endpoint uses Unix timestamps (seconds)
        var fromUnix = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeSeconds();
        var toUnix = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeSeconds();

        var url = $"/market/udf/history?symbol={sourceInstrumentId}&resolution=D&from={fromUnix}&to={toUnix}";

        Logger.LogDebug("Fetching Nobitex OHLC data: {Url}", url);

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        var authError = await AuthenticateRequestAsync<IReadOnlyList<Candle>>(request, cancellationToken);
        if (authError != null)
        {
            Logger.LogWarning("Nobitex authentication failed: {Code}", authError.Error?.Code);
        }

        var response = await HttpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var json = JsonDocument.Parse(content);

        return ParseNobitexOhlc(json, sourceInstrumentId, from, to);
    }

    protected override async Task<Candle?> FetchLatestCandleFromSourceAsync(
        string sourceInstrumentId,
        CancellationToken cancellationToken)
    {
        // Parse symbol: USDTIRT → srcCurrency=usdt, dstCurrency=rls
        var (srcCurrency, dstCurrency) = ParseNobitexSymbol(sourceInstrumentId);
        var url = $"/market/stats?srcCurrency={srcCurrency}&dstCurrency={dstCurrency}";

        Logger.LogDebug("Fetching Nobitex market stats: {Url}", url);

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        var authError = await AuthenticateRequestAsync<Candle>(request, cancellationToken);
        if (authError != null)
        {
            Logger.LogWarning("Nobitex authentication failed: {Code}", authError.Error?.Code);
        }

        var response = await HttpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var json = JsonDocument.Parse(content);

        return ParseNobitexStats(json, sourceInstrumentId);
    }

    /// <summary>
    /// Parses Nobitex OHLC UDF-format response into candles.
    /// Response format: { "s":"ok", "t":[...], "o":[...], "h":[...], "l":[...], "c":[...], "v":[...] }
    /// </summary>
    private IReadOnlyList<Candle> ParseNobitexOhlc(JsonDocument json, string symbol, DateOnly from, DateOnly to)
    {
        var candles = new List<Candle>();

        if (!json.RootElement.TryGetProperty("s", out var statusProp) ||
            statusProp.GetString() != "ok")
        {
            Logger.LogWarning("Nobitex OHLC response status: {Status}", statusProp.GetString());
            return candles;
        }

        if (!json.RootElement.TryGetProperty("t", out var times) ||
            !json.RootElement.TryGetProperty("o", out var opens) ||
            !json.RootElement.TryGetProperty("h", out var highs) ||
            !json.RootElement.TryGetProperty("l", out var lows) ||
            !json.RootElement.TryGetProperty("c", out var closes) ||
            !json.RootElement.TryGetProperty("v", out var volumes))
        {
            Logger.LogWarning("Nobitex OHLC response missing required fields");
            return candles;
        }

        var count = times.GetArrayLength();
        for (int i = 0; i < count; i++)
        {
            try
            {
                var timestamp = times[i].GetInt64();
                var dt = DateTimeOffset.FromUnixTimeSeconds(timestamp);
                var date = DateOnly.FromDateTime(dt.DateTime);

                if (date < from || date > to)
                    continue;

                var open = opens[i].GetDecimal();
                var high = highs[i].GetDecimal();
                var low = lows[i].GetDecimal();
                var close = closes[i].GetDecimal();
                var volume = (long)volumes[i].GetDecimal();

                if (open <= 0 && close <= 0)
                    continue;

                candles.Add(new Candle
                {
                    Date = date,
                    Open = open,
                    High = high,
                    Low = low,
                    Close = close,
                    Volume = volume,
                    MarketTimezone = "Asia/Tehran",
                    Adjustment = DataAdjustment.Unadjusted,
                    Provenance = new DataProvenance
                    {
                        Source = SourceAdapterType.Nobitex,
                        SourceSymbol = symbol,
                        FetchedAtUtc = DateTimeOffset.UtcNow,
                        SourceTimestampUtc = dt,
                        IsCached = false,
                        Endpoint = "market/udf/history"
                    },
                    ExtraFields = new Dictionary<string, string>
                    {
                        ["nobitexSymbol"] = symbol,
                        ["rateType"] = "exchange"
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Failed to parse Nobitex OHLC item at index {Index}", i);
            }
        }

        return candles.OrderBy(c => c.Date).ToList().AsReadOnly();
    }

    /// <summary>
    /// Parses Nobitex market/stats response into a single latest candle.
    /// Response: { "status":"ok", "stats": { "{src}-{dst}": { "latest":"...", "dayOpen":"...", ... } } }
    /// </summary>
    private Candle? ParseNobitexStats(JsonDocument json, string symbol)
    {
        if (!json.RootElement.TryGetProperty("status", out var statusProp) ||
            statusProp.GetString() != "ok")
        {
            return null;
        }

        if (!json.RootElement.TryGetProperty("stats", out var stats))
            return null;

        // Find the first stats entry
        foreach (var prop in stats.EnumerateObject())
        {
            var marketStats = prop.Value;

            var latest = GetDecimal(marketStats, "latest");
            var dayOpen = GetDecimal(marketStats, "dayOpen");
            var dayHigh = GetDecimal(marketStats, "dayHigh");
            var dayLow = GetDecimal(marketStats, "dayLow");
            var bestSell = GetDecimal(marketStats, "bestSell");
            var bestBuy = GetDecimal(marketStats, "bestBuy");
            var volumeSrcStr = GetString(marketStats, "volumeSrc");

            if (latest == null || latest <= 0)
                continue;

            var volumeSrc = decimal.TryParse(volumeSrcStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;

            return new Candle
            {
                Date = DateOnly.FromDateTime(DateTime.UtcNow),
                Open = dayOpen ?? latest.Value,
                High = dayHigh ?? latest.Value,
                Low = dayLow ?? latest.Value,
                Close = latest.Value,
                Volume = 0,
                BidPrice = bestBuy,
                AskPrice = bestSell,
                MarketTimezone = "Asia/Tehran",
                Adjustment = DataAdjustment.Unadjusted,
                Provenance = new DataProvenance
                {
                    Source = SourceAdapterType.Nobitex,
                    SourceSymbol = symbol,
                    FetchedAtUtc = DateTimeOffset.UtcNow,
                    IsCached = false,
                    Endpoint = "market/stats"
                },
                ExtraFields = new Dictionary<string, string>
                {
                    ["nobitexSymbol"] = symbol,
                    ["rateType"] = "exchange",
                    ["marketPair"] = prop.Name,
                    ["volumeSrc"] = volumeSrc.ToString(CultureInfo.InvariantCulture)
                }
            };
        }

        return null;
    }

    /// <summary>
    /// Parses Nobitex symbol (e.g., "USDTIRT") into source/destination currency components.
    /// </summary>
    private static (string srcCurrency, string dstCurrency) ParseNobitexSymbol(string nobitexSymbol)
    {
        // USDTIRT → ("usdt", "rls"), BTCIRT → ("btc", "rls")
        if (nobitexSymbol.EndsWith("IRT", StringComparison.OrdinalIgnoreCase))
        {
            var src = nobitexSymbol[..^3].ToLowerInvariant();
            return (src, "rls");
        }

        // Fallback: lowercase the whole thing
        return (nobitexSymbol.ToLowerInvariant(), "rls");
    }

    /// <summary>Gets a decimal value from a JSON element property.</summary>
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

    /// <summary>Gets a string value from a JSON element property.</summary>
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

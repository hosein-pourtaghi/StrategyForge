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
/// Data source adapter for TSE Web Gateway — additional TSETMC CDN endpoints
/// for instrument information and order book data.
///
/// This adapter complements TsetmcAdapter (which handles OHLC history) by providing:
///   - Instrument metadata (sector, ISIN, company info)
///   - Order book / best limits (5-level bid/ask)
///   - Live quote snapshots
///
/// Both adapters target cdn.tsetmc.com but provide different capabilities.
///
/// Verified endpoints:
///   GET /api/Instrument/GetInstrumentInfo/{insCode}  — Instrument metadata
///   GET /api/BestLimits/{insCode}                    — Order book / best limits
///
/// Public CDN: Authentication = None
/// </summary>
public sealed class TseWebGatewayAdapter : BaseDataSourceAdapter
{
    public override SourceAdapterType SourceType => SourceAdapterType.TseWebGateway;
    public override IReadOnlyList<string> Domains { get; } = ["cdn.tsetmc.com"];
    public override IReadOnlyList<MarketDataType> SupportedCapabilities { get; } = [MarketDataType.Snapshot, MarketDataType.OrderBook, MarketDataType.InstrumentMetadata];

    public TseWebGatewayAdapter(
        HttpClient httpClient,
        IOptions<DataSourceSettings> settings,
        ILogger<TseWebGatewayAdapter> logger,
        RateLimiter rateLimiter,
        InMemoryDataCache cache,
        DataQualityValidator qualityValidator,
        IDataSourceAuthenticator authenticator)
        : base(httpClient, settings, logger, rateLimiter, cache, qualityValidator, authenticator, "tsewebgateway")
    {
    }

    protected override bool CanSupportInstrument(InstrumentMapping instrument) =>
        instrument.AssetClass is AssetType.Stock or AssetType.ETF or AssetType.Index;

    protected override async Task<IReadOnlyList<Candle>> FetchCandlesFromSourceAsync(
        string sourceInstrumentId,
        DateOnly from,
        DateOnly to,
        CandleResolution? resolution,
        CancellationToken cancellationToken)
    {
        // This adapter does not provide historical OHLC — use TsetmcAdapter for that.
        // Delegates to order book endpoint to get the latest quote as a single candle.
        Logger.LogDebug("TSE Web Gateway: historical OHLC not directly available, use TsetmcAdapter");
        return [];
    }

    protected override async Task<Candle?> FetchLatestCandleFromSourceAsync(
        string sourceInstrumentId,
        CancellationToken cancellationToken)
    {
        // Fetch order book and instrument info in parallel for a rich snapshot
        var orderBookTask = FetchOrderBookAsync(sourceInstrumentId, cancellationToken);
        var infoTask = FetchInstrumentInfoAsync(sourceInstrumentId, cancellationToken);

        await Task.WhenAll(orderBookTask, infoTask);

        var orderBook = orderBookTask.Result;
        var info = infoTask.Result;

        if (orderBook == null && info == null)
            return null;

        // Build candle from order book + instrument info
        return BuildCandle(sourceInstrumentId, orderBook, info);
    }

    /// <summary>
    /// Overrides the base class to provide real order book data.
    /// Parses TSE Web Gateway BestLimits response into canonical OrderBook model.
    /// </summary>
    protected override async Task<DataResult<OrderBook>> FetchOrderBookFromSourceAsync(
        InstrumentMapping instrument,
        CancellationToken cancellationToken)
    {
        var sourceId = instrument.SourceIdentifiers.GetValueOrDefault(SourceType);
        if (sourceId == null)
        {
            return DataResult<OrderBook>.Failure(new DataCollectionError2
            {
                Code = "PROVIDER_IDENTIFIER_NOT_FOUND",
                Message = $"No TSE Web Gateway identifier found for {instrument.Symbol}",
                Retryable = false
            });
        }

        var rawBook = await FetchOrderBookAsync(sourceId.Id, cancellationToken);
        if (rawBook == null)
        {
            return DataResult<OrderBook>.Failure(new DataCollectionError2
            {
                Code = "DATA_VALIDATION_FAILED",
                Message = $"No order book data returned from TSE Web Gateway for {instrument.Symbol}",
                Retryable = true,
                SourceHttpStatus = 200
            });
        }

        var bids = new List<OrderBookLevel>();
        var asks = new List<OrderBookLevel>();

        if (rawBook.Value.TryGetProperty("bestLimits", out var limits) && limits.ValueKind == JsonValueKind.Array)
        {
            foreach (var level in limits.EnumerateArray())
            {
                var bidPrice = GetDecimal(level, "pd");
                var bidQty = GetDecimal(level, "qd");
                var askPrice = GetDecimal(level, "po");
                var askQty = GetDecimal(level, "qo");

                if (bidPrice.HasValue && bidQty.HasValue && bidQty > 0)
                    bids.Add(new OrderBookLevel { Price = bidPrice.Value, Quantity = bidQty.Value });

                if (askPrice.HasValue && askQty.HasValue && askQty > 0)
                    asks.Add(new OrderBookLevel { Price = askPrice.Value, Quantity = askQty.Value });
            }
        }

        // Sort bids descending (best bid = highest price first)
        bids = bids.OrderByDescending(b => b.Price).ToList();
        // Sort asks ascending (best ask = lowest price first)
        asks = asks.OrderBy(a => a.Price).ToList();

        var orderBook = new OrderBook
        {
            InstrumentId = instrument.InstrumentId,
            Timestamp = DateTimeOffset.UtcNow,
            Bids = bids.AsReadOnly(),
            Asks = asks.AsReadOnly(),
            Provenance = new DataProvenance
            {
                Source = SourceAdapterType.TseWebGateway,
                SourceInstrumentId = sourceId.Id,
                FetchedAtUtc = DateTimeOffset.UtcNow,
                IsCached = false,
                Endpoint = "BestLimits"
            }
        };

        return DataResult<OrderBook>.Success(orderBook);
    }

    /// <summary>
    /// Fetches the order book (best limits) for an instrument.
    /// Response: { "bestLimits": [{ "number":1, "zo":..., "qo":..., "po":..., "zd":..., "qd":..., "pd":... }] }
    /// </summary>
    private async Task<JsonElement?> FetchOrderBookAsync(string insCode, CancellationToken cancellationToken)
    {
        var url = $"/api/BestLimits/{insCode}";

        try
        {
            Logger.LogDebug("Fetching TSE Web Gateway order book: {Url}", url);

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            var authError = await AuthenticateRequestAsync<JsonElement>(request, cancellationToken);
            if (authError != null)
            {
                Logger.LogWarning("TSE Web Gateway authentication failed: {Code}", authError.Error?.Code);
            }

            var response = await HttpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var json = JsonDocument.Parse(content);
            return json.RootElement;
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Failed to fetch TSE Web Gateway order book for {InsCode}", insCode);
            return null;
        }
    }

    /// <summary>
    /// Fetches instrument information (sector, ISIN, company details).
    /// Response: { "instrumentInfo": { "eps":..., "sector":..., "staticThreshold":... } }
    /// </summary>
    private async Task<JsonElement?> FetchInstrumentInfoAsync(string insCode, CancellationToken cancellationToken)
    {
        var url = $"/api/Instrument/GetInstrumentInfo/{insCode}";

        try
        {
            Logger.LogDebug("Fetching TSE Web Gateway instrument info: {Url}", url);

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            var authError = await AuthenticateRequestAsync<JsonElement>(request, cancellationToken);
            if (authError != null)
            {
                Logger.LogWarning("TSE Web Gateway authentication failed: {Code}", authError.Error?.Code);
            }

            var response = await HttpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var json = JsonDocument.Parse(content);
            return json.RootElement;
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Failed to fetch TSE Web Gateway instrument info for {InsCode}", insCode);
            return null;
        }
    }

    /// <summary>
    /// Builds a Candle from order book and instrument info data.
    /// Uses the best bid/ask from the order book to construct the price snapshot.
    /// </summary>
    private Candle? BuildCandle(string insCode, JsonElement? orderBook, JsonElement? instrumentInfo)
    {
        decimal? bestBid = null;
        decimal? bestAsk = null;
        string? isin = null;
        string? sector = null;

        if (orderBook.HasValue)
        {
            ExtractBestBidAsk(orderBook.Value, out bestBid, out bestAsk);
        }

        if (instrumentInfo.HasValue)
        {
            ExtractInstrumentMetadata(instrumentInfo.Value, out isin, out sector);
        }

        // If we have no price data at all, return null
        var midPrice = bestBid.HasValue && bestAsk.HasValue
            ? (bestBid.Value + bestAsk.Value) / 2
            : bestBid ?? bestAsk;

        if (midPrice == null || midPrice <= 0)
            return null;

        return new Candle
        {
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            Open = midPrice.Value,
            High = bestAsk ?? midPrice.Value,
            Low = bestBid ?? midPrice.Value,
            Close = midPrice.Value,
            Volume = 0,
            BidPrice = bestBid,
            AskPrice = bestAsk,
            MarketTimezone = "Asia/Tehran",
            Adjustment = DataAdjustment.Unadjusted,
            Provenance = new DataProvenance
            {
                Source = SourceAdapterType.TseWebGateway,
                SourceInstrumentId = insCode,
                FetchedAtUtc = DateTimeOffset.UtcNow,
                IsCached = false,
                Endpoint = "BestLimits+InstrumentInfo"
            },
            ExtraFields = new Dictionary<string, string>
            {
                ["tseWebGatewayInsCode"] = insCode,
                ["rateType"] = "market"
            }.Also(d =>
            {
                if (isin != null) d["isin"] = isin;
                if (sector != null) d["sector"] = sector;
            })
        };
    }

    private static void ExtractBestBidAsk(JsonElement orderBook, out decimal? bestBid, out decimal? bestAsk)
    {
        bestBid = null;
        bestAsk = null;

        // Order book may have different formats
        if (orderBook.TryGetProperty("bestLimits", out var limits) && limits.ValueKind == JsonValueKind.Array)
        {
            foreach (var level in limits.EnumerateArray())
            {
                // zo/qo/po = sell side (offers), zd/qd/pd = buy side (bids)
                var bidPrice = GetDecimal(level, "pd");
                var askPrice = GetDecimal(level, "po");

                if (bidPrice.HasValue && (!bestBid.HasValue || bidPrice > bestBid))
                    bestBid = bidPrice;

                if (askPrice.HasValue && (!bestAsk.HasValue || askPrice < bestAsk))
                    bestAsk = askPrice;
            }
        }
    }

    private static void ExtractInstrumentMetadata(JsonElement info, out string? isin, out string? sector)
    {
        isin = null;
        sector = null;

        // May be wrapped in "instrumentInfo" or at root
        var root = info;
        if (info.TryGetProperty("instrumentInfo", out var inner))
            root = inner;

        if (root.TryGetProperty("isin", out var isinProp) && isinProp.ValueKind == JsonValueKind.String)
            isin = isinProp.GetString();

        if (root.TryGetProperty("sector", out var sectorProp))
        {
            sector = sectorProp.ValueKind switch
            {
                JsonValueKind.String => sectorProp.GetString(),
                JsonValueKind.Object => sectorProp.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null,
                _ => null
            };
        }
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
}

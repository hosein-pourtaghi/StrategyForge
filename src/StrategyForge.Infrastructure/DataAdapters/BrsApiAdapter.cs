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
/// Data source adapter for BRSAPI — a third-party API wrapping TSETMC data.
/// Provides real-time stock/ETF snapshots with API key authentication.
///
/// BRSAPI authentication: API key required (free key from brsapi.ir).
/// BRSAPI wraps TSETMC data — does NOT provide independent market data.
///
/// Verified endpoint:
///   GET /Tsetmc/AllSymbols.php?key={key}&type={type}
///   Returns all symbols with real-time data (price, order book, institutional/retail).
///
/// NOT implemented (not available from BRSAPI):
///   Historical OHLC — use TsetmcAdapter or TseWebGatewayAdapter instead.
/// </summary>
public sealed class BrsApiAdapter : BaseDataSourceAdapter
{
    public override SourceAdapterType SourceType => SourceAdapterType.BrsApi;
    public override IReadOnlyList<string> Domains { get; } = ["Api.BrsApi.ir"];
    public override IReadOnlyList<MarketDataType> SupportedCapabilities { get; } = [MarketDataType.Snapshot];

    public BrsApiAdapter(
        HttpClient httpClient,
        IOptions<DataSourceSettings> settings,
        ILogger<BrsApiAdapter> logger,
        RateLimiter rateLimiter,
        InMemoryDataCache cache,
        DataQualityValidator qualityValidator,
        IDataSourceAuthenticator authenticator)
        : base(httpClient, settings, logger, rateLimiter, cache, qualityValidator, authenticator, "brsapi")
    {
    }

    protected override bool CanSupportInstrument(InstrumentMapping instrument) =>
        instrument.AssetClass is AssetType.Stock or AssetType.ETF or AssetType.Index;

    protected override Task<IReadOnlyList<Candle>> FetchCandlesFromSourceAsync(
        string sourceInstrumentId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        // BRSAPI does not provide historical OHLC data.
        Logger.LogDebug("BRSAPI does not support historical OHLC — returning empty");
        return Task.FromResult<IReadOnlyList<Candle>>([]);
    }

    /// <summary>
    /// Override to handle auth failure before the base adapter's retry pipeline.
    /// BRSAPI requires an API key; if credentials are missing, return a clear
    /// non-retryable AUTHENTICATION_REQUIRED error immediately.
    /// </summary>
    public override async Task<DataResult<Candle>> GetLatestCandleAsync(
        InstrumentMapping instrument,
        CancellationToken cancellationToken = default)
    {
        var sourceId = instrument.SourceIdentifiers.GetValueOrDefault(SourceType);
        if (sourceId == null)
        {
            return DataResult<Candle>.Failure(CreateError(
                "INSTRUMENT_NOT_FOUND",
                $"No {Name} identifier found for instrument {instrument.Symbol}",
                retryable: false));
        }

        // Check cache first
        var cacheKey = InMemoryDataCache.LatestCandleKey(instrument.InstrumentId, SourceType.ToString());
        if (Cache.TryGet<Candle>(cacheKey, out var cached) && cached != null)
        {
            return DataResult<Candle>.Success(cached,
                freshness: DataFreshness.Cached(DateTimeOffset.UtcNow.AddMinutes(-Config.CacheMinutes)),
                quality: QualityValidator.ValidateCandle(cached),
                metadata: new AcquisitionMetadata { CacheHit = true, Sources = [Name] });
        }

        // Authenticate BEFORE fetching — BRSAPI requires API key
        var dummyRequest = new HttpRequestMessage(HttpMethod.Get, "/Tsetmc/AllSymbols.php?type=1");
        var authResult = await Authenticator.AuthenticateAsync(dummyRequest, Config.Authentication, cancellationToken);
        if (!authResult.Success)
        {
            Logger.LogWarning("BRSAPI authentication failed: {Code} - {Message}",
                authResult.ErrorCode, authResult.ErrorMessage);

            return DataResult<Candle>.Failure(CreateError(
                authResult.ErrorCode ?? "AUTHENTICATION_FAILED",
                authResult.ErrorMessage ?? "Authentication failed",
                retryable: false));
        }

        // Auth succeeded — delegate to base implementation
        return await base.GetLatestCandleAsync(instrument, cancellationToken);
    }

    protected override async Task<Candle?> FetchLatestCandleFromSourceAsync(
        string sourceInstrumentId,
        CancellationToken cancellationToken)
    {
        // BRSAPI returns ALL symbols in one call; filter by sourceInstrumentId (InsCode).
        var url = $"/Tsetmc/AllSymbols.php?type=1";

        Logger.LogDebug("Fetching BRSAPI market data: {Url}", url);

        // Auth is already checked by GetLatestCardenAsync override — apply credentials to this request
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        await Authenticator.AuthenticateAsync(request, Config.Authentication, cancellationToken);

        var response = await HttpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var json = JsonDocument.Parse(content);

        return ParseBrsApiSymbol(json, sourceInstrumentId);
    }

    /// <summary>
    /// Parses BRSAPI response and extracts the matching symbol by InsCode.
    /// BRSAPI wraps TSETMC data with field names matching TSETMC filter notation.
    /// </summary>
    private Candle? ParseBrsApiSymbol(JsonDocument json, string insCode)
    {
        if (json.RootElement.ValueKind != JsonValueKind.Array)
        {
            Logger.LogWarning("BRSAPI response is not an array");
            return null;
        }

        foreach (var item in json.RootElement.EnumerateArray())
        {
            if (!item.TryGetProperty("id", out var idProp))
                continue;

            var id = idProp.GetString();
            if (!string.Equals(id, insCode, StringComparison.Ordinal))
                continue;

            return ParseBrsApiItem(item, insCode);
        }

        Logger.LogDebug("BRSAPI: Instrument {InsCode} not found in response", insCode);
        return null;
    }

    /// <summary>
    /// Parses a single BRSAPI symbol item into a Candle.
    /// Field names match TSETMC filter notation (pf, pl, pc, pmin, pmax, tvol, etc.).
    /// </summary>
    private Candle ParseBrsApiItem(JsonElement item, string insCode)
    {
        var symbol = GetString(item, "l18") ?? insCode;
        var companyName = GetString(item, "l30") ?? symbol;
        var isin = GetString(item, "isin");

        var firstPrice = GetDecimal(item, "pf");       // Open
        var lastPrice = GetDecimal(item, "pl");        // Last
        var closingPrice = GetDecimal(item, "pc");     // Close
        var dayLow = GetDecimal(item, "pmin");         // Low
        var dayHigh = GetDecimal(item, "pmax");        // High
        var previousClose = GetDecimal(item, "py");    // Yesterday's close
        var tradeCount = GetLong(item, "tno");
        var tradeVolume = GetLong(item, "tvol");
        var tradeValue = GetDecimal(item, "tval");

        var open = firstPrice ?? previousClose ?? 0;
        var high = dayHigh ?? open;
        var low = dayLow ?? open;
        var close = closingPrice ?? lastPrice ?? open;

        if (open <= 0 && close <= 0)
        {
            Logger.LogDebug("BRSAPI: No valid price data for {InsCode}", insCode);
            return null;
        }

        if (high < low) (high, low) = (low, high);

        var provenance = new DataProvenance
        {
            Source = SourceAdapterType.BrsApi,
            SourceInstrumentId = insCode,
            SourceSymbol = symbol,
            FetchedAtUtc = DateTimeOffset.UtcNow,
            IsCached = false,
            Endpoint = "Tsetmc/AllSymbols"
        };

        return new Candle
        {
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            Open = open,
            High = high,
            Low = low,
            Close = close,
            Volume = tradeVolume,
            Value = tradeValue,
            TradeCount = tradeCount,
            LastPrice = lastPrice,
            MarketTimezone = "Asia/Tehran",
            Adjustment = DataAdjustment.Unadjusted,
            Provenance = provenance,
            ExtraFields = new Dictionary<string, string>
            {
                ["brsapiId"] = insCode,
                ["brsapiSymbol"] = symbol,
                ["companyName"] = companyName,
                ["rateType"] = "market"
            }.Also(d =>
            {
                if (isin != null) d["isin"] = isin;
                if (previousClose.HasValue) d["previousClose"] = previousClose.Value.ToString(CultureInfo.InvariantCulture);
            })
        };
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

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
            return prop.GetString();
        return null;
    }
}

/// <summary>Extension method for inline dictionary population.</summary>
internal static class DictionaryExtensions
{
    public static T Also<T>(this T dict, Action<T> action) where T : IDictionary<string, string>
    {
        action(dict);
        return dict;
    }
}

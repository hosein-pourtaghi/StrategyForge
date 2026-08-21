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
/// Data source adapter for the Central Bank of Iran (CBI).
/// Provides official government FX reference rates.
/// 
/// Official rates must always remain distinguishable from free-market rates.
/// Never use official and free-market rates interchangeably.
/// </summary>
public sealed class CbiAdapter : BaseDataSourceAdapter
{
    public override SourceAdapterType SourceType => SourceAdapterType.Cbi;
    public override IReadOnlyList<string> Domains { get; } = ["cbi.ir"];

    public CbiAdapter(
        HttpClient httpClient,
        IOptions<DataSourceSettings> settings,
        ILogger<CbiAdapter> logger,
        RateLimiter rateLimiter,
        InMemoryDataCache cache,
        DataQualityValidator qualityValidator)
        : base(httpClient, settings, logger, rateLimiter, cache, qualityValidator, "cbi")
    {
    }

    protected override bool CanSupportInstrument(InstrumentMapping instrument) =>
        instrument.AssetClass == AssetType.Currency;

    protected override async Task<IReadOnlyList<Candle>> FetchCandlesFromSourceAsync(
        string sourceInstrumentId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        // CBI does not typically provide historical daily candle data in the same format
        // We can fetch current rates and return them as a single-point candle
        // For historical data, other sources (TGJU) are more suitable
        Logger.LogDebug("CBI adapter: historical candle data not directly available");

        return [];
    }

    protected override async Task<Candle?> FetchLatestCandleFromSourceAsync(
        string sourceInstrumentId,
        CancellationToken cancellationToken)
    {
        // CBI current exchange rate page
        var url = $"/apps/Currency";

        Logger.LogDebug("Fetching CBI official rates: {Url}", url);

        var response = await HttpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var json = JsonDocument.Parse(content);

        return ParseCbiRate(json, sourceInstrumentId);
    }

    private Candle? ParseCbiRate(JsonDocument json, string currencyCode)
    {
        // CBI API returns rates in various formats
        // Try to find the requested currency
        if (json.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in json.RootElement.EnumerateArray())
            {
                if (!item.TryGetProperty("CurrencyCode", out var codeProp) &&
                    !item.TryGetProperty("code", out codeProp))
                    continue;

                var code = codeProp.GetString();
                if (!string.Equals(code, currencyCode, StringComparison.OrdinalIgnoreCase))
                    continue;

                var rate = GetDecimal(item, "Rate") ?? GetDecimal(item, "rate") ?? GetDecimal(item, "Price");
                if (rate == null || rate <= 0)
                    continue;

                var buyRate = GetDecimal(item, "BuyRate") ?? GetDecimal(item, "buy");
                var sellRate = GetDecimal(item, "SellRate") ?? GetDecimal(item, "sell");

                return new Candle
                {
                    Date = DateOnly.FromDateTime(DateTime.UtcNow),
                    Open = rate.Value,
                    High = sellRate ?? rate.Value,
                    Low = buyRate ?? rate.Value,
                    Close = rate.Value,
                    Volume = 0,
                    MarketTimezone = "Asia/Tehran",
                    Adjustment = DataAdjustment.Unadjusted,
                    Provenance = new DataProvenance
                    {
                        Source = SourceAdapterType.Cbi,
                        SourceSymbol = currencyCode,
                        FetchedAtUtc = DateTimeOffset.UtcNow,
                        IsCached = false,
                        Endpoint = "official_rate"
                    },
                    ExtraFields = new Dictionary<string, string>
                    {
                        ["rateType"] = "official",
                        ["source"] = "cbi"
                    }
                };
            }
        }

        return null;
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

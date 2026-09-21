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
/// Endpoint (verified live 2026-09-21):
///   GET {BaseUrl}/v1/market/indicator/summary-table-data/{symbol}?length={n}
///   Returns a DataTables-style payload:
///     { "recordsTotal": N, "data": [ [open, low, high, last, change, changePct, gregorian, jalali], ... ] }
///   Rows are newest-first; all cells are JSON strings (numeric cells use
///   comma thousands separators; change cells may contain HTML markup).
///
/// TGJU public endpoints: Authentication = None
/// TGJU authenticated Web Service: Authentication = ApiKey (future)
///
/// Free-market rates must always be explicitly labeled as free-market rates
/// and must never be represented as official government rates.
/// </summary>
public sealed class TgjuAdapter : BaseDataSourceAdapter
{
    private readonly JalaliCalendarService _jalali;

    /// <summary>
    /// TGJU indicator slugs for the instruments StrategyForge tracks. Values are
    /// config-driven via SourceIdentifier.Id; this map only records which canonical
    /// keys were verified against the live API (2026-09-21).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> VerifiedSymbols =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["USD/IRR"] = "price_dollar_rl",
            ["GOLD_18K"] = "geram18",
            ["GOLD_SEKKEH"] = "sekee",
            ["GOLD_MESGHAL"] = "mesghal"
        };

    public override SourceAdapterType SourceType => SourceAdapterType.Tgju;
    public override IReadOnlyList<string> Domains { get; } = ["api.tgju.org"];
    public override IReadOnlyList<MarketDataType> SupportedCapabilities { get; } =
        [MarketDataType.HistoricalCandles, MarketDataType.Snapshot, MarketDataType.FreeMarketFxRate, MarketDataType.MarketStatistics];

    public TgjuAdapter(
        HttpClient httpClient,
        IOptions<DataSourceSettings> settings,
        ILogger<TgjuAdapter> logger,
        RateLimiter rateLimiter,
        InMemoryDataCache cache,
        DataQualityValidator qualityValidator,
        IDataSourceAuthenticator authenticator,
        JalaliCalendarService jalali)
        : base(httpClient, settings, logger, rateLimiter, cache, qualityValidator, authenticator, "tgju")
    {
        _jalali = jalali;
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
        var daysBack = Math.Max(1, to.DayNumber - from.DayNumber + 1);
        var url = $"/v1/market/indicator/summary-table-data/{Uri.EscapeDataString(sourceInstrumentId)}?length={daysBack}";

        Logger.LogDebug("Fetching TGJU history: {Url}", url);

        var json = await FetchJsonAsync(url, cancellationToken);
        return ParseTgjuRows(json, sourceInstrumentId, from, to);
    }

    protected override async Task<Candle?> FetchLatestCandleFromSourceAsync(
        string sourceInstrumentId,
        CancellationToken cancellationToken)
    {
        // The snapshot is the newest row of the same verified history endpoint;
        // api.tgju.org exposes no dedicated current-rate endpoint (verified live).
        var url = $"/v1/market/indicator/summary-table-data/{Uri.EscapeDataString(sourceInstrumentId)}?length=1";

        Logger.LogDebug("Fetching TGJU latest rate: {Url}", url);

        var json = await FetchJsonAsync(url, cancellationToken);
        var rows = GetDataRows(json);
        return rows.Count > 0 ? ParseTgjuRow(rows[0], sourceInstrumentId, endpoint: "latest") : null;
    }

    private async Task<JsonDocument> FetchJsonAsync(string url, CancellationToken cancellationToken)
    {
        var response = await HttpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonDocument.Parse(content);
    }

    // ===========================
    // Parsing
    // ===========================

    private IReadOnlyList<Candle> ParseTgjuRows(JsonDocument json, string symbol, DateOnly from, DateOnly to)
    {
        var rows = GetDataRows(json);
        var candles = new List<Candle>(rows.Count);

        foreach (var row in rows)
        {
            try
            {
                var candle = ParseTgjuRow(row, symbol, endpoint: "history");
                if (candle != null && candle.Date >= from && candle.Date <= to)
                    candles.Add(candle);
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Failed to parse TGJU row for {Symbol}", symbol);
            }
        }

        // Oldest → newest for downstream indicator consumers.
        return candles.OrderBy(c => c.Date).ToList().AsReadOnly();
    }

    private static List<JsonElement> GetDataRows(JsonDocument json)
    {
        var rows = new List<JsonElement>();

        if (json.RootElement.ValueKind != JsonValueKind.Object ||
            !json.RootElement.TryGetProperty("data", out var data) ||
            data.ValueKind != JsonValueKind.Array)
        {
            return rows;
        }

        foreach (var row in data.EnumerateArray())
            rows.Add(row);
        return rows;
    }

    /// <summary>
    /// Parses one DataTables row:
    /// [0]=open, [1]=low, [2]=high, [3]=last, [4]=change, [5]=change%, [6]=Gregorian, [7]=Jalali.
    /// Returns null (not fabricated data) when required fields are missing or invalid.
    /// </summary>
    private Candle? ParseTgjuRow(JsonElement row, string symbol, string endpoint)
    {
        if (row.ValueKind != JsonValueKind.Array)
            return null;

        var cells = new JsonElement[8];
        var count = 0;
        foreach (var cell in row.EnumerateArray())
        {
            if (count < cells.Length)
                cells[count] = cell;
            count++;
        }

        // Required: last price (index 3) and a usable date (index 6 or 7).
        var last = count > 3 ? ParseTgjuNumber(cells[3]) : null;
        if (last is not > 0)
            return null;

        var open = count > 0 ? ParseTgjuNumber(cells[0]) : null;
        var low = count > 1 ? ParseTgjuNumber(cells[1]) : null;
        var high = count > 2 ? ParseTgjuNumber(cells[2]) : null;
        var change = count > 4 ? ParseTgjuNumber(cells[4]) : null;
        var changePercent = count > 5 ? ParseTgjuNumber(cells[5]) : null;

        var date = count > 6 ? ParseTgjuDate(cells[6]) : null;
        date ??= count > 7 ? ParseTgjuDate(cells[7]) : null;
        if (date == null)
        {
            Logger.LogDebug("TGJU row for {Symbol} has no usable date; skipping", symbol);
            return null;
        }

        // High/low are optional in the payload; degrade to the observed price
        // instead of inventing values (mirrors TsetmcAdapter's convention).
        var effectiveOpen = open is > 0 ? open.Value : last.Value;
        var effectiveHigh = high is > 0 ? high.Value : Math.Max(effectiveOpen, last.Value);
        var effectiveLow = low is > 0 ? low.Value : Math.Min(effectiveOpen, last.Value);
        if (effectiveHigh < effectiveLow)
            (effectiveHigh, effectiveLow) = (effectiveLow, effectiveHigh);

        var gregorianDate = date.Value;
        var jalaliDate = count > 7 ? GetStringCell(cells[7]) : null;

        return new Candle
        {
            Date = gregorianDate,
            Open = effectiveOpen,
            High = effectiveHigh,
            Low = effectiveLow,
            Close = last.Value,
            Volume = 0, // TGJU history rows do not carry volume (verified contract)
            Change = change,
            ChangePercent = changePercent,
            MarketTimezone = "Asia/Tehran",
            SourceDate = jalaliDate,
            SourceCalendar = "jalali",
            Adjustment = DataAdjustment.Unadjusted,
            Provenance = new DataProvenance
            {
                Source = SourceAdapterType.Tgju,
                SourceSymbol = symbol,
                FetchedAtUtc = DateTimeOffset.UtcNow,
                IsCached = false,
                Endpoint = endpoint
            },
            ExtraFields = new Dictionary<string, string>
            {
                ["rateType"] = "free_market",
                ["source"] = "tgju"
            }
        };
    }

    /// <summary>
    /// Parses a TGJU numeric cell: JSON numbers or strings with comma thousands
    /// separators, optional sign/decimals, and optional HTML markup around the
    /// value. Returns null for ambiguous/malformed values (never fabricates 0).
    /// </summary>
    private static decimal? ParseTgjuNumber(JsonElement cell)
    {
        string? raw = cell.ValueKind switch
        {
            JsonValueKind.Number => cell.GetRawText(),
            JsonValueKind.String => cell.GetString(),
            _ => null
        };

        if (string.IsNullOrWhiteSpace(raw))
            return null;

        // Strip HTML markup (e.g. <span class="high" dir="ltr">0.09%</span>):
        // the value sits between the first '>' and the next '<' after it.
        var lt = raw.IndexOf('<');
        if (lt >= 0)
        {
            var gt = raw.IndexOf('>', lt);
            if (gt < 0)
                return null; // malformed markup — ambiguous, reject

            raw = raw[(gt + 1)..];
            var end = raw.IndexOf('<');
            if (end >= 0)
                raw = raw[..end];
        }

        raw = raw.Replace(",", "").Trim().TrimEnd('%');
        if (raw.Length == 0)
            return null;

        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    /// <summary>
    /// Parses a date cell: Gregorian (yyyy/MM/dd), Unix seconds, or Jalali (yyyy/MM/dd).
    /// Jalali and Gregorian strings share the same shape, so the year is
    /// disambiguated by range: Jalali years are ~1300-1500, while Gregorian
    /// market dates are ≥1700 (verified live 2026-09-21: columns 6/7 carry
    /// "2026/09/21" and "1405/06/30" respectively).
    /// </summary>
    private DateOnly? ParseTgjuDate(JsonElement cell)
    {
        var raw = GetStringCell(cell);
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        if (raw.Length == 10 &&
            raw[4] == '/' && raw[7] == '/' &&
            int.TryParse(raw.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var y) &&
            int.TryParse(raw.AsSpan(5, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var m) &&
            int.TryParse(raw.AsSpan(8, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var d) &&
            m >= 1 && m <= 12 && d >= 1 && d <= 31)
        {
            // Jalali-shaped year → convert via the shared production calendar service.
            if (y is >= 1000 and < 1700)
            {
                try
                {
                    return _jalali.ToGregorian(y, m, d);
                }
                catch (Exception ex)
                {
                    Logger.LogDebug(ex, "Failed to convert TGJU Jalali date {Jalali}", raw);
                    return null;
                }
            }

            try
            {
                return new DateOnly(y, m, d);
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        // Fallback: Unix timestamp in seconds.
        if (long.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var ts) && ts > 1_000_000_000)
            return DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(ts).UtcDateTime);

        return null;
    }

    private static string? GetStringCell(JsonElement cell) => cell.ValueKind switch
    {
        JsonValueKind.String => cell.GetString(),
        JsonValueKind.Number => cell.GetRawText(),
        _ => null
    };
}

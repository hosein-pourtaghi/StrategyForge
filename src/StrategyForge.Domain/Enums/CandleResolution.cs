namespace StrategyForge.Domain.Enums;

/// <summary>
/// Specifies the time resolution (interval) of candle data.
/// Not all sources support all resolutions — check adapter capabilities.
/// 
/// Nobitex supports: 1, 5, 15, 30, 60, 240, D, W, M
/// TSETMC supports: Daily only
/// TGJU supports: Daily only
/// 
/// Note: Sub-daily resolutions from Nobitex have data retention limits:
///   - 1-minute candles: last 7 days only
///   - 5/15/30-minute candles: last 30 days
///   - 1/4-hour candles: last 90 days
/// </summary>
public enum CandleResolution
{
    /// <summary>1-minute candles. Nobitex only, max 7-day range.</summary>
    Minute1 = 1,

    /// <summary>5-minute candles. Nobitex only.</summary>
    Minute5 = 5,

    /// <summary>15-minute candles. Nobitex only.</summary>
    Minute15 = 15,

    /// <summary>30-minute candles. Nobitex only.</summary>
    Minute30 = 30,

    /// <summary>1-hour candles. Nobitex only.</summary>
    Hour1 = 60,

    /// <summary>4-hour candles. Nobitex only.</summary>
    Hour4 = 240,

    /// <summary>Daily candles. Supported by all candle-capable adapters.</summary>
    Daily,

    /// <summary>Weekly candles. Nobitex only.</summary>
    Weekly,

    /// <summary>Monthly candles. Nobitex only.</summary>
    Monthly
}

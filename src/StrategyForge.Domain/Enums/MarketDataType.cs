namespace StrategyForge.Domain.Enums;

/// <summary>
/// Identifies the type of market data being requested or returned.
/// Each value corresponds to a specific data shape and capability.
/// Adapters declare which data types they support.
/// </summary>
public enum MarketDataType
{
    /// <summary>Historical OHLCV candle data.</summary>
    HistoricalCandles,

    /// <summary>Latest single price snapshot.</summary>
    Snapshot,

    /// <summary>Order book / depth-of-market.</summary>
    OrderBook,

    /// <summary>Official government exchange rate.</summary>
    OfficialFxRate,

    /// <summary>Free-market / parallel exchange rate.</summary>
    FreeMarketFxRate,

    /// <summary>Exchange market statistics.</summary>
    MarketStatistics,

    /// <summary>Instrument metadata / information.</summary>
    InstrumentMetadata
}

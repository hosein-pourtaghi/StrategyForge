namespace StrategyForge.Domain.Enums;

/// <summary>
/// Categories of data sources used by StrategyForge.
/// </summary>
public enum DataSourceType
{
    /// <summary>Market price data (OHLCV, volume, order book).</summary>
    MarketData,

    /// <summary>News articles and announcements.</summary>
    News,

    /// <summary>Economic indicators and statistics.</summary>
    Economic,

    /// <summary>Company fundamentals and financial statements.</summary>
    Fundamental,

    /// <summary>Currency exchange rates.</summary>
    Currency,

    /// <summary>Commodity prices (gold, silver, etc.).</summary>
    Commodity,

    /// <summary>Political and geopolitical information.</summary>
    Political,

    /// <summary>Sentiment and market psychology data.</summary>
    Sentiment,

    /// <summary>Sector and industry information.</summary>
    Sector
}

namespace StrategyForge.Domain.Enums;

/// <summary>
/// Types of financial assets supported by StrategyForge.
/// </summary>
public enum AssetType
{
    /// <summary>Individual stock on an exchange.</summary>
    Stock,

    /// <summary>Market index (e.g., TEDPIX).</summary>
    Index,

    /// <summary>Fiat currency pair (e.g., USD/IRR).</summary>
    Currency,

    /// <summary>Commodity (e.g., gold, silver).</summary>
    Commodity,

    /// <summary>Cryptocurrency or stablecoin (e.g., USDT).</summary>
    Crypto,

    /// <summary>Exchange-traded fund.</summary>
    ETF,

    /// <summary>Bond or fixed-income instrument.</summary>
    Bond,

    /// <summary>Other asset type not covered by the above.</summary>
    Other
}

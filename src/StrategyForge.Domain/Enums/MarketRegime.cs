namespace StrategyForge.Domain.Enums;

/// <summary>
/// Current market regime classification.
/// </summary>
public enum MarketRegime
{
    /// <summary>Sustained upward price movement.</summary>
    Uptrend,

    /// <summary>Sustained downward price movement.</summary>
    Downtrend,

    /// <summary>Price moving within a range, no clear trend.</summary>
    Sideways,

    /// <summary>High volatility, unpredictable direction.</summary>
    Volatile,

    /// <summary>Transitioning between regimes.</summary>
    Transitional,

    /// <summary>Unable to determine current regime.</summary>
    Unknown
}

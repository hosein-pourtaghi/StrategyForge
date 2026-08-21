namespace StrategyForge.Domain.Enums;

/// <summary>
/// Market or analysis sentiment classification.
/// </summary>
public enum Sentiment
{
    /// <summary>Positive outlook, expecting upward movement.</summary>
    Bullish,

    /// <summary>Negative outlook, expecting downward movement.</summary>
    Bearish,

    /// <summary>Neutral outlook, no strong directional bias.</summary>
    Neutral,

    /// <summary>Insufficient data to determine sentiment.</summary>
    Unknown
}

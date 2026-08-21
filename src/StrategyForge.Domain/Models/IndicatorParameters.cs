namespace StrategyForge.Domain.Models;

/// <summary>
/// Configuration parameters for indicator computation.
/// Allows indicators to be configured without code changes.
/// </summary>
public sealed record IndicatorParameters
{
    /// <summary>Primary period (e.g., 14 for RSI-14, 20 for SMA-20).</summary>
    public int? Period { get; init; }

    /// <summary>Secondary period (e.g., signal line period for MACD).</summary>
    public int? SecondaryPeriod { get; init; }

    /// <summary>Standard deviation multiplier (e.g., 2.0 for Bollinger Bands).</summary>
    public decimal? StandardDeviation { get; init; }

    /// <summary>Source price for the indicator (e.g., "Close", "HL2", "HLC3").</summary>
    public string? PriceSource { get; init; }

    /// <summary>Additional custom parameters.</summary>
    public IReadOnlyDictionary<string, decimal>? Custom { get; init; }

    /// <summary>Default RSI parameters (14-period, Close price).</summary>
    public static IndicatorParameters DefaultRsi => new() { Period = 14, PriceSource = "Close" };

    /// <summary>Default MACD parameters (12, 26, 9).</summary>
    public static IndicatorParameters DefaultMacd => new()
    {
        Period = 12,
        SecondaryPeriod = 26,
        Custom = new Dictionary<string, decimal> { ["SignalPeriod"] = 9 }
    };

    /// <summary>Default Bollinger Bands parameters (20-period, 2 standard deviations).</summary>
    public static IndicatorParameters DefaultBollinger => new()
    {
        Period = 20,
        StandardDeviation = 2.0m,
        PriceSource = "Close"
    };

    /// <summary>Default ATR parameters (14-period).</summary>
    public static IndicatorParameters DefaultAtr => new() { Period = 14 };
}

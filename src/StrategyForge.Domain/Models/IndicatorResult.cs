namespace StrategyForge.Domain.Models;

/// <summary>
/// Represents the output of a single indicator computation for a specific date.
/// Indicators may produce multiple values per computation (e.g., MACD produces MACD line, Signal line, Histogram).
/// </summary>
public sealed record IndicatorResult
{
    /// <summary>Name of the indicator that produced this result (e.g., "RSI", "MACD").</summary>
    public required string IndicatorName { get; init; }

    /// <summary>The date this result applies to.</summary>
    public required DateOnly Date { get; init; }

    /// <summary>The primary numeric value of the indicator.</summary>
    public required decimal Value { get; init; }

    /// <summary>
    /// Optional signal interpretation (e.g., "Overbought", "Bullish Crossover", "Above Zero").
    /// Human-readable interpretation of the indicator value.
    /// </summary>
    public string? Signal { get; init; }

    /// <summary>
    /// Additional values produced by complex indicators.
    /// For example, MACD might store: { "MACD": 1.23, "Signal": 0.98, "Histogram": 0.25 }
    /// Bollinger Bands might store: { "Upper": 150.5, "Middle": 145.0, "Lower": 139.5, "PercentB": 0.72 }
    /// </summary>
    public IReadOnlyDictionary<string, decimal>? AdditionalValues { get; init; }

    /// <summary>
    /// The period length used for this computation (e.g., 14 for RSI-14).
    /// Useful when the same indicator is computed with different parameters.
    /// </summary>
    public int? Period { get; init; }

    /// <summary>Optional parameters used for this computation.</summary>
    public IndicatorParameters? Parameters { get; init; }
}

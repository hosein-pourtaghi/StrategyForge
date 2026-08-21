using StrategyForge.Domain.Models;

namespace StrategyForge.Domain.Interfaces.Analysis;

/// <summary>
/// Interface for the indicator engine that orchestrates computation of all registered indicators.
/// The engine maintains a registry of available indicators and runs them against candle data.
/// </summary>
public interface IIndicatorEngine
{
    /// <summary>All registered indicators.</summary>
    IReadOnlyList<IIndicator> RegisteredIndicators { get; }

    /// <summary>
    /// Computes all enabled indicators against the provided candle data.
    /// </summary>
    /// <param name="candles">Historical OHLCV data.</param>
    /// <param name="configuration">Optional configuration to enable/disable specific indicators.</param>
    /// <returns>Aggregated indicator results.</returns>
    IndicatorEngineResult ComputeAll(
        IReadOnlyList<Candle> candles,
        IndicatorConfiguration? configuration = null);
}

/// <summary>
/// Configuration for which indicators to run and with what parameters.
/// </summary>
public sealed record IndicatorConfiguration
{
    /// <summary>
    /// Indicators to include. If null, all registered indicators are included.
    /// </summary>
    public IReadOnlyList<string>? EnabledIndicators { get; init; }

    /// <summary>
    /// Indicators to exclude from computation.
    /// </summary>
    public IReadOnlyList<string>? DisabledIndicators { get; init; }

    /// <summary>
    /// Custom parameters for specific indicators.
    /// Key = indicator name, Value = parameters.
    /// </summary>
    public IReadOnlyDictionary<string, IndicatorParameters>? IndicatorParameters { get; init; }
}

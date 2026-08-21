using StrategyForge.Domain.Models;

namespace StrategyForge.Domain.Interfaces.Analysis;

/// <summary>
/// Interface for deterministic technical indicators.
/// Each indicator is a self-contained calculation module with clear inputs and outputs.
/// Indicators do NOT depend on LLM or any external service.
/// 
/// Adding a new indicator = implementing this interface + registering in DI.
/// No other code changes required.
/// </summary>
public interface IIndicator
{
    /// <summary>Short name of the indicator (e.g., "RSI", "MACD", "SMA-20").</summary>
    string Name { get; }

    /// <summary>Human-readable description of what this indicator measures.</summary>
    string Description { get; }

    /// <summary>
    /// Computes the indicator values for the given candle data.
    /// </summary>
    /// <param name="candles">Historical OHLCV data, ordered oldest to newest.</param>
    /// <param name="parameters">Optional parameters (period, source, etc.).</param>
    /// <returns>Indicator results for each date where the indicator could be computed.</returns>
    IReadOnlyList<IndicatorResult> Compute(
        IReadOnlyList<Candle> candles,
        IndicatorParameters? parameters = null);
}

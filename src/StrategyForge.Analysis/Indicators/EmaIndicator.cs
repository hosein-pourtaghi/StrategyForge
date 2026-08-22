using StrategyForge.Domain.Interfaces.Analysis;
using StrategyForge.Domain.Models;

namespace StrategyForge.Analysis.Indicators;

/// <summary>
/// Exponential Moving Average (EMA).
/// 
/// Convention:
/// - First EMA value = SMA of the first 'period' closing prices (standard seed)
/// - Subsequent: EMA = Close * k + previousEMA * (1 - k), where k = 2 / (period + 1)
/// - Uses Close price by default
/// - Period must be > 0
/// - Minimum observations = period
/// </summary>
public sealed class EmaIndicator : IIndicator
{
    public string Name => "EMA";
    public string Description => "Exponential Moving Average — weighted average giving more importance to recent prices.";

    public IReadOnlyList<IndicatorResult> Compute(
        IReadOnlyList<Candle> candles,
        IndicatorParameters? parameters = null)
    {
        var period = parameters?.Period ?? 20;
        if (period <= 0)
            throw new ArgumentException($"EMA period must be > 0, got {period}.", nameof(parameters));
        if (candles.Count < period)
            return [];

        var k = 2.0m / (period + 1);
        var results = new List<IndicatorResult>();

        // Seed: SMA of first 'period' closing prices
        decimal sum = 0;
        for (int i = 0; i < period; i++)
            sum += candles[i].Close;

        var ema = sum / period;
        results.Add(new IndicatorResult
        {
            IndicatorName = Name,
            Date = candles[period - 1].Date,
            Value = Math.Round(ema, 6),
            Period = period,
            Parameters = parameters
        });

        // Subsequent values
        for (int i = period; i < candles.Count; i++)
        {
            ema = candles[i].Close * k + ema * (1 - k);
            results.Add(new IndicatorResult
            {
                IndicatorName = Name,
                Date = candles[i].Date,
                Value = Math.Round(ema, 6),
                Period = period,
                Parameters = parameters
            });
        }

        return results;
    }
}

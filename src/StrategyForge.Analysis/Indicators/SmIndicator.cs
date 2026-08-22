using StrategyForge.Domain.Interfaces.Analysis;
using StrategyForge.Domain.Models;

namespace StrategyForge.Analysis.Indicators;

/// <summary>
/// Simple Moving Average (SMA).
/// SMA = sum of closing prices over period / period
/// 
/// Convention:
/// - Uses Close price by default
/// - Period must be > 0
/// - Minimum observations = period
/// - Results start from the first date where a full period of data is available
/// </summary>
public sealed class SmIndicator : IIndicator
{
    public string Name => "SMA";
    public string Description => "Simple Moving Average — arithmetic mean of closing prices over a configurable period.";

    public IReadOnlyList<IndicatorResult> Compute(
        IReadOnlyList<Candle> candles,
        IndicatorParameters? parameters = null)
    {
        var period = parameters?.Period ?? 20;
        if (period <= 0)
            throw new ArgumentException($"SMA period must be > 0, got {period}.", nameof(parameters));
        if (candles.Count < period)
            return [];

        var results = new List<IndicatorResult>();
        decimal sum = 0;

        for (int i = 0; i < candles.Count; i++)
        {
            sum += candles[i].Close;

            if (i >= period)
                sum -= candles[i - period].Close;

            if (i >= period - 1)
            {
                var sma = sum / period;
                results.Add(new IndicatorResult
                {
                    IndicatorName = Name,
                    Date = candles[i].Date,
                    Value = Math.Round(sma, 6),
                    Period = period,
                    Parameters = parameters
                });
            }
        }

        return results;
    }
}

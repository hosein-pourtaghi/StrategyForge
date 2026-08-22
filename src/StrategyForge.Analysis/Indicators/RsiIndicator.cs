using StrategyForge.Domain.Interfaces.Analysis;
using StrategyForge.Domain.Models;

namespace StrategyForge.Analysis.Indicators;

/// <summary>
/// Relative Strength Index (RSI).
/// 
/// Convention (Wilder's smoothing):
/// - First avg gain/loss = simple average of first 'period' changes
/// - Subsequent: avgGain = (prevAvgGain * (period-1) + currentGain) / period
/// - RS = avgGain / avgLoss
/// - RSI = 100 - (100 / (1 + RS))
/// - If avgLoss == 0, RSI = 100
/// - If avgGain == 0, RSI = 0
/// - Minimum observations = period + 1 (need at least one change)
/// - Default period = 14
/// </summary>
public sealed class RsiIndicator : IIndicator
{
    public string Name => "RSI";
    public string Description => "Relative Strength Index — momentum oscillator measuring speed and magnitude of price changes (0-100).";

    public IReadOnlyList<IndicatorResult> Compute(
        IReadOnlyList<Candle> candles,
        IndicatorParameters? parameters = null)
    {
        var period = parameters?.Period ?? 14;
        if (period <= 0)
            throw new ArgumentException($"RSI period must be > 0, got {period}.", nameof(parameters));
        if (candles.Count < period + 1)
            return [];

        // Calculate price changes
        var changes = new decimal[candles.Count - 1];
        for (int i = 1; i < candles.Count; i++)
            changes[i - 1] = candles[i].Close - candles[i - 1].Close;

        // First average gain/loss: simple average of first 'period' changes
        decimal avgGain = 0, avgLoss = 0;
        for (int i = 0; i < period; i++)
        {
            if (changes[i] > 0) avgGain += changes[i];
            else avgLoss += Math.Abs(changes[i]);
        }
        avgGain /= period;
        avgLoss /= period;

        var results = new List<IndicatorResult>();
        results.Add(CreateRsiResult(candles[period].Date, avgGain, avgLoss, period, parameters));

        // Subsequent values using Wilder's smoothing
        for (int i = period; i < changes.Length; i++)
        {
            var gain = changes[i] > 0 ? changes[i] : 0;
            var loss = changes[i] < 0 ? Math.Abs(changes[i]) : 0;

            avgGain = (avgGain * (period - 1) + gain) / period;
            avgLoss = (avgLoss * (period - 1) + loss) / period;

            results.Add(CreateRsiResult(candles[i + 1].Date, avgGain, avgLoss, period, parameters));
        }

        return results;
    }

    private static IndicatorResult CreateRsiResult(
        DateOnly date, decimal avgGain, decimal avgLoss, int period, IndicatorParameters? parameters)
    {
        decimal rsi;
        if (avgLoss == 0)
            rsi = 100;
        else if (avgGain == 0)
            rsi = 0;
        else
        {
            var rs = avgGain / avgLoss;
            rsi = 100 - (100 / (1 + rs));
        }

        return new IndicatorResult
        {
            IndicatorName = "RSI",
            Date = date,
            Value = Math.Round(rsi, 4),
            Period = period,
            Parameters = parameters
        };
    }
}

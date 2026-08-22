using StrategyForge.Domain.Interfaces.Analysis;
using StrategyForge.Domain.Models;

namespace StrategyForge.Analysis.Indicators;

/// <summary>
/// Bollinger Bands.
/// 
/// Convention:
/// - Middle band = SMA of closing prices
/// - Upper band = Middle + (multiplier × sample standard deviation)
/// - Lower band = Middle - (multiplier × sample standard deviation)
/// - Uses sample standard deviation (N-1 denominator)
/// - Default: period = 20, multiplier = 2.0
/// - Minimum observations = period
/// </summary>
public sealed class BollingerBandsIndicator : IIndicator
{
    public string Name => "BollingerBands";
    public string Description => "Bollinger Bands — volatility indicator with upper/lower bands based on standard deviation from SMA.";

    public IReadOnlyList<IndicatorResult> Compute(
        IReadOnlyList<Candle> candles,
        IndicatorParameters? parameters = null)
    {
        var period = parameters?.Period ?? 20;
        var multiplier = parameters?.StandardDeviation ?? 2.0m;

        if (period <= 0)
            throw new ArgumentException($"Bollinger Bands period must be > 0, got {period}.");
        if (multiplier <= 0)
            throw new ArgumentException($"Bollinger Bands multiplier must be > 0, got {multiplier}.");

        if (candles.Count < period)
            return [];

        var results = new List<IndicatorResult>();

        for (int i = period - 1; i < candles.Count; i++)
        {
            // Calculate SMA and sample standard deviation for the window
            decimal sum = 0;
            for (int j = i - period + 1; j <= i; j++)
                sum += candles[j].Close;

            var sma = sum / period;

            // Sample standard deviation (N-1 denominator)
            decimal sumSqDiff = 0;
            for (int j = i - period + 1; j <= i; j++)
            {
                var diff = candles[j].Close - sma;
                sumSqDiff += diff * diff;
            }
            var stdDev = Math.Sqrt((double)(sumSqDiff / (period - 1)));
            var stdDevDecimal = (decimal)stdDev;

            var upper = sma + multiplier * stdDevDecimal;
            var lower = sma - multiplier * stdDevDecimal;

            // %B = (Price - Lower) / (Upper - Lower)
            var bandwidth = upper - lower;
            var percentB = bandwidth > 0
                ? (candles[i].Close - lower) / bandwidth
                : 0m;

            results.Add(new IndicatorResult
            {
                IndicatorName = Name,
                Date = candles[i].Date,
                Value = Math.Round(sma, 6),
                Period = period,
                Parameters = parameters,
                AdditionalValues = new Dictionary<string, decimal>
                {
                    ["Upper"] = Math.Round(upper, 6),
                    ["Middle"] = Math.Round(sma, 6),
                    ["Lower"] = Math.Round(lower, 6),
                    ["Bandwidth"] = Math.Round(bandwidth, 6),
                    ["PercentB"] = Math.Round(percentB, 6)
                }
            });
        }

        return results;
    }
}

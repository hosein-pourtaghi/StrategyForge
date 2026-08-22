using StrategyForge.Domain.Interfaces.Analysis;
using StrategyForge.Domain.Models;

namespace StrategyForge.Analysis.Indicators;

/// <summary>
/// MACD — Moving Average Convergence Divergence.
/// 
/// Convention:
/// - Fast EMA period (default 12)
/// - Slow EMA period (default 26)
/// - Signal line period (default 9)
/// - MACD Line = Fast EMA - Slow EMA
/// - Signal Line = EMA of MACD Line over signal period
/// - Histogram = MACD Line - Signal Line
/// - EMA initialization: SMA seed (first 'period' values)
/// - Minimum observations = slow period + signal period
/// - Parameters: Period=fast, SecondaryPeriod=slow, Custom["SignalPeriod"]=signal
/// </summary>
public sealed class MacdIndicator : IIndicator
{
    public string Name => "MACD";
    public string Description => "Moving Average Convergence Divergence — trend-following momentum indicator showing relationship between two EMAs.";

    public IReadOnlyList<IndicatorResult> Compute(
        IReadOnlyList<Candle> candles,
        IndicatorParameters? parameters = null)
    {
        var fastPeriod = parameters?.Period ?? 12;
        var slowPeriod = parameters?.SecondaryPeriod ?? 26;
        var signalPeriod = parameters?.Custom?.GetValueOrDefault("SignalPeriod") is decimal sp
            ? (int)sp : 9;

        if (fastPeriod <= 0)
            throw new ArgumentException($"MACD fast period must be > 0, got {fastPeriod}.");
        if (slowPeriod <= 0)
            throw new ArgumentException($"MACD slow period must be > 0, got {slowPeriod}.");
        if (fastPeriod >= slowPeriod)
            throw new ArgumentException($"MACD fast period ({fastPeriod}) must be < slow period ({slowPeriod}).");
        if (signalPeriod <= 0)
            throw new ArgumentException($"MACD signal period must be > 0, got {signalPeriod}.");

        if (candles.Count < slowPeriod + signalPeriod)
            return [];

        // Compute EMA for each candle directly — no array offset bugs
        var fastEma = ComputeEma(candles, fastPeriod);
        var slowEma = ComputeEma(candles, slowPeriod);

        // MACD line: available from index slowPeriod-1 onward
        var macdStart = slowPeriod - 1;
        var macdLine = new decimal[candles.Count - macdStart];
        var macdDates = new DateOnly[candles.Count - macdStart];

        for (int i = macdStart; i < candles.Count; i++)
        {
            macdLine[i - macdStart] = fastEma[i] - slowEma[i];
            macdDates[i - macdStart] = candles[i].Date;
        }

        // Signal line: EMA of MACD line, needs signalPeriod values
        if (macdLine.Length < signalPeriod)
            return [];

        // Seed = SMA of first signalPeriod MACD values
        decimal signalSum = 0;
        for (int i = 0; i < signalPeriod; i++)
            signalSum += macdLine[i];
        var signalEma = signalSum / signalPeriod;

        var results = new List<IndicatorResult>();
        var k = 2.0m / (signalPeriod + 1);

        for (int i = signalPeriod - 1; i < macdLine.Length; i++)
        {
            if (i > signalPeriod - 1)
                signalEma = macdLine[i] * k + signalEma * (1 - k);

            var histogram = macdLine[i] - signalEma;
            results.Add(new IndicatorResult
            {
                IndicatorName = Name,
                Date = macdDates[i],
                Value = Math.Round(macdLine[i], 6),
                Period = fastPeriod,
                Parameters = parameters,
                AdditionalValues = new Dictionary<string, decimal>
                {
                    ["MACD"] = Math.Round(macdLine[i], 6),
                    ["Signal"] = Math.Round(signalEma, 6),
                    ["Histogram"] = Math.Round(histogram, 6)
                }
            });
        }

        return results;
    }

    /// <summary>
    /// Compute EMA value for each candle by absolute index.
    /// Returns array indexed by candle index. Undefined indices (before seed) contain 0.
    /// </summary>
    private static decimal[] ComputeEma(IReadOnlyList<Candle> candles, int period)
    {
        var values = new decimal[candles.Count];

        // Seed: SMA of first 'period' closing prices
        decimal sum = 0;
        for (int i = 0; i < period; i++)
            sum += candles[i].Close;

        var ema = sum / period;
        values[period - 1] = ema;

        var k = 2.0m / (period + 1);
        for (int i = period; i < candles.Count; i++)
        {
            ema = candles[i].Close * k + ema * (1 - k);
            values[i] = ema;
        }
        return values;
    }
}

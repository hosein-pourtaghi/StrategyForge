using StrategyForge.Domain.Interfaces.Analysis;
using StrategyForge.Domain.Models;

namespace StrategyForge.Infrastructure.Services;

/// <summary>
/// Derived-indicator warm-up requirement prober.
///
/// Instead of hardcoding warm-up values, this prober asks the registered
/// <see cref="IIndicator"/> implementations how many observations each one actually
/// needs before it emits its first value, by probing each indicator with a synthetic
/// rising candle series of growing length (exponential growth, capped).
///
/// This keeps the processing pipeline correct when indicator defaults change — e.g.,
/// MACD (slow 26 + signal 9) requires far more history than SMA-20 — without the
/// processing layer duplicating indicator arithmetic.
///
/// The probed requirement is the number of input candles at which the indicator
/// produces its first result. The pipeline then applies the configured
/// <see cref="HistoricalProcessingSettings.WarmupFactor"/> (absorbing EMA convergence)
/// and <see cref="HistoricalProcessingSettings.MaxWarmupRows"/> cap.
///
/// Deterministic: identical registered indicators always probe identically.
/// </summary>
public sealed class IndicatorWarmupProber
{
    private const int MinProbe = 1;
    private const int MaxProbe = 4096;

    private readonly IIndicatorEngine _engine;

    public IndicatorWarmupProber(IIndicatorEngine engine)
    {
        _engine = engine;
    }

    /// <summary>
    /// Probes every registered indicator and returns the maximum number of input
    /// candles any indicator needs before emitting its first value. Returns 0 when
    /// no indicators are registered or none ever emit.
    /// </summary>
    public int GetMaxWarmupRows(IReadOnlyDictionary<string, IndicatorParameters>? indicatorParameters = null)
    {
        var max = 0;

        foreach (var indicator in _engine.RegisteredIndicators)
        {
            var required = ProbeIndicator(indicator, indicatorParameters);
            if (required > max)
            {
                max = required;
            }
        }

        return max;
    }

    /// <summary>
    /// Finds the smallest candle count N (probed exponentially, then binary-searched
    /// within the final bracket) at which the indicator emits at least one result.
    /// </summary>
    private static int ProbeIndicator(
        IIndicator indicator,
        IReadOnlyDictionary<string, IndicatorParameters>? indicatorParameters)
    {
        var parameters = indicatorParameters?.GetValueOrDefault(indicator.Name);

        // Exponential growth to bracket the requirement.
        var low = MinProbe;
        var high = MinProbe;

        while (high <= MaxProbe)
        {
            if (Emits(indicator, high, parameters))
            {
                low = high / 2 + 1; // Previous failing probe length (the bracket lower bound)
                break;
            }

            low = high;
            high *= 2;
        }

        if (high > MaxProbe)
        {
            // Never emits within the probe cap — treat as requiring more than available.
            return int.MaxValue;
        }

        // Binary search inside (low, high).
        while (low < high)
        {
            var mid = low + (high - low) / 2;
            if (Emits(indicator, mid, parameters))
            {
                high = mid;
            }
            else
            {
                low = mid + 1;
            }
        }

        return low;
    }

    private static bool Emits(
        IIndicator indicator,
        int candleCount,
        IndicatorParameters? parameters)
    {
        var candles = CreateSyntheticSeries(candleCount);
        var results = indicator.Compute(candles, parameters);
        return results.Count > 0;
    }

    /// <summary>
    /// Creates a deterministic rising series (dates every 2 days from a fixed anchor —
    /// avoids weekend-gap semantics; only dates matter to indicators).
    /// </summary>
    private static IReadOnlyList<Candle> CreateSyntheticSeries(int count)
    {
        var anchor = new DateOnly(2000, 1, 1);
        var candles = new List<Candle>(count);

        for (var i = 0; i < count; i++)
        {
            var price = 100m + i;
            candles.Add(new Candle
            {
                Date = anchor.AddDays(i * 2),
                Open = price,
                High = price + 1,
                Low = price - 1,
                Close = price,
                Volume = 0
            });
        }

        return candles;
    }
}

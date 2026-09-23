using StrategyForge.Domain.Models;

namespace StrategyForge.Analysis.Strategy;

/// <summary>
/// Extracts deterministic <see cref="MarketFeatures"/> from enriched observations.
///
/// Single source of truth: every indicator-derived feature reads the stored
/// indicator values produced by the <c>IndicatorEngine</c> during Phase 7
/// enrichment — nothing here recomputes an indicator. Missing indicator values
/// yield null features (never zero-fabricated); missing optional inputs
/// (e.g., the previous observation for rising/falling comparisons) also yield
/// null. Volume semantics follow Phase 3/7: Volume = 0 means the provider did
/// not supply volume or no trades occurred, and is never treated as real volume.
///
/// Deterministic: identical input always produces identical output.
/// </summary>
public static class MarketFeatureExtractor
{
    /// <summary>
    /// Extracts features for a chronologically ordered sequence of enriched
    /// observations (oldest first). The sequence must belong to ONE instrument
    /// and ONE source — sources are never merged.
    /// </summary>
    public static IReadOnlyList<MarketFeatures> Extract(IReadOnlyList<EnrichedObservation> observations)
    {
        var result = new List<MarketFeatures>(observations.Count);

        for (var i = 0; i < observations.Count; i++)
        {
            var current = observations[i];
            EnrichedObservation? previous = i > 0 ? observations[i - 1] : null;
            result.Add(Extract(current, previous));
        }

        return result;
    }

    /// <summary>Extracts features for one observation given its predecessor (or null when first).</summary>
    public static MarketFeatures Extract(EnrichedObservation current, EnrichedObservation? previous)
    {
        var indicators = current.Indicators;

        var sma = GetSingle(indicators, "SMA");
        var rsi = GetSingle(indicators, "RSI");

        decimal? macdLine = null, macdSignal = null, macdHistogram = null;
        if (indicators.TryGetValue("MACD", out var macd))
        {
            macdLine = macd.GetValueOrDefault("MACD");
            macdSignal = macd.GetValueOrDefault("Signal");
            macdHistogram = macd.GetValueOrDefault("Histogram");
        }

        decimal? bbUpper = null, bbMiddle = null, bbLower = null, bbBandwidth = null, bbPercentB = null;
        if (indicators.TryGetValue("BollingerBands", out var bb))
        {
            bbUpper = bb.GetValueOrDefault("Upper");
            bbMiddle = bb.GetValueOrDefault("Middle");
            bbLower = bb.GetValueOrDefault("Lower");
            bbBandwidth = bb.GetValueOrDefault("Bandwidth");
            bbPercentB = bb.GetValueOrDefault("PercentB");
        }

        // Bandwidth percent: dimensionless volatility measure derived
        // arithmetically from stored components (no indicator recomputation).
        decimal? bandwidthPercent = null;
        if (bbBandwidth.HasValue && bbMiddle is > 0)
        {
            bandwidthPercent = Math.Round(bbBandwidth.Value / bbMiddle.Value * 100m, 6);
        }

        // (nullable conditional expressions need an explicit decimal? target type —
        // handled by the assignments above; nothing to do here)

        var previousMacdHistogram = previous is not null
            && previous.Indicators.TryGetValue("MACD", out var prevMacd)
            ? prevMacd.GetValueOrDefault("Histogram")
            : (decimal?)null;

        var hasMeaningfulVolume = current.Volume > 0;

        return new MarketFeatures
        {
            ObservationDate = current.ObservationDate,
            Close = current.Close,

            Sma = sma,
            PriceAboveSma = sma.HasValue ? current.Close > sma.Value : null,

            MacdLine = macdLine,
            EmaFastAboveSlow = macdLine.HasValue ? macdLine.Value > 0 : null,

            Rsi = rsi,
            MacdSignal = macdSignal,
            MacdHistogram = macdHistogram,
            MacdAboveSignal = macdLine.HasValue && macdSignal.HasValue
                ? macdLine.Value > macdSignal.Value
                : null,
            HistogramRising = macdHistogram.HasValue && previousMacdHistogram.HasValue
                ? macdHistogram.Value > previousMacdHistogram.Value
                : null,

            BollingerUpper = bbUpper,
            BollingerMiddle = bbMiddle,
            BollingerLower = bbLower,
            BandwidthPercent = bandwidthPercent,
            PercentB = bbPercentB,

            Volume = current.Volume,
            HasMeaningfulVolume = hasMeaningfulVolume
        };
    }

    /// <summary>
    /// Reads a single-value indicator's value. The enrichment pipeline stores
    /// single-value indicators under their own name as the component key.
    /// </summary>
    private static decimal? GetSingle(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, decimal>> indicators,
        string name) =>
        indicators.TryGetValue(name, out var components)
            ? components.GetValueOrDefault(name)
            : null;
}

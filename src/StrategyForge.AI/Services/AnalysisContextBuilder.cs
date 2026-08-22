using StrategyForge.Domain.Models;

namespace StrategyForge.AI.Services;

/// <summary>
/// Builds AnalysisEvidence from canonical market data and indicator results.
/// This is the bridge between the deterministic Analysis Engine and the LLM layer.
/// </summary>
public sealed class AnalysisContextBuilder
{
    /// <summary>
    /// Builds a complete AnalysisEvidence from candles and indicator engine results.
    /// </summary>
    public AnalysisEvidence Build(
        InstrumentMapping instrument,
        IReadOnlyList<Candle> candles,
        IndicatorEngineResult indicatorResult,
        IReadOnlyList<string>? dataSources = null)
    {
        var warnings = new List<string>();
        var missingData = new List<string>();

        // Validate data sufficiency
        if (candles.Count == 0)
        {
            missingData.Add("No candle data available");
        }

        // Extract market context from candles
        decimal? currentPrice = candles.Count > 0 ? candles[^1].Close : null;
        decimal? dailyChangePercent = null;
        long? latestVolume = candles.Count > 0 ? candles[^1].Volume : null;

        if (candles.Count >= 2)
        {
            var prevClose = candles[^2].Close;
            if (prevClose > 0)
                dailyChangePercent = Math.Round((candles[^1].Close - prevClose) / prevClose * 100, 2);
        }

        // Calculate average volume
        decimal? averageVolume = null;
        decimal? volumeRatio = null;
        if (candles.Count >= 20)
        {
            var recentVolumes = candles.Skip(Math.Max(0, candles.Count - 20)).Take(20).Select(c => (decimal)c.Volume);
            averageVolume = Math.Round(recentVolumes.Average(), 0);
            if (averageVolume > 0 && latestVolume.HasValue)
                volumeRatio = Math.Round((decimal)latestVolume.Value / averageVolume.Value, 2);
        }
        else if (candles.Count > 0)
        {
            averageVolume = Math.Round(candles.Average(c => (decimal)c.Volume), 0);
        }

        // Collect warnings from indicator engine
        foreach (var error in indicatorResult.Errors)
        {
            warnings.Add($"{error.IndicatorName}: {error.ErrorMessage}");
        }

        if (indicatorResult.FailedIndicators.Count > 0)
        {
            warnings.Add($"Failed indicators: {string.Join(", ", indicatorResult.FailedIndicators)}");
        }

        // Build the asset
        var asset = new Asset
        {
            Symbol = instrument.Symbol,
            Name = instrument.DisplayName,
            Market = instrument.Exchange,
            AssetType = instrument.AssetClass
        };

        return new AnalysisEvidence
        {
            Asset = asset,
            AssembledAt = DateTimeOffset.UtcNow,
            DataStartDate = candles.Count > 0 ? candles[0].Date : DateOnly.MinValue,
            DataEndDate = candles.Count > 0 ? candles[^1].Date : DateOnly.MinValue,
            CurrentPrice = currentPrice,
            DailyChangePercent = dailyChangePercent,
            LatestVolume = latestVolume,
            AverageVolume = averageVolume,
            VolumeRatio = volumeRatio,
            IndicatorValues = indicatorResult.GetLatestValues(),
            IndicatorHistory = indicatorResult.Results,
            DataSources = dataSources ?? [],
            MissingData = missingData,
            Warnings = warnings
        };
    }
}

using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Models;

namespace StrategyForge.Infrastructure.Services;

/// <summary>
/// Validates data quality for acquired datasets.
/// Performs deterministic, documented quality checks.
/// Quality scores are based on completeness, freshness, and consistency.
/// </summary>
public sealed class DataQualityValidator
{
    /// <summary>
    /// Validates a collection of candles for quality issues.
    /// </summary>
    public DataQuality ValidateCandles(
        IReadOnlyList<Candle> candles,
        DataFreshness? freshness = null)
    {
        if (candles.Count == 0)
        {
            return DataQuality.WithFlags(0, false, QualityFlag.MissingFields, "No candles provided");
        }

        var flags = QualityFlag.None;
        var descriptions = new List<string>();
        var score = 100;

        // Check freshness
        if (freshness != null && !freshness.IsFresh)
        {
            flags |= QualityFlag.Stale;
            descriptions.Add($"Data is stale (age: {freshness.AgeMs}ms, max: {freshness.MaxAllowedAgeMs}ms)");
            score -= 20;
        }

        // Check OHLC consistency
        var invalidCandles = candles.Count(c => !c.IsValid);
        if (invalidCandles > 0)
        {
            flags |= QualityFlag.OhlcInconsistency;
            descriptions.Add($"{invalidCandles} candles have invalid OHLC relationships");
            score -= (int)(20.0 * invalidCandles / candles.Count);
        }

        // Check for missing volume
        var zeroVolumeCount = candles.Count(c => c.Volume == 0);
        if (zeroVolumeCount > 0)
        {
            flags |= QualityFlag.MissingFields;
            descriptions.Add($"{zeroVolumeCount} candles have zero volume");
            score -= (int)(10.0 * zeroVolumeCount / candles.Count);
        }

        // Check chronological ordering
        for (int i = 1; i < candles.Count; i++)
        {
            if (candles[i].Date <= candles[i - 1].Date)
            {
                flags |= QualityFlag.TimestampIssue;
                descriptions.Add($"Candles are not in chronological order at index {i}");
                score -= 15;
                break;
            }
        }

        // Check for duplicate dates
        var duplicateDates = candles
            .GroupBy(c => c.Date)
            .Where(g => g.Count() > 1)
            .ToList();
        if (duplicateDates.Count > 0)
        {
            flags |= QualityFlag.DuplicateRecords;
            descriptions.Add($"{duplicateDates.Count} duplicate dates found");
            score -= 15;
        }

        score = Math.Clamp(score, 0, 100);
        var isComplete = !flags.HasFlag(QualityFlag.MissingFields) && !flags.HasFlag(QualityFlag.OhlcInconsistency);

        return DataQuality.WithFlags(score, isComplete, flags, descriptions.ToArray());
    }

    /// <summary>
    /// Validates a single candle.
    /// </summary>
    public DataQuality ValidateCandle(Candle candle)
    {
        var flags = QualityFlag.None;
        var descriptions = new List<string>();
        var score = 100;

        if (!candle.IsValid)
        {
            flags |= QualityFlag.OhlcInconsistency;
            descriptions.Add("Candle has invalid OHLC relationships");
            score -= 30;
        }

        if (candle.Volume < 0)
        {
            flags |= QualityFlag.InvalidNumeric;
            descriptions.Add("Negative volume detected");
            score -= 20;
        }

        if (candle.Open <= 0 || candle.Close <= 0)
        {
            flags |= QualityFlag.InvalidNumeric;
            descriptions.Add("Non-positive price detected");
            score -= 30;
        }

        score = Math.Clamp(score, 0, 100);
        return DataQuality.WithFlags(score, score >= 70, flags, descriptions.ToArray());
    }

    /// <summary>
    /// Validates currency rate data.
    /// </summary>
    public DataQuality ValidateCurrencyRates(IReadOnlyList<CurrencyRate> rates)
    {
        if (rates.Count == 0)
        {
            return DataQuality.WithFlags(0, false, QualityFlag.MissingFields, "No currency rates provided");
        }

        var flags = QualityFlag.None;
        var descriptions = new List<string>();
        var score = 100;

        var invalidRates = rates.Count(r => r.Rate <= 0);
        if (invalidRates > 0)
        {
            flags |= QualityFlag.InvalidNumeric;
            descriptions.Add($"{invalidRates} rates have non-positive values");
            score -= (int)(30.0 * invalidRates / rates.Count);
        }

        score = Math.Clamp(score, 0, 100);
        return DataQuality.WithFlags(score, score >= 70, flags, descriptions.ToArray());
    }
}

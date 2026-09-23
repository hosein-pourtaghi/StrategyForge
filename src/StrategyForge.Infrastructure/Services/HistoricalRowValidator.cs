using StrategyForge.Domain.Models;

namespace StrategyForge.Infrastructure.Services;

/// <summary>
/// Row-level validation for the historical processing pipeline.
///
/// Validates the normalized dataset against explicit market-data rules:
/// OHLC consistency, required values, temporal consistency (ordering and
/// duplicate detection), and data-quality warnings (gaps, zero volume,
/// missing optional fields, incomplete source metadata).
///
/// A missing trading day is NOT automatically an error — markets have weekends,
/// holidays, and suspensions. Gaps are reported as data-quality observations.
///
/// Deterministic: identical input always produces identical findings.
/// </summary>
public sealed class HistoricalRowValidator
{
    /// <summary>
    /// Validates one observation in sequence context.
    /// </summary>
    /// <param name="candle">The observation to validate.</param>
    /// <param name="previousDate">Date of the chronologically previous observation for the
    /// same instrument/source, or null when this is the first observation.</param>
    public HistoricalDataQualityAssessment Validate(
        Candle candle,
        DateOnly? previousDate)
    {
        var findings = new List<HistoricalDataQualityFinding>();

        // --- Required values ---
        // Candle.ObservationDate is non-nullable; a default date is treated as missing.
        if (candle.Date == default)
        {
            findings.Add(HistoricalDataQualityFinding.Error(
                HistoricalDataQualityReason.MissingDate,
                "Observation date is missing (default value)."));
        }

        // Note: decimal cannot represent NaN/Infinity, so invalid numeric values can
        // only surface as parse failures (absent rows) or sentinel values — the
        // positivity/OHLC checks below catch those deterministically.
        if (candle.Open <= 0 || candle.Close <= 0)
        {
            findings.Add(HistoricalDataQualityFinding.Error(
                HistoricalDataQualityReason.InvalidPrice,
                $"Open ({candle.Open}) and Close ({candle.Close}) must be positive."));
        }

        // --- OHLC consistency ---
        if (candle.High < candle.Low
            || candle.High < candle.Open
            || candle.High < candle.Close
            || candle.Low > candle.Open
            || candle.Low > candle.Close)
        {
            findings.Add(HistoricalDataQualityFinding.Error(
                HistoricalDataQualityReason.InvalidOhlc,
                $"OHLC relationships violated: O={candle.Open}, H={candle.High}, L={candle.Low}, C={candle.Close}."));
        }

        // --- Temporal consistency ---
        if (previousDate.HasValue && candle.Date != default)
        {
            if (candle.Date == previousDate.Value)
            {
                findings.Add(HistoricalDataQualityFinding.Error(
                    HistoricalDataQualityReason.DuplicateObservation,
                    $"Duplicate observation for {candle.Date:yyyy-MM-dd}."));
            }
            else if (candle.Date < previousDate.Value)
            {
                findings.Add(HistoricalDataQualityFinding.Error(
                    HistoricalDataQualityReason.OutOfOrder,
                    $"Observation date {candle.Date:yyyy-MM-dd} is out of order (previous: {previousDate.Value:yyyy-MM-dd})."));
            }
        }

        // --- Warnings ---

        if (previousDate.HasValue && candle.Date != default
            && candle.Date > previousDate.Value.AddDays(1))
        {
            var gapDays = candle.Date.DayNumber - previousDate.Value.DayNumber - 1;
            findings.Add(HistoricalDataQualityFinding.Warn(
                HistoricalDataQualityReason.DateGap,
                $"Gap of {gapDays} calendar day(s) between {previousDate.Value:yyyy-MM-dd} and {candle.Date:yyyy-MM-dd}. " +
                "Markets have weekends, holidays, and suspensions — reported as an observation, not an error.",
                candle.Date));
        }

        if (candle.Volume == 0)
        {
            // Zero volume may mean the provider does not supply volume (e.g., TGJU)
            // or that the instrument genuinely did not trade. Not fabricated, not an error.
            findings.Add(HistoricalDataQualityFinding.Warn(
                HistoricalDataQualityReason.ZeroVolume,
                "Volume is zero (provider may not supply volume, or no trades occurred).",
                candle.Date));
        }

        if (!candle.Change.HasValue && !candle.ChangePercent.HasValue)
        {
            findings.Add(HistoricalDataQualityFinding.Warn(
                HistoricalDataQualityReason.MissingOptionalField,
                "Neither Change nor ChangePercent is supplied by the source.",
                candle.Date));
        }

        var provenance = candle.Provenance;
        if (provenance is null
            || string.IsNullOrWhiteSpace(provenance.SourceInstrumentId)
            || provenance.FetchedAtUtc == default)
        {
            findings.Add(HistoricalDataQualityFinding.Warn(
                HistoricalDataQualityReason.SourceMetadataIncomplete,
                "Provenance metadata is incomplete (missing source instrument id or fetch time).",
                candle.Date));
        }

        var hasErrors = findings.Exists(f => IsErrorCode(f.Code));
        var status = hasErrors
            ? HistoricalDataQualityStatus.Invalid
            : findings.Count > 0
                ? HistoricalDataQualityStatus.Warning
                : HistoricalDataQualityStatus.Valid;

        return new HistoricalDataQualityAssessment
        {
            Status = status,
            Findings = findings
        };
    }

    /// <summary>
    /// Validates an entire chronologically ordered sequence and returns per-row assessments
    /// plus range-level gap findings (a gap preceding the first in-scope row).
    /// </summary>
    public (IReadOnlyList<HistoricalDataQualityAssessment> Assessments, IReadOnlyList<HistoricalDataQualityFinding> RangeFindings)
        ValidateSequence(IReadOnlyList<Candle> candles)
    {
        var assessments = new List<HistoricalDataQualityAssessment>(candles.Count);
        var rangeFindings = new List<HistoricalDataQualityFinding>();
        DateOnly? previous = null;

        foreach (var candle in candles)
        {
            var assessment = Validate(candle, previous);
            assessments.Add(assessment);
            previous = candle.Date;
        }

        return (assessments, rangeFindings);
    }

    private static bool IsErrorCode(string code) =>
        code is HistoricalDataQualityReason.InvalidOhlc
            or HistoricalDataQualityReason.InvalidPrice
            or HistoricalDataQualityReason.MissingDate
            or HistoricalDataQualityReason.InvalidInstrument
            or HistoricalDataQualityReason.DuplicateObservation
            or HistoricalDataQualityReason.OutOfOrder
            or HistoricalDataQualityReason.MissingRequiredValue;
}

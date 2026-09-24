using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Interfaces.Providers;
using StrategyForge.Domain.Models;

namespace StrategyForge.Infrastructure.Services;

/// <summary>
/// Deterministic dataset coverage and quality reports over the stored
/// historical dataset.
///
/// Coverage (Step 5): per instrument/source availability accounting —
/// first/last observation, count, calendar gaps, observations per year.
/// Normal market holidays/weekends are NOT treated as invalid; gaps are
/// reported as observations only, and the report cannot distinguish "no market
/// observation expected" from "potential missing data" (no market calendar
/// exists in the system), so it reports exactly what is stored.
///
/// Quality (Step 6): per instrument/source deterministic quality accounting,
/// reusing the Phase 7 row validator so the report matches the processing
/// pipeline's semantics exactly (same reason codes, same severity rules).
///
/// Bounded memory: rows are read in date-cursor batches, never one unbounded read.
/// </summary>
public sealed class DatasetReportsService
{
    /// <summary>Read batch size for report building. Peak memory scales with this, not dataset size.</summary>
    private const int ReportBatchSize = 1000;

    private readonly IHistoricalDatasetPageReader _pageReader;
    private readonly HistoricalRowValidator _validator;

    public DatasetReportsService(
        IHistoricalDatasetPageReader pageReader,
        HistoricalRowValidator validator)
    {
        _pageReader = pageReader;
        _validator = validator;
    }

    /// <summary>
    /// Builds the deterministic coverage report for the requested combinations.
    /// Rows are read in bounded batches; counts are accumulated incrementally.
    /// </summary>
    public async Task<DatasetCoverageReport> BuildCoverageReportAsync(
        IReadOnlyList<(string InstrumentId, SourceAdapterType Source)> combinations,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var entries = new List<DatasetCoverageEntry>(combinations.Count);

        foreach (var (instrumentId, source) in combinations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            entries.Add(await BuildCoverageEntryAsync(instrumentId, source, from, to, cancellationToken));
        }

        return new DatasetCoverageReport
        {
            From = from,
            To = to,
            Entries = entries
        };
    }

    /// <summary>
    /// Builds the deterministic quality report for the requested combinations.
    /// Re-validates stored rows with <see cref="HistoricalRowValidator"/> so the
    /// accounting matches the Phase 7 processing semantics.
    /// </summary>
    public async Task<DatasetQualityReport> BuildQualityReportAsync(
        IReadOnlyList<(string InstrumentId, SourceAdapterType Source)> combinations,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var entries = new List<DatasetQualitySummary>(combinations.Count);

        foreach (var (instrumentId, source) in combinations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            entries.Add(await BuildQualityEntryAsync(instrumentId, source, from, to, cancellationToken));
        }

        return new DatasetQualityReport
        {
            From = from,
            To = to,
            Entries = entries
        };
    }

    // --- Coverage ---

    private async Task<DatasetCoverageEntry> BuildCoverageEntryAsync(
        string instrumentId,
        SourceAdapterType source,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        DateOnly? first = null;
        DateOnly? last = null;
        var count = 0;
        var gapCount = 0;
        var largestGapDays = 0;
        DateOnly? previous = null;

        await foreach (var candle in ReadCandlesAsync(instrumentId, source, from, to, cancellationToken))
        {
            if (!first.HasValue)
            {
                first = candle.Date;
            }

            last = candle.Date;
            count++;

            if (previous.HasValue)
            {
                var gapDays = candle.Date.DayNumber - previous.Value.DayNumber;
                if (gapDays > 1)
                {
                    gapCount++;
                    if (gapDays > largestGapDays)
                    {
                        largestGapDays = gapDays;
                    }
                }
            }

            previous = candle.Date;
        }

        var averagePerYear = 0m;
        if (first.HasValue && last.HasValue)
        {
            var spanDays = last.Value.DayNumber - first.Value.DayNumber;
            if (spanDays > 0)
            {
                averagePerYear = Math.Round(count * 365.25m / spanDays, 1);
            }
        }

        return new DatasetCoverageEntry
        {
            InstrumentId = instrumentId,
            Source = source,
            FirstObservation = first,
            LastObservation = last,
            ObservationCount = count,
            GapCount = gapCount,
            LargestGapDays = largestGapDays,
            AverageObservationsPerYear = averagePerYear
        };
    }

    // --- Quality ---

    private async Task<DatasetQualitySummary> BuildQualityEntryAsync(
        string instrumentId,
        SourceAdapterType source,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var reasonCounts = new Dictionary<string, int>();
        var total = 0;
        var valid = 0;
        var warning = 0;
        var invalid = 0;
        DateOnly? previous = null;

        await foreach (var candle in ReadCandlesAsync(instrumentId, source, from, to, cancellationToken))
        {
            total++;
            var assessment = _validator.Validate(candle, previous);

            foreach (var finding in assessment.Findings)
            {
                reasonCounts[finding.Code] = reasonCounts.GetValueOrDefault(finding.Code) + 1;
            }

            switch (assessment.Status)
            {
                case HistoricalDataQualityStatus.Valid:
                    valid++;
                    break;
                case HistoricalDataQualityStatus.Warning:
                    warning++;
                    break;
                case HistoricalDataQualityStatus.Invalid:
                    invalid++;
                    break;
            }

            previous = candle.Date;
        }

        return new DatasetQualitySummary
        {
            InstrumentId = instrumentId,
            Source = source,
            From = from,
            To = to,
            TotalRows = total,
            ValidRows = valid,
            WarningRows = warning,
            InvalidRows = invalid,
            InvalidOhlcRows = reasonCounts.GetValueOrDefault(HistoricalDataQualityReason.InvalidOhlc),
            InvalidPriceRows = reasonCounts.GetValueOrDefault(HistoricalDataQualityReason.InvalidPrice),
            DuplicateRows = reasonCounts.GetValueOrDefault(HistoricalDataQualityReason.DuplicateObservation),
            OutOfOrderRows = reasonCounts.GetValueOrDefault(HistoricalDataQualityReason.OutOfOrder),
            MissingDateRows = reasonCounts.GetValueOrDefault(HistoricalDataQualityReason.MissingDate),
            DateGapWarnings = reasonCounts.GetValueOrDefault(HistoricalDataQualityReason.DateGap),
            ZeroVolumeRows = reasonCounts.GetValueOrDefault(HistoricalDataQualityReason.ZeroVolume),
            MissingOptionalFieldWarnings = reasonCounts.GetValueOrDefault(HistoricalDataQualityReason.MissingOptionalField),
            IncompleteProvenanceWarnings = reasonCounts.GetValueOrDefault(HistoricalDataQualityReason.SourceMetadataIncomplete),
            ReasonCounts = reasonCounts
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .ToDictionary(kv => kv.Key, kv => kv.Value)
        };
    }

    // --- Bounded paged reading ---

    /// <summary>
    /// Streams stored candles via the keyset-cursor page reader. Bounded memory:
    /// one page at a time, never the full range.
    /// </summary>
    private async IAsyncEnumerable<Candle> ReadCandlesAsync(
        string instrumentId,
        SourceAdapterType source,
        DateOnly from,
        DateOnly to,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        DateOnly? cursor = null;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var page = await _pageReader.ReadPageAsync(
                instrumentId, source, from, to, cursor, ReportBatchSize, cancellationToken);

            if (page.Count == 0)
            {
                yield break;
            }

            foreach (var candle in page)
            {
                yield return candle;
            }

            cursor = page[^1].Date;

            if (page.Count < ReportBatchSize)
            {
                // Short page — no more rows exist.
                yield break;
            }
        }
    }
}

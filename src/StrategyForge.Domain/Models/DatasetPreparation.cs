using StrategyForge.Domain.Enums;

namespace StrategyForge.Domain.Models;

// ============================================================
// AI-ready dataset — the third data layer.
//
//   RAW       provider observations + provenance (never mutated)
//   ENRICHED  validated observations + deterministic indicators
//   AI-READY  selected structured features/evidence for AI consumption
//
// The AI-ready layer is a PROJECTION of the enriched dataset, never the
// source of truth. Every record stays traceable to its originating
// observation (instrument + source + date + provenance reference).
//
// Leakage protection is structural: <see cref="AiReadyObservation.Features"/>
// may only contain information available at or before the observation date,
// while <see cref="AiReadyObservation.Labels"/> is a separate concept holding
// strictly-future outcomes. Nothing mixes them into one uncontrolled dictionary.
// ============================================================

/// <summary>
/// Deterministic feature-selection policy for AI-ready records.
///
/// Only these fields are projected (Step 11 policy); fields the source or
/// indicator pipeline did not supply remain absent (null), never zero-filled,
/// unless zero has genuine semantic meaning (volume). Indicator values are
/// read from the enriched dataset — the IndicatorEngine remains the single
/// source of truth; nothing here recomputes an indicator.
/// </summary>
public static class AiReadyFeaturePolicy
{
    /// <summary>Ordered feature names included in every AI-ready record.</summary>
    public static IReadOnlyList<string> IncludedFeatures { get; } =
    [
        "Close",
        "Volume",
        "HasMeaningfulVolume",
        "SMA",
        "RSI",
        "MACD",
        "MACD_Signal",
        "MACD_Histogram",
        "Bollinger_Upper",
        "Bollinger_Middle",
        "Bollinger_Lower",
        "Bollinger_BandwidthPercent",
        "Bollinger_PercentB",
        "PriceAboveSMA",
        "EmaFastAboveSlow",
        "MACD_AboveSignal",
        "HistogramRising",
        "TrendRegime",
        "VolatilityRegime",
        "MomentumRegime",
        "MatchedRules"
    ];
}

/// <summary>
/// One chronological context point preceding the target observation.
/// Strictly earlier than the observation date — never current or future data.
/// </summary>
public sealed record AiReadyContextPoint
{
    public required DateOnly Date { get; init; }
    public required decimal Close { get; init; }
}

/// <summary>
/// Deterministic features for one observation — everything here was available
/// at or before <c>ObservationDate</c>. Null means "not supplied by the source
/// or indicator pipeline" and is preserved as null (never fabricated).
/// </summary>
public sealed record AiReadyFeatures
{
    public required decimal Close { get; init; }

    /// <summary>Raw volume; 0 when the provider does not supply volume (Phase 3/7 semantics, never fabricated).</summary>
    public required long Volume { get; init; }

    /// <summary>True only when the source supplied a positive volume.</summary>
    public required bool HasMeaningfulVolume { get; init; }

    public decimal? Sma { get; init; }
    public decimal? Rsi { get; init; }
    public decimal? Macd { get; init; }
    public decimal? MacdSignal { get; init; }
    public decimal? MacdHistogram { get; init; }
    public decimal? BollingerUpper { get; init; }
    public decimal? BollingerMiddle { get; init; }
    public decimal? BollingerLower { get; init; }

    /// <summary>(Upper − Lower) / Middle × 100 — dimensionless volatility measure.</summary>
    public decimal? BollingerBandwidthPercent { get; init; }

    public decimal? BollingerPercentB { get; init; }

    public bool? PriceAboveSma { get; init; }
    public bool? EmaFastAboveSlow { get; init; }
    public bool? MacdAboveSignal { get; init; }
    public bool? HistogramRising { get; init; }
}

/// <summary>
/// Evidence for one matched deterministic rule, with the actual values that
/// produced the match. Unmatched rules are not embedded per-row (the dataset
/// statistics report the match distribution across all rules); matched rules
/// carry their evidence so "why did this match?" is answerable from the record.
/// </summary>
public sealed record AiReadyRuleEvidence
{
    public required string RuleName { get; init; }

    /// <summary>Structured evidence items: actual value + comparison evaluated.</summary>
    public required IReadOnlyList<StrategyEvidenceItem> Items { get; init; }
}

/// <summary>
/// Future outcome labels for one observation — computed strictly from
/// observations AFTER <c>ObservationDate</c> (forward horizons in trading
/// observations, not calendar days). Null means the horizon extended beyond
/// available data; it is never fabricated. Kept structurally separate from
/// <see cref="AiReadyFeatures"/> to prevent look-ahead leakage.
/// </summary>
public sealed record AiReadyLabels
{
    /// <summary>Horizon (in subsequent observations) → Close(T+h)/Close(T) − 1; null when unavailable.</summary>
    public required IReadOnlyDictionary<int, decimal?> ForwardReturns { get; init; }

    /// <summary>max(High(T+1..T+maxHorizon))/Close(T) − 1 over the largest configured horizon; null when unavailable.</summary>
    public decimal? MaxForwardGain { get; init; }

    /// <summary>min(Low(T+1..T+maxHorizon))/Close(T) − 1 over the largest configured horizon; null when unavailable.</summary>
    public decimal? MaxForwardLoss { get; init; }
}

/// <summary>
/// One AI-ready record: a traceable, structured projection of one enriched
/// observation plus deterministic derived evidence and future labels.
/// </summary>
public sealed record AiReadyObservation
{
    // --- Lineage (traceable back to raw provider observations) ---

    /// <summary>StrategyForge canonical instrument ID.</summary>
    public required string InstrumentId { get; init; }

    /// <summary>Provider source. Records from different sources are never merged.</summary>
    public required SourceAdapterType Source { get; init; }

    /// <summary>Observation date (the enriched/raw dataset identity component).</summary>
    public required DateOnly ObservationDate { get; init; }

    /// <summary>Provenance reference from the enriched/raw layer (may be partially unavailable; never fabricated).</summary>
    public required AiReadyProvenanceReference ProvenanceReference { get; init; }

    // --- Features (available at or before the observation date) ---

    public required AiReadyFeatures Features { get; init; }

    public required TrendRegime TrendRegime { get; init; }
    public required VolatilityRegime VolatilityRegime { get; init; }
    public required MomentumRegime MomentumRegime { get; init; }

    /// <summary>Matched deterministic rules with their structured evidence (matched rules only).</summary>
    public required IReadOnlyList<AiReadyRuleEvidence> MatchedRules { get; init; }

    /// <summary>Chronological context strictly before the observation date (oldest first); empty when no prior observations exist.</summary>
    public required IReadOnlyList<AiReadyContextPoint> ContextWindow { get; init; }

    // --- Quality ---

    /// <summary>Quality status inherited from the enriched observation (Valid or Warning; Invalid rows are never projected).</summary>
    public required HistoricalDataQualityStatus QualityStatus { get; init; }

    /// <summary>Warning reason codes inherited from the enriched observation.</summary>
    public required IReadOnlyList<string> WarningCodes { get; init; }

    // --- Labels (strictly future outcomes; separate from Features) ---

    public required AiReadyLabels Labels { get; init; }
}

/// <summary>
/// Traceability reference for one AI-ready record. Points back to the raw
/// observation's identity and provenance so the chain
/// AI-ready → enriched → raw → provider → source endpoint is always answerable.
/// </summary>
public sealed record AiReadyProvenanceReference
{
    /// <summary>Source's native instrument identifier (e.g., TSETMC InsCode, TGJU slug); null when not recorded.</summary>
    public string? SourceInstrumentId { get; init; }

    /// <summary>Source's native symbol; null when not recorded.</summary>
    public string? SourceSymbol { get; init; }

    /// <summary>Endpoint/data type requested from the source; null when not recorded.</summary>
    public string? Endpoint { get; init; }

    /// <summary>Pipeline version that produced the enriched observation.</summary>
    public required string ProcessedBy { get; init; }
}

// ============================================================
// Dataset manifest — metadata about one prepared dataset.
// Written once alongside the export; never embedded per-row.
// ============================================================

/// <summary>Chronological segment boundaries recorded in the manifest (actual observation dates).</summary>
public sealed record DatasetManifestSegment
{
    public required TimeSeriesSegment Segment { get; init; }
    public required DateOnly FirstObservation { get; init; }
    public required DateOnly LastObservation { get; init; }
    public required int Observations { get; init; }
}

/// <summary>
/// Manifest describing one AI-ready dataset export: identity, scope, feature
/// policy, temporal configuration, split boundaries, and quality summary.
/// </summary>
public sealed record AiReadyDatasetManifest
{
    /// <summary>Application-level dataset identifier (fingerprint-based; stable for identical inputs).</summary>
    public required string DatasetId { get; init; }

    /// <summary>Application-level version fingerprint over configuration + feature policy + range + pipeline versions.</summary>
    public required string DatasetVersion { get; init; }

    /// <summary>When this export was created (metadata only — never inside data records).</summary>
    public required DateTimeOffset CreatedAtUtc { get; init; }

    public required IReadOnlyList<string> InstrumentIds { get; init; }
    public required IReadOnlyList<string> Sources { get; init; }

    /// <summary>Inclusive observation-date range covered (actual stored dates are reported separately).</summary>
    public required DateOnly From { get; init; }
    public required DateOnly To { get; init; }

    /// <summary>First/last actual observation dates per instrument/source, from the coverage report.</summary>
    public required IReadOnlyList<DatasetCoverageEntry> Coverage { get; init; }

    public required int RowCount { get; init; }

    /// <summary>Ordered feature names included per the feature-selection policy.</summary>
    public required IReadOnlyList<string> FeatureSet { get; init; }

    /// <summary>Configured historical context window in observations.</summary>
    public required int ContextWindowObservations { get; init; }

    /// <summary>Configured forward horizons (trading observations).</summary>
    public required IReadOnlyList<int> ForwardHorizons { get; init; }

    /// <summary>Chronological Research/Validation/Holdout boundaries (actual observation dates).</summary>
    public required IReadOnlyList<DatasetManifestSegment> SplitSegments { get; init; }

    public required DatasetQualityReport QualitySummary { get; init; }

    /// <summary>Pipeline versions that produced the data (processing + preparation).</summary>
    public required IReadOnlyDictionary<string, string> PipelineVersions { get; init; }
}

// ============================================================
// Coverage report — deterministic availability accounting per
// instrument/source. Normal market holidays/weekends are NOT
// treated as invalid; gaps are reported as observations only.
// ============================================================

/// <summary>Coverage summary for one instrument/source combination.</summary>
public sealed record DatasetCoverageEntry
{
    public required string InstrumentId { get; init; }
    public required SourceAdapterType Source { get; init; }

    /// <summary>First stored observation date; null when no rows exist in range.</summary>
    public DateOnly? FirstObservation { get; init; }

    /// <summary>Last stored observation date; null when no rows exist in range.</summary>
    public DateOnly? LastObservation { get; init; }

    /// <summary>Stored observation count in range.</summary>
    public required int ObservationCount { get; init; }

    /// <summary>Number of calendar-day gaps > 1 day between consecutive observations.</summary>
    public required int GapCount { get; init; }

    /// <summary>Largest calendar-day gap in days (0 when no gap); informational — markets have weekends/holidays.</summary>
    public required int LargestGapDays { get; init; }

    /// <summary>Average observations per calendar year over the covered span (0 when span < 1 day).</summary>
    public required decimal AverageObservationsPerYear { get; init; }
}

/// <summary>Deterministic coverage report across requested instrument/source combinations.</summary>
public sealed record DatasetCoverageReport
{
    public required DateOnly From { get; init; }
    public required DateOnly To { get; init; }
    public required IReadOnlyList<DatasetCoverageEntry> Entries { get; init; }
}

// ============================================================
// Quality report — deterministic quality accounting so we know
// exactly what data the future AI system will consume.
// ============================================================

/// <summary>Quality accounting for one instrument/source combination.</summary>
public sealed record DatasetQualitySummary
{
    public required string InstrumentId { get; init; }
    public required SourceAdapterType Source { get; init; }
    public required DateOnly From { get; init; }
    public required DateOnly To { get; init; }

    public required int TotalRows { get; init; }
    public required int ValidRows { get; init; }

    /// <summary>Rows accepted with warnings (usable, flagged).</summary>
    public required int WarningRows { get; init; }

    /// <summary>Rows rejected by validation (never enriched, never projected).</summary>
    public required int InvalidRows { get; init; }

    // --- Reason-code breakdown (deterministic counts) ---

    public required int InvalidOhlcRows { get; init; }
    public required int InvalidPriceRows { get; init; }
    public required int DuplicateRows { get; init; }
    public required int OutOfOrderRows { get; init; }
    public required int MissingDateRows { get; init; }

    public required int DateGapWarnings { get; init; }

    /// <summary>Rows with zero volume — provider may not supply volume (e.g., TGJU) or no trades; never fabricated.</summary>
    public required int ZeroVolumeRows { get; init; }

    public required int MissingOptionalFieldWarnings { get; init; }
    public required int IncompleteProvenanceWarnings { get; init; }

    /// <summary>All observed reason codes and their counts (errors and warnings), including any not broken out above.</summary>
    public required IReadOnlyDictionary<string, int> ReasonCounts { get; init; }
}

/// <summary>Deterministic quality report across requested instrument/source combinations.</summary>
public sealed record DatasetQualityReport
{
    public required DateOnly From { get; init; }
    public required DateOnly To { get; init; }
    public required IReadOnlyList<DatasetQualitySummary> Entries { get; init; }

    public int TotalRows => Entries.Sum(e => e.TotalRows);
    public int ValidRows => Entries.Sum(e => e.ValidRows);
    public int WarningRows => Entries.Sum(e => e.WarningRows);
    public int InvalidRows => Entries.Sum(e => e.InvalidRows);
}

// ============================================================
// Dataset statistics — descriptive understanding of the dataset.
// NOT trading performance; nothing here proves profitability.
// ============================================================

/// <summary>Forward-return distribution statistics for one horizon.</summary>
public sealed record ForwardReturnDistribution
{
    public required int HorizonObservations { get; init; }

    /// <summary>Records where this horizon's label was measurable.</summary>
    public required int MeasurableCount { get; init; }

    /// <summary>Records where the horizon extended beyond available data.</summary>
    public required int UnavailableCount { get; init; }

    public required int PositiveCount { get; init; }
    public required int NegativeCount { get; init; }
    public required int ZeroCount { get; init; }

    public decimal? Average { get; init; }
    public decimal? Median { get; init; }
    public decimal? Minimum { get; init; }
    public decimal? Maximum { get; init; }
}

/// <summary>
/// Incremental, deterministic accumulator for dataset statistics. Fed once per
/// projected record during streaming (bounded memory: counts only, plus the
/// bounded forward-return sample needed for medians).
/// </summary>
public sealed class DatasetStatisticsAccumulator
{
    private readonly Dictionary<string, int> _regimeTrend = new();
    private readonly Dictionary<string, int> _regimeVolatility = new();
    private readonly Dictionary<string, int> _regimeMomentum = new();
    private readonly Dictionary<string, int> _ruleMatches = new();
    private readonly Dictionary<string, int> _warningCodes = new();

    private readonly Dictionary<string, (int NullCount, int Total)> _missingValues = new();
    private readonly Dictionary<int, List<decimal>> _forwardReturns = new();

    private int _records;
    private int _withMatchedRules;

    /// <summary>Total records accumulated.</summary>
    public int Records => _records;

    /// <summary>Records with at least one matched rule.</summary>
    public int RecordsWithMatchedRules => _withMatchedRules;

    public IReadOnlyDictionary<string, int> TrendRegimeDistribution => _regimeTrend;
    public IReadOnlyDictionary<string, int> VolatilityRegimeDistribution => _regimeVolatility;
    public IReadOnlyDictionary<string, int> MomentumRegimeDistribution => _regimeMomentum;

    /// <summary>Rule name → match count across all evaluated records.</summary>
    public IReadOnlyDictionary<string, int> RuleMatchDistribution => _ruleMatches;

    /// <summary>Warning code → row count.</summary>
    public IReadOnlyDictionary<string, int> WarningCodeCounts => _warningCodes;

    /// <summary>Feature name → (null count, total records) for explicit missing-value rates.</summary>
    public IReadOnlyDictionary<string, (int NullCount, int Total)> MissingValueCounts => _missingValues;

    /// <summary>Accumulates one record. Deterministic and order-independent for counts.</summary>
    public void Add(AiReadyObservation record, IReadOnlyList<string> allRuleNames)
    {
        _records++;

        Bump(_regimeTrend, record.TrendRegime.ToString());
        Bump(_regimeVolatility, record.VolatilityRegime.ToString());
        Bump(_regimeMomentum, record.MomentumRegime.ToString());

        foreach (var name in allRuleNames)
        {
            Bump(_ruleMatches, name, increment: 0);
        }

        if (record.MatchedRules.Count > 0)
        {
            _withMatchedRules++;
        }

        foreach (var matched in record.MatchedRules)
        {
            Bump(_ruleMatches, matched.RuleName);
        }

        foreach (var code in record.WarningCodes)
        {
            Bump(_warningCodes, code);
        }

        TrackMissing("Close", record.Features.Close == 0);
        TrackMissing("SMA", record.Features.Sma is null);
        TrackMissing("RSI", record.Features.Rsi is null);
        TrackMissing("MACD", record.Features.Macd is null);
        TrackMissing("MACD_Signal", record.Features.MacdSignal is null);
        TrackMissing("MACD_Histogram", record.Features.MacdHistogram is null);
        TrackMissing("Bollinger_BandwidthPercent", record.Features.BollingerBandwidthPercent is null);
        TrackMissing("Bollinger_PercentB", record.Features.BollingerPercentB is null);

        foreach (var (horizon, value) in record.Labels.ForwardReturns)
        {
            if (!_forwardReturns.TryGetValue(horizon, out var list))
            {
                list = [];
                _forwardReturns[horizon] = list;
            }

            if (value.HasValue)
            {
                list.Add(value.Value);
            }
        }
    }

    /// <summary>
    /// Builds the deterministic statistics snapshot. Medians are computed from
    /// the accumulated measurable forward-return samples (memory bounded by
    /// the dataset's measurable rows — one decimal per row per horizon).
    /// </summary>
    public DatasetStatistics Build()
    {
        var distributions = _forwardReturns
            .OrderBy(kv => kv.Key)
            .Select(kv =>
            {
                var samples = kv.Value;
                var sorted = samples.OrderBy(x => x).ToList();
                decimal? median = sorted.Count == 0
                    ? null
                    : sorted.Count % 2 == 1
                        ? sorted[sorted.Count / 2]
                        : (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2m;

                return new ForwardReturnDistribution
                {
                    HorizonObservations = kv.Key,
                    MeasurableCount = samples.Count,
                    UnavailableCount = _records - samples.Count,
                    PositiveCount = samples.Count(x => x > 0),
                    NegativeCount = samples.Count(x => x < 0),
                    ZeroCount = samples.Count(x => x == 0),
                    Average = samples.Count == 0 ? null : Math.Round(samples.Average(), 8),
                    Median = median is null ? null : Math.Round(median.Value, 8),
                    Minimum = samples.Count == 0 ? null : sorted[0],
                    Maximum = samples.Count == 0 ? null : sorted[^1]
                };
            })
            .ToList();

        return new DatasetStatistics
        {
            Records = _records,
            RecordsWithMatchedRules = _withMatchedRules,
            TrendRegimeDistribution = SortedCopy(_regimeTrend),
            VolatilityRegimeDistribution = SortedCopy(_regimeVolatility),
            MomentumRegimeDistribution = SortedCopy(_regimeMomentum),
            RuleMatchDistribution = SortedCopy(_ruleMatches),
            WarningCodeCounts = SortedCopy(_warningCodes),
            MissingValueCounts = _missingValues
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .ToDictionary(kv => kv.Key, kv => kv.Value),
            ForwardReturnDistributions = distributions
        };
    }

    private static void Bump(Dictionary<string, int> dict, string key, int increment = 1)
    {
        dict[key] = dict.GetValueOrDefault(key) + increment;
    }

    private void TrackMissing(string feature, bool isMissing)
    {
        var (nulls, total) = _missingValues.TryGetValue(feature, out var state)
            ? state
            : (0, 0);
        _missingValues[feature] = (nulls + (isMissing ? 1 : 0), total + 1);
    }

    private static IReadOnlyDictionary<string, int> SortedCopy(IReadOnlyDictionary<string, int> source) =>
        source.OrderBy(kv => kv.Key, StringComparer.Ordinal).ToDictionary(kv => kv.Key, kv => kv.Value);
}

/// <summary>Deterministic descriptive statistics for one prepared dataset.</summary>
public sealed record DatasetStatistics
{
    public required int Records { get; init; }
    public required int RecordsWithMatchedRules { get; init; }
    public required IReadOnlyDictionary<string, int> TrendRegimeDistribution { get; init; }
    public required IReadOnlyDictionary<string, int> VolatilityRegimeDistribution { get; init; }
    public required IReadOnlyDictionary<string, int> MomentumRegimeDistribution { get; init; }
    public required IReadOnlyDictionary<string, int> RuleMatchDistribution { get; init; }
    public required IReadOnlyDictionary<string, int> WarningCodeCounts { get; init; }

    /// <summary>Feature name → (null count, total records); missing values stay explicit, never fabricated.</summary>
    public required IReadOnlyDictionary<string, (int NullCount, int Total)> MissingValueCounts { get; init; }

    public required IReadOnlyList<ForwardReturnDistribution> ForwardReturnDistributions { get; init; }
}

// ============================================================
// Preparation request / result
// ============================================================

/// <summary>
/// Request to prepare an AI-ready dataset for one canonical instrument and one
/// provider source over an inclusive date range. Sources are never merged.
/// </summary>
public sealed record AiReadyDatasetRequest
{
    public required string InstrumentId { get; init; }
    public required SourceAdapterType Source { get; init; }
    public required DateOnly From { get; init; }
    public required DateOnly To { get; init; }

    /// <summary>Historical context window per record (observations). Null uses the configured default.</summary>
    public int? ContextWindowObservations { get; init; }

    /// <summary>Forward-return horizons (observations). Null uses the configured default (1/5/10).</summary>
    public IReadOnlyList<int>? ForwardHorizons { get; init; }
}

/// <summary>Deterministic result of one AI-ready dataset preparation run.</summary>
public sealed record AiReadyDatasetResult
{
    public required string DatasetId { get; init; }
    public required string DatasetVersion { get; init; }

    public required string InstrumentId { get; init; }
    public required SourceAdapterType Source { get; init; }
    public required DateOnly From { get; init; }
    public required DateOnly To { get; init; }

    public required bool Ok { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }

    /// <summary>Raw rows in range (from the historical dataset).</summary>
    public required int RawRows { get; init; }

    /// <summary>Rows projected into AI-ready records (valid + warning rows with enriched data).</summary>
    public required int ProjectedRows { get; init; }

    /// <summary>Records whose labels were fully measurable for the largest horizon.</summary>
    public required int LabelledRows { get; init; }

    /// <summary>Chronological split boundaries (actual observation dates).</summary>
    public required IReadOnlyList<DatasetManifestSegment> SplitSegments { get; init; }

    /// <summary>Context window used to prepare this dataset (manifest/reproducibility).</summary>
    public required int ContextWindowObservations { get; init; }

    /// <summary>Forward horizons used to prepare this dataset (manifest/reproducibility).</summary>
    public required IReadOnlyList<int> ForwardHorizons { get; init; }

    public required DatasetQualitySummary Quality { get; init; }

    /// <summary>Deterministic first records (bounded sample for human inspection).</summary>
    public required IReadOnlyList<AiReadyObservation> FirstRecords { get; init; }

    /// <summary>Deterministic last records (bounded sample for human inspection).</summary>
    public required IReadOnlyList<AiReadyObservation> LastRecords { get; init; }

    public required DatasetStatistics Statistics { get; init; }

    public required TimeSpan Duration { get; init; }
}

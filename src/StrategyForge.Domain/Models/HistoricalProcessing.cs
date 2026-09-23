using StrategyForge.Domain.Enums;

namespace StrategyForge.Domain.Models;

/// <summary>
/// Data-quality status of one historical market observation under the processing pipeline.
/// Statuses are explicit: nothing is silently accepted or silently dropped.
/// </summary>
public enum HistoricalDataQualityStatus
{
    /// <summary>Observation passed all validation rules.</summary>
    Valid,

    /// <summary>Observation has quality observations (e.g., a gap before it) but is usable.</summary>
    Warning,

    /// <summary>Observation violates a market-data rule and is excluded from derived output.</summary>
    Invalid
}

/// <summary>
/// Machine-readable reason codes for historical market-data quality findings.
/// Specific to historical market data — deliberately not a generic validation framework.
/// </summary>
public static class HistoricalDataQualityReason
{
    // --- Errors (status = Invalid) ---
    public const string InvalidOhlc = "INVALID_OHLC";
    public const string InvalidPrice = "INVALID_PRICE";
    public const string MissingDate = "MISSING_DATE";
    public const string InvalidInstrument = "INVALID_INSTRUMENT";
    public const string DuplicateObservation = "DUPLICATE_OBSERVATION";
    public const string OutOfOrder = "OUT_OF_ORDER";
    public const string MissingRequiredValue = "MISSING_REQUIRED_VALUE";

    // --- Warnings (status = Warning) ---
    public const string DateGap = "DATE_GAP";
    public const string ZeroVolume = "ZERO_VOLUME";
    public const string MissingOptionalField = "MISSING_OPTIONAL_FIELD";
    public const string SourceMetadataIncomplete = "SOURCE_METADATA_INCOMPLETE";
}

/// <summary>
/// One data-quality finding for a single historical observation (or a range of observations).
/// </summary>
public sealed record HistoricalDataQualityFinding
{
    /// <summary>Reason code from <see cref="HistoricalDataQualityReason"/>.</summary>
    public required string Code { get; init; }

    /// <summary>Human-readable explanation of the finding.</summary>
    public required string Message { get; init; }

    /// <summary>Observation date the finding applies to (null for range-level findings such as gaps).</summary>
    public DateOnly? ObservationDate { get; init; }

    public static HistoricalDataQualityFinding Error(string code, string message, DateOnly? date = null) =>
        new() { Code = code, Message = message, ObservationDate = date };

    public static HistoricalDataQualityFinding Warn(string code, string message, DateOnly? date = null) =>
        new() { Code = code, Message = message, ObservationDate = date };
}

/// <summary>
/// Quality assessment of a single historical observation.
/// </summary>
public sealed record HistoricalDataQualityAssessment
{
    public required HistoricalDataQualityStatus Status { get; init; }

    /// <summary>All findings for this observation (errors and warnings).</summary>
    public IReadOnlyList<HistoricalDataQualityFinding> Findings { get; init; } = [];

    public bool IsValid => Status == HistoricalDataQualityStatus.Valid;
    public bool HasErrors => Status == HistoricalDataQualityStatus.Invalid;
}

/// <summary>
/// A request to process the stored historical dataset into a validated,
/// indicator-enriched, analysis-ready dataset for one instrument/source over a range.
/// </summary>
public sealed record HistoricalProcessingRequest
{
    /// <summary>StrategyForge canonical instrument ID (not a provider identifier).</summary>
    public required string InstrumentId { get; init; }

    /// <summary>The provider source to process. Sources are never merged.</summary>
    public required SourceAdapterType Source { get; init; }

    /// <summary>Inclusive start of the range whose derived output should be persisted (Gregorian).</summary>
    public required DateOnly From { get; init; }

    /// <summary>Inclusive end of the range whose derived output should be persisted (Gregorian).</summary>
    public required DateOnly To { get; init; }

    /// <summary>Optional indicator parameters overrides keyed by indicator name.</summary>
    public IReadOnlyDictionary<string, IndicatorParameters>? IndicatorParameters { get; init; }

    public static HistoricalProcessingRequest Create(
        string instrumentId,
        SourceAdapterType source,
        DateOnly from,
        DateOnly to) => new()
    {
        InstrumentId = instrumentId,
        Source = source,
        From = from,
        To = to
    };
}

/// <summary>
/// Deterministic summary of one historical-dataset processing run.
/// All accounting is explicit — the summary explains what happened to the dataset
/// without inspecting individual rows or relying on log messages.
/// </summary>
public sealed record HistoricalProcessingResult
{
    public required string InstrumentId { get; init; }
    public required SourceAdapterType Source { get; init; }
    public required DateOnly RequestedFrom { get; init; }
    public required DateOnly RequestedTo { get; init; }

    /// <summary>Whether the processing run completed without a terminal failure.</summary>
    public required bool Ok { get; init; }

    // --- Dataset reading ---
    /// <summary>Raw rows read from the historical dataset (including warm-up context).</summary>
    public required int RowsRead { get; init; }

    /// <summary>Rows inside the requested output range (excludes warm-up context).</summary>
    public required int RowsInScope { get; init; }

    /// <summary>Rows read purely as indicator warm-up context (before the requested range).</summary>
    public required int WarmupRows { get; init; }

    // --- Validation accounting ---
    /// <summary>Rows accepted (valid or warning-only) as the normalized/validated dataset.</summary>
    public required int RowsAccepted { get; init; }

    /// <summary>Rows rejected by validation (INVALID_* reason codes).</summary>
    public required int RowsRejected { get; init; }

    /// <summary>Rows accepted with at least one warning finding.</summary>
    public required int RowsWithWarnings { get; init; }

    // --- Persistence accounting ---
    /// <summary>Derived (enriched) observations newly written.</summary>
    public required int RowsWritten { get; init; }

    /// <summary>Derived observations whose stored values changed during this run.</summary>
    public required int RowsChanged { get; init; }

    /// <summary>Derived observations that already existed with identical values.</summary>
    public required int RowsUnchanged { get; init; }

    /// <summary>Same-date raw rows collapsed within a processing batch.</summary>
    public required int Duplicates { get; init; }

    /// <summary>Total validation errors encountered (one row may produce several).</summary>
    public required int ValidationErrors { get; init; }

    /// <summary>Total warning findings encountered (one row may produce several).</summary>
    public required int Warnings { get; init; }

    /// <summary>Number of database batches processed (observable batching evidence).</summary>
    public required int BatchesProcessed { get; init; }

    /// <summary>Elapsed wall-clock time of the processing run.</summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>Machine-readable error code when <see cref="Ok"/> is false.</summary>
    public string? ErrorCode { get; init; }

    /// <summary>Human-readable error description when <see cref="Ok"/> is false.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Aggregated reason-code counts across all findings (errors and warnings).</summary>
    public IReadOnlyDictionary<string, int> ReasonCounts { get; init; }
        = new Dictionary<string, int>();

    public static HistoricalProcessingResult Empty(
        HistoricalProcessingRequest request,
        string errorCode,
        string errorMessage) => new()
    {
        InstrumentId = request.InstrumentId,
        Source = request.Source,
        RequestedFrom = request.From,
        RequestedTo = request.To,
        Ok = false,
        RowsRead = 0,
        RowsInScope = 0,
        WarmupRows = 0,
        RowsAccepted = 0,
        RowsRejected = 0,
        RowsWithWarnings = 0,
        RowsWritten = 0,
        RowsChanged = 0,
        RowsUnchanged = 0,
        Duplicates = 0,
        ValidationErrors = 0,
        Warnings = 0,
        BatchesProcessed = 0,
        Duration = TimeSpan.Zero,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage
    };
}

/// <summary>
/// A derived (enriched) observation: the analysis-ready projection of one stored
/// historical observation plus deterministic indicator values.
///
/// Identity mirrors the raw dataset — (canonical instrument, source, observation date) —
/// so re-processing can never create duplicate derived observations.
///
/// Traceability: every enriched observation points back to the underlying raw
/// observation's identity (instrument/source/date) and the pipeline version that
/// produced it, so "where did this value come from" is always answerable.
/// The raw dataset is never mutated; enrichment is a separate derived store.
/// </summary>
public sealed record EnrichedObservation
{
    /// <summary>StrategyForge canonical instrument ID.</summary>
    public required string InstrumentId { get; init; }

    /// <summary>Provider source (never merged across sources).</summary>
    public required SourceAdapterType Source { get; init; }

    /// <summary>Observation date; identical to the raw observation's date.</summary>
    public required DateOnly ObservationDate { get; init; }

    // --- Normalized market data (projection of the validated raw observation) ---

    public required decimal Open { get; init; }
    public required decimal High { get; init; }
    public required decimal Low { get; init; }
    public required decimal Close { get; init; }

    /// <summary>Trading volume; 0 when the provider does not supply volume (never fabricated).</summary>
    public long Volume { get; init; }

    public decimal? Change { get; init; }
    public decimal? ChangePercent { get; init; }

    // --- Deterministic indicator enrichment ---

    /// <summary>Indicator values keyed by indicator name (e.g., "RSI", "MACD").</summary>
    /// <para>
    /// For indicators with additional components (MACD → Signal/Histogram,
    /// BollingerBands → Upper/Lower/Middle), components live in a nested dictionary
    /// keyed by component name; single-value indicators map name → value.
    /// </para>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, decimal>> Indicators { get; init; }
        = new Dictionary<string, IReadOnlyDictionary<string, decimal>>();

    // --- Quality ---

    /// <summary>Quality status of the underlying observation (Valid or Warning only; Invalid rows are not enriched).</summary>
    public HistoricalDataQualityStatus QualityStatus { get; init; } = HistoricalDataQualityStatus.Valid;

    /// <summary>Warning reason codes attached to the underlying observation (empty when Valid).</summary>
    public IReadOnlyList<string> WarningCodes { get; init; } = [];

    // --- Traceability ---

    /// <summary>Pipeline version that produced this derived observation (semver-ish string).</summary>
    public required string ProcessedBy { get; init; }

    /// <summary>When this derived observation was computed.</summary>
    public DateTimeOffset ProcessedAtUtc { get; init; }

    /// <summary>Identity key for derived observations — mirrors the raw dataset identity.</summary>
    public static (string InstrumentId, string Source, DateOnly Date) IdentityKey(
        string instrumentId, SourceAdapterType source, DateOnly date) =>
        (instrumentId, source.ToString(), date);
}

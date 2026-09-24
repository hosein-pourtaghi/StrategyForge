using StrategyForge.Domain.Enums;

namespace StrategyForge.Domain.Models;

// ============================================================
// Strategy Setup — the deterministic output of a Phase 8 setup rule.
// A setup DESCRIBES a historically matched condition set with explicit
// entry/invalidation definitions and risk metadata. It is structured
// evidence for the later AI layer — never an instruction, never an
// order, never LLM output, never provider-coupled.
// ============================================================

/// <summary>Deterministic direction/bias of a setup. Analysis-only vocabulary.</summary>
public enum SetupDirection
{
    /// <summary>Conditions favor upward continuation/reversion.</summary>
    Long,

    /// <summary>Conditions favor downward continuation/reversion.</summary>
    Short,

    /// <summary>Conditions are descriptive without directional bias.</summary>
    Neutral
}

/// <summary>
/// Deterministic invalidation definition for a setup: the measurable condition
/// that, on a LATER observation, removes the setup's premise. This is a
/// definition evaluated by the consumer on subsequent observations — the setup
/// layer never simulates the future and never promises the invalidation.
/// </summary>
public sealed record SetupInvalidation
{
    /// <summary>Deterministic definition of the invalidation condition (e.g., "MACD line below Signal line").</summary>
    public required string Condition { get; init; }

    /// <summary>Feature name(s) the consumer must compare on later observations (e.g., "MACD.Signal").</summary>
    public required IReadOnlyList<string> FeatureNames { get; init; }

    /// <summary>
    /// Optional numeric reference level captured at the setup observation
    /// (e.g., the signal value at match time). Null when the condition is
    /// purely relational and has no meaningful fixed level.
    /// </summary>
    public decimal? ReferenceLevel { get; init; }
}

/// <summary>
/// Deterministic risk/uncertainty metadata captured at the setup observation.
/// Every value is either the actual stored number or null (unavailable) —
/// nothing is fabricated, nothing is a trading recommendation.
/// </summary>
public sealed record SetupRiskMetadata
{
    /// <summary>Stored RSI at the setup observation; null when unavailable.</summary>
    public decimal? Rsi { get; init; }

    /// <summary>Stored Bollinger %B at the setup observation; null when unavailable.</summary>
    public decimal? PercentB { get; init; }

    /// <summary>Bollinger bandwidth percent (volatility state); null when unavailable.</summary>
    public decimal? BandwidthPercent { get; init; }

    /// <summary>Close vs SMA distance in percent ((Close/SMA − 1) × 100); null when SMA unavailable or non-positive.</summary>
    public decimal? CloseVsSmaPercent { get; init; }

    /// <summary>Volatility regime at the setup observation.</summary>
    public VolatilityRegime Volatility { get; init; }
}

/// <summary>
/// A deterministic strategy setup produced by one setup rule at one observation.
///
/// Identity is stable and content-derived: (instrument, source, rule, observation date) —
/// re-running the same rules over the same dataset always yields the same identities
/// and the same setups, in the same order.
///
/// Provenance reuses the Phase 7 dataset identity (instrument + source + observation
/// date) plus the processing pipeline version that produced the underlying enriched
/// observation, so every setup is traceable to the exact stored observation and its
/// indicator values (carried in <see cref="SupportingEvidence"/>).
/// </summary>
public sealed record StrategySetup
{
    /// <summary>Stable deterministic identity: instrument|source|rule|date.</summary>
    public required string SetupId { get; init; }

    /// <summary>Name of the setup rule that produced this setup.</summary>
    public required string RuleName { get; init; }

    /// <summary>StrategyForge canonical instrument ID.</summary>
    public required string InstrumentId { get; init; }

    /// <summary>Provider source (never merged across sources).</summary>
    public required SourceAdapterType Source { get; init; }

    /// <summary>Observation date the setup was evaluated at (Phase 7 dataset identity component).</summary>
    public required DateOnly ObservationDate { get; init; }

    /// <summary>Close price of the underlying observation (snapshot for consumers).</summary>
    public required decimal Close { get; init; }

    /// <summary>Deterministic direction/bias of the setup.</summary>
    public required SetupDirection Direction { get; init; }

    /// <summary>Market regime at the setup observation (compatibility was verified by the rule).</summary>
    public required MarketRegimeSnapshot Regime { get; init; }

    /// <summary>Deterministic definition of the entry condition that qualified.</summary>
    public required string EntryCondition { get; init; }

    /// <summary>Structured evidence: the actual stored values behind every supporting condition.</summary>
    public IReadOnlyList<StrategyEvidenceItem> SupportingEvidence { get; init; } = [];

    /// <summary>Deterministic invalidation definition.</summary>
    public required SetupInvalidation Invalidation { get; init; }

    /// <summary>Risk/uncertainty metadata captured at the setup observation.</summary>
    public required SetupRiskMetadata Risk { get; init; }

    /// <summary>
    /// Required evidence that was unavailable at this observation. A setup is
    /// only produced when this list is empty — it exists so consumers can see
    /// the completeness state; the engine never emits setups with unavailable
    /// required evidence.
    /// </summary>
    public IReadOnlyList<string> UnavailableEvidence { get; init; } = [];

    /// <summary>Phase 7 processing pipeline version of the underlying enriched observation.</summary>
    public required string ProcessedBy { get; init; }

    /// <summary>
    /// Deterministic identity composition. Same inputs → same ID, always.
    /// </summary>
    public static string ComposeId(
        string instrumentId, SourceAdapterType source, string ruleName, DateOnly observationDate) =>
        $"{instrumentId}|{source}|{ruleName}|{observationDate:yyyy-MM-dd}";
}

/// <summary>
/// Deterministic result of one setup-generation run over one instrument/source
/// observation sequence. Accounting is explicit; ordering is stable
/// (observation date ascending, then rule name ordinal).
/// </summary>
public sealed record StrategySetupResult
{
    public required string InstrumentId { get; init; }
    public required SourceAdapterType Source { get; init; }
    public required DateOnly From { get; init; }
    public required DateOnly To { get; init; }

    /// <summary>Observations the setup rules were evaluated against.</summary>
    public required int ObservationsEvaluated { get; init; }

    /// <summary>Setups produced, in deterministic order (date, then rule name).</summary>
    public required IReadOnlyList<StrategySetup> Setups { get; init; }

    /// <summary>Setups per rule name (ordered by rule name ordinal).</summary>
    public required IReadOnlyDictionary<string, int> SetupsPerRule { get; init; }

    /// <summary>Evaluations skipped because required evidence was unavailable (with the missing items).</summary>
    public required IReadOnlyDictionary<string, int> SkippedInsufficientEvidence { get; init; }

    public static StrategySetupResult Empty(
        string instrumentId, SourceAdapterType source, DateOnly from, DateOnly to) => new()
    {
        InstrumentId = instrumentId,
        Source = source,
        From = from,
        To = to,
        ObservationsEvaluated = 0,
        Setups = [],
        SetupsPerRule = new Dictionary<string, int>(),
        SkippedInsufficientEvidence = new Dictionary<string, int>()
    };
}

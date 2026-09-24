using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Models;

namespace StrategyForge.Api.Contracts;

// ============================================================
// Market regime endpoint (GET api/Strategy/regime)
// ============================================================

/// <summary>Query-bound request for the market regime endpoint.</summary>
public sealed record RegimeQuery
{
    /// <summary>Instrument query (canonical ID or resolvable symbol).</summary>
    public string? Instrument { get; init; }

    /// <summary>Provider source (required; sources are never merged).</summary>
    public SourceAdapterType? Source { get; init; }

    /// <summary>Inclusive start date (Gregorian).</summary>
    public DateOnly? From { get; init; }

    /// <summary>Inclusive end date (Gregorian).</summary>
    public DateOnly? To { get; init; }

    /// <summary>Maximum number of regime snapshots to return (most recent first), default 30, max 100.</summary>
    public int Take { get; init; } = 30;
}

/// <summary>One regime snapshot for one observation.</summary>
public sealed record RegimeSnapshotResponse
{
    public DateOnly ObservationDate { get; init; }
    public string Trend { get; init; } = "";
    public string Volatility { get; init; } = "";
    public string Momentum { get; init; } = "";
    public decimal Close { get; init; }
}

/// <summary>Response body for the market regime endpoint.</summary>
public sealed record RegimeResponse
{
    public bool Ok { get; init; }
    public string InstrumentId { get; init; } = "";
    public string Source { get; init; } = "";
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
    public int ObservationsEvaluated { get; init; }
    public IReadOnlyList<RegimeSnapshotResponse> Regimes { get; init; } = [];
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
}

// ============================================================
// Strategy evaluation endpoint (POST api/Strategy/evaluate)
// ============================================================

/// <summary>Request body for the deterministic strategy evaluation endpoint.</summary>
public sealed record StrategyEvaluateRequest
{
    /// <summary>Instrument query (canonical ID or resolvable symbol).</summary>
    public string? Instrument { get; init; }

    /// <summary>Provider source (required; sources are never merged).</summary>
    public SourceAdapterType? Source { get; init; }

    /// <summary>Inclusive start date (Gregorian).</summary>
    public DateOnly? From { get; init; }

    /// <summary>Inclusive end date (Gregorian).</summary>
    public DateOnly? To { get; init; }

    /// <summary>Rule names to evaluate; empty evaluates all registered rules.</summary>
    public IReadOnlyList<string> RuleNames { get; init; } = [];

    /// <summary>Forward-return horizons in trading observations; empty uses 1/5/10.</summary>
    public IReadOnlyList<int> ForwardHorizons { get; init; } = [];

    /// <summary>Maximum matched-observation evidence samples returned. Default 5, max 50.</summary>
    public int MaxEvidenceSamples { get; init; } = 5;
}

/// <summary>Forward-return statistics for one horizon.</summary>
public sealed record ForwardReturnStatsResponse
{
    public int HorizonObservations { get; init; }
    public int MeasurableCount { get; init; }
    public int PositiveOutcomeCount { get; init; }
    public int NegativeOutcomeCount { get; init; }
    public int UnavailableOutcomeCount { get; init; }
    public decimal? AverageForwardReturn { get; init; }
    public decimal? MedianForwardReturn { get; init; }
}

/// <summary>Per-rule evaluation statistics.</summary>
public sealed record StrategyRuleStatisticsResponse
{
    public string RuleName { get; init; } = "";
    public int MatchCount { get; init; }
    public IReadOnlyList<ForwardReturnStatsResponse> ForwardReturns { get; init; } = [];
}

/// <summary>One matched observation with structured evidence and forward returns.</summary>
public sealed record StrategyMatchSampleResponse
{
    public DateOnly ObservationDate { get; init; }
    public RegimeSnapshotResponse Regime { get; init; } = new();
    public decimal Close { get; init; }

    /// <summary>Actual indicator values behind the match (name → component → value).</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, decimal>> Indicators { get; init; }
        = new Dictionary<string, IReadOnlyDictionary<string, decimal>>();

    /// <summary>Per-rule evidence items: actual values and comparisons.</summary>
    public IReadOnlyList<RuleEvaluationResponse> RuleEvaluations { get; init; } = [];

    /// <summary>Horizon → forward return percent (null when unavailable).</summary>
    public IReadOnlyDictionary<string, decimal?> ForwardReturns { get; init; }
        = new Dictionary<string, decimal?>();
}

/// <summary>One rule evaluation with structured evidence.</summary>
public sealed record RuleEvaluationResponse
{
    public string RuleName { get; init; } = "";
    public bool Matched { get; init; }
    public IReadOnlyList<StrategyEvidenceItemResponse> Evidence { get; init; } = [];
}

/// <summary>One structured evidence value for a rule evaluation.</summary>
public sealed record StrategyEvidenceItemResponse
{
    public string Name { get; init; } = "";
    public decimal? Value { get; init; }
    public string? Comparison { get; init; }
}

/// <summary>Response body for the deterministic strategy evaluation endpoint.</summary>
public sealed record StrategyEvaluateResponse
{
    public bool Ok { get; init; }
    public string InstrumentId { get; init; } = "";
    public string Source { get; init; } = "";
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }

    public int ObservationsEvaluated { get; init; }
    public int NoMatchCount { get; init; }
    public int TotalRuleMatches { get; init; }
    public IReadOnlyList<StrategyRuleStatisticsResponse> RuleStatistics { get; init; } = [];
    public IReadOnlyList<StrategyMatchSampleResponse> MatchSamples { get; init; } = [];

    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
}

// ============================================================
// Setup generation endpoint (POST api/Strategy/setups)
// ============================================================

/// <summary>Request body for the deterministic setup generation endpoint.</summary>
public sealed record StrategySetupsRequest
{
    /// <summary>Instrument query (canonical ID or resolvable symbol).</summary>
    public string? Instrument { get; init; }

    /// <summary>Provider source (required; sources are never merged).</summary>
    public SourceAdapterType? Source { get; init; }

    /// <summary>Inclusive start date (Gregorian).</summary>
    public DateOnly? From { get; init; }

    /// <summary>Inclusive end date (Gregorian).</summary>
    public DateOnly? To { get; init; }

    /// <summary>Setup rule names to evaluate; empty evaluates all registered setup rules.</summary>
    public IReadOnlyList<string> RuleNames { get; init; } = [];
}

/// <summary>One deterministic strategy setup (structured, provider-neutral, LLM-free).</summary>
public sealed record StrategySetupResponse
{
    public string SetupId { get; init; } = "";
    public string RuleName { get; init; } = "";
    public string InstrumentId { get; init; } = "";
    public string Source { get; init; } = "";
    public DateOnly ObservationDate { get; init; }
    public string Direction { get; init; } = "";

    public RegimeSnapshotResponse Regime { get; init; } = new();
    public string EntryCondition { get; init; } = "";

    /// <summary>Structured evidence: actual stored values behind every supporting condition.</summary>
    public IReadOnlyList<StrategyEvidenceItemResponse> SupportingEvidence { get; init; } = [];

    public SetupInvalidationResponse Invalidation { get; init; } = new();
    public SetupRiskResponse Risk { get; init; } = new();

    /// <summary>Phase 7 pipeline version of the underlying enriched observation.</summary>
    public string ProcessedBy { get; init; } = "";
}

/// <summary>Deterministic invalidation definition exposed to consumers.</summary>
public sealed record SetupInvalidationResponse
{
    public string Condition { get; init; } = "";

    /// <summary>Feature names the consumer must compare on later observations.</summary>
    public IReadOnlyList<string> FeatureNames { get; init; } = [];

    /// <summary>Numeric reference level captured at the setup observation; null when purely relational.</summary>
    public decimal? ReferenceLevel { get; init; }
}

/// <summary>Deterministic risk/uncertainty metadata (actual values or null — never fabricated).</summary>
public sealed record SetupRiskResponse
{
    public decimal? Rsi { get; init; }
    public decimal? PercentB { get; init; }
    public decimal? BandwidthPercent { get; init; }
    public decimal? CloseVsSmaPercent { get; init; }
    public string Volatility { get; init; } = "";
}

/// <summary>Response body for the setup generation endpoint.</summary>
public sealed record StrategySetupsResponse
{
    public bool Ok { get; init; }
    public string InstrumentId { get; init; } = "";
    public string Source { get; init; } = "";
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
    public int ObservationsEvaluated { get; init; }

    /// <summary>Setups in deterministic order (observation date, then rule name).</summary>
    public IReadOnlyList<StrategySetupResponse> Setups { get; init; } = [];

    /// <summary>Setups per rule name (stable ordering by rule name ordinal).</summary>
    public IReadOnlyDictionary<string, int> SetupsPerRule { get; init; }
        = new Dictionary<string, int>();

    /// <summary>Rule evaluations skipped because required evidence was unavailable.</summary>
    public IReadOnlyDictionary<string, int> SkippedInsufficientEvidence { get; init; }
        = new Dictionary<string, int>();

    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
}

// ============================================================
// Time-series split endpoint (GET api/Strategy/splits)
// ============================================================

/// <summary>Query-bound request for the chronological split endpoint.</summary>
public sealed record SplitQuery
{
    /// <summary>Instrument query (canonical ID or resolvable symbol).</summary>
    public string? Instrument { get; init; }

    /// <summary>Provider source (required; sources are never merged).</summary>
    public SourceAdapterType? Source { get; init; }

    /// <summary>Inclusive start date (Gregorian).</summary>
    public DateOnly? From { get; init; }

    /// <summary>Inclusive end date (Gregorian).</summary>
    public DateOnly? To { get; init; }

    /// <summary>Research fraction (0..1). Default 0.6.</summary>
    public decimal? ResearchFraction { get; init; }

    /// <summary>Validation fraction (0..1). Default 0.2; remainder is holdout.</summary>
    public decimal? ValidationFraction { get; init; }
}

/// <summary>One chronological segment.</summary>
public sealed record SplitSegmentResponse
{
    public string Segment { get; init; } = "";
    public DateOnly FirstObservation { get; init; }
    public DateOnly LastObservation { get; init; }
    public int Observations { get; init; }
}

/// <summary>Response body for the chronological split endpoint.</summary>
public sealed record SplitResponse
{
    public bool Ok { get; init; }
    public string InstrumentId { get; init; } = "";
    public string Source { get; init; } = "";
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
    public int TotalObservations { get; init; }
    public IReadOnlyList<SplitSegmentResponse> Segments { get; init; } = [];
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
}

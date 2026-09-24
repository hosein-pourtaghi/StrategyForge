using StrategyForge.Domain.Models;

namespace StrategyForge.Domain.Interfaces.Analysis;

/// <summary>
/// Phase 8 setup rule contract. A setup rule is a deterministic interpretation
/// of market features and stored indicator values that PRODUCES structured
/// setup definitions (direction, entry, invalidation).
///
/// Like <see cref="IStrategyRule"/>, a setup rule never computes indicators
/// (the IndicatorEngine is the single source of truth), never touches
/// providers/HTTP/LLMs, and never produces trading instructions.
///
/// A rule NEVER produces a setup definition when required evidence is missing:
/// it returns a non-matched evaluation naming the missing items. Unknown is
/// never silently converted to false without being reported as missing.
///
/// Separation of concerns: rules define WHAT qualifies; the consuming engine
/// owns identity, provenance stamping, and risk metadata — rules never see
/// dataset identity or pipeline versions.
/// </summary>
public interface ISetupRule
{
    /// <summary>Stable rule name used in requests, results, and setup identities.</summary>
    string Name { get; }

    /// <summary>Human-readable description of required inputs, entry, exclusion, and invalidation.</summary>
    string Description { get; }

    /// <summary>
    /// Evaluates the rule for one observation. Returns a matched evaluation with
    /// the setup definition when every required input is available, the regime is
    /// compatible, the entry conditions hold, and no exclusion applies; otherwise
    /// a non-matched evaluation recording which required items were missing.
    /// Never throws on missing data and never fabricates values.
    /// </summary>
    SetupRuleEvaluation Evaluate(MarketFeatures features, StrategyThresholds thresholds);
}

/// <summary>
/// Outcome of evaluating one setup rule against one observation.
/// When <see cref="Matched"/> is true, the definition fields describe the setup
/// the engine materializes (identity/provenance/risk are added by the engine).
/// </summary>
public sealed record SetupRuleEvaluation
{
    public required string RuleName { get; init; }

    /// <summary>True when all required inputs were available, the regime is compatible, entry conditions hold, and no exclusion applies.</summary>
    public required bool Matched { get; init; }

    /// <summary>The regime classified at this observation (evidence even when not matched).</summary>
    public required MarketRegimeSnapshot Regime { get; init; }

    /// <summary>Deterministic direction/bias of the qualifying conditions (present when matched).</summary>
    public SetupDirection? Direction { get; init; }

    /// <summary>Deterministic definition of the entry condition that qualified (present when matched).</summary>
    public string? EntryCondition { get; init; }

    /// <summary>Deterministic invalidation definition (present when matched).</summary>
    public SetupInvalidation? Invalidation { get; init; }

    /// <summary>Evidence for every condition the rule evaluates (actual values included).</summary>
    public IReadOnlyList<StrategyEvidenceItem> Evidence { get; init; } = [];

    /// <summary>Required inputs that were unavailable (empty when everything needed was present).</summary>
    public IReadOnlyList<string> MissingInputs { get; init; } = [];

    public static SetupRuleEvaluation CreateMatched(
        string ruleName,
        MarketRegimeSnapshot regime,
        SetupDirection direction,
        string entryCondition,
        IReadOnlyList<StrategyEvidenceItem> evidence,
        SetupInvalidation invalidation) => new()
    {
        RuleName = ruleName,
        Matched = true,
        Regime = regime,
        Direction = direction,
        EntryCondition = entryCondition,
        Invalidation = invalidation,
        Evidence = evidence
    };

    public static SetupRuleEvaluation CreateNotMatched(
        string ruleName,
        MarketRegimeSnapshot regime,
        IReadOnlyList<StrategyEvidenceItem> evidence,
        IReadOnlyList<string> missingInputs) => new()
    {
        RuleName = ruleName,
        Matched = false,
        Regime = regime,
        Evidence = evidence,
        MissingInputs = missingInputs
    };
}

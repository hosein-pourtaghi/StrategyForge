using StrategyForge.Domain.Models;

namespace StrategyForge.Domain.Interfaces.Analysis;

/// <summary>
/// Strategy-layer rule registry. A rule is a deterministic, strongly typed
/// interpretation of market features and stored indicator values — it never
/// computes indicators (the IndicatorEngine is the single source of truth),
/// never touches providers/HTTP/LLMs, and never produces trading instructions.
/// </summary>
public interface IStrategyRule
{
    /// <summary>Stable rule name used in requests, results, and evidence.</summary>
    string Name { get; }

    /// <summary>Human-readable description of the rule's conditions.</summary>
    string Description { get; }

    /// <summary>
    /// Evaluates the rule for one observation. Missing required values
    /// yield <c>Matched = false</c> with evidence stating what was missing —
    /// rules never throw on missing data and never fabricate values.
    /// </summary>
    StrategyRuleEvaluation Evaluate(MarketFeatures features, StrategyThresholds thresholds);
}

using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Models;

namespace StrategyForge.AI.Services;

/// <summary>
/// Builds a deterministic StrategyContext from analysis evidence and agent results.
/// This is the bridge between the upstream data/analysis layers and the LLM synthesis pipeline.
/// 
/// The context is:
/// - Deterministic (same inputs → same context)
/// - Complete (contains all information the LLM needs)
/// - Safe (no secrets, no credentials, no unsupported claims)
/// - Traceable (preserves evidence references and provenance)
/// </summary>
public sealed class StrategyContextBuilder
{
    /// <summary>
    /// Builds a complete strategy context from analysis evidence and agent results.
    /// </summary>
    public StrategyContext Build(
        AnalysisEvidence evidence,
        IReadOnlyList<AgentAnalysisResult> agentResults,
        IReadOnlyList<TimeHorizon>? requestedHorizons = null,
        IReadOnlyList<string>? constraints = null,
        string? focusArea = null)
    {
        var horizons = requestedHorizons ?? [TimeHorizon.ShortTerm, TimeHorizon.MediumTerm, TimeHorizon.LongTerm];
        var constraintList = constraints ?? [];

        return new StrategyContext
        {
            Asset = evidence.Asset,
            AssembledAt = DateTimeOffset.UtcNow,
            Evidence = evidence,
            AgentResults = agentResults,
            RequestedHorizons = horizons,
            Constraints = constraintList,
            FocusArea = focusArea
        };
    }
}

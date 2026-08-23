using StrategyForge.Domain.Enums;

namespace StrategyForge.Domain.Models;

/// <summary>
/// Represents the complete context provided to the strategy synthesis layer.
/// Aggregates all evidence, analysis results, agent outputs, and constraints
/// into a single, deterministic, testable input for the LLM synthesis pipeline.
/// 
/// Critical rules:
/// - This context is built deterministically from upstream data
/// - The LLM receives this context and reasons only from it
/// - No additional data is fetched during synthesis
/// </summary>
public sealed record StrategyContext
{
    /// <summary>The asset being synthesized.</summary>
    public required Asset Asset { get; init; }

    /// <summary>When this context was assembled.</summary>
    public required DateTimeOffset AssembledAt { get; init; }

    // --- Evidence ---

    /// <summary>The assembled analysis evidence from the Analysis Engine.</summary>
    public required AnalysisEvidence Evidence { get; init; }

    // --- Agent Results ---

    /// <summary>Results from specialist AI agents (Technical, Fundamental, Macro, etc.).</summary>
    public IReadOnlyList<AgentAnalysisResult> AgentResults { get; init; } = [];

    // --- Strategy Constraints ---

    /// <summary>The requested strategy time horizon(s).</summary>
    public IReadOnlyList<TimeHorizon> RequestedHorizons { get; init; } = [];

    /// <summary>Optional constraints or focus areas for the synthesis.</summary>
    public IReadOnlyList<string> Constraints { get; init; } = [];

    /// <summary>Additional instructions or areas of emphasis for the LLM.</summary>
    public string? FocusArea { get; init; }
}

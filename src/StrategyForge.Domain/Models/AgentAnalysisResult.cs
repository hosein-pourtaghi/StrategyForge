using StrategyForge.Domain.Enums;

namespace StrategyForge.Domain.Models;

/// <summary>
/// The structured output produced by a specialist AI agent.
/// Each agent (Technical, Fundamental, Macro, etc.) produces one of these.
/// The Strategy Agent receives all of them for synthesis.
/// </summary>
public sealed record AgentAnalysisResult
{
    /// <summary>Name of the agent that produced this result (e.g., "TechnicalAnalyst").</summary>
    public required string AgentName { get; init; }

    /// <summary>The asset this analysis is about.</summary>
    public required string AssetSymbol { get; init; }

    /// <summary>When this analysis was generated.</summary>
    public required DateTimeOffset GeneratedAt { get; init; }

    /// <summary>The overall sentiment/assessment from this agent.</summary>
    public required Sentiment Sentiment { get; init; }

    /// <summary>Confidence level in this analysis (0.0 to 1.0).</summary>
    public required decimal Confidence { get; init; }

    /// <summary>Human-readable summary of the analysis.</summary>
    public required string Summary { get; init; }

    /// <summary>Detailed analysis text.</summary>
    public string? DetailedAnalysis { get; init; }

    /// <summary>Key evidence items supporting this analysis.</summary>
    public IReadOnlyList<EvidenceItem> SupportingEvidence { get; init; } = [];

    /// <summary>Evidence items that contradict or qualify this analysis.</summary>
    public IReadOnlyList<EvidenceItem> ContradictingEvidence { get; init; } = [];

    /// <summary>Key price levels identified by this agent.</summary>
    public IReadOnlyList<PriceLevel> KeyLevels { get; init; } = [];

    /// <summary>Specific risks identified by this agent.</summary>
    public IReadOnlyList<string> IdentifiedRisks { get; init; } = [];

    /// <summary>What additional information would improve this analysis.</summary>
    public IReadOnlyList<string> InformationGaps { get; init; } = [];

    /// <summary>Agent-specific structured data (varies by agent type).</summary>
    public IReadOnlyDictionary<string, string>? AgentSpecificData { get; init; }

    /// <summary>Token usage for this agent's LLM call.</summary>
    public int? TokensUsed { get; init; }

    /// <summary>Duration of the LLM call.</summary>
    public TimeSpan? LlmDuration { get; init; }
}

/// <summary>
/// A significant price level identified during analysis.
/// </summary>
public sealed record PriceLevel
{
    /// <summary>The price value.</summary>
    public required decimal Price { get; init; }

    /// <summary>What this level represents (e.g., "Support", "Resistance", "Stop Loss", "Target").</summary>
    public required string Label { get; init; }

    /// <summary>The time horizon this level is relevant to.</summary>
    public TimeHorizon? TimeHorizon { get; init; }

    /// <summary>Strength or significance of this level (0.0 to 1.0).</summary>
    public decimal? Significance { get; init; }
}

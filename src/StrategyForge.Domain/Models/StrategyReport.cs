namespace StrategyForge.Domain.Models;

/// <summary>
/// The final structured strategy output produced by StrategyForge.
/// This is the primary deliverable presented to the human user for decision-making.
/// 
/// Critical rules:
/// - Every conclusion must be traceable to evidence
/// - Facts must be distinguished from interpretations
/// - Scenarios must be labeled as scenarios, not predictions
/// - Missing information must be explicitly listed
/// - Confidence must be qualified, never presented as certainty
/// </summary>
public sealed record StrategyReport
{
    /// <summary>The asset this strategy is about.</summary>
    public required Asset Asset { get; init; }

    /// <summary>When this report was generated.</summary>
    public required DateTimeOffset GeneratedAt { get; init; }

    /// <summary>How fresh the underlying data is.</summary>
    public required DateTimeOffset DataAsOf { get; init; }

    /// <summary>High-level executive summary.</summary>
    public required ExecutiveSummary ExecutiveSummary { get; init; }

    /// <summary>Current market context.</summary>
    public required MarketContext MarketContext { get; init; }

    // --- Specialist Agent Sections ---

    /// <summary>Technical analysis section.</summary>
    public AgentAnalysisResult? TechnicalAnalysis { get; init; }

    /// <summary>Fundamental analysis section.</summary>
    public AgentAnalysisResult? FundamentalAnalysis { get; init; }

    /// <summary>Macro/economic analysis section.</summary>
    public AgentAnalysisResult? MacroAnalysis { get; init; }

    /// <summary>News analysis section.</summary>
    public AgentAnalysisResult? NewsAnalysis { get; init; }

    /// <summary>Political/risk analysis section.</summary>
    public AgentAnalysisResult? PoliticalRiskAnalysis { get; init; }

    /// <summary>Risk analysis section.</summary>
    public AgentAnalysisResult? RiskAnalysis { get; init; }

    // --- Scenarios ---

    /// <summary>The bullish (optimistic) scenario.</summary>
    public Scenario? BullishScenario { get; init; }

    /// <summary>The base (most likely) scenario.</summary>
    public Scenario? BaseScenario { get; init; }

    /// <summary>The bearish (pessimistic) scenario.</summary>
    public Scenario? BearishScenario { get; init; }

    // --- Strategy Sections by Time Horizon ---

    /// <summary>Short-term strategy (days to weeks).</summary>
    public StrategySection? ShortTermStrategy { get; init; }

    /// <summary>Medium-term strategy (weeks to months).</summary>
    public StrategySection? MediumTermStrategy { get; init; }

    /// <summary>Long-term strategy (months to years).</summary>
    public StrategySection? LongTermStrategy { get; init; }

    // --- Risk and Confidence ---

    /// <summary>Risk/reward assessment.</summary>
    public RiskRewardAssessment? RiskReward { get; init; }

    /// <summary>Confidence assessment.</summary>
    public ConfidenceAssessment? Confidence { get; init; }

    // --- Evidence Traceability ---

    /// <summary>Evidence items supporting the overall strategy.</summary>
    public IReadOnlyList<EvidenceItem> SupportingEvidence { get; init; } = [];

    /// <summary>Evidence items that contradict or qualify the strategy.</summary>
    public IReadOnlyList<EvidenceItem> ContradictingEvidence { get; init; } = [];

    /// <summary>Information that was unavailable or missing during analysis.</summary>
    public IReadOnlyList<string> MissingInformation { get; init; } = [];

    /// <summary>Conditions that would invalidate the overall thesis.</summary>
    public IReadOnlyList<string> InvalidationConditions { get; init; } = [];

    /// <summary>What should be monitored next and when.</summary>
    public IReadOnlyList<string> MonitoringRecommendations { get; init; } = [];

    // --- Metadata ---

    /// <summary>Names of all specialist agents that contributed to this report.</summary>
    public IReadOnlyList<string> ContributingAgents { get; init; } = [];

    /// <summary>Names of data providers used for this report.</summary>
    public IReadOnlyList<string> DataProvidersUsed { get; init; } = [];

    /// <summary>LLM model used for agent analysis.</summary>
    public string? LlmModel { get; init; }

    /// <summary>Total tokens consumed during generation.</summary>
    public int? TotalTokensUsed { get; init; }

    /// <summary>Total time to generate this report.</summary>
    public TimeSpan? GenerationDuration { get; init; }

    /// <summary>Pipeline execution state indicating success, partial, or failure.</summary>
    public Enums.PipelineState PipelineState { get; init; } = Enums.PipelineState.Completed;

    /// <summary>Structured diagnostics about the pipeline execution.</summary>
    public PipelineDiagnostics? Diagnostics { get; init; }
}

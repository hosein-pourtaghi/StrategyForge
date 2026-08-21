namespace StrategyForge.Domain.Models;

/// <summary>
/// Assesses the overall confidence in the generated strategy.
/// Must be honest about uncertainty — never overstate confidence.
/// </summary>
public sealed record ConfidenceAssessment
{
    /// <summary>
    /// Overall confidence level (0.0 to 1.0).
    /// 0.0 = completely uncertain, 1.0 = very high confidence.
    /// In practice, financial analysis rarely exceeds 0.7.
    /// </summary>
    public required decimal OverallConfidence { get; init; }

    /// <summary>Text description of the confidence level (e.g., "Moderate", "Low-Moderate").</summary>
    public required string Level { get; init; }

    /// <summary>Factors that increase confidence in this strategy.</summary>
    public IReadOnlyList<string> ConfidenceFactors { get; init; } = [];

    /// <summary>Factors that decrease confidence in this strategy.</summary>
    public IReadOnlyList<string> UncertaintyFactors { get; init; } = [];

    /// <summary>What additional information would increase confidence.</summary>
    public IReadOnlyList<string> InformationThatWouldHelp { get; init; } = [];

    /// <summary>How many data sources contributed to this analysis.</summary>
    public int DataSourcesUsed { get; init; }

    /// <summary>How many specialist agents contributed.</summary>
    public int AgentsContributed { get; init; }
}

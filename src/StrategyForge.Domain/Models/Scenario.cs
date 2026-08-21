namespace StrategyForge.Domain.Models;

/// <summary>
/// Represents a market scenario (bullish, base, or bearish).
/// The Strategy Agent constructs these from specialist agent outputs.
/// </summary>
public sealed record Scenario
{
    /// <summary>Scenario name (e.g., "Bullish", "Base", "Bearish").</summary>
    public required string Name { get; init; }

    /// <summary>Human-readable description of what this scenario assumes.</summary>
    public required string Description { get; init; }

    /// <summary>The key assumptions underlying this scenario.</summary>
    public IReadOnlyList<string> Assumptions { get; init; } = [];

    /// <summary>Evidence items supporting this scenario.</summary>
    public IReadOnlyList<EvidenceItem> SupportingEvidence { get; init; } = [];

    /// <summary>Evidence items that weaken this scenario.</summary>
    public IReadOnlyList<EvidenceItem> WeakeningEvidence { get; init; } = [];

    /// <summary>
    /// Qualitative probability assessment (e.g., "Most likely", "Possible", "Unlikely").
    /// This is NOT a precise probability — it is a qualitative judgment.
    /// </summary>
    public string? ProbabilityAssessment { get; init; }

    /// <summary>Expected outcome if this scenario plays out.</summary>
    public string? ExpectedOutcome { get; init; }

    /// <summary>Conditions that would confirm this scenario is playing out.</summary>
    public IReadOnlyList<string> ConfirmationConditions { get; init; } = [];

    /// <summary>Conditions that would invalidate this scenario.</summary>
    public IReadOnlyList<string> InvalidationConditions { get; init; } = [];
}

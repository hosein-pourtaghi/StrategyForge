using StrategyForge.Domain.Enums;

namespace StrategyForge.Domain.Models;

/// <summary>
/// A single evidence item that supports or contradicts a strategy conclusion.
/// Critical for traceability — every conclusion should be backed by evidence.
/// </summary>
public sealed record EvidenceItem
{
    /// <summary>The content of this evidence (what it says).</summary>
    public required string Content { get; init; }

    /// <summary>The type of evidence (Fact, Calculation, Interpretation, Scenario, Uncertain).</summary>
    public required EvidenceType Type { get; init; }

    /// <summary>The source of this evidence (e.g., "TSETMC", "RSI Calculation", "AI Analysis").</summary>
    public required string Source { get; init; }

    /// <summary>When this evidence was generated or reported.</summary>
    public DateTimeOffset? Timestamp { get; init; }

    /// <summary>Confidence in this evidence item (0.0 to 1.0).</summary>
    public decimal Confidence { get; init; }

    /// <summary>The time horizon this evidence is most relevant to.</summary>
    public TimeHorizon? RelevantHorizon { get; init; }
}

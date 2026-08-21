namespace StrategyForge.Domain.Enums;

/// <summary>
/// Classifies the nature of evidence items in a StrategyReport.
/// Critical for distinguishing facts from interpretations.
/// </summary>
public enum EvidenceType
{
    /// <summary>Verified data point from a reliable source.</summary>
    Fact,

    /// <summary>Deterministic calculation from raw data (e.g., RSI value).</summary>
    Calculation,

    /// <summary>AI interpretation or reasoning over evidence.</summary>
    Interpretation,

    /// <summary>Hypothetical construction (e.g., "if X happens, then Y").</summary>
    Scenario,

    /// <summary>Insufficient or unavailable data.</summary>
    Uncertain
}

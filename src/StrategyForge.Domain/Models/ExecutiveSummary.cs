using StrategyForge.Domain.Enums;

namespace StrategyForge.Domain.Models;

/// <summary>
/// A high-level executive summary of the strategy analysis.
/// Provides the key takeaway without requiring the reader to go through the full report.
/// </summary>
public sealed record ExecutiveSummary
{
    /// <summary>Overall market sentiment assessment.</summary>
    public required Sentiment OverallSentiment { get; init; }

    /// <summary>One-paragraph summary of the strategy recommendation.</summary>
    public required string Summary { get; init; }

    /// <summary>The most important takeaway from this analysis.</summary>
    public string? KeyTakeaway { get; init; }

    /// <summary>The single most important level to watch.</summary>
    public string? CriticalLevel { get; init; }

    /// <summary>Urgency assessment (e.g., "Action needed soon", "Monitor", "No rush").</summary>
    public string? Urgency { get; init; }
}

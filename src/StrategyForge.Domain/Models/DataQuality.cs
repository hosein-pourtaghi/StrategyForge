using StrategyForge.Domain.Enums;

namespace StrategyForge.Domain.Models;

/// <summary>
/// Measures and records data quality for acquired datasets.
/// Quality scores are based on deterministic, documented criteria.
/// </summary>
public sealed record DataQuality
{
    /// <summary>
    /// Quality score from 0-100 based on completeness, freshness, and consistency.
    /// Computed deterministically — never arbitrary.
    /// </summary>
    public required int Score { get; init; }

    /// <summary>Whether the dataset is complete (no missing required fields).</summary>
    public required bool IsComplete { get; init; }

    /// <summary>Active quality flags indicating specific issues.</summary>
    public QualityFlag Flags { get; init; } = QualityFlag.None;

    /// <summary>Whether this data has been cross-validated against another source.</summary>
    public bool CrossValidated { get; init; }

    /// <summary>Human-readable descriptions of quality issues.</summary>
    public IReadOnlyList<string> FlagDescriptions { get; init; } = [];

    /// <summary>A perfect quality score with no issues.</summary>
    public static DataQuality Perfect => new()
    {
        Score = 100,
        IsComplete = true,
        Flags = QualityFlag.None
    };

    /// <summary>Create a quality assessment with specific flags.</summary>
    public static DataQuality WithFlags(int score, bool isComplete, QualityFlag flags, params string[] descriptions) => new()
    {
        Score = Math.Clamp(score, 0, 100),
        IsComplete = isComplete,
        Flags = flags,
        FlagDescriptions = descriptions
    };
}

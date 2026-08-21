namespace StrategyForge.Domain.Models;

/// <summary>
/// Assesses the risk/reward profile of the proposed strategy.
/// </summary>
public sealed record RiskRewardAssessment
{
    /// <summary>Estimated potential upside (as a percentage or price range).</summary>
    public string? PotentialUpside { get; init; }

    /// <summary>Estimated potential downside (as a percentage or price range).</summary>
    public string? PotentialDownside { get; init; }

    /// <summary>
    /// Risk/reward ratio (e.g., "1:2" means risking 1 unit to gain 2).
    /// This is an estimate, not a guarantee.
    /// </summary>
    public string? RiskRewardRatio { get; init; }

    /// <summary>Overall risk level assessment (e.g., "Low", "Moderate", "High", "Very High").</summary>
    public string? RiskLevel { get; init; }

    /// <summary>Key factors driving the risk assessment.</summary>
    public IReadOnlyList<string> KeyRiskFactors { get; init; } = [];

    /// <summary>Factors that could improve the risk/reward profile.</summary>
    public IReadOnlyList<string> FavorableFactors { get; init; } = [];

    /// <summary>Factors that could worsen the risk/reward profile.</summary>
    public IReadOnlyList<string> UnfavorableFactors { get; init; } = [];
}

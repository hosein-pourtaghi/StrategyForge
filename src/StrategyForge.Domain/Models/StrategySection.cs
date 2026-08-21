using StrategyForge.Domain.Enums;

namespace StrategyForge.Domain.Models;

/// <summary>
/// A strategy section for a specific time horizon (short-term, medium-term, long-term).
/// Contains actionable strategy components for that horizon.
/// </summary>
public sealed record StrategySection
{
    /// <summary>The time horizon this section addresses.</summary>
    public required TimeHorizon TimeHorizon { get; init; }

    /// <summary>
    /// Description of possible entry scenario(s).
    /// May describe conditions, price zones, or triggers for entering a position.
    /// </summary>
    public string? EntryScenario { get; init; }

    /// <summary>Price zone or level where entry might be considered.</summary>
    public IReadOnlyList<string> EntryZones { get; init; } = [];

    /// <summary>Conditions that should be confirmed before acting.</summary>
    public IReadOnlyList<string> ConfirmationConditions { get; init; } = [];

    /// <summary>Stop-loss or invalidation level for this strategy.</summary>
    public string? StopInvalidation { get; init; }

    /// <summary>Target price levels or zones.</summary>
    public IReadOnlyList<string> TargetLevels { get; init; } = [];

    /// <summary>Conditions for exiting the position.</summary>
    public string? ExitConditions { get; init; }

    /// <summary>Risk assessment for this time horizon.</summary>
    public string? RiskAssessment { get; init; }

    /// <summary>Key actions to monitor for this horizon.</summary>
    public IReadOnlyList<string> MonitoringActions { get; init; } = [];
}

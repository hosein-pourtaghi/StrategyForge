namespace StrategyForge.Infrastructure.Data.Entities;

/// <summary>
/// EF Core entity for IntelligenceRun tracking.
/// </summary>
public sealed class IntelligenceRunEntity
{
    public Guid Id { get; set; }

    /// <summary>When the run was scheduled.</summary>
    public DateTimeOffset ScheduledAt { get; set; }

    /// <summary>When the run started.</summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>When the run completed.</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Run state as string.</summary>
    public required string State { get; set; }

    /// <summary>JSON array of target asset symbols.</summary>
    public required string TargetAssetsJson { get; set; }

    /// <summary>Number of successful assets.</summary>
    public int SuccessfulAssets { get; set; }

    /// <summary>Number of failed assets.</summary>
    public int FailedAssets { get; set; }

    /// <summary>JSON array of evidence record IDs.</summary>
    public string? EvidenceIdsJson { get; set; }

    /// <summary>JSON array of strategy record IDs.</summary>
    public string? StrategyIdsJson { get; set; }

    /// <summary>Whether strategy generation was requested.</summary>
    public bool GenerateStrategies { get; set; }

    /// <summary>Error message if failed.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Total tokens consumed.</summary>
    public int TotalTokensUsed { get; set; }

    /// <summary>Total duration in milliseconds.</summary>
    public long? TotalDurationMs { get; set; }
}

namespace StrategyForge.Domain.Models;

/// <summary>
/// Tracks a single background intelligence run — an automated collection
/// of data, evidence assembly, and optional strategy generation for one or more assets.
/// 
/// Intelligence runs are scheduled by the Background Intelligence Engine
/// and produce PersistedEvidence and optionally PersistedStrategy records.
/// 
/// Critical rules:
/// - Each run has a unique ID for correlation
/// - Run state progresses through: Scheduled → Running → Completed/Failed
/// - Partial completion is tracked (some assets succeed, others fail)
/// - All timing and error information is preserved for observability
/// </summary>
public sealed record IntelligenceRun
{
    /// <summary>Unique identifier for this intelligence run.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>When this run was scheduled.</summary>
    public required DateTimeOffset ScheduledAt { get; init; }

    /// <summary>When this run actually started executing.</summary>
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>When this run completed or failed.</summary>
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>The current state of this run.</summary>
    public IntelligenceRunState State { get; init; } = IntelligenceRunState.Scheduled;

    /// <summary>Assets targeted for this run.</summary>
    public IReadOnlyList<string> TargetAssets { get; init; } = [];

    /// <summary>Number of assets successfully processed.</summary>
    public int SuccessfulAssets { get; init; }

    /// <summary>Number of assets that failed processing.</summary>
    public int FailedAssets { get; init; }

    /// <summary>IDs of evidence records produced by this run.</summary>
    public IReadOnlyList<Guid> EvidenceIds { get; init; } = [];

    /// <summary>IDs of strategy records produced by this run (if strategy generation was requested).</summary>
    public IReadOnlyList<Guid> StrategyIds { get; init; } = [];

    /// <summary>Whether strategy generation was requested for this run.</summary>
    public bool GenerateStrategies { get; init; }

    /// <summary>Error message if the run failed.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Total tokens consumed across all LLM calls in this run.</summary>
    public int TotalTokensUsed { get; init; }

    /// <summary>Total duration of the run.</summary>
    public TimeSpan? TotalDuration { get; init; }
}

/// <summary>
/// State of a background intelligence run.
/// </summary>
public enum IntelligenceRunState
{
    /// <summary>Run is scheduled but has not started.</summary>
    Scheduled = 0,

    /// <summary>Run is currently executing.</summary>
    Running = 1,

    /// <summary>Run completed successfully (all or some assets).</summary>
    Completed = 2,

    /// <summary>Run completed with partial results (some assets failed).</summary>
    PartiallyCompleted = 3,

    /// <summary>Run failed completely.</summary>
    Failed = 4,

    /// <summary>Run was cancelled before completion.</summary>
    Cancelled = 5
}

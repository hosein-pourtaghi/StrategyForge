namespace StrategyForge.Domain.Enums;

/// <summary>
/// Represents the execution state of the strategy generation pipeline.
/// Distinguishes between successful completion, partial results, failures, and cancellation.
/// </summary>
public enum PipelineState
{
    /// <summary>Pipeline has not started yet.</summary>
    NotStarted = 0,

    /// <summary>Pipeline is currently executing.</summary>
    Running = 1,

    /// <summary>Pipeline completed successfully with all components.</summary>
    Completed = 2,

    /// <summary>Pipeline completed but some specialist agents or data sources failed.</summary>
    CompletedWithWarnings = 3,

    /// <summary>Pipeline completed with partial results — some critical data was unavailable.</summary>
    PartiallyCompleted = 4,

    /// <summary>Pipeline failed — no usable strategy could be produced.</summary>
    Failed = 5,

    /// <summary>Pipeline was cancelled by the caller.</summary>
    Cancelled = 6
}

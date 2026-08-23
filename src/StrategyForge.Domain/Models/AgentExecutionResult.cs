using StrategyForge.Domain.Enums;

namespace StrategyForge.Domain.Models;

/// <summary>
/// Wraps an AgentAnalysisResult with execution metadata.
/// Distinguishes between: successful analysis, agent failure, timeout, cancellation.
/// These are NOT equivalent to Sentiment.Unknown with Confidence=0.
/// </summary>
public sealed record AgentExecutionResult
{
    /// <summary>The agent's analysis result (null if agent failed or was cancelled).</summary>
    public AgentAnalysisResult? Result { get; init; }

    /// <summary>The name of the agent that was executed.</summary>
    public required string AgentName { get; init; }

    /// <summary>How the agent execution went.</summary>
    public required AgentExecutionStatus Status { get; init; }

    /// <summary>Duration of the agent execution including LLM call.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Error message if the agent failed (null on success).</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>When this agent started execution.</summary>
    public DateTimeOffset StartedAt { get; init; }

    /// <summary>When this agent finished execution.</summary>
    public DateTimeOffset CompletedAt { get; init; }
}

/// <summary>
/// Execution status for an individual specialist agent.
/// Must be distinguishable from neutral/unknown analysis.
/// </summary>
public enum AgentExecutionStatus
{
    /// <summary>Agent completed successfully and produced analysis.</summary>
    Success = 0,

    /// <summary>Agent completed but the evidence was insufficient for meaningful analysis.</summary>
    InsufficientEvidence = 1,

    /// <summary>Agent execution failed (LLM error, validation error, etc.).</summary>
    Failed = 2,

    /// <summary>Agent execution timed out.</summary>
    Timeout = 3,

    /// <summary>Agent execution was cancelled.</summary>
    Cancelled = 4,

    /// <summary>Agent was not executed (e.g., not registered).</summary>
    NotExecuted = 5
}

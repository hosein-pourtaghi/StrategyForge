using StrategyForge.Domain.Enums;

namespace StrategyForge.Domain.Models;

/// <summary>
/// Structured diagnostics produced during a strategy generation run.
/// Provides observability into pipeline execution without exposing internal details.
/// </summary>
public sealed record PipelineDiagnostics
{
    /// <summary>Unique correlation/execution ID for tracing this request through the pipeline.</summary>
    public required string ExecutionId { get; init; }

    /// <summary>The final pipeline state after execution.</summary>
    public required PipelineState State { get; init; }

    /// <summary>When the pipeline started.</summary>
    public DateTimeOffset StartedAt { get; init; }

    /// <summary>When the pipeline completed.</summary>
    public DateTimeOffset CompletedAt { get; init; }

    /// <summary>Total pipeline duration.</summary>
    public TimeSpan TotalDuration { get; init; }

    // --- Stage Durations ---

    /// <summary>Duration of data collection stage.</summary>
    public TimeSpan? DataCollectionDuration { get; init; }

    /// <summary>Duration of indicator analysis stage.</summary>
    public TimeSpan? AnalysisDuration { get; init; }

    /// <summary>Duration of parallel agent execution stage.</summary>
    public TimeSpan? AgentExecutionDuration { get; init; }

    /// <summary>Duration of strategy synthesis stage.</summary>
    public TimeSpan? SynthesisDuration { get; init; }

    // --- Agent Execution Summary ---

    /// <summary>Execution results for each specialist agent.</summary>
    public IReadOnlyList<AgentExecutionResult> AgentResults { get; init; } = [];

    /// <summary>Number of agents that completed successfully.</summary>
    public int SuccessfulAgentCount =>
        AgentResults.Count(a => a.Status == AgentExecutionStatus.Success);

    /// <summary>Number of agents that failed.</summary>
    public int FailedAgentCount =>
        AgentResults.Count(a => a.Status is AgentExecutionStatus.Failed
            or AgentExecutionStatus.Timeout);

    /// <summary>Number of agents that produced no usable results.</summary>
    public int UnavailableAgentCount =>
        AgentResults.Count(a => a.Status is AgentExecutionStatus.InsufficientEvidence
            or AgentExecutionStatus.Cancelled
            or AgentExecutionStatus.NotExecuted);

    // --- Data Quality ---

    /// <summary>Number of evidence items assembled.</summary>
    public int EvidenceCount { get; init; }

    /// <summary>Number of data providers that succeeded.</summary>
    public int SuccessfulDataProviders { get; init; }

    /// <summary>Number of data providers that failed.</summary>
    public int FailedDataProviders { get; init; }

    // --- LLM Metrics ---

    /// <summary>Total LLM calls made (agents + synthesis).</summary>
    public int LlmCallCount { get; init; }

    /// <summary>Number of LLM calls that failed.</summary>
    public int LlmFailureCount { get; init; }

    /// <summary>Total tokens consumed.</summary>
    public int TotalTokensUsed { get; init; }

    // --- Warnings ---

    /// <summary>Non-fatal warnings collected during execution.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

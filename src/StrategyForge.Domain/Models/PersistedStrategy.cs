using StrategyForge.Domain.Enums;

namespace StrategyForge.Domain.Models;

/// <summary>
/// A persisted strategy report for historical tracking and comparison.
/// 
/// Stores the complete StrategyReport output from the pipeline,
/// enabling historical analysis, strategy evolution tracking, and
/// provenance-preserving intelligence accumulation.
/// 
/// Critical rules:
/// - Every persisted strategy is immutable once stored
/// - Strategy state (PipelineState) is preserved at time of generation
/// - Full diagnostics are preserved for debugging and auditing
/// - Supports retrieval by asset, date range, and pipeline state
/// </summary>
public sealed record PersistedStrategy
{
    /// <summary>Unique identifier for this persisted strategy.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>The asset this strategy is about.</summary>
    public required Asset Asset { get; init; }

    /// <summary>When this strategy was generated.</summary>
    public required DateTimeOffset GeneratedAt { get; init; }

    /// <summary>The complete strategy report.</summary>
    public required StrategyReport Report { get; init; }

    /// <summary>The overall sentiment at generation time.</summary>
    public Sentiment OverallSentiment { get; init; }

    /// <summary>The overall confidence at generation time.</summary>
    public decimal? OverallConfidence { get; init; }

    /// <summary>The pipeline execution state.</summary>
    public PipelineState PipelineState { get; init; }

    /// <summary>Names of specialist agents that contributed.</summary>
    public IReadOnlyList<string> ContributingAgents { get; init; } = [];

    /// <summary>Number of tokens consumed during generation.</summary>
    public int? TokensUsed { get; init; }

    /// <summary>Generation duration.</summary>
    public TimeSpan? GenerationDuration { get; init; }

    /// <summary>Related evidence record ID (for provenance linking).</summary>
    public Guid? EvidenceId { get; init; }

    /// <summary>LLM model used for generation.</summary>
    public string? LlmModel { get; init; }
}

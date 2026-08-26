namespace StrategyForge.Infrastructure.Data.Entities;

/// <summary>
/// EF Core entity for PersistedStrategy.
/// Complex domain objects are stored as JSON text columns.
/// Scalar columns are kept for efficient filtering.
/// </summary>
public sealed class StrategyEntity
{
    public Guid Id { get; set; }

    /// <summary>Asset symbol for indexed lookups.</summary>
    public required string AssetSymbol { get; set; }

    /// <summary>Asset name (denormalized for display).</summary>
    public required string AssetName { get; set; }

    /// <summary>Market/exchange identifier.</summary>
    public required string AssetMarket { get; set; }

    /// <summary>When the strategy was generated.</summary>
    public DateTimeOffset GeneratedAt { get; set; }

    /// <summary>Full Asset record as JSON.</summary>
    public required string AssetJson { get; set; }

    /// <summary>Full StrategyReport record as JSON.</summary>
    public required string ReportJson { get; set; }

    /// <summary>Overall sentiment at generation time.</summary>
    public string? OverallSentiment { get; set; }

    /// <summary>Overall confidence at generation time.</summary>
    public decimal? OverallConfidence { get; set; }

    /// <summary>Pipeline state as string.</summary>
    public string? PipelineState { get; set; }

    /// <summary>Comma-separated contributing agent names.</summary>
    public string? ContributingAgents { get; set; }

    /// <summary>LLM model used.</summary>
    public string? LlmModel { get; set; }

    /// <summary>Tokens consumed.</summary>
    public int? TokensUsed { get; set; }

    /// <summary>Generation duration in milliseconds.</summary>
    public long? GenerationDurationMs { get; set; }

    /// <summary>Related evidence record ID.</summary>
    public Guid? EvidenceId { get; set; }
}

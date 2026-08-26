namespace StrategyForge.Infrastructure.Data.Entities;

/// <summary>
/// EF Core entity for PersistedEvidence.
/// Complex domain objects (Asset, AnalysisEvidence) are stored as JSON text
/// to avoid brittle relational mappings against mutable record types.
/// Scalar columns are kept for efficient filtering and querying.
/// </summary>
public sealed class EvidenceEntity
{
    public Guid Id { get; set; }

    /// <summary>Asset symbol for indexed lookups.</summary>
    public required string AssetSymbol { get; set; }

    /// <summary>Asset name (denormalized for display).</summary>
    public required string AssetName { get; set; }

    /// <summary>Market/exchange identifier.</summary>
    public required string AssetMarket { get; set; }

    /// <summary>When the evidence was assembled.</summary>
    public DateTimeOffset AssembledAt { get; set; }

    /// <summary>Full Asset record as JSON.</summary>
    public required string AssetJson { get; set; }

    /// <summary>Full AnalysisEvidence record as JSON.</summary>
    public required string EvidenceJson { get; set; }

    /// <summary>Comma-separated data source names.</summary>
    public string? DataSources { get; set; }

    /// <summary>Number of indicators in this evidence.</summary>
    public int IndicatorCount { get; set; }

    /// <summary>Number of news items in this evidence.</summary>
    public int NewsItemCount { get; set; }

    /// <summary>Overall data quality score (0.0 to 1.0).</summary>
    public decimal? DataQualityScore { get; set; }

    /// <summary>Pipeline execution ID for correlation.</summary>
    public string? ExecutionId { get; set; }
}

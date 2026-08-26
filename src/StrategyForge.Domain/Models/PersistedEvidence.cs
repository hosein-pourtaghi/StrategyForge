namespace StrategyForge.Domain.Models;

/// <summary>
/// A persisted snapshot of AnalysisEvidence with full provenance.
/// Stored for historical comparison, backtesting, and intelligence accumulation.
/// 
/// Critical rules:
/// - Every persisted evidence record is immutable once stored
/// - Evidence is linked to an asset and a specific point in time
/// - Full provenance is preserved (data sources, collection timestamps)
/// - Supports retrieval by asset, date range, and recency
/// </summary>
public sealed record PersistedEvidence
{
    /// <summary>Unique identifier for this persisted evidence record.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>The asset this evidence is about.</summary>
    public required Asset Asset { get; init; }

    /// <summary>When this evidence was assembled by the pipeline.</summary>
    public required DateTimeOffset AssembledAt { get; init; }

    /// <summary>The assembled analysis evidence snapshot.</summary>
    public required AnalysisEvidence Evidence { get; init; }

    /// <summary>Names of data sources that contributed to this evidence.</summary>
    public IReadOnlyList<string> DataSources { get; init; } = [];

    /// <summary>Number of indicators computed for this evidence.</summary>
    public int IndicatorCount { get; init; }

    /// <summary>Number of news items included in this evidence.</summary>
    public int NewsItemCount { get; init; }

    /// <summary>Overall data quality score (0.0 to 1.0) if available.</summary>
    public decimal? DataQualityScore { get; init; }

    /// <summary>Pipeline execution ID that produced this evidence (for correlation).</summary>
    public string? ExecutionId { get; init; }
}

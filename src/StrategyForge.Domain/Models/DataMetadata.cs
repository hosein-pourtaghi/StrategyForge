using StrategyForge.Domain.Enums;

namespace StrategyForge.Domain.Models;

/// <summary>
/// Metadata about a piece of data, preserving provenance and freshness.
/// Every external data point should carry this information.
/// </summary>
public sealed record DataMetadata
{
    /// <summary>The source that provided this data (e.g., "TSETMC", "CentralBank").</summary>
    public required string Source { get; init; }

    /// <summary>When this data was retrieved by StrategyForge.</summary>
    public required DateTimeOffset RetrievedAt { get; init; }

    /// <summary>When the data was originally generated (may differ from RetrievedAt).</summary>
    public DateTimeOffset? DataTimestamp { get; init; }

    /// <summary>The category of data (e.g., MarketData, News, Economic).</summary>
    public required DataSourceType DataType { get; init; }

    /// <summary>
    /// Reliability assessment: "Verified", "Estimated", "Unverified", "Calculated".
    /// </summary>
    public string? Reliability { get; init; }

    /// <summary>Optional provider-specific metadata.</summary>
    public IReadOnlyDictionary<string, string>? ExtraProperties { get; init; }
}

using StrategyForge.Domain.Enums;

namespace StrategyForge.Domain.Models;

/// <summary>
/// Standardized error envelope returned by data acquisition operations.
/// Every error must be explicit — never silently fail.
/// </summary>
public sealed record DataCollectionError2
{
    /// <summary>Machine-readable error code.</summary>
    public required string Code { get; init; }

    /// <summary>Human-readable error description.</summary>
    public required string Message { get; init; }

    /// <summary>Whether this error is transient and may succeed on retry.</summary>
    public required bool Retryable { get; init; }

    /// <summary>HTTP status code from the source, if applicable.</summary>
    public int? SourceHttpStatus { get; init; }

    /// <summary>When this error occurred.</summary>
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Market context metadata attached to data responses.
/// Describes the economic/semantic context of the data.
/// </summary>
public sealed record MarketContext2
{
    /// <summary>The asset class of the data (e.g., "equity", "fx", "commodity").</summary>
    public string? AssetClass { get; init; }

    /// <summary>The exchange or market (e.g., "TSE", "free_market").</summary>
    public string? Exchange { get; init; }

    /// <summary>Rate type description (e.g., "official", "free_market", "exchange").</summary>
    public string? RateType { get; init; }

    /// <summary>Whether this data is being used as a proxy for another data type.</summary>
    public bool IsProxy { get; init; }

    /// <summary>If proxy, what data type this is proxying for.</summary>
    public string? ProxyFor { get; init; }
}

/// <summary>
/// Summary metadata about a collection of data records.
/// </summary>
public sealed record DataSummary
{
    /// <summary>Total number of records.</summary>
    public required int Count { get; init; }

    /// <summary>Period description (e.g., "1y", "6mo").</summary>
    public string? Period { get; init; }

    /// <summary>Earliest date in the dataset (Gregorian).</summary>
    public DateOnly? StartDate { get; init; }

    /// <summary>Latest date in the dataset (Gregorian).</summary>
    public DateOnly? EndDate { get; init; }

    /// <summary>Currency of the price data (e.g., "IRR", "USD").</summary>
    public string? QuoteCurrency { get; init; }

    /// <summary>Price unit multiplier (e.g., 1 for IRR, 0.01 for cents).</summary>
    public decimal PriceUnit { get; init; } = 1;

    /// <summary>Description of what this data represents.</summary>
    public string? Description { get; init; }
}

/// <summary>
/// Metadata about the acquisition operation itself.
/// </summary>
public sealed record AcquisitionMetadata
{
    /// <summary>Total elapsed time for the acquisition.</summary>
    public TimeSpan Elapsed { get; init; }

    /// <summary>Whether a cache was used.</summary>
    public bool CacheHit { get; init; }

    /// <summary>Names of sources that contributed data.</summary>
    public IReadOnlyList<string> Sources { get; init; } = [];
}

/// <summary>
/// Standardized warning that accompanies data responses.
/// Warnings describe data quality or semantic concerns without failing the request.
/// </summary>
public sealed record DataWarning
{
    /// <summary>Warning code for programmatic handling.</summary>
    public required string Code { get; init; }

    /// <summary>Human-readable warning description.</summary>
    public required string Message { get; init; }

    /// <summary>Severity of the warning.</summary>
    public WarningSeverity Severity { get; init; } = WarningSeverity.Info;
}

/// <summary>
/// Severity levels for data warnings.
/// </summary>
public enum WarningSeverity
{
    /// <summary>Minor informational note.</summary>
    Info,

    /// <summary>Potential data quality concern.</summary>
    Warning,

    /// <summary>Serious data quality issue.</summary>
    Error
}

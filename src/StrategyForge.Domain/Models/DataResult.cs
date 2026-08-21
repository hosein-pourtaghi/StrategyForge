namespace StrategyForge.Domain.Models;

/// <summary>
/// The standardized response envelope for all data acquisition operations.
/// Every successful and failed response conforms to this structure.
/// 
/// This is the primary output of the Data Acquisition Layer.
/// It carries the data, provenance, freshness, quality, errors, and warnings
/// in a single consistent contract.
/// </summary>
/// <typeparam name="T">The type of data records in the response.</typeparam>
public sealed record DataResult<T>
{
    /// <summary>Whether the acquisition was successful.</summary>
    public required bool Ok { get; init; }

    /// <summary>Information about the original request.</summary>
    public DataRequestInfo? Request { get; init; }

    /// <summary>The acquired data records (null when Ok is false).</summary>
    public T? Data { get; init; }

    /// <summary>Summary information about the data collection.</summary>
    public DataSummary? Summary { get; init; }

    /// <summary>Market context metadata.</summary>
    public MarketContext2? MarketContext { get; init; }

    /// <summary>Freshness information for the data.</summary>
    public DataFreshness? Freshness { get; init; }

    /// <summary>Quality assessment of the data.</summary>
    public DataQuality? Quality { get; init; }

    /// <summary>Metadata about the acquisition operation.</summary>
    public AcquisitionMetadata? Metadata { get; init; }

    /// <summary>Error details (null when Ok is true).</summary>
    public DataCollectionError2? Error { get; init; }

    /// <summary>Warnings that accompanied the data (even on success).</summary>
    public IReadOnlyList<DataWarning> Warnings { get; init; } = [];

    /// <summary>Create a successful result.</summary>
    public static DataResult<T> Success(
        T data,
        DataSummary? summary = null,
        DataFreshness? freshness = null,
        DataQuality? quality = null,
        MarketContext2? marketContext = null,
        AcquisitionMetadata? metadata = null,
        IReadOnlyList<DataWarning>? warnings = null) => new()
    {
        Ok = true,
        Data = data,
        Summary = summary,
        Freshness = freshness ?? DataFreshness.Fresh(),
        Quality = quality ?? DataQuality.Perfect,
        MarketContext = marketContext,
        Metadata = metadata,
        Warnings = warnings ?? []
    };

    /// <summary>Create a failed result.</summary>
    public static DataResult<T> Failure(
        DataCollectionError2 error,
        IReadOnlyList<DataWarning>? warnings = null) => new()
    {
        Ok = false,
        Error = error,
        Warnings = warnings ?? []
    };
}

/// <summary>
/// Information about the data request that produced a result.
/// </summary>
public sealed record DataRequestInfo
{
    /// <summary>The StrategyForge instrument ID.</summary>
    public string? InstrumentId { get; init; }

    /// <summary>The user-provided symbol in the original request.</summary>
    public string? RequestedSymbol { get; init; }

    /// <summary>The data type requested (e.g., "daily_ohlc", "current_rate").</summary>
    public string? DataType { get; init; }

    /// <summary>Date range requested, if applicable.</summary>
    public DateOnly? From { get; init; }

    /// <summary>Date range requested, if applicable.</summary>
    public DateOnly? To { get; init; }
}

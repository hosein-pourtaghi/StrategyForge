namespace StrategyForge.Api.Contracts;

/// <summary>
/// Standardized API response wrapper for data acquisition results.
/// </summary>
public sealed record DataResultResponse<T>
{
    public bool Ok { get; init; }
    public T? Data { get; init; }
    public DataMetadataResponse? Summary { get; init; }
    public FreshnessResponse? Freshness { get; init; }
    public QualityResponse? Quality { get; init; }
    public IReadOnlyList<WarningResponse> Warnings { get; init; } = [];
    public ErrorDetailResponse? Error { get; init; }
}

public sealed record DataMetadataResponse
{
    public int? Count { get; init; }
    public string? Description { get; init; }
}

public sealed record FreshnessResponse
{
    public DateTimeOffset FetchedAtUtc { get; init; }
    public long AgeMs { get; init; }
    public long MaxAllowedAgeMs { get; init; }
    public bool IsFresh { get; init; }
    public bool IsCached { get; init; }
}

public sealed record QualityResponse
{
    public int Score { get; init; }
    public bool IsComplete { get; init; }
    public string? Flags { get; init; }
}

public sealed record WarningResponse
{
    public string Code { get; init; } = "";
    public string Message { get; init; } = "";
}

public sealed record ErrorDetailResponse
{
    public string Code { get; init; } = "";
    public string Message { get; init; } = "";
    public bool Retryable { get; init; }
}

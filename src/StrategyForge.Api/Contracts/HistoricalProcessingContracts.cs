using StrategyForge.Domain.Models;

namespace StrategyForge.Api.Contracts;

/// <summary>
/// Response contract for one processed (enriched) historical observation.
/// </summary>
public sealed record EnrichedObservationResponse
{
    public string InstrumentId { get; init; } = "";
    public string Source { get; init; } = "";
    public DateOnly ObservationDate { get; init; }
    public decimal Open { get; init; }
    public decimal High { get; init; }
    public decimal Low { get; init; }
    public decimal Close { get; init; }
    public long Volume { get; init; }
    public decimal? Change { get; init; }
    public decimal? ChangePercent { get; init; }

    /// <summary>Indicator values keyed by indicator name → component → value.</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, decimal>> Indicators { get; init; }
        = new Dictionary<string, IReadOnlyDictionary<string, decimal>>();

    public string QualityStatus { get; init; } = "";
    public IReadOnlyList<string> WarningCodes { get; init; } = [];
    public string ProcessedBy { get; init; } = "";
    public DateTimeOffset ProcessedAtUtc { get; init; }
}

/// <summary>
/// Paginated response for processed historical observations.
/// </summary>
public sealed record EnrichedObservationsPageResponse
{
    public string InstrumentId { get; init; } = "";
    public string Source { get; init; } = "";
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }

    public int Skip { get; init; }
    public int Take { get; init; }
    public int TotalCount { get; init; }

    public IReadOnlyList<EnrichedObservationResponse> Items { get; init; } = [];
}

/// <summary>
/// Response body for triggering a processing run.
/// </summary>
public sealed record HistoricalProcessingResponse
{
    public bool Ok { get; init; }
    public string InstrumentId { get; init; } = "";
    public string Source { get; init; } = "";
    public DateOnly RequestedFrom { get; init; }
    public DateOnly RequestedTo { get; init; }

    public int RowsRead { get; init; }
    public int RowsInScope { get; init; }
    public int WarmupRows { get; init; }
    public int RowsAccepted { get; init; }
    public int RowsRejected { get; init; }
    public int RowsWithWarnings { get; init; }
    public int RowsWritten { get; init; }
    public int RowsChanged { get; init; }
    public int RowsUnchanged { get; init; }
    public int Duplicates { get; init; }
    public int ValidationErrors { get; init; }
    public int Warnings { get; init; }
    public int BatchesProcessed { get; init; }
    public double DurationMs { get; init; }
    public IReadOnlyDictionary<string, int> ReasonCounts { get; init; } = new Dictionary<string, int>();
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
}

using StrategyForge.Domain.Enums;

namespace StrategyForge.Domain.Models;

/// <summary>
/// Preserves the origin and acquisition context of a piece of data.
/// Every significant acquired dataset must carry provenance information.
/// Enables downstream modules to understand where data came from,
/// when it was fetched, and whether it is raw or derived.
/// </summary>
public sealed record DataProvenance
{
    /// <summary>Source adapter that provided this data (e.g., "tsetmc", "tgju").</summary>
    public required SourceAdapterType Source { get; init; }

    /// <summary>The source's native symbol for the instrument (e.g., "فولاد").</summary>
    public string? SourceSymbol { get; init; }

    /// <summary>The source's native instrument identifier (e.g., InsCode for TSETMC).</summary>
    public string? SourceInstrumentId { get; init; }

    /// <summary>When this data was fetched by StrategyForge.</summary>
    public required DateTimeOffset FetchedAtUtc { get; init; }

    /// <summary>The original timestamp from the source, if available.</summary>
    public DateTimeOffset? SourceTimestampUtc { get; init; }

    /// <summary>Whether this data was served from cache.</summary>
    public required bool IsCached { get; init; }

    /// <summary>Whether this data is raw from the source or derived/calculated by StrategyForge.</summary>
    public bool IsDerived { get; init; }

    /// <summary>If derived, which source(s) were used as input.</summary>
    public IReadOnlyList<SourceAdapterType> InputSources { get; init; } = [];

    /// <summary>The endpoint or data type requested from the source.</summary>
    public string? Endpoint { get; init; }

    /// <summary>Additional source-specific metadata.</summary>
    public IReadOnlyDictionary<string, string>? ExtraProperties { get; init; }
}

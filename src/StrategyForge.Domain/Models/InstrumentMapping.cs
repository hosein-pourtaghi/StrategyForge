using StrategyForge.Domain.Enums;

namespace StrategyForge.Domain.Models;

/// <summary>
/// Canonical instrument identity in StrategyForge.
/// Maps user-facing symbols to internal IDs and source-specific identifiers.
/// Never makes a source-specific symbol the universal identity of an instrument.
/// </summary>
public sealed record InstrumentMapping
{
    /// <summary>StrategyForge canonical instrument ID (e.g., "iran-equity-foolad-4439113430858354").</summary>
    public required string InstrumentId { get; init; }

    /// <summary>User-facing Persian symbol (e.g., "فولاد").</summary>
    public required string Symbol { get; init; }

    /// <summary>Latin symbol (e.g., "Foolad").</summary>
    public string? LatinSymbol { get; init; }

    /// <summary>Human-readable display name (e.g., "Foolad Mobarakeh").</summary>
    public required string DisplayName { get; init; }

    /// <summary>The asset class (e.g., "equity", "index", "fx").</summary>
    public required AssetType AssetClass { get; init; }

    /// <summary>The exchange or market (e.g., "TSE", "free_market").</summary>
    public required string Exchange { get; init; }

    /// <summary>Quote currency (e.g., "IRR", "USD").</summary>
    public required string QuoteCurrency { get; init; }

    /// <summary>Source-specific identifiers for each registered adapter.</summary>
    public IReadOnlyDictionary<SourceAdapterType, SourceIdentifier> SourceIdentifiers { get; init; }
        = new Dictionary<SourceAdapterType, SourceIdentifier>();

    /// <summary>Whether this instrument is currently active/tradeable.</summary>
    public bool IsActive { get; init; } = true;

    /// <summary>Additional metadata.</summary>
    public IReadOnlyDictionary<string, string>? ExtraProperties { get; init; }
}

/// <summary>
/// A source-specific identifier for an instrument.
/// </summary>
public sealed record SourceIdentifier
{
    /// <summary>The identifier used by the source (e.g., InsCode for TSETMC).</summary>
    public required string Id { get; init; }

    /// <summary>The symbol used by the source (may differ from canonical symbol).</summary>
    public string? SourceSymbol { get; init; }

    /// <summary>When this mapping was last verified.</summary>
    public DateTimeOffset? LastVerified { get; init; }
}

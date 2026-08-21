using StrategyForge.Domain.Enums;

namespace StrategyForge.Domain.Models;

/// <summary>
/// Represents a financial instrument that can be analyzed by StrategyForge.
/// </summary>
public sealed record Asset
{
    /// <summary>Unique identifier for this asset.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Ticker symbol or identifier (e.g., "فولاد", "TEDPIX", "USD-IRR").</summary>
    public required string Symbol { get; init; }

    /// <summary>Human-readable name (e.g., "Foolad Mobarakeh", "Tehran Exchange Index").</summary>
    public required string Name { get; init; }

    /// <summary>The market or exchange this asset trades on.</summary>
    public required string Market { get; init; }

    /// <summary>The type of financial instrument.</summary>
    public required AssetType AssetType { get; init; }

    /// <summary>Optional sector or industry classification.</summary>
    public string? Sector { get; init; }

    /// <summary>Optional ISIN or other standardized identifier.</summary>
    public string? Isin { get; init; }

    /// <summary>Additional provider-specific identifiers.</summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}

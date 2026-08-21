using StrategyForge.Domain.Enums;

namespace StrategyForge.Domain.Models;

/// <summary>
/// Describes the current market context for the asset being analyzed.
/// Provides the backdrop against which the strategy is set.
/// </summary>
public sealed record MarketContext
{
    /// <summary>Current market regime.</summary>
    public required MarketRegime Regime { get; init; }

    /// <summary>Human-readable description of the current market condition.</summary>
    public required string Description { get; init; }

    /// <summary>Current price of the asset.</summary>
    public decimal? CurrentPrice { get; init; }

    /// <summary>Price change over the last period.</summary>
    public decimal? RecentPriceChange { get; init; }

    /// <summary>Volume description (e.g., "Above average", "Declining").</summary>
    public string? VolumeContext { get; init; }

    /// <summary>Relevant macro context (e.g., "Rising interest rates", "Currency instability").</summary>
    public string? MacroContext { get; init; }

    /// <summary>Key events or catalysts in the near term.</summary>
    public IReadOnlyList<string> UpcomingEvents { get; init; } = [];
}

namespace StrategyForge.Domain.Models;

/// <summary>
/// Represents a gold price data point.
/// </summary>
public sealed record GoldPrice
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>The price of gold (unit specified by Unit).</summary>
    public required decimal Price { get; init; }

    /// <summary>The currency or unit of the price (e.g., "USD/oz", "IRR/mithqal").</summary>
    public required string Unit { get; init; }

    /// <summary>The type of gold (e.g., "Spot", "18K", "24K", "Coin").</summary>
    public string? GoldType { get; init; }

    /// <summary>The date and time this price was recorded.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Change from previous period if available.</summary>
    public decimal? Change { get; init; }

    /// <summary>Percentage change from previous period if available.</summary>
    public decimal? ChangePercent { get; init; }

    /// <summary>Metadata about when and from where this data was retrieved.</summary>
    public DataMetadata? Metadata { get; init; }
}

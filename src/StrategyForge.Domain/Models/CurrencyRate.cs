namespace StrategyForge.Domain.Models;

/// <summary>
/// Represents a currency exchange rate (e.g., USD/IRR, USDT/IRR).
/// </summary>
public sealed record CurrencyRate
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>The base currency code (e.g., "USD").</summary>
    public required string BaseCurrency { get; init; }

    /// <summary>The quote currency code (e.g., "IRR").</summary>
    public required string QuoteCurrency { get; init; }

    /// <summary>The exchange rate (how many units of QuoteCurrency per 1 unit of BaseCurrency).</summary>
    public required decimal Rate { get; init; }

    /// <summary>The date and time this rate was recorded.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Bid price if available.</summary>
    public decimal? Bid { get; init; }

    /// <summary>Ask price if available.</summary>
    public decimal? Ask { get; init; }

    /// <summary>High for the period if available.</summary>
    public decimal? High { get; init; }

    /// <summary>Low for the period if available.</summary>
    public decimal? Low { get; init; }

    /// <summary>Metadata about when and from where this data was retrieved.</summary>
    public DataMetadata? Metadata { get; init; }
}

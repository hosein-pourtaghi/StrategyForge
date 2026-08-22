namespace StrategyForge.Domain.Models;

/// <summary>
/// Represents a market order book (depth-of-market) with bid and ask levels.
/// </summary>
public sealed record OrderBook
{
    /// <summary>The instrument this order book belongs to.</summary>
    public required string InstrumentId { get; init; }

    /// <summary>When this order book was observed.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Bid levels, ordered from best (highest) to worst (lowest) price.</summary>
    public IReadOnlyList<OrderBookLevel> Bids { get; init; } = [];

    /// <summary>Ask levels, ordered from best (lowest) to worst (highest) price.</summary>
    public IReadOnlyList<OrderBookLevel> Asks { get; init; } = [];

    /// <summary>Best bid price (highest bid), if available.</summary>
    public decimal? BestBid => Bids.Count > 0 ? Bids[0].Price : null;

    /// <summary>Best ask price (lowest ask), if available.</summary>
    public decimal? BestAsk => Asks.Count > 0 ? Asks[0].Price : null;

    /// <summary>Mid-market price (average of best bid and ask), if both available.</summary>
    public decimal? MidPrice => BestBid.HasValue && BestAsk.HasValue
        ? (BestBid.Value + BestAsk.Value) / 2
        : BestBid ?? BestAsk;

    /// <summary>Spread between best ask and best bid.</summary>
    public decimal? Spread => BestBid.HasValue && BestAsk.HasValue
        ? BestAsk.Value - BestBid.Value
        : null;

    /// <summary>Provenance information.</summary>
    public DataProvenance? Provenance { get; init; }

    /// <summary>Source-specific extra fields.</summary>
    public IReadOnlyDictionary<string, string>? ExtraFields { get; init; }
}

/// <summary>
/// A single price level in an order book.
/// </summary>
public sealed record OrderBookLevel
{
    /// <summary>Price at this level.</summary>
    public required decimal Price { get; init; }

    /// <summary>Volume/quantity at this level.</summary>
    public required decimal Quantity { get; init; }

    /// <summary>Number of orders at this level (if available).</summary>
    public int? OrderCount { get; init; }
}

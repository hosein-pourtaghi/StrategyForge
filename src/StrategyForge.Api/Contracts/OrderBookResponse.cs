using StrategyForge.Domain.Models;

namespace StrategyForge.Api.Contracts;

/// <summary>
/// API response for an order book level.
/// </summary>
public sealed record OrderBookLevelResponse
{
    public decimal Price { get; init; }
    public decimal Quantity { get; init; }
    public int? OrderCount { get; init; }
}

/// <summary>
/// API response for an order book.
/// </summary>
public sealed record OrderBookResponse
{
    public string? InstrumentId { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public IReadOnlyList<OrderBookLevelResponse> Bids { get; init; } = [];
    public IReadOnlyList<OrderBookLevelResponse> Asks { get; init; } = [];
    public decimal? BestBid { get; init; }
    public decimal? BestAsk { get; init; }
    public decimal? MidPrice { get; init; }
    public decimal? Spread { get; init; }
    public ProvenanceResponse? Provenance { get; init; }
}

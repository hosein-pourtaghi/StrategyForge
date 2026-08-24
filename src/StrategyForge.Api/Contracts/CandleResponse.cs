using StrategyForge.Domain.Models;

namespace StrategyForge.Api.Contracts;

/// <summary>
/// API response for a single candle.
/// </summary>
public sealed record CandleResponse
{
    public DateOnly Date { get; init; }
    /// <summary>The candle resolution/interval (e.g., Daily, Hour1, Minute5).</summary>
    public string? Resolution { get; init; }
    public decimal Open { get; init; }
    public decimal High { get; init; }
    public decimal Low { get; init; }
    public decimal Close { get; init; }
    public long Volume { get; init; }
    public decimal? Value { get; init; }
    public long? TradeCount { get; init; }
    public decimal? LastPrice { get; init; }
    public decimal? Change { get; init; }
    public decimal? ChangePercent { get; init; }
    public string? MarketTimezone { get; init; }
    public string? SourceDate { get; init; }
    public string? SourceCalendar { get; init; }
    public AdjustmentResponse? Adjustment { get; init; }
    public ProvenanceResponse? Provenance { get; init; }
}

public sealed record AdjustmentResponse
{
    public bool IsAdjusted { get; init; }
    public string? Type { get; init; }
    public string? AdjustmentSource { get; init; }
}

public sealed record ProvenanceResponse
{
    public string? Source { get; init; }
    public string? SourceSymbol { get; init; }
    public string? SourceInstrumentId { get; init; }
    public DateTimeOffset FetchedAtUtc { get; init; }
    public bool IsCached { get; init; }
    public string? Endpoint { get; init; }
}

using StrategyForge.Domain.Enums;

namespace StrategyForge.Api.Contracts;

/// <summary>
/// API response for instrument resolution and lookup.
/// </summary>
public sealed record InstrumentResponse
{
    public required string InstrumentId { get; init; }
    public required string Symbol { get; init; }
    public string? LatinSymbol { get; init; }
    public required string DisplayName { get; init; }
    public required AssetType AssetClass { get; init; }
    public required string Exchange { get; init; }
    public required string QuoteCurrency { get; init; }
    public bool IsActive { get; init; } = true;
    public IReadOnlyDictionary<SourceAdapterType, SourceIdentifierResponse> SourceIdentifiers { get; init; }
        = new Dictionary<SourceAdapterType, SourceIdentifierResponse>();
}

public sealed record SourceIdentifierResponse
{
    public required string Id { get; init; }
    public string? SourceSymbol { get; init; }
}

using StrategyForge.Api.Contracts;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Interfaces.Providers;
using StrategyForge.Domain.Models;

namespace StrategyForge.Api.Services;

/// <summary>
/// Application service for instrument resolution and lookup.
/// Thin layer between controllers and domain interfaces.
/// </summary>
public sealed class InstrumentService
{
    private readonly IInstrumentResolver _resolver;

    public InstrumentService(IInstrumentResolver resolver)
    {
        _resolver = resolver;
    }

    public async Task<InstrumentResponse?> ResolveAsync(string query, CancellationToken ct = default)
    {
        var mapping = await _resolver.ResolveAsync(query, ct);
        return mapping != null ? MapToResponse(mapping) : null;
    }

    public async Task<IReadOnlyList<InstrumentResponse>> SearchAsync(string query, int maxResults = 10, CancellationToken ct = default)
    {
        var results = await _resolver.SearchAsync(query, maxResults, ct);
        return results.Select(MapToResponse).ToList().AsReadOnly();
    }

    public async Task<InstrumentResponse?> GetByIdAsync(string instrumentId, CancellationToken ct = default)
    {
        var mapping = await _resolver.ResolveAsync(instrumentId, ct);
        return mapping != null ? MapToResponse(mapping) : null;
    }

    public static InstrumentResponse MapToResponse(InstrumentMapping mapping) => new()
    {
        InstrumentId = mapping.InstrumentId,
        Symbol = mapping.Symbol,
        LatinSymbol = mapping.LatinSymbol,
        DisplayName = mapping.DisplayName,
        AssetClass = mapping.AssetClass,
        Exchange = mapping.Exchange,
        QuoteCurrency = mapping.QuoteCurrency,
        IsActive = mapping.IsActive,
        SourceIdentifiers = mapping.SourceIdentifiers.ToDictionary(
            kvp => kvp.Key,
            kvp => new SourceIdentifierResponse { Id = kvp.Value.Id, SourceSymbol = kvp.Value.SourceSymbol })
    };
}

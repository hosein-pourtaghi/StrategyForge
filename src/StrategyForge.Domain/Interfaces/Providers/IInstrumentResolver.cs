using StrategyForge.Domain.Models;

namespace StrategyForge.Domain.Interfaces.Providers;

/// <summary>
/// Resolves user-provided instrument identifiers to canonical StrategyForge instrument mappings.
/// 
/// Supports resolution from:
/// - Persian symbols (e.g., "فولاد")
/// - Latin symbols (e.g., "Foolad")
/// - Numeric identifiers (e.g., "4439113430858354")
/// - Canonical instrument IDs
/// 
/// Resolution flow:
///   User Input → StrategyForge Instrument Resolution → Canonical Instrument ID
///   → Source-Specific Identifier Resolution → External Source Request
/// </summary>
public interface IInstrumentResolver
{
    /// <summary>
    /// Resolves a user-provided identifier to a canonical instrument mapping.
    /// </summary>
    /// <param name="identifier">Persian symbol, Latin symbol, numeric ID, or canonical instrument ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resolved instrument mapping, or null if not found.</returns>
    Task<InstrumentMapping?> ResolveAsync(
        string identifier,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves multiple identifiers in batch.
    /// </summary>
    Task<IReadOnlyList<InstrumentMapping>> ResolveBatchAsync(
        IReadOnlyList<string> identifiers,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for instruments matching a query string.
    /// Returns candidates when the input is ambiguous.
    /// </summary>
    /// <param name="query">Partial symbol or name to search for.</param>
    /// <param name="maxResults">Maximum number of results.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching instruments, ordered by relevance.</returns>
    Task<IReadOnlyList<InstrumentMapping>> SearchAsync(
        string query,
        int maxResults = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the source-specific identifier for an instrument and a specific adapter.
    /// </summary>
    /// <param name="instrument">The canonical instrument.</param>
    /// <param name="sourceType">The source adapter type.</param>
    /// <returns>The source-specific identifier, or null if not mapped.</returns>
    SourceIdentifier? GetSourceIdentifier(
        InstrumentMapping instrument,
        Enums.SourceAdapterType sourceType);
}

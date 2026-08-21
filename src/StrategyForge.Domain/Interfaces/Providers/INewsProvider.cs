using StrategyForge.Domain.Models;

namespace StrategyForge.Domain.Interfaces.Providers;

/// <summary>
/// Interface for news providers that supply news items and announcements.
/// </summary>
public interface INewsProvider
{
    /// <summary>Human-readable name of this provider.</summary>
    string Name { get; }

    /// <summary>
    /// Retrieves recent news items related to a specific asset.
    /// </summary>
    /// <param name="asset">The asset to get news for.</param>
    /// <param name="maxItems">Maximum number of news items to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of news items, ordered by most recent first.</returns>
    Task<IReadOnlyList<NewsItem>> GetRecentNewsAsync(
        Asset asset,
        int maxItems = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves recent news items related to general market topics.
    /// </summary>
    /// <param name="topics">Topics to search for (e.g., "Iran economy", "sanctions").</param>
    /// <param name="maxItems">Maximum number of news items to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of news items, ordered by most recent first.</returns>
    Task<IReadOnlyList<NewsItem>> GetMarketNewsAsync(
        IReadOnlyList<string> topics,
        int maxItems = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether this provider can serve news for the given asset.
    /// </summary>
    bool Supports(Asset asset);
}

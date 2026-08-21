using StrategyForge.Domain.Enums;

namespace StrategyForge.Domain.Models;

/// <summary>
/// Represents a news item, announcement, or event relevant to an asset or market.
/// Preserves source attribution and timestamp for traceability.
/// </summary>
public sealed record NewsItem
{
    /// <summary>Unique identifier for this news item.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Headline or title of the news item.</summary>
    public required string Title { get; init; }

    /// <summary>Full content or summary of the news item.</summary>
    public string? Content { get; init; }

    /// <summary>When this news was published.</summary>
    public required DateTimeOffset PublishedAt { get; init; }

    /// <summary>The source that published this news (e.g., "IRNA", "TSETMC").</summary>
    public required string Source { get; init; }

    /// <summary>The URL of the original article (if available).</summary>
    public string? Url { get; init; }

    /// <summary>Assets or symbols this news relates to.</summary>
    public IReadOnlyList<string>? RelatedSymbols { get; init; }

    /// <summary>Topics or tags categorizing this news.</summary>
    public IReadOnlyList<string>? Topics { get; init; }

    /// <summary>Assessed sentiment impact: positive, negative, or neutral.</summary>
    public Sentiment? Sentiment { get; init; }

    /// <summary>Confidence in the sentiment assessment (0.0 to 1.0).</summary>
    public decimal? SentimentConfidence { get; init; }

    /// <summary>How relevant this news is to the target asset (0.0 to 1.0).</summary>
    public decimal? RelevanceScore { get; init; }

    /// <summary>Metadata about when and from where this data was retrieved.</summary>
    public DataMetadata? Metadata { get; init; }
}

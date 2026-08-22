namespace StrategyForge.Domain.Enums;

/// <summary>
/// Controls how the DataSourceRegistry selects and falls back between providers.
/// </summary>
public enum SourceSelectionMode
{
    /// <summary>Use the best compatible source by deterministic rules.</summary>
    BestAvailable,

    /// <summary>Use only the preferred source. No fallback.</summary>
    PreferredOnly,

    /// <summary>Try the preferred source first, then fall back to compatible alternatives.</summary>
    PreferredThenFallback
}

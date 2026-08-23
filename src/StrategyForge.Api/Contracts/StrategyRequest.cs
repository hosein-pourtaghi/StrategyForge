using StrategyForge.Domain.Enums;

namespace StrategyForge.Api.Contracts;

/// <summary>
/// API request for strategy generation.
/// </summary>
public sealed record StrategyRequest
{
    /// <summary>
    /// Instrument query (Persian symbol, Latin symbol, numeric ID, or canonical ID).
    /// </summary>
    public required string Instrument { get; init; }

    /// <summary>
    /// Time horizons to include in the strategy.
    /// Defaults to all horizons if not specified.
    /// </summary>
    public IReadOnlyList<TimeHorizon>? Horizons { get; init; }

    /// <summary>
    /// Optional constraints or focus areas for the synthesis.
    /// </summary>
    public IReadOnlyList<string>? Constraints { get; init; }

    /// <summary>
    /// Optional area of emphasis for the LLM (e.g., "Focus on risk factors").
    /// </summary>
    public string? FocusArea { get; init; }
}

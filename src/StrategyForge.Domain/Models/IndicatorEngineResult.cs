namespace StrategyForge.Domain.Models;

/// <summary>
/// Aggregated output from the IndicatorEngine after running all enabled indicators.
/// Contains the complete set of indicator results for an asset's candle data.
/// </summary>
public sealed record IndicatorEngineResult
{
    /// <summary>The date range of the input candle data.</summary>
    public DateOnly DataStartDate { get; init; }

    /// <summary>The date range of the input candle data.</summary>
    public DateOnly DataEndDate { get; init; }

    /// <summary>The number of candles processed.</summary>
    public int CandleCount { get; init; }

    /// <summary>All indicator results, grouped by indicator name.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<IndicatorResult>> Results { get; init; }
        = new Dictionary<string, IReadOnlyList<IndicatorResult>>();

    /// <summary>Names of indicators that were computed successfully.</summary>
    public IReadOnlyList<string> SuccessfulIndicators { get; init; } = [];

    /// <summary>Names of indicators that failed during computation.</summary>
    public IReadOnlyList<string> FailedIndicators { get; init; } = [];

    /// <summary>Errors encountered during indicator computation.</summary>
    public IReadOnlyList<IndicatorError> Errors { get; init; } = [];

    /// <summary>Gets the latest (most recent) result for a given indicator.</summary>
    public IndicatorResult? GetLatest(string indicatorName)
    {
        if (Results.TryGetValue(indicatorName, out var results) && results.Count > 0)
        {
            return results[^1]; // Last element is the most recent
        }
        return null;
    }

    /// <summary>
    /// Gets a summary of the latest values for all indicators.
    /// Useful for building the AnalysisEvidence sent to AI agents.
    /// </summary>
    public IReadOnlyDictionary<string, IndicatorResult> GetLatestValues()
    {
        var summary = new Dictionary<string, IndicatorResult>();
        foreach (var (name, results) in Results)
        {
            if (results.Count > 0)
            {
                summary[name] = results[^1];
            }
        }
        return summary;
    }
}

/// <summary>
/// Records an error that occurred during indicator computation.
/// </summary>
public sealed record IndicatorError
{
    /// <summary>The indicator that failed.</summary>
    public required string IndicatorName { get; init; }

    /// <summary>Description of the error.</summary>
    public required string ErrorMessage { get; init; }

    /// <summary>The original exception message (if any).</summary>
    public string? ExceptionMessage { get; init; }
}

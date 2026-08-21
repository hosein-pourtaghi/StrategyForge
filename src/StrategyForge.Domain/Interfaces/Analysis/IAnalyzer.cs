using StrategyForge.Domain.Models;

namespace StrategyForge.Domain.Interfaces.Analysis;

/// <summary>
/// Interface for higher-level analysis modules that coordinate multiple indicators
/// or perform more complex analysis (e.g., trend detection, support/resistance).
/// 
/// Analyzers produce structured evidence that feeds into AnalysisEvidence.
/// </summary>
public interface IAnalyzer
{
    /// <summary>Name of this analyzer (e.g., "TrendDetector", "SupportResistanceAnalyzer").</summary>
    string Name { get; }

    /// <summary>
    /// Analyzes the provided data bundle and indicator results.
    /// </summary>
    /// <param name="dataBundle">The raw market data bundle.</param>
    /// <param name="indicatorResults">Pre-computed indicator results.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Analysis findings as structured evidence.</returns>
    Task<AnalyzerResult> AnalyzeAsync(
        MarketDataBundle dataBundle,
        IndicatorEngineResult indicatorResults,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Output from an analyzer module.
/// </summary>
public sealed record AnalyzerResult
{
    /// <summary>Name of the analyzer that produced this result.</summary>
    public required string AnalyzerName { get; init; }

    /// <summary>Structured findings from the analysis.</summary>
    public IReadOnlyDictionary<string, string> Findings { get; init; }
        = new Dictionary<string, string>();

    /// <summary>Supporting evidence items.</summary>
    public IReadOnlyList<EvidenceItem> Evidence { get; init; } = [];

    /// <summary>Warnings or caveats about the analysis.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

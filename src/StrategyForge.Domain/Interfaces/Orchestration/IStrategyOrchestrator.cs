using StrategyForge.Domain.Models;

namespace StrategyForge.Domain.Interfaces.Orchestration;

/// <summary>
/// Interface for the strategy orchestrator that coordinates the full analysis pipeline.
/// 
/// The orchestrator drives:
/// 1. Data collection from providers
/// 2. Analysis (indicators, analyzers)
/// 3. AI agent analysis
/// 4. Strategy synthesis
/// 5. StrategyReport generation
/// </summary>
public interface IStrategyOrchestrator
{
    /// <summary>
    /// Generates a complete strategy report for the specified asset.
    /// This is the primary entry point for the full pipeline.
    /// </summary>
    /// <param name="asset">The asset to analyze.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A complete, structured StrategyReport.</returns>
    Task<StrategyReport> GenerateStrategyAsync(
        Asset asset,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Collects all available data for an asset without running analysis or AI.
    /// Useful for debugging the data layer independently.
    /// </summary>
    /// <param name="asset">The asset to collect data for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The assembled data bundle.</returns>
    Task<MarketDataBundle> CollectDataAsync(
        Asset asset,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs the analysis layer on a pre-collected data bundle.
    /// Useful for debugging the analysis layer independently.
    /// </summary>
    /// <param name="dataBundle">The data bundle to analyze.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Structured analytical evidence.</returns>
    Task<AnalysisEvidence> AnalyzeAsync(
        MarketDataBundle dataBundle,
        CancellationToken cancellationToken = default);
}

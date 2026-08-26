using StrategyForge.Domain.Models;

namespace StrategyForge.Domain.Interfaces.Background;

/// <summary>
/// Interface for the Background Intelligence Engine.
/// Coordinates scheduled and on-demand intelligence collection runs:
/// data collection → evidence assembly → optional strategy generation → persistence.
/// 
/// The engine:
/// - Schedules periodic intelligence collection for registered assets
/// - Collects data from all available providers
/// - Assembles evidence and persists it
/// - Optionally generates strategies and persists them
/// - Tracks run history for observability
/// - Handles partial failures gracefully (one asset failure doesn't block others)
/// 
/// The engine does NOT:
/// - Execute trades or make investment decisions
/// - Replace manual strategy generation (it runs in parallel)
/// - Override user-initiated pipeline runs
/// </summary>
public interface IIntelligenceEngine
{
    /// <summary>
    /// Triggers an immediate intelligence collection run for the specified assets.
    /// This is an on-demand run, independent of any schedule.
    /// </summary>
    /// <param name="assetSymbols">Asset symbols to collect intelligence for. Empty = all registered assets.</param>
    /// <param name="generateStrategies">Whether to also generate strategies (requires LLM).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The completed intelligence run record.</returns>
    Task<IntelligenceRun> RunAsync(
        IReadOnlyList<string>? assetSymbols = null,
        bool generateStrategies = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the history of intelligence runs.
    /// </summary>
    /// <param name="maxResults">Maximum number of runs to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of intelligence runs ordered by scheduled time (newest first).</returns>
    Task<IReadOnlyList<IntelligenceRun>> GetRunHistoryAsync(
        int maxResults = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific intelligence run by ID.
    /// </summary>
    Task<IntelligenceRun?> GetRunByIdAsync(
        Guid runId,
        CancellationToken cancellationToken = default);
}

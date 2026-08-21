using StrategyForge.Domain.Models;

namespace StrategyForge.Domain.Interfaces.AI;

/// <summary>
/// Interface for specialist AI agents that reason over structured evidence.
/// 
/// Each agent:
/// 1. Receives structured evidence (not raw text or raw data)
/// 2. Constructs a prompt from evidence + template
/// 3. Calls ILLMProvider
/// 4. Parses the response into AgentAnalysisResult
/// 5. Returns structured output
/// 
/// Agents do NOT:
/// - Know which LLM is being used
/// - Calculate indicators themselves
/// - Access data providers directly
/// - Make trading decisions
/// </summary>
public interface IAgent
{
    /// <summary>Human-readable name of this agent (e.g., "TechnicalAnalyst", "MacroAnalyst").</summary>
    string Name { get; }

    /// <summary>
    /// Analyzes the provided evidence and produces a structured assessment.
    /// </summary>
    /// <param name="evidence">The structured evidence from Data and Analysis layers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Structured analysis result from this agent.</returns>
    Task<AgentAnalysisResult> AnalyzeAsync(
        AnalysisEvidence evidence,
        CancellationToken cancellationToken = default);
}

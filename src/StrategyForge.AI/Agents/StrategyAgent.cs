using Microsoft.Extensions.Logging;
using StrategyForge.Domain.Interfaces.AI;
using StrategyForge.Domain.Interfaces.Orchestration;
using StrategyForge.Domain.Models;

namespace StrategyForge.AI.Agents;

/// <summary>
/// The Strategy Agent synthesizes all specialist agent outputs into a coherent
/// StrategyReport. It is the final agent in the analysis pipeline.
/// 
/// Unlike specialist agents that analyze evidence independently, the Strategy Agent:
/// - Receives outputs from all specialist agents
/// - Identifies agreements and conflicts between agents
/// - Constructs structured scenarios (base, bull, bear)
/// - Produces the final StrategyReport
/// 
/// This agent is accessed via IStrategySynthesisService.SynthesizeAsync(),
/// which builds a complete StrategyContext before calling the LLM.
/// </summary>
public sealed class StrategyAgent
{
    private readonly IStrategySynthesisService _synthesisService;
    private readonly StrategyContextBuilder _contextBuilder;
    private readonly ILogger<StrategyAgent> _logger;

    public StrategyAgent(
        IStrategySynthesisService synthesisService,
        StrategyContextBuilder contextBuilder,
        ILogger<StrategyAgent> logger)
    {
        _synthesisService = synthesisService;
        _contextBuilder = contextBuilder;
        _logger = logger;
    }

    /// <summary>
    /// Synthesizes a StrategyReport from analysis evidence and specialist agent results.
    /// This is the primary entry point for the Strategy Agent.
    /// </summary>
    /// <param name="evidence">The assembled analysis evidence.</param>
    /// <param name="agentResults">Results from all specialist agents.</param>
    /// <param name="requestedHorizons">Time horizons to include in the strategy.</param>
    /// <param name="constraints">Optional strategy constraints.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The synthesis outcome.</returns>
    public async Task<StrategySynthesisOutcome> SynthesizeAsync(
        AnalysisEvidence evidence,
        IReadOnlyList<AgentAnalysisResult> agentResults,
        IReadOnlyList<Enums.TimeHorizon>? requestedHorizons = null,
        IReadOnlyList<string>? constraints = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Strategy Agent synthesizing {AgentCount} agent results for {Symbol}",
            agentResults.Count, evidence.Asset.Symbol);

        var context = _contextBuilder.Build(evidence, agentResults, requestedHorizons, constraints);
        return await _synthesisService.SynthesizeAsync(context, cancellationToken);
    }
}

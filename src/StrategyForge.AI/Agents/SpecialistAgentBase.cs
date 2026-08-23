using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Interfaces.AI;
using StrategyForge.Domain.Models;

namespace StrategyForge.AI.Agents;

/// <summary>
/// Base class for specialist AI agents that reason over structured evidence via LLM.
///
/// Each specialist:
/// 1. Receives scoped evidence (determined by EvidenceScope)
/// 2. Constructs a prompt with agent-specific system instructions
/// 3. Calls ILLMProvider through the provider-independent abstraction
/// 4. Validates the LLM response into a structured AgentAnalysisResult
/// 5. Returns the result with evidence traceability
///
/// Agents do NOT:
/// - Know which LLM is being used
/// - Calculate indicators (that is Phase 3's responsibility)
/// - Access data providers directly
/// - Make trading decisions
/// </summary>
public abstract class SpecialistAgentBase : IAgent
{
    protected readonly ILLMProvider LlmProvider;
    protected readonly ILogger Logger;

    protected SpecialistAgentBase(ILLMProvider llmProvider, ILogger logger)
    {
        LlmProvider = llmProvider;
        Logger = logger;
    }

    /// <inheritdoc/>
    public abstract string Name { get; }

    /// <summary>
    /// The evidence scope defining which evidence categories this agent requires.
    /// </summary>
    protected abstract EvidenceScope EvidenceScope { get; }

    /// <summary>
    /// The agent-specific system prompt that defines the agent's role and output format.
    /// Must instruct the LLM to return JSON with the standard agent output schema.
    /// </summary>
    protected abstract string GetSystemPrompt();

    /// <summary>
    /// Optional task instruction appended to the evidence prompt.
    /// Provides agent-specific guidance on what to analyze.
    /// </summary>
    protected abstract string GetTaskInstruction();

    /// <summary>
    /// Optional additional context beyond standard evidence.
    /// For example, cross-cutting context from other agent results.
    /// </summary>
    protected virtual string? GetAdditionalContext(AnalysisEvidence evidence) => null;

    /// <summary>
    /// Validates the LLM response. Base implementation validates common fields.
    /// Override for agent-specific validation.
    /// </summary>
    protected virtual AgentAnalysisResult ValidateResponse(
        LlmResponse response, string assetSymbol)
    {
        if (!response.Success)
            throw new InvalidOperationException($"LLM request failed: {response.Error}");

        if (string.IsNullOrWhiteSpace(response.Content))
            throw new InvalidOperationException("LLM returned empty content");

        var doc = JsonDocument.Parse(response.Content);
        var root = doc.RootElement;

        var validated = AgentPromptBuilder.ValidateCommonFields(root, Name);
        if (validated == null)
            throw new InvalidOperationException("LLM response missing required fields");

        var (agentName, sentiment, confidence, summary, detailedAnalysis,
            supportingEvidence, contradictingEvidence, identifiedRisks,
            informationGaps, agentSpecificData) = validated.Value;

        return new AgentAnalysisResult
        {
            AgentName = agentName,
            AssetSymbol = assetSymbol,
            GeneratedAt = DateTimeOffset.UtcNow,
            Sentiment = sentiment,
            Confidence = confidence,
            Summary = summary,
            DetailedAnalysis = detailedAnalysis,
            SupportingEvidence = supportingEvidence,
            ContradictingEvidence = contradictingEvidence,
            IdentifiedRisks = identifiedRisks,
            InformationGaps = informationGaps,
            AgentSpecificData = agentSpecificData
        };
    }

    /// <inheritdoc/>
    public async Task<AgentAnalysisResult> AnalyzeAsync(
        AnalysisEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInformation(
            "{AgentName} starting analysis for {Symbol}",
            Name, evidence.Asset.Symbol);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Build the prompt
            var request = new LlmRequest
            {
                SystemPrompt = GetSystemPrompt(),
                UserPrompt = AgentPromptBuilder.BuildUserPrompt(
                    evidence, EvidenceScope, GetTaskInstruction(), GetAdditionalContext(evidence)),
                ResponseFormat = "json",
                Temperature = 0.3,
                MaxTokens = 4096
            };

            // Call LLM
            LlmResponse response;
            try
            {
                response = await LlmProvider.CompleteAsync(request, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                Logger.LogWarning("{AgentName} analysis cancelled for {Symbol}", Name, evidence.Asset.Symbol);
                return CreateFailureResult(evidence.Asset.Symbol, "Analysis was cancelled",
                    stopwatch.Elapsed);
            }

            if (!response.Success)
            {
                stopwatch.Stop();
                Logger.LogWarning("{AgentName} LLM failed for {Symbol}: {Error}",
                    Name, evidence.Asset.Symbol, response.Error);
                return CreateFailureResult(evidence.Asset.Symbol,
                    $"LLM request failed: {response.Error}",
                    stopwatch.Elapsed, response.TotalTokens, response.Model);
            }

            // Validate and parse
            var result = ValidateResponse(response, evidence.Asset.Symbol);
            stopwatch.Stop();

            result = result with
            {
                TokensUsed = response.TotalTokens,
                LlmDuration = stopwatch.Elapsed
            };

            Logger.LogInformation(
                "{AgentName} analysis complete for {Symbol}: sentiment={Sentiment}, confidence={Confidence}",
                Name, evidence.Asset.Symbol, result.Sentiment, result.Confidence);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "{AgentName} analysis failed for {Symbol}", Name, evidence.Asset.Symbol);
            return CreateFailureResult(evidence.Asset.Symbol,
                $"Agent analysis failed: {ex.Message}",
                stopwatch.Elapsed);
        }
    }

    /// <summary>
    /// Creates a result representing an agent failure or unavailable analysis.
    /// This is explicitly represented — failures never silently become fake findings.
    /// </summary>
    private AgentAnalysisResult CreateFailureResult(
        string assetSymbol, string reason, TimeSpan duration,
        int? tokensUsed = null, string? llmModel = null)
    {
        return new AgentAnalysisResult
        {
            AgentName = Name,
            AssetSymbol = assetSymbol,
            GeneratedAt = DateTimeOffset.UtcNow,
            Sentiment = Sentiment.Unknown,
            Confidence = 0m,
            Summary = $"Analysis unavailable: {reason}",
            InformationGaps = [reason],
            LlmDuration = duration,
            TokensUsed = tokensUsed,
            AgentSpecificData = new Dictionary<string, string>
            {
                ["Status"] = "Failed",
                ["FailureReason"] = reason
            }
        };
    }
}

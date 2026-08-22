using Microsoft.Extensions.Logging;
using StrategyForge.Domain.Interfaces.AI;
using StrategyForge.Domain.Models;

namespace StrategyForge.AI.Services;

/// <summary>
/// Orchestrates the LLM interpretation pipeline:
/// Build evidence → Build prompt → Call LLM → Validate response.
/// 
/// This is the single entry point for LLM interpretation.
/// It does NOT bypass EvidenceQueryPipeline or Analysis Engine.
/// </summary>
public sealed class LlmInterpretationService
{
    private readonly ILLMProvider _llmProvider;
    private readonly AnalysisContextBuilder _contextBuilder;
    private readonly PromptBuilder _promptBuilder;
    private readonly LlmResponseValidator _validator;
    private readonly ILogger<LlmInterpretationService> _logger;

    public LlmInterpretationService(
        ILLMProvider llmProvider,
        AnalysisContextBuilder contextBuilder,
        PromptBuilder promptBuilder,
        LlmResponseValidator validator,
        ILogger<LlmInterpretationService> logger)
    {
        _llmProvider = llmProvider;
        _contextBuilder = contextBuilder;
        _promptBuilder = promptBuilder;
        _validator = validator;
        _logger = logger;
    }

    /// <summary>
    /// Interprets market evidence and technical analysis via LLM.
    /// Returns structured interpretation with explicit fact/interpretation separation.
    /// </summary>
    public async Task<LlmInterpretationOutcome> InterpretAsync(
        AnalysisEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Starting LLM interpretation for {Symbol} ({AssetType})",
            evidence.Asset.Symbol, evidence.Asset.AssetType);

        // Build prompt from evidence
        var request = _promptBuilder.BuildRequest(evidence);

        // Call LLM
        LlmResponse response;
        try
        {
            response = await _llmProvider.CompleteAsync(request, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return new LlmInterpretationOutcome
            {
                Success = false,
                ErrorMessage = "LLM request was cancelled",
                ProviderModel = _llmProvider.Model
            };
        }

        if (!response.Success)
        {
            _logger.LogWarning("LLM request failed: {Error}", response.Error);
            return new LlmInterpretationOutcome
            {
                Success = false,
                ErrorMessage = response.Error,
                ProviderModel = response.Model,
                TokensUsed = response.TotalTokens,
                Duration = response.ResponseDuration
            };
        }

        // Validate and parse response
        var validation = _validator.Validate(response);

        if (!validation.IsValid)
        {
            _logger.LogWarning("LLM response validation failed: {Error}", validation.ErrorMessage);
            return new LlmInterpretationOutcome
            {
                Success = false,
                ErrorMessage = validation.ErrorMessage,
                ProviderModel = response.Model,
                TokensUsed = response.TotalTokens,
                Duration = response.ResponseDuration,
                RawContent = response.Content
            };
        }

        _logger.LogInformation(
            "LLM interpretation complete for {Symbol}: {Observations} observations, {Interpretations} interpretations",
            evidence.Asset.Symbol,
            validation.ParsedResult?.Observations.Count ?? 0,
            validation.ParsedResult?.Interpretations.Count ?? 0);

        return new LlmInterpretationOutcome
        {
            Success = true,
            Result = validation.ParsedResult,
            ProviderModel = response.Model,
            TokensUsed = response.TotalTokens,
            Duration = response.ResponseDuration
        };
    }
}

/// <summary>
/// The outcome of an LLM interpretation attempt.
/// Contains either a successful parsed result or error information.
/// </summary>
public sealed class LlmInterpretationOutcome
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public LlmInterpretationResult? Result { get; init; }
    public string? ProviderModel { get; init; }
    public int TokensUsed { get; init; }
    public TimeSpan? Duration { get; init; }
    public string? RawContent { get; init; }
}

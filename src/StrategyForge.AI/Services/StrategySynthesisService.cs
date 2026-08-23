using Microsoft.Extensions.Logging;
using StrategyForge.Domain.Interfaces.AI;
using StrategyForge.Domain.Interfaces.Orchestration;
using StrategyForge.Domain.Models;

namespace StrategyForge.AI.Services;

/// <summary>
/// Orchestrates the strategy synthesis pipeline:
/// Build context → Build prompt → Call LLM → Validate response → Build StrategyReport.
/// 
/// This is the single entry point for strategy synthesis.
/// It reuses the existing Phase 4 LLM abstraction (ILLMProvider) and
/// follows the same patterns as LlmInterpretationService.
/// 
/// The pipeline:
/// 1. StrategyContextBuilder builds deterministic context
/// 2. StrategySynthesisPromptBuilder constructs the prompt
/// 3. ILLMProvider processes the request
/// 4. StrategyResponseValidator validates and parses the response
/// 5. The validated StrategyReport is returned
/// </summary>
public sealed class StrategySynthesisService : IStrategySynthesisService
{
    private readonly ILLMProvider _llmProvider;
    private readonly StrategyContextBuilder _contextBuilder;
    private readonly StrategySynthesisPromptBuilder _promptBuilder;
    private readonly StrategyResponseValidator _validator;
    private readonly ILogger<StrategySynthesisService> _logger;

    public StrategySynthesisService(
        ILLMProvider llmProvider,
        StrategyContextBuilder contextBuilder,
        StrategySynthesisPromptBuilder promptBuilder,
        StrategyResponseValidator validator,
        ILogger<StrategySynthesisService> logger)
    {
        _llmProvider = llmProvider;
        _contextBuilder = contextBuilder;
        _promptBuilder = promptBuilder;
        _validator = validator;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<StrategySynthesisOutcome> SynthesizeAsync(
        StrategyContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Starting strategy synthesis for {Symbol} ({AssetType}) with {AgentCount} agents",
            context.Asset.Symbol, context.Asset.AssetType, context.AgentResults.Count);

        // Build prompt from context
        var request = _promptBuilder.BuildRequest(context);

        _logger.LogDebug(
            "Strategy synthesis prompt built: {SystemPromptLength} chars system, {UserPromptLength} chars user",
            request.SystemPrompt.Length, request.UserPrompt.Length);

        // Call LLM
        LlmResponse response;
        try
        {
            response = await _llmProvider.CompleteAsync(request, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return new StrategySynthesisOutcome
            {
                Success = false,
                ErrorMessage = "LLM request was cancelled",
                ProviderModel = _llmProvider.Model
            };
        }

        if (!response.Success)
        {
            _logger.LogWarning("LLM strategy synthesis failed: {Error}", response.Error);
            return new StrategySynthesisOutcome
            {
                Success = false,
                ErrorMessage = response.Error,
                ProviderModel = response.Model,
                TokensUsed = response.TotalTokens,
                Duration = response.ResponseDuration
            };
        }

        // Validate and parse response
        var validation = _validator.Validate(response, context.Asset, DateTimeOffset.UtcNow);

        if (!validation.IsValid)
        {
            _logger.LogWarning("Strategy validation failed: {Error}", validation.ErrorMessage);
            return new StrategySynthesisOutcome
            {
                Success = false,
                ErrorMessage = validation.ErrorMessage,
                ProviderModel = response.Model,
                TokensUsed = response.TotalTokens,
                Duration = response.ResponseDuration,
                RawContent = response.Content
            };
        }

        // Enrich the report with metadata
        var report = validation.Report! with
        {
            ContributingAgents = context.AgentResults.Select(a => a.AgentName).ToList(),
            DataProvidersUsed = context.Evidence.DataSources,
            LlmModel = response.Model,
            TotalTokensUsed = response.TotalTokens
        };

        _logger.LogInformation(
            "Strategy synthesis complete for {Symbol}: sentiment={Sentiment}, confidence={Confidence}",
            context.Asset.Symbol,
            report.ExecutiveSummary.OverallSentiment,
            report.Confidence?.OverallConfidence.ToString("F2") ?? "N/A");

        return new StrategySynthesisOutcome
        {
            Success = true,
            Report = report,
            ProviderModel = response.Model,
            TokensUsed = response.TotalTokens,
            Duration = response.ResponseDuration
        };
    }
}

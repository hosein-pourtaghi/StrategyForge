using Microsoft.Extensions.DependencyInjection;
using StrategyForge.AI.Providers;
using StrategyForge.AI.Services;
using StrategyForge.Domain.Interfaces.AI;

namespace StrategyForge.AI;

/// <summary>
/// DI registration extension for the AI layer.
/// Registers the LLM provider, context builder, prompt builder, and interpretation service.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers StrategyForge AI services (LLM provider, interpretation pipeline).
    /// </summary>
    public static IServiceCollection AddStrategyForgeAI(this IServiceCollection services)
    {
        // Register the LLM provider as a typed HttpClient
        services.AddHttpClient<ILLMProvider, OpenAiCompatibleLlmProvider>();

        // Register AI services
        services.AddSingleton<AnalysisContextBuilder>();
        services.AddSingleton<PromptBuilder>();
        services.AddSingleton<LlmResponseValidator>();
        services.AddSingleton<LlmInterpretationService>();

        return services;
    }
}

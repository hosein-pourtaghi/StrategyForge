using Microsoft.Extensions.DependencyInjection;
using StrategyForge.AI.Agents;
using StrategyForge.AI.Providers;
using StrategyForge.AI.Services;
using StrategyForge.Domain.Interfaces.AI;
using StrategyForge.Domain.Interfaces.Orchestration;

namespace StrategyForge.AI;

/// <summary>
/// DI registration extension for the AI layer.
/// Registers the LLM provider, context builder, prompt builder, interpretation service,
/// and strategy synthesis services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers StrategyForge AI services (LLM provider, interpretation pipeline, strategy synthesis).
    /// </summary>
    public static IServiceCollection AddStrategyForgeAI(this IServiceCollection services)
    {
        // Register the LLM provider as a typed HttpClient
        services.AddHttpClient<ILLMProvider, OpenAiCompatibleLlmProvider>();

        // Register Phase 4 interpretation services
        services.AddSingleton<AnalysisContextBuilder>();
        services.AddSingleton<PromptBuilder>();
        services.AddSingleton<LlmResponseValidator>();
        services.AddSingleton<LlmInterpretationService>();

        // Register Phase 5 strategy synthesis services
        services.AddSingleton<StrategyContextBuilder>();
        services.AddSingleton<StrategySynthesisPromptBuilder>();
        services.AddSingleton<StrategyResponseValidator>();
        services.AddSingleton<IStrategySynthesisService, StrategySynthesisService>();
        services.AddSingleton<StrategyAgent>();

        // Register Phase 6 specialist agents
        services.AddSingleton<IAgent, TechnicalAnalyst>();
        services.AddSingleton<IAgent, FundamentalAnalyst>();
        services.AddSingleton<IAgent, MacroAnalyst>();
        services.AddSingleton<IAgent, NewsAnalyst>();
        services.AddSingleton<IAgent, PoliticalRiskAnalyst>();
        services.AddSingleton<IAgent, RiskAnalyst>();

        return services;
    }
}

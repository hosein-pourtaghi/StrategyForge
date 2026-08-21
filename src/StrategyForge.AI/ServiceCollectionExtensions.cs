using Microsoft.Extensions.DependencyInjection;
using StrategyForge.Domain.Interfaces.AI;

namespace StrategyForge.AI;

/// <summary>
/// DI registration extension for the AI layer.
/// Registers agents and LLM provider.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers StrategyForge AI services (agents, LLM provider).
    /// </summary>
    public static IServiceCollection AddStrategyForgeAI(this IServiceCollection services)
    {
        // Register the LLM provider
        // To switch providers: change the registration here or via configuration.
        // services.AddSingleton<ILLMProvider, OpenAiCompatibleLlmProvider>();

        // Register specialist agents
        // Each agent is registered as IAgent so the orchestrator can discover them.
        // To add a new agent: implement IAgent + add a registration line here.
        // services.AddSingleton<IAgent, TechnicalAnalystAgent>();
        // services.AddSingleton<IAgent, FundamentalAnalystAgent>();
        // services.AddSingleton<IAgent, MacroAnalystAgent>();
        // services.AddSingleton<IAgent, NewsAnalystAgent>();
        // services.AddSingleton<IAgent, PoliticalRiskAnalystAgent>();
        // services.AddSingleton<IAgent, RiskAnalystAgent>();

        return services;
    }
}

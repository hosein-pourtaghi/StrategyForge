using Microsoft.Extensions.DependencyInjection;
using StrategyForge.Domain.Interfaces.Background;
using StrategyForge.Domain.Interfaces.Orchestration;
using StrategyForge.Orchestration.Background;

namespace StrategyForge.Orchestration;

/// <summary>
/// DI registration extension for the Orchestration layer.
/// Registers the strategy orchestrator and coordinates all layers.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers StrategyForge Orchestration services.
    /// </summary>
    public static IServiceCollection AddStrategyForgeOrchestration(this IServiceCollection services)
    {
        // Register the orchestrator (Scoped to align with API controllers)
        services.AddScoped<IStrategyOrchestrator, StrategyOrchestrator>();

        // Register the Background Intelligence Engine (Phase 9)
        services.AddSingleton<IntelligenceEngine>();
        services.AddSingleton<IIntelligenceEngine>(sp => sp.GetRequiredService<IntelligenceEngine>());
        services.AddHostedService(sp => sp.GetRequiredService<IntelligenceEngine>());

        return services;
    }
}

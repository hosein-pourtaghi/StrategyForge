using Microsoft.Extensions.DependencyInjection;
using StrategyForge.Domain.Interfaces.Orchestration;

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

        return services;
    }
}

using Microsoft.Extensions.DependencyInjection;
using StrategyForge.Domain.Interfaces.Analysis;

namespace StrategyForge.Analysis;

/// <summary>
/// DI registration extension for the Analysis layer.
/// Registers the indicator engine and all available indicators.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers StrategyForge Analysis services (indicators, engine).
    /// </summary>
    public static IServiceCollection AddStrategyForgeAnalysis(this IServiceCollection services)
    {
        // Register the indicator engine
        services.AddSingleton<IIndicatorEngine, IndicatorEngine>();

        // Register individual indicators
        // Each indicator is registered as IIndicator so the engine can discover them.
        // To add a new indicator: implement IIndicator + add a registration line here.
        // services.AddSingleton<IIndicator, RsiIndicator>();
        // services.AddSingleton<IIndicator, MacdIndicator>();
        // services.AddSingleton<IIndicator, SmIndicator>();
        // services.AddSingleton<IIndicator, EmaIndicator>();
        // services.AddSingleton<IIndicator, BollingerBandsIndicator>();
        // services.AddSingleton<IIndicator, AtrIndicator>();

        return services;
    }
}

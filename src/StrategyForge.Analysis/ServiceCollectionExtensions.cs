using Microsoft.Extensions.DependencyInjection;
using StrategyForge.Analysis.Strategy;
using StrategyForge.Domain.Interfaces.Analysis;

namespace StrategyForge.Analysis;

/// <summary>
/// DI registration extension for the Analysis layer.
/// Registers the indicator engine, all available indicators, and the
/// Phase 8 deterministic strategy layer (rules + evaluation engine).
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
        services.AddSingleton<IIndicator, Indicators.SmIndicator>();
        services.AddSingleton<IIndicator, Indicators.EmaIndicator>();
        services.AddSingleton<IIndicator, Indicators.RsiIndicator>();
        services.AddSingleton<IIndicator, Indicators.MacdIndicator>();
        services.AddSingleton<IIndicator, Indicators.BollingerBandsIndicator>();

        // --- Deterministic strategy layer (Phase 8) ---
        // Rules are stateless interpretations of stored indicator values; the
        // evaluation engine consumes them via the strongly typed list below —
        // deliberately not a generic rule-engine framework.
        services.AddSingleton<StrategyEvaluationEngine>(sp =>
            new StrategyEvaluationEngine(StrategyRuleRegistry.BuiltIn));

        return services;
    }
}

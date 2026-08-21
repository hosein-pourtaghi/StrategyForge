using Microsoft.Extensions.DependencyInjection;
using StrategyForge.Domain.Configuration;
using StrategyForge.Domain.Interfaces.Providers;
using StrategyForge.Infrastructure.DataAdapters;
using StrategyForge.Infrastructure.Services;

namespace StrategyForge.Infrastructure;

/// <summary>
/// DI registration extension for the Infrastructure layer.
/// Registers data providers, services, adapters, and external integrations.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers StrategyForge Infrastructure services.
    /// Includes: rate limiter, cache, calendar, quality validator, adapters, registry.
    /// </summary>
    public static IServiceCollection AddStrategyForgeInfrastructure(this IServiceCollection services)
    {
        // --- Core Infrastructure Services (Singleton) ---
        services.AddSingleton<RateLimiter>();
        services.AddSingleton<InMemoryDataCache>();
        services.AddSingleton<JalaliCalendarService>();
        services.AddSingleton<DataQualityValidator>();

        // --- Named HttpClients for each adapter ---
        services.AddHttpClient("tsetmc");
        services.AddHttpClient("tgju");
        services.AddHttpClient("cbi");

        // --- Source Adapters ---
        // Each adapter gets its own named HttpClient via factory
        services.AddTransient<TsetmcAdapter>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var client = factory.CreateClient("tsetmc");
            return new TsetmcAdapter(
                client,
                sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<DataSourceSettings>>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<TsetmcAdapter>>(),
                sp.GetRequiredService<RateLimiter>(),
                sp.GetRequiredService<InMemoryDataCache>(),
                sp.GetRequiredService<DataQualityValidator>(),
                sp.GetRequiredService<JalaliCalendarService>());
        });

        services.AddTransient<TgjuAdapter>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var client = factory.CreateClient("tgju");
            return new TgjuAdapter(
                client,
                sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<DataSourceSettings>>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<TgjuAdapter>>(),
                sp.GetRequiredService<RateLimiter>(),
                sp.GetRequiredService<InMemoryDataCache>(),
                sp.GetRequiredService<DataQualityValidator>());
        });

        services.AddTransient<CbiAdapter>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var client = factory.CreateClient("cbi");
            return new CbiAdapter(
                client,
                sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<DataSourceSettings>>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<CbiAdapter>>(),
                sp.GetRequiredService<RateLimiter>(),
                sp.GetRequiredService<InMemoryDataCache>(),
                sp.GetRequiredService<DataQualityValidator>());
        });

        // Register adapters under IDataSourceAdapter for IEnumerable injection
        services.AddTransient<IDataSourceAdapter>(sp => sp.GetRequiredService<TsetmcAdapter>());
        services.AddTransient<IDataSourceAdapter>(sp => sp.GetRequiredService<TgjuAdapter>());
        services.AddTransient<IDataSourceAdapter>(sp => sp.GetRequiredService<CbiAdapter>());

        // --- Registry (Singleton — coordinates adapters) ---
        services.AddSingleton<IDataSourceRegistry, DataSourceRegistry>();

        return services;
    }
}

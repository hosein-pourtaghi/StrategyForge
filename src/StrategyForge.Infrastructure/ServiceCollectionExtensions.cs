using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StrategyForge.Domain.Configuration;
using StrategyForge.Domain.Interfaces.Providers;
using StrategyForge.Infrastructure.Authentication;
using StrategyForge.Infrastructure.DataAdapters;
using StrategyForge.Infrastructure.InstrumentResolution;
using StrategyForge.Infrastructure.Services;

namespace StrategyForge.Infrastructure;

/// <summary>
/// DI registration extension for the Infrastructure layer.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddStrategyForgeInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // --- Configuration ---
        services.Configure<DataSourceSettings>(configuration.GetSection(DataSourceSettings.SectionName));

        // --- Core Infrastructure Services (Singleton) ---
        services.AddSingleton<RateLimiter>();
        services.AddSingleton<InMemoryDataCache>();
        services.AddSingleton<JalaliCalendarService>();
        services.AddSingleton<DataQualityValidator>();

        // --- Authentication ---
        services.AddSingleton<CredentialResolver>();
        services.AddSingleton<IDataSourceAuthenticator, CompositeDataSourceAuthenticator>();

        // --- Instrument Resolver ---
        services.AddSingleton<IInstrumentResolver, InMemoryInstrumentResolver>();

        // --- Named HttpClients for each adapter ---
        services.AddHttpClient("tsetmc");
        services.AddHttpClient("tgju");
        services.AddHttpClient("cbi");

        // --- Source Adapters ---
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
                sp.GetRequiredService<IDataSourceAuthenticator>(),
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
                sp.GetRequiredService<DataQualityValidator>(),
                sp.GetRequiredService<IDataSourceAuthenticator>());
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
                sp.GetRequiredService<DataQualityValidator>(),
                sp.GetRequiredService<IDataSourceAuthenticator>());
        });

        services.AddTransient<IDataSourceAdapter>(sp => sp.GetRequiredService<TsetmcAdapter>());
        services.AddTransient<IDataSourceAdapter>(sp => sp.GetRequiredService<TgjuAdapter>());
        services.AddTransient<IDataSourceAdapter>(sp => sp.GetRequiredService<CbiAdapter>());

        // --- Registry ---
        services.AddSingleton<IDataSourceRegistry, DataSourceRegistry>();

        return services;
    }
}

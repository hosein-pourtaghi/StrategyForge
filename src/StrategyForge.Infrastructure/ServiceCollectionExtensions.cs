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
        services.Configure<HistoricalIngestionSettings>(configuration.GetSection(HistoricalIngestionSettings.SectionName));

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
        services.AddHttpClient("tsewebgateway");
        services.AddHttpClient("brsapi");
        services.AddHttpClient("nobitex");

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
                sp.GetRequiredService<IDataSourceAuthenticator>(),
                sp.GetRequiredService<JalaliCalendarService>());
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

        services.AddTransient<TseWebGatewayAdapter>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var client = factory.CreateClient("tsewebgateway");
            return new TseWebGatewayAdapter(
                client,
                sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<DataSourceSettings>>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<TseWebGatewayAdapter>>(),
                sp.GetRequiredService<RateLimiter>(),
                sp.GetRequiredService<InMemoryDataCache>(),
                sp.GetRequiredService<DataQualityValidator>(),
                sp.GetRequiredService<IDataSourceAuthenticator>());
        });

        services.AddTransient<BrsApiAdapter>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var client = factory.CreateClient("brsapi");
            return new BrsApiAdapter(
                client,
                sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<DataSourceSettings>>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<BrsApiAdapter>>(),
                sp.GetRequiredService<RateLimiter>(),
                sp.GetRequiredService<InMemoryDataCache>(),
                sp.GetRequiredService<DataQualityValidator>(),
                sp.GetRequiredService<IDataSourceAuthenticator>());
        });

        services.AddTransient<NobitexAdapter>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var client = factory.CreateClient("nobitex");
            return new NobitexAdapter(
                client,
                sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<DataSourceSettings>>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<NobitexAdapter>>(),
                sp.GetRequiredService<RateLimiter>(),
                sp.GetRequiredService<InMemoryDataCache>(),
                sp.GetRequiredService<DataQualityValidator>(),
                sp.GetRequiredService<IDataSourceAuthenticator>());
        });

        services.AddTransient<IDataSourceAdapter>(sp => sp.GetRequiredService<TsetmcAdapter>());
        services.AddTransient<IDataSourceAdapter>(sp => sp.GetRequiredService<TgjuAdapter>());
        services.AddTransient<IDataSourceAdapter>(sp => sp.GetRequiredService<CbiAdapter>());
        services.AddTransient<IDataSourceAdapter>(sp => sp.GetRequiredService<TseWebGatewayAdapter>());
        services.AddTransient<IDataSourceAdapter>(sp => sp.GetRequiredService<BrsApiAdapter>());
        services.AddTransient<IDataSourceAdapter>(sp => sp.GetRequiredService<NobitexAdapter>());

        // --- Registry ---
        services.AddSingleton<IDataSourceRegistry, DataSourceRegistry>();

        // --- Persistence (Phase 9) ---
        services.Configure<DatabaseSettings>(configuration.GetSection(DatabaseSettings.SectionName));

        // Register persistence stores — always register in-memory fallbacks;
        // PostgreSQL repositories are registered when a real connection string is configured.
        services.AddSingleton<IEvidenceStore, InMemoryEvidenceStore>();
        services.AddSingleton<IStrategyHistoryStore, InMemoryStrategyHistoryStore>();
        services.AddSingleton<IIntelligenceRunStore, InMemoryIntelligenceRunStore>();

        // --- Historical Dataset Pipeline (Phase 6) ---
        // In-memory dataset store is the default; tests and development use it,
        // and callers may replace the registration with the EF Core store when
        // a real database is configured (mirrors the evidence-store pattern above).
        services.AddSingleton<IHistoricalDatasetStore, InMemoryHistoricalDatasetStore>();
        services.AddSingleton<HistoricalIngestionService>();

        // --- Historical Processing Pipeline (Phase 7) ---
        // The in-memory dataset store also implements the paged reader used by the
        // processing pipeline; when a real database is configured, callers replace
        // IHistoricalDatasetStore (and this forwarded reader) with the EF Core
        // implementations (HistoricalDatasetStore + HistoricalDatasetPageReader).
        services.AddSingleton<IHistoricalDatasetPageReader>(sp =>
            (IHistoricalDatasetPageReader)sp.GetRequiredService<IHistoricalDatasetStore>());
        services.Configure<HistoricalProcessingSettings>(configuration.GetSection(HistoricalProcessingSettings.SectionName));
        services.AddSingleton<HistoricalRowValidator>();
        services.AddSingleton<IndicatorWarmupProber>();
        services.AddSingleton<IEnrichedDatasetStore, InMemoryEnrichedDatasetStore>();
        services.AddSingleton<HistoricalProcessingService>();

        // --- AI-Ready Dataset Preparation (Phase 9) ---
        // In-memory enriched page reader mirrors the raw-dataset pattern: forwarded
        // from the registered IEnrichedDatasetStore singleton in dev/test; replaced
        // by the EF Core EnrichedDatasetPageReader when a real database is configured.
        services.Configure<DatasetPreparationSettings>(configuration.GetSection(DatasetPreparationSettings.SectionName));
        services.AddSingleton<IEnrichedDatasetPageReader>(sp =>
        {
            var store = sp.GetRequiredService<IEnrichedDatasetStore>();
            return store is InMemoryEnrichedDatasetStore inMemory
                ? new InMemoryEnrichedDatasetPageReader(inMemory)
                : throw new InvalidOperationException(
                    "No IEnrichedDatasetPageReader registered for the configured IEnrichedDatasetStore. " +
                    "Register EnrichedDatasetPageReader (EF Core) when replacing the in-memory store.");
        });
        services.AddSingleton<AiReadyJsonlExporter>();
        services.AddSingleton<DatasetReportsService>();
        services.AddSingleton<DatasetPreparationService>();
        services.AddSingleton<DatasetPreparationRunner>();

        return services;
    }
}

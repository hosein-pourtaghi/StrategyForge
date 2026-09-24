using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StrategyForge.Domain.Configuration;
using StrategyForge.Domain.Interfaces.Providers;
using StrategyForge.Infrastructure.Data;
using StrategyForge.Infrastructure.Repositories;
using StrategyForge.Infrastructure.Services;

namespace StrategyForge.Infrastructure;

/// <summary>
/// Composition wiring for the PostgreSQL persistence layer.
///
/// Phase 6/7/9 datasets are idempotent on (instrument, source, observation date) —
/// but only when the stores actually survive the process. The in-memory defaults
/// registered by <see cref="ServiceCollectionExtensions.AddStrategyForgeInfrastructure"/>
/// are correct for unit tests and dev exploration; hosts that must keep data across
/// restarts call <see cref="AddStrategyForgePersistence"/>, which replaces:
///
///   IHistoricalDatasetStore      → HistoricalDatasetStore      (EF Core)
///   IHistoricalDatasetPageReader → HistoricalDatasetPageReader (EF Core keyset paging)
///   IEnrichedDatasetStore        → EnrichedDatasetStore        (EF Core)
///   IEnrichedDatasetPageReader   → EnrichedDatasetPageReader   (EF Core keyset paging)
///
/// and registers the <see cref="StrategyForgeDbContext"/> on Npgsql using
/// DatabaseSettings.ConnectionString. Dataset pipeline services that consume the
/// stores are re-registered scoped so they share the DbContext within one
/// operation (scoped DbContext, scoped consumers — no captive-dependency traps;
/// the IntelligenceEngine already resolves collaborators per scope).
///
/// Nothing here mutates the database schema at registration time; apply
/// migrations explicitly (see <see cref="StrategyForgeDesignTimeFactory"/>).
/// </summary>
public static class ServiceCollectionExtensionsPersistence
{
    /// <summary>
    /// Replaces the in-memory historical/enriched dataset stores with the EF Core
    /// PostgreSQL implementations and registers <see cref="StrategyForgeDbContext"/>.
    /// Call AFTER <see cref="ServiceCollectionExtensions.AddStrategyForgeInfrastructure"/>.
    /// </summary>
    public static IServiceCollection AddStrategyForgePersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // --- Options (idempotent if already registered) ---
        services.Configure<DatabaseSettings>(configuration.GetSection(DatabaseSettings.SectionName));

        // --- DbContext (scoped; PostgreSQL) ---
        services.AddDbContext<StrategyForgeDbContext>(options =>
        {
            var settings = configuration
                .GetSection(DatabaseSettings.SectionName)
                .Get<DatabaseSettings>() ?? new DatabaseSettings();

            options.UseNpgsql(settings.ConnectionString, npgsql =>
            {
                if (settings.CommandTimeoutSeconds > 0)
                {
                    npgsql.CommandTimeout(settings.CommandTimeoutSeconds);
                }
            });
        });

        // --- Historical dataset (raw) ---
        services.AddScoped<IHistoricalDatasetStore, HistoricalDatasetStore>();
        services.AddScoped<IHistoricalDatasetPageReader, HistoricalDatasetPageReader>();

        // --- Enriched (derived) dataset ---
        services.AddScoped<IEnrichedDatasetStore, EnrichedDatasetStore>();
        services.AddScoped<IEnrichedDatasetPageReader, EnrichedDatasetPageReader>();

        // --- Dataset pipeline consumers: align with the scoped DbContext ---
        // (These were singletons over in-memory stores; with EF persistence they
        // must share the request scope, otherwise a singleton would capture one
        // DbContext for the process lifetime.)
        services.AddScoped<HistoricalIngestionService>();
        services.AddScoped<HistoricalProcessingService>();
        services.AddScoped<DatasetReportsService>();
        services.AddScoped<DatasetPreparationService>();
        services.AddScoped<DatasetPreparationRunner>();

        return services;
    }
}

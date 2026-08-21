using StrategyForge.Api.Contracts;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Interfaces.Providers;

namespace StrategyForge.Api.Services;

/// <summary>
/// Application service for querying data source capabilities and health.
/// </summary>
public sealed class DataSourceService
{
    private readonly IDataSourceRegistry _registry;

    public DataSourceService(IDataSourceRegistry registry)
    {
        _registry = registry;
    }

    public async Task<IReadOnlyList<DataSourceResponse>> GetSourcesAsync(CancellationToken ct = default)
    {
        var adapters = _registry.GetAllAdapters();
        var healthStatuses = await _registry.GetAllHealthStatusesAsync(ct);

        var responses = adapters.Select(adapter =>
        {
            var supportedAssetClasses = adapter switch
            {
                Infrastructure.DataAdapters.TsetmcAdapter => new[] { "Stock", "ETF", "Index" },
                Infrastructure.DataAdapters.TgjuAdapter => new[] { "Currency", "Commodity", "Crypto" },
                Infrastructure.DataAdapters.CbiAdapter => new[] { "Currency" },
                _ => Array.Empty<string>()
            };

            healthStatuses.TryGetValue(adapter.SourceType, out var health);

            return new DataSourceResponse
            {
                Name = adapter.Name,
                SourceType = adapter.SourceType,
                IsEnabled = adapter.IsEnabled,
                SupportedAssetClasses = supportedAssetClasses,
                Health = health != null ? new HealthResponse
                {
                    IsHealthy = health.IsHealthy,
                    LastSuccessfulRequest = health.LastSuccessfulRequest,
                    LastError = health.LastError,
                    ConsecutiveFailures = health.ConsecutiveFailures
                } : null
            };
        }).ToList().AsReadOnly();

        return responses;
    }
}

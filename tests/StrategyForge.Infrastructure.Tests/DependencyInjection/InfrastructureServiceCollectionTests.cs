using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Interfaces.Providers;
using StrategyForge.Domain.Models;
using StrategyForge.Infrastructure;
using Xunit;

namespace StrategyForge.Infrastructure.Tests.DependencyInjection;

/// <summary>
/// Verifies that the TSETMC provider is registered and resolvable through the
/// existing provider-selection mechanism (IDataSourceRegistry) using the real
/// AddStrategyForgeInfrastructure registration path — with no network access.
/// </summary>
public class InfrastructureServiceCollectionTests
{
    private static IConfiguration BuildConfiguration()
    {
        var values = new Dictionary<string, string?>
        {
            ["DataSourceSettings:HttpTimeoutSeconds"] = "10",
            ["DataSourceSettings:RetryBaseDelayMs"] = "10",
            ["DataSourceSettings:RetryMaxDelayMs"] = "100",
            ["DataSourceSettings:UserAgent"] = "StrategyForge-Test/1.0",
            ["DataSourceSettings:DefaultRateLimit:MaxRequests"] = "1000",
            ["DataSourceSettings:DefaultRateLimit:Window"] = "00:00:01"
        };

        var sources = new (string Key, string Type, string BaseUrl)[]
        {
            ("tsetmc", "Tsetmc", "https://cdn.tsetmc.com"),
            ("tgju", "Tgju", "https://tgju.org"),
            ("cbi", "Cbi", "https://cbi.ir"),
            ("tsewebgateway", "TseWebGateway", "https://cdn.tsetmc.com"),
            ("brsapi", "BrsApi", "https://Api.BrsApi.ir"),
            ("nobitex", "Nobitex", "https://apiv2.nobitex.ir")
        };

        foreach (var (key, type, baseUrl) in sources)
        {
            var prefix = $"DataSourceSettings:Sources:{key}";
            values[$"{prefix}:Name"] = type;
            values[$"{prefix}:SourceType"] = type;
            values[$"{prefix}:Enabled"] = "true";
            values[$"{prefix}:BaseUrl"] = baseUrl;
            values[$"{prefix}:CacheMinutes"] = "5";
            values[$"{prefix}:MaxRetries"] = "0";
            values[$"{prefix}:RateLimit:MaxRequests"] = "1000";
            values[$"{prefix}:RateLimit:Window"] = "00:00:01";
            values[$"{prefix}:Authentication:Mode"] = key == "brsapi" ? "ApiKey" : "None";
            if (key == "brsapi")
            {
                values[$"{prefix}:Authentication:CredentialReference"] = "TestBrsApiKey";
            }
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();

        // Production hosts (WebApplicationBuilder) register IConfiguration
        // automatically; CredentialResolver depends on it, so replicate that here.
        services.AddSingleton<IConfiguration>(BuildConfiguration());

        services.AddStrategyForgeInfrastructure(BuildConfiguration());
        return services.BuildServiceProvider();
    }

    private static InstrumentMapping CreateFooladInstrument() => new()
    {
        InstrumentId = "iran-equity-foolad-4439113430858354",
        Symbol = "Foolad",
        LatinSymbol = "Foolad",
        DisplayName = "Foolad Mobarakeh Steel",
        AssetClass = AssetType.Stock,
        Exchange = "TSE",
        QuoteCurrency = "IRR",
        SourceIdentifiers = new Dictionary<SourceAdapterType, SourceIdentifier>
        {
            [SourceAdapterType.Tsetmc] = new SourceIdentifier { Id = "4439113430858354" }
        }
    };

    [Fact]
    public void AddInfrastructure_TsetmcIsResolvableThroughRegistry()
    {
        using var provider = BuildProvider();

        var registry = provider.GetRequiredService<IDataSourceRegistry>();
        var tsetmc = registry.GetAdapter(SourceAdapterType.Tsetmc);

        Assert.NotNull(tsetmc);
        Assert.Equal("Tsetmc", tsetmc.Name);
        Assert.Contains(MarketDataType.HistoricalCandles, tsetmc.SupportedCapabilities);
        Assert.Contains(MarketDataType.Snapshot, tsetmc.SupportedCapabilities);
        Assert.True(tsetmc.IsEnabled);
        Assert.Equal("cdn.tsetmc.com", Assert.Single(tsetmc.Domains));
    }

    [Fact]
    public void AddInfrastructure_TsetmcSupportsTseStockInstrumentAndCapabilitySelection()
    {
        using var provider = BuildProvider();
        var registry = provider.GetRequiredService<IDataSourceRegistry>();

        var instrument = CreateFooladInstrument();
        var tsetmc = registry.GetAdapter(SourceAdapterType.Tsetmc);

        Assert.NotNull(tsetmc);
        Assert.True(tsetmc.Supports(instrument));

        // Capability-aware source selection must surface TSETMC for historical candles
        var capable = registry.GetAdaptersForCapability(instrument, MarketDataType.HistoricalCandles);
        Assert.Contains(capable, a => a.SourceType == SourceAdapterType.Tsetmc);
    }
}

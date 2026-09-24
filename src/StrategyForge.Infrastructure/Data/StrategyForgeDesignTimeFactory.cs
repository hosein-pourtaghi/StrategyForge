using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using StrategyForge.Domain.Configuration;

namespace StrategyForge.Infrastructure.Data;

/// <summary>
/// Design-time factory so `dotnet ef migrations add/database update` can construct
/// <see cref="StrategyForgeDbContext"/> without a host. Connection string comes from
/// DatabaseSettings:ConnectionString of the API appsettings.json, overridable via the
/// STRATEGYFORGE_CONNECTION_STRING environment variable (CI / tooling).
/// </summary>
public sealed class StrategyForgeDesignTimeFactory
    : IDesignTimeDbContextFactory<StrategyForgeDbContext>
{
    public StrategyForgeDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(
                Path.Combine(Directory.GetCurrentDirectory(), "src", "StrategyForge.Api", "appsettings.json"),
                optional: true)
            .AddEnvironmentVariables()
            .Build();

        var settings = configuration
            .GetSection(DatabaseSettings.SectionName)
            .Get<DatabaseSettings>() ?? new DatabaseSettings();

        var options = new DbContextOptionsBuilder<StrategyForgeDbContext>()
            .UseNpgsql(settings.ConnectionString)
            .Options;

        return new StrategyForgeDbContext(options);
    }
}

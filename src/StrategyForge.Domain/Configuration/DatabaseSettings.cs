namespace StrategyForge.Domain.Configuration;

/// <summary>
/// Configuration for the database connection.
/// Maps to the "DatabaseSettings" section in appsettings.json.
/// </summary>
public sealed record DatabaseSettings
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "DatabaseSettings";

    /// <summary>PostgreSQL connection string.</summary>
    public string ConnectionString { get; init; }
        = "Host=localhost;Port=5432;Database=strategyforge;Username=postgres;Password=postgres";

    /// <summary>Whether to auto-apply migrations on startup.</summary>
    public bool AutoMigrate { get; init; } = true;

    /// <summary>Command timeout in seconds.</summary>
    public int CommandTimeoutSeconds { get; init; } = 30;
}

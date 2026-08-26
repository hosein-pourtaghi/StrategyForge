namespace StrategyForge.Domain.Configuration;

/// <summary>
/// Configuration for the Background Intelligence Engine.
/// Maps to the "BackgroundSettings" section in appsettings.json.
/// </summary>
public sealed record BackgroundSettings
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "BackgroundSettings";

    /// <summary>Whether the background intelligence engine is enabled.</summary>
    public bool Enabled { get; init; } = false;

    /// <summary>
    /// Interval between scheduled intelligence collection runs.
    /// Default: 6 hours. Minimum: 1 hour.
    /// </summary>
    public int IntervalMinutes { get; init; } = 360;

    /// <summary>Whether to automatically generate strategies during scheduled runs.</summary>
    public bool AutoGenerateStrategies { get; init; } = false;

    /// <summary>
    /// Maximum number of assets to process per scheduled run.
    /// Prevents overload when many assets are registered.
    /// </summary>
    public int MaxAssetsPerRun { get; init; } = 10;

    /// <summary>
    /// Timeout for a single intelligence run (in seconds).
    /// Default: 10 minutes.
    /// </summary>
    public int RunTimeoutSeconds { get; init; } = 600;

    /// <summary>
    /// Maximum number of historical evidence records to keep per asset.
    /// Older records are pruned. 0 = unlimited.
    /// </summary>
    public int MaxEvidenceRetention { get; init; } = 500;

    /// <summary>
    /// Maximum number of historical strategy records to keep per asset.
    /// Older records are pruned. 0 = unlimited.
    /// </summary>
    public int MaxStrategyRetention { get; init; } = 200;
}

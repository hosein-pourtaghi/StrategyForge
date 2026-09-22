namespace StrategyForge.Domain.Configuration;

/// <summary>
/// Configuration for optional cross-source validation.
/// When enabled, compatible secondary sources are queried and compared
/// against the primary source for data quality assurance.
/// </summary>
public sealed record CrossValidationSettings
{
    /// <summary>Whether cross-validation is globally enabled. Default: false.</summary>
    public bool Enabled { get; init; } = false;

    /// <summary>
    /// Data types for which cross-validation is enabled.
    /// If empty and Enabled is true, validation applies to all types.
    /// </summary>
    public IReadOnlyList<string> EnabledDataTypes { get; init; } = [];

    /// <summary>
    /// Maximum acceptable age difference between primary and secondary observations.
    /// Default: 5 minutes. Currently advisory — the date comparison is exact-match
    /// on the canonical trading date; no separate staleness window is applied.
    /// </summary>
    public TimeSpan MaximumAgeDifference { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Maximum acceptable relative price deviation (percent) between sources.
    /// differencePercent = abs(primary - secondary) / primary * 100.
    /// A difference exactly equal to the tolerance is within tolerance.
    /// Default: 2.0% — conservative, deterministic, instrument-independent.
    /// </summary>
    public decimal MaximumPriceDeviationPercent { get; init; } = 2.0m;

    /// <summary>
    /// Whether to fail the request when cross-validation detects excessive deviation.
    /// Default: false (warn only, still return primary data).
    /// </summary>
    public bool FailOnExcessiveDeviation { get; init; } = false;
}

using StrategyForge.Domain.Enums;

namespace StrategyForge.Domain.Models;

/// <summary>
/// Describes the adjustment status of historical price data.
/// Historical prices must explicitly state their adjustment status.
/// Never silently assume that OHLC data is adjusted.
/// </summary>
public sealed record DataAdjustment
{
    /// <summary>Whether the prices have been adjusted.</summary>
    public required bool IsAdjusted { get; init; }

    /// <summary>The type of adjustment applied.</summary>
    public required DataAdjustmentType Type { get; init; }

    /// <summary>Which entity performed the adjustment (source, StrategyForge, manual).</summary>
    public string? AdjustmentSource { get; init; }

    /// <summary>Additional notes about the adjustment.</summary>
    public IReadOnlyList<string> Notes { get; init; } = [];

    /// <summary>Unadjusted — raw prices from source.</summary>
    public static DataAdjustment Unadjusted => new()
    {
        IsAdjusted = false,
        Type = DataAdjustmentType.None
    };

    /// <summary>Adjusted by the source provider.</summary>
    public static DataAdjustment SourceAdjusted(string? source = null, params string[] notes) => new()
    {
        IsAdjusted = true,
        Type = DataAdjustmentType.SourceAdjusted,
        AdjustmentSource = source ?? "source",
        Notes = notes
    };

    /// <summary>Adjustment status cannot be determined.</summary>
    public static DataAdjustment Unknown => new()
    {
        IsAdjusted = false,
        Type = DataAdjustmentType.Unknown,
        Notes = ["Adjustment status could not be verified"]
    };
}

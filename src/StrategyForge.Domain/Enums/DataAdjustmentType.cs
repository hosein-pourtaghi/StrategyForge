namespace StrategyForge.Domain.Enums;

/// <summary>
/// Types of price adjustments applied to historical price data.
/// Preserves the nature of any adjustment to maintain data integrity.
/// </summary>
public enum DataAdjustmentType
{
    /// <summary>No adjustment applied — raw/unadjusted prices.</summary>
    None,

    /// <summary>Adjusted by the source provider.</summary>
    SourceAdjusted,

    /// <summary>Adjusted for stock splits.</summary>
    Split,

    /// <summary>Adjusted for dividend payments.</summary>
    Dividend,

    /// <summary>Adjusted for capital increases.</summary>
    CapitalIncrease,

    /// <summary>Manually corrected data.</summary>
    ManualCorrection,

    /// <summary>Adjustment status cannot be determined.</summary>
    Unknown
}

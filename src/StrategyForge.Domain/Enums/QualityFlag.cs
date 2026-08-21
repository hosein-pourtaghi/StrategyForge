namespace StrategyForge.Domain.Enums;

/// <summary>
/// Quality flags that can be attached to acquired data.
/// Each flag indicates a specific quality concern.
/// </summary>
[Flags]
public enum QualityFlag
{
    /// <summary>No quality issues detected.</summary>
    None = 0,

    /// <summary>Data is stale (exceeds freshness threshold).</summary>
    Stale = 1,

    /// <summary>One or more required fields are missing.</summary>
    MissingFields = 2,

    /// <summary>Numeric values failed validation (negative, zero, out-of-range).</summary>
    InvalidNumeric = 4,

    /// <summary>Timestamps are inconsistent or out of order.</summary>
    TimestampIssue = 8,

    /// <summary>OHLC relationships are inconsistent.</summary>
    OhlcInconsistency = 16,

    /// <summary>Data may have been interpolated or estimated.</summary>
    Interpolated = 32,

    /// <summary>Cross-source validation failed.</summary>
    CrossValidationFailed = 64,

    /// <summary>Source returned an unexpected schema.</summary>
    SchemaChange = 128,

    /// <summary>Duplicate records detected.</summary>
    DuplicateRecords = 256,

    /// <summary>Price data may be from a different instrument.</summary>
    InstrumentMismatch = 512
}

namespace StrategyForge.Domain.Enums;

/// <summary>
/// Outcome of a cross-source validation attempt.
/// The states deliberately distinguish "sources agree", "sources disagree",
/// and "comparison could not be performed" so callers never mistake a
/// skipped or incomplete validation for a successful one.
/// </summary>
public enum CrossValidationStatus
{
    /// <summary>
    /// Validation did not run (disabled for this data type, no secondary
    /// source supports the instrument, or nothing comparable was available).
    /// The primary data is unvalidated — never treat it as verified.
    /// </summary>
    NotAttempted,

    /// <summary>Both sources available and within configured tolerance.</summary>
    Consistent,

    /// <summary>
    /// Both sources available but at least one compared field differs beyond
    /// the configured tolerance.
    /// </summary>
    Discrepancy,

    /// <summary>
    /// One source is available but the other could not provide a comparable
    /// observation (unavailable or failed).
    /// </summary>
    Incomplete,

    /// <summary>Neither source could provide usable data.</summary>
    Unavailable,

    /// <summary>
    /// Data is present but unusable: missing required fields, malformed or
    /// non-positive values, different observation dates, identity mismatch,
    /// or semantically incomparable observations (e.g. different rate types).
    /// </summary>
    Invalid
}

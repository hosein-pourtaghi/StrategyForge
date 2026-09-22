using StrategyForge.Domain.Enums;

namespace StrategyForge.Domain.Models;

/// <summary>
/// Result of comparing the same market observation from two independent
/// data sources. Carries enough detail (sources, values, difference,
/// tolerance, reasons) to diagnose a discrepancy without exposing raw
/// provider payloads.
/// </summary>
public sealed record CrossValidationResult
{
    /// <summary>Outcome of the validation attempt.</summary>
    public required CrossValidationStatus Status { get; init; }

    /// <summary>Canonical StrategyForge instrument ID the observations belong to.</summary>
    public string? InstrumentId { get; init; }

    /// <summary>Source adapter that produced the primary observation (if known).</summary>
    public SourceAdapterType? PrimarySource { get; init; }

    /// <summary>Source adapter that produced the secondary observation (if any).</summary>
    public SourceAdapterType? SecondarySource { get; init; }

    /// <summary>Trading date of the compared observation, when identified.</summary>
    public DateOnly? ObservationDate { get; init; }

    /// <summary>Primary source's headline price (close/last).</summary>
    public decimal? PrimaryPrice { get; init; }

    /// <summary>Secondary source's headline price (close/last).</summary>
    public decimal? SecondaryPrice { get; init; }

    /// <summary>
    /// Relative difference between the two headline prices, in percent:
    /// abs(primary - secondary) / reference * 100 (reference = primary value).
    /// </summary>
    public decimal? DifferencePercent { get; init; }

    /// <summary>Configured tolerance (percent) the difference was judged against.</summary>
    public decimal? TolerancePercent { get; init; }

    /// <summary>
    /// Human-readable, payload-free reasons describing the outcome
    /// (e.g. which field differed beyond tolerance, why a comparison
    /// was not possible).
    /// </summary>
    public IReadOnlyList<string> Reasons { get; init; } = [];
}

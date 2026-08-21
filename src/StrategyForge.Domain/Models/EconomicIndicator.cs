namespace StrategyForge.Domain.Models;

/// <summary>
/// Represents a single economic indicator data point (e.g., inflation rate, interest rate).
/// </summary>
public sealed record EconomicIndicator
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Name of the indicator (e.g., "Inflation Rate", "Central Bank Rate").</summary>
    public required string Name { get; init; }

    /// <summary>Optional category (e.g., "Monetary", "Fiscal", "Trade").</summary>
    public string? Category { get; init; }

    /// <summary>The numeric value of the indicator.</summary>
    public required decimal Value { get; init; }

    /// <summary>Unit of measurement (e.g., "%", "IRR", "USD").</summary>
    public string? Unit { get; init; }

    /// <summary>The period this data refers to (e.g., "2024-Q1", "2024-01").</summary>
    public string? Period { get; init; }

    /// <summary>The date this data was reported or published.</summary>
    public DateOnly? ReportedDate { get; init; }

    /// <summary>Previous value for comparison (if available).</summary>
    public decimal? PreviousValue { get; init; }

    /// <summary>Change from previous period (if available).</summary>
    public decimal? Change { get; init; }

    /// <summary>Country or region this indicator applies to.</summary>
    public string? Region { get; init; }

    /// <summary>Metadata about when and from where this data was retrieved.</summary>
    public DataMetadata? Metadata { get; init; }
}

namespace StrategyForge.Domain.Models;

/// <summary>
/// Represents fundamental company information for a stock.
/// Fields are nullable because not all data may be available for every company.
/// </summary>
public sealed record CompanyInfo
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>The asset this company info relates to.</summary>
    public required string Symbol { get; init; }

    /// <summary>Full company name.</summary>
    public required string CompanyName { get; init; }

    /// <summary>Sector classification.</summary>
    public string? Sector { get; init; }

    /// <summary>Industry classification.</summary>
    public string? Industry { get; init; }

    /// <summary>Date the company was established or listed.</summary>
    public DateOnly? EstablishedDate { get; init; }

    // --- Financial Metrics (all nullable — may not be available) ---

    /// <summary>Most recent earnings per share.</summary>
    public decimal? Eps { get; init; }

    /// <summary>Price-to-earnings ratio.</summary>
    public decimal? Pe { get; init; }

    /// <summary>Price-to-book ratio.</summary>
    public decimal? Pb { get; init; }

    /// <summary>Dividend yield as a percentage.</summary>
    public decimal? DividendYield { get; init; }

    /// <summary>Market capitalization.</summary>
    public decimal? MarketCap { get; init; }

    /// <summary>Most recent quarterly/annual revenue.</summary>
    public decimal? Revenue { get; init; }

    /// <summary>Revenue growth rate (year-over-year or quarter-over-quarter).</summary>
    public decimal? RevenueGrowth { get; init; }

    /// <summary>Most recent net profit.</summary>
    public decimal? NetProfit { get; init; }

    /// <summary>Profit growth rate.</summary>
    public decimal? ProfitGrowth { get; init; }

    /// <summary>Gross margin percentage.</summary>
    public decimal? GrossMargin { get; init; }

    /// <summary>Net margin percentage.</summary>
    public decimal? NetMargin { get; init; }

    /// <summary>Total debt.</summary>
    public decimal? TotalDebt { get; init; }

    /// <summary>Cash and cash equivalents.</summary>
    public decimal? Cash { get; init; }

    /// <summary>Date of the most recent financial data.</summary>
    public DateOnly? FinancialDataDate { get; init; }

    /// <summary>Description of the company's business.</summary>
    public string? Description { get; init; }

    /// <summary>Additional company-specific data points.</summary>
    public IReadOnlyDictionary<string, string>? AdditionalData { get; init; }

    /// <summary>Metadata about when and from where this data was retrieved.</summary>
    public DataMetadata? Metadata { get; init; }
}

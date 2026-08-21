namespace StrategyForge.Domain.Enums;

/// <summary>
/// Investment time horizons for strategy analysis.
/// </summary>
public enum TimeHorizon
{
    /// <summary>Days to a few weeks.</summary>
    ShortTerm,

    /// <summary>Weeks to several months.</summary>
    MediumTerm,

    /// <summary>Months to years.</summary>
    LongTerm
}

using StrategyForge.Domain.Interfaces.Analysis;
using StrategyForge.Domain.Models;

namespace StrategyForge.Analysis.Strategy;

/// <summary>
/// Analysis-layer adapter exposing the static <see cref="TimeSeriesSplitter"/>
/// through the Domain <see cref="ITimeSeriesSplitter"/> contract so the dataset
/// preparation layer reuses the ONE existing splitting algorithm without a
/// project reference (dependency inversion, IIndicatorEngine pattern).
/// </summary>
public sealed class TimeSeriesSplitterAdapter : ITimeSeriesSplitter
{
    /// <inheritdoc/>
    public IReadOnlyList<TimeSeriesSplitSegment> Split(
        IReadOnlyList<EnrichedObservation> observations,
        TimeSeriesSplitFractions? fractions = null) =>
        TimeSeriesSplitter.Split(observations, fractions);
}

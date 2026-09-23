using StrategyForge.Domain.Models;

namespace StrategyForge.Analysis.Strategy;

/// <summary>
/// Deterministic chronological splitting of an observation sequence into
/// Research / Validation / Holdout segments. Splitting is strictly
/// positional from the front: past → future. Random shuffling of
/// time-series data is never performed, and no future data can leak into
/// an earlier period because segments are disjoint contiguous date ranges.
/// </summary>
public static class TimeSeriesSplitter
{
    /// <summary>
    /// Splits a chronologically ordered observation sequence into three
    /// chronological segments by fractions of the observation count.
    /// </summary>
    /// <param name="observations">Chronologically ordered observations (oldest first).</param>
    /// <param name="fractions">Split fractions; must sum to at most 1.0.</param>
    public static IReadOnlyList<TimeSeriesSplitSegment> Split(
        IReadOnlyList<EnrichedObservation> observations,
        TimeSeriesSplitFractions? fractions = null)
    {
        var f = fractions ?? TimeSeriesSplitFractions.Default;
        var total = observations.Count;

        if (total == 0)
        {
            return [];
        }

        if (f.ResearchFraction < 0 || f.ValidationFraction < 0
            || f.ResearchFraction + f.ValidationFraction > 1m)
        {
            throw new ArgumentException(
                "Split fractions must be non-negative and sum to at most 1.0.");
        }

        var researchCount = (int)Math.Floor(total * f.ResearchFraction);
        var validationCount = (int)Math.Floor(total * f.ValidationFraction);

        // Remainder goes to Holdout. Guard against rounding producing
        // segments that overlap or leave a segment empty at the boundaries.
        researchCount = Math.Clamp(researchCount, 0, total);
        validationCount = Math.Clamp(validationCount, 0, total - researchCount);

        var segments = new List<TimeSeriesSplitSegment>(3);
        var index = 0;

        index = AddSegment(segments, TimeSeriesSegment.Research, observations, index, researchCount);
        index = AddSegment(segments, TimeSeriesSegment.Validation, observations, index, validationCount);
        AddSegment(segments, TimeSeriesSegment.Holdout, observations, index, total - index);

        return segments;
    }

    private static int AddSegment(
        List<TimeSeriesSplitSegment> segments,
        TimeSeriesSegment segment,
        IReadOnlyList<EnrichedObservation> observations,
        int start,
        int count)
    {
        if (count <= 0)
        {
            return start;
        }

        segments.Add(new TimeSeriesSplitSegment
        {
            Segment = segment,
            From = observations[start].ObservationDate,
            FirstObservation = observations[start].ObservationDate,
            LastObservation = observations[start + count - 1].ObservationDate,
            Observations = count
        });

        return start + count;
    }
}

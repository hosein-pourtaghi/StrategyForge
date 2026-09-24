using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Models;

namespace StrategyForge.Domain.Interfaces.Analysis;

/// <summary>
/// Abstraction over the deterministic AI-ready projection so the dataset
/// preparation layer can depend on the abstraction rather than the Analysis
/// project (dependency inversion — the same pattern as <c>IIndicatorEngine</c>).
///
/// The implementation (Analysis) transforms validated, indicator-enriched
/// observations into <see cref="AiReadyObservation"/> records:
///   - Features for index i come strictly from observations [0..i].
///   - Labels for index i are computed strictly from observations [i+1..].
///   - Rules are evaluated against the same features a decision at T would see.
/// No future information can leak into the feature state of any record.
/// </summary>
public interface IAiReadyDatasetProjector
{
    /// <summary>
    /// Projects a chronologically ordered sequence (oldest first) of enriched
    /// observations for ONE instrument and ONE source into AI-ready records.
    /// </summary>
    /// <param name="observations">Chronologically ordered enriched observations. Invalid rows must already be excluded.</param>
    /// <param name="thresholds">Deterministic rule/regime thresholds; null uses the implementation defaults.</param>
    /// <param name="contextWindowObservations">Historical context points per record (0 disables context).</param>
    /// <param name="forwardHorizons">Forward-return horizons in observations (must be non-empty and positive).</param>
    /// <param name="provenanceResolver">Maps an observation to its provenance reference (from raw metadata when available).</param>
    /// <param name="rules">Rules evaluated per record; null uses the registered built-in rules.</param>
    IReadOnlyList<AiReadyObservation> Project(
        IReadOnlyList<EnrichedObservation> observations,
        StrategyThresholds? thresholds = null,
        int contextWindowObservations = 20,
        IReadOnlyList<int>? forwardHorizons = null,
        Func<EnrichedObservation, AiReadyProvenanceReference>? provenanceResolver = null,
        IReadOnlyList<IStrategyRule>? rules = null);
}

/// <summary>
/// Abstraction over the deterministic chronological time-series splitter
/// (Research → Validation → Holdout). Reused by the AI-ready dataset
/// preparation layer so there is exactly one splitting algorithm.
/// </summary>
public interface ITimeSeriesSplitter
{
    /// <summary>
    /// Splits a chronologically ordered observation sequence into three
    /// chronological segments by fractions of the observation count.
    /// Never shuffles — chronological order is preserved.
    /// </summary>
    IReadOnlyList<TimeSeriesSplitSegment> Split(
        IReadOnlyList<EnrichedObservation> observations,
        TimeSeriesSplitFractions? fractions = null);
}

/// <summary>
/// Registry contract exposing the built-in deterministic strategy rule names in
/// stable evaluation order. Consumed by statistics accumulation so rule match
/// distributions include zero-match rules. Keeps the Infrastructure layer from
/// referencing the Analysis implementation's static registry.
/// </summary>
public interface IStrategyRuleRegistry
{
    /// <summary>All built-in rules in stable evaluation order.</summary>
    IReadOnlyList<IStrategyRule> BuiltIn { get; }

    /// <summary>All built-in rule names in stable evaluation order.</summary>
    IReadOnlyList<string> BuiltInNames { get; }
}

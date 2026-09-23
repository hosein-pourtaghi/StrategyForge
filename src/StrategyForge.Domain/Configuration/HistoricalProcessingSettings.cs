namespace StrategyForge.Domain.Configuration;

/// <summary>
/// Configuration for the historical data processing pipeline.
/// Maps to the "HistoricalProcessing" section in appsettings.json.
///
/// Design rules enforced by this phase:
/// - The raw historical dataset is never mutated; enrichment writes to a derived store.
/// - Batches are bounded: the pipeline never loads an entire dataset into memory.
/// - Indicator warm-up is derived from the registered indicators' actual requirements,
///   not hardcoded.
/// - Deterministic ordering: (instrument, source, observation date).
/// - Re-processing is idempotent: identity = (instrument, source, observation date).
/// </summary>
public sealed record HistoricalProcessingSettings
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "HistoricalProcessing";

    /// <summary>
    /// Raw rows read per database batch. Must be at least 1.
    /// Default: 1000. Bounded memory: peak usage scales with this, not dataset size.
    /// </summary>
    public int BatchSize { get; init; } = 1000;

    /// <summary>
    /// Multiplier applied to the derived warm-up requirement when loading indicator
    /// context before the requested range. The derived requirement is computed from
    /// the registered indicators' actual period parameters; this multiplier exists to
    /// absorb EMA convergence behavior (an EMA seeded at N observations has not fully
    /// converged to its asymptotic value). Must be at least 1. Default: 3.
    /// </summary>
    public int WarmupFactor { get; init; } = 3;

    /// <summary>
    /// Upper bound on warm-up rows loaded before the requested range, regardless of
    /// indicator parameters. Prevents pathological parameter sets from loading
    /// unbounded history. Must be at least 0. Default: 5000.
    /// </summary>
    public int MaxWarmupRows { get; init; } = 5000;

    /// <summary>Pipeline version stamped on derived observations for traceability.</summary>
    public string PipelineVersion { get; init; } = "1.0.0";
}

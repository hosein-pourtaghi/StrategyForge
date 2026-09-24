namespace StrategyForge.Domain.Configuration;

/// <summary>
/// Configuration for the AI-ready dataset preparation pipeline (Phase 9).
/// Maps to the "DatasetPreparation" section in appsettings.json.
///
/// Design rules enforced by this phase:
/// - The AI-ready layer is a projection — raw and enriched datasets are never mutated.
/// - Feature selection is deterministic and explicit (AiReadyFeaturePolicy).
/// - Future labels are computed strictly from observations after the target date.
/// - Export is streaming: memory scales with batch size, not dataset size.
/// - Determinism: identical dataset + configuration produce identical output
///   (excluding the manifest's CreatedAtUtc, which is export metadata only).
/// </summary>
public sealed record DatasetPreparationSettings
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "DatasetPreparation";

    /// <summary>
    /// Enriched rows read per streaming batch. Must be at least 1. Default: 1000.
    /// Bounded memory: peak usage scales with this, not dataset size.
    /// </summary>
    public int BatchSize { get; init; } = 1000;

    /// <summary>Default historical context window per record (observations). Must be at least 0. Default: 20.</summary>
    public int ContextWindowObservations { get; init; } = 20;

    /// <summary>
    /// Default forward-return horizons in observations. Null uses <see cref="DefaultForwardHorizons"/>.
    /// Deliberately nullable: the configuration binder appends to pre-initialized collections,
    /// which would duplicate the defaults (1,5,10 → 1,5,10,1,5,10).
    /// </summary>
    public IReadOnlyList<int>? ForwardHorizons { get; init; }

    /// <summary>Default forward-return horizons used when not configured.</summary>
    public static IReadOnlyList<int> DefaultForwardHorizons { get; } = [1, 5, 10];

    /// <summary>Horizons to use for preparation (configured or default).</summary>
    public IReadOnlyList<int> EffectiveForwardHorizons =>
        ForwardHorizons is { Count: > 0 } ? ForwardHorizons : DefaultForwardHorizons;

    /// <summary>Pipeline version stamped into the manifest for traceability.</summary>
    public string PipelineVersion { get; init; } = "1.0.0";

    /// <summary>
    /// Number of deterministic first/last records returned for human inspection. Default: 10.
    /// </summary>
    public int InspectionSampleSize { get; init; } = 10;
}

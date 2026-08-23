using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Models;

namespace StrategyForge.Api.Contracts;

/// <summary>
/// API response wrapper for strategy generation results.
/// </summary>
public sealed record StrategyResultResponse
{
    /// <summary>Whether the request was successful.</summary>
    public bool Ok { get; init; }

    /// <summary>The strategy report (only when Ok is true).</summary>
    public StrategyReportResponse? Data { get; init; }

    /// <summary>Error details (only when Ok is false).</summary>
    public StrategyErrorResponse? Error { get; init; }

    /// <summary>Metadata about the synthesis process.</summary>
    public StrategyMetadataResponse? Metadata { get; init; }
}

/// <summary>
/// Serialized strategy report for API responses.
/// Flattens the nested domain model into a clean JSON shape.
/// </summary>
public sealed record StrategyReportResponse
{
    // --- Identity ---
    public required string AssetSymbol { get; init; }
    public required string AssetName { get; init; }
    public required AssetType AssetType { get; init; }
    public required string Market { get; init; }
    public required DateTimeOffset GeneratedAt { get; init; }
    public required DateTimeOffset DataAsOf { get; init; }

    // --- Executive Summary ---
    public required ExecutiveSummaryResponse ExecutiveSummary { get; init; }

    // --- Market Context ---
    public required MarketContextResponse MarketContext { get; init; }

    // --- Agent Analysis Results ---
    public AgentAnalysisResponse? TechnicalAnalysis { get; init; }
    public AgentAnalysisResponse? FundamentalAnalysis { get; init; }
    public AgentAnalysisResponse? MacroAnalysis { get; init; }
    public AgentAnalysisResponse? NewsAnalysis { get; init; }
    public AgentAnalysisResponse? PoliticalRiskAnalysis { get; init; }
    public AgentAnalysisResponse? RiskAnalysis { get; init; }

    // --- Scenarios ---
    public ScenarioResponse? BullishScenario { get; init; }
    public ScenarioResponse? BaseScenario { get; init; }
    public ScenarioResponse? BearishScenario { get; init; }

    // --- Strategy Sections ---
    public StrategySectionResponse? ShortTermStrategy { get; init; }
    public StrategySectionResponse? MediumTermStrategy { get; init; }
    public StrategySectionResponse? LongTermStrategy { get; init; }

    // --- Risk & Confidence ---
    public RiskRewardResponse? RiskReward { get; init; }
    public ConfidenceResponse? Confidence { get; init; }

    // --- Evidence Traceability ---
    public IReadOnlyList<EvidenceItemResponse> SupportingEvidence { get; init; } = [];
    public IReadOnlyList<EvidenceItemResponse> ContradictingEvidence { get; init; } = [];
    public IReadOnlyList<string> MissingInformation { get; init; } = [];
    public IReadOnlyList<string> InvalidationConditions { get; init; } = [];
    public IReadOnlyList<string> MonitoringRecommendations { get; init; } = [];

    // --- Metadata ---
    public IReadOnlyList<string> ContributingAgents { get; init; } = [];
    public IReadOnlyList<string> DataProvidersUsed { get; init; } = [];

    /// <summary>
    /// Maps from a domain StrategyReport to the API response.
    /// </summary>
    public static StrategyReportResponse FromDomain(StrategyReport report) => new()
    {
        AssetSymbol = report.Asset.Symbol,
        AssetName = report.Asset.Name,
        AssetType = report.Asset.AssetType,
        Market = report.Asset.Market,
        GeneratedAt = report.GeneratedAt,
        DataAsOf = report.DataAsOf,
        ExecutiveSummary = ExecutiveSummaryResponse.FromDomain(report.ExecutiveSummary),
        MarketContext = MarketContextResponse.FromDomain(report.MarketContext),
        TechnicalAnalysis = report.TechnicalAnalysis != null ? AgentAnalysisResponse.FromDomain(report.TechnicalAnalysis) : null,
        FundamentalAnalysis = report.FundamentalAnalysis != null ? AgentAnalysisResponse.FromDomain(report.FundamentalAnalysis) : null,
        MacroAnalysis = report.MacroAnalysis != null ? AgentAnalysisResponse.FromDomain(report.MacroAnalysis) : null,
        NewsAnalysis = report.NewsAnalysis != null ? AgentAnalysisResponse.FromDomain(report.NewsAnalysis) : null,
        PoliticalRiskAnalysis = report.PoliticalRiskAnalysis != null ? AgentAnalysisResponse.FromDomain(report.PoliticalRiskAnalysis) : null,
        RiskAnalysis = report.RiskAnalysis != null ? AgentAnalysisResponse.FromDomain(report.RiskAnalysis) : null,
        BullishScenario = report.BullishScenario != null ? ScenarioResponse.FromDomain(report.BullishScenario) : null,
        BaseScenario = report.BaseScenario != null ? ScenarioResponse.FromDomain(report.BaseScenario) : null,
        BearishScenario = report.BearishScenario != null ? ScenarioResponse.FromDomain(report.BearishScenario) : null,
        ShortTermStrategy = report.ShortTermStrategy != null ? StrategySectionResponse.FromDomain(report.ShortTermStrategy) : null,
        MediumTermStrategy = report.MediumTermStrategy != null ? StrategySectionResponse.FromDomain(report.MediumTermStrategy) : null,
        LongTermStrategy = report.LongTermStrategy != null ? StrategySectionResponse.FromDomain(report.LongTermStrategy) : null,
        RiskReward = report.RiskReward != null ? RiskRewardResponse.FromDomain(report.RiskReward) : null,
        Confidence = report.Confidence != null ? ConfidenceResponse.FromDomain(report.Confidence) : null,
        SupportingEvidence = report.SupportingEvidence.Select(EvidenceItemResponse.FromDomain).ToList(),
        ContradictingEvidence = report.ContradictingEvidence.Select(EvidenceItemResponse.FromDomain).ToList(),
        MissingInformation = report.MissingInformation,
        InvalidationConditions = report.InvalidationConditions,
        MonitoringRecommendations = report.MonitoringRecommendations,
        ContributingAgents = report.ContributingAgents,
        DataProvidersUsed = report.DataProvidersUsed
    };
}

// --- Sub-responses ---

public sealed record ExecutiveSummaryResponse
{
    public required Sentiment OverallSentiment { get; init; }
    public required string Summary { get; init; }
    public string? KeyTakeaway { get; init; }
    public string? CriticalLevel { get; init; }
    public string? Urgency { get; init; }

    public static ExecutiveSummaryResponse FromDomain(ExecutiveSummary s) => new()
    {
        OverallSentiment = s.OverallSentiment,
        Summary = s.Summary,
        KeyTakeaway = s.KeyTakeaway,
        CriticalLevel = s.CriticalLevel,
        Urgency = s.Urgency
    };
}

public sealed record MarketContextResponse
{
    public required MarketRegime Regime { get; init; }
    public required string Description { get; init; }
    public decimal? CurrentPrice { get; init; }
    public decimal? RecentPriceChange { get; init; }
    public string? VolumeContext { get; init; }
    public string? MacroContext { get; init; }
    public IReadOnlyList<string> UpcomingEvents { get; init; } = [];

    public static MarketContextResponse FromDomain(MarketContext m) => new()
    {
        Regime = m.Regime,
        Description = m.Description,
        CurrentPrice = m.CurrentPrice,
        RecentPriceChange = m.RecentPriceChange,
        VolumeContext = m.VolumeContext,
        MacroContext = m.MacroContext,
        UpcomingEvents = m.UpcomingEvents
    };
}

public sealed record AgentAnalysisResponse
{
    public required string AgentName { get; init; }
    public required Sentiment Sentiment { get; init; }
    public required decimal Confidence { get; init; }
    public required string Summary { get; init; }
    public string? DetailedAnalysis { get; init; }
    public IReadOnlyList<EvidenceItemResponse> SupportingEvidence { get; init; } = [];
    public IReadOnlyList<EvidenceItemResponse> ContradictingEvidence { get; init; } = [];
    public IReadOnlyList<string> IdentifiedRisks { get; init; } = [];

    public static AgentAnalysisResponse FromDomain(AgentAnalysisResult r) => new()
    {
        AgentName = r.AgentName,
        Sentiment = r.Sentiment,
        Confidence = r.Confidence,
        Summary = r.Summary,
        DetailedAnalysis = r.DetailedAnalysis,
        SupportingEvidence = r.SupportingEvidence.Select(EvidenceItemResponse.FromDomain).ToList(),
        ContradictingEvidence = r.ContradictingEvidence.Select(EvidenceItemResponse.FromDomain).ToList(),
        IdentifiedRisks = r.IdentifiedRisks
    };
}

public sealed record ScenarioResponse
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public IReadOnlyList<string> Assumptions { get; init; } = [];
    public string? ProbabilityAssessment { get; init; }
    public string? ExpectedOutcome { get; init; }
    public IReadOnlyList<string> ConfirmationConditions { get; init; } = [];
    public IReadOnlyList<string> InvalidationConditions { get; init; } = [];

    public static ScenarioResponse FromDomain(Scenario s) => new()
    {
        Name = s.Name,
        Description = s.Description,
        Assumptions = s.Assumptions,
        ProbabilityAssessment = s.ProbabilityAssessment,
        ExpectedOutcome = s.ExpectedOutcome,
        ConfirmationConditions = s.ConfirmationConditions,
        InvalidationConditions = s.InvalidationConditions
    };
}

public sealed record StrategySectionResponse
{
    public required TimeHorizon TimeHorizon { get; init; }
    public string? EntryScenario { get; init; }
    public IReadOnlyList<string> EntryZones { get; init; } = [];
    public IReadOnlyList<string> ConfirmationConditions { get; init; } = [];
    public string? StopInvalidation { get; init; }
    public IReadOnlyList<string> TargetLevels { get; init; } = [];
    public string? ExitConditions { get; init; }
    public string? RiskAssessment { get; init; }
    public IReadOnlyList<string> MonitoringActions { get; init; } = [];

    public static StrategySectionResponse FromDomain(StrategySection s) => new()
    {
        TimeHorizon = s.TimeHorizon,
        EntryScenario = s.EntryScenario,
        EntryZones = s.EntryZones,
        ConfirmationConditions = s.ConfirmationConditions,
        StopInvalidation = s.StopInvalidation,
        TargetLevels = s.TargetLevels,
        ExitConditions = s.ExitConditions,
        RiskAssessment = s.RiskAssessment,
        MonitoringActions = s.MonitoringActions
    };
}

public sealed record RiskRewardResponse
{
    public string? PotentialUpside { get; init; }
    public string? PotentialDownside { get; init; }
    public string? RiskRewardRatio { get; init; }
    public string? RiskLevel { get; init; }
    public IReadOnlyList<string> KeyRiskFactors { get; init; } = [];
    public IReadOnlyList<string> FavorableFactors { get; init; } = [];
    public IReadOnlyList<string> UnfavorableFactors { get; init; } = [];

    public static RiskRewardResponse FromDomain(RiskRewardAssessment r) => new()
    {
        PotentialUpside = r.PotentialUpside,
        PotentialDownside = r.PotentialDownside,
        RiskRewardRatio = r.RiskRewardRatio,
        RiskLevel = r.RiskLevel,
        KeyRiskFactors = r.KeyRiskFactors,
        FavorableFactors = r.FavorableFactors,
        UnfavorableFactors = r.UnfavorableFactors
    };
}

public sealed record ConfidenceResponse
{
    public decimal OverallConfidence { get; init; }
    public string? Level { get; init; }
    public IReadOnlyList<string> ConfidenceFactors { get; init; } = [];
    public IReadOnlyList<string> UncertaintyFactors { get; init; } = [];
    public IReadOnlyList<string> InformationThatWouldHelp { get; init; } = [];
    public int DataSourcesUsed { get; init; }
    public int AgentsContributed { get; init; }

    public static ConfidenceResponse FromDomain(ConfidenceAssessment c) => new()
    {
        OverallConfidence = c.OverallConfidence,
        Level = c.Level,
        ConfidenceFactors = c.ConfidenceFactors,
        UncertaintyFactors = c.UncertaintyFactors,
        InformationThatWouldHelp = c.InformationThatWouldHelp,
        DataSourcesUsed = c.DataSourcesUsed,
        AgentsContributed = c.AgentsContributed
    };
}

public sealed record EvidenceItemResponse
{
    public required string Content { get; init; }
    public required EvidenceType Type { get; init; }
    public required string Source { get; init; }
    public decimal? Confidence { get; init; }

    public static EvidenceItemResponse FromDomain(EvidenceItem e) => new()
    {
        Content = e.Content,
        Type = e.Type,
        Source = e.Source,
        Confidence = e.Confidence
    };
}

public sealed record StrategyErrorResponse
{
    public required string Code { get; init; }
    public required string Message { get; init; }
    public bool Retryable { get; init; }
}

public sealed record StrategyMetadataResponse
{
    public string? LlmModel { get; init; }
    public int TokensUsed { get; init; }
    public TimeSpan? Duration { get; init; }
}

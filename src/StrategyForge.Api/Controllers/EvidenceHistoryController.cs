using Microsoft.AspNetCore.Mvc;
using StrategyForge.Domain.Interfaces.Background;
using StrategyForge.Domain.Interfaces.Providers;

namespace StrategyForge.Api.Controllers;

/// <summary>
/// API for querying persisted evidence and strategy history.
/// Provides access to historical analysis results and background intelligence runs.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class EvidenceHistoryController : ControllerBase
{
    private readonly IEvidenceStore _evidenceStore;
    private readonly IStrategyHistoryStore _strategyStore;
    private readonly IIntelligenceEngine _intelligenceEngine;

    public EvidenceHistoryController(
        IEvidenceStore evidenceStore,
        IStrategyHistoryStore strategyStore,
        IIntelligenceEngine intelligenceEngine)
    {
        _evidenceStore = evidenceStore;
        _strategyStore = strategyStore;
        _intelligenceEngine = intelligenceEngine;
    }

    /// <summary>
    /// Get the most recent evidence for an asset.
    /// </summary>
    [HttpGet("evidence/latest/{assetSymbol}")]
    [ProducesResponseType(typeof(EvidenceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLatestEvidence(
        string assetSymbol,
        CancellationToken ct)
    {
        var evidence = await _evidenceStore.GetLatestByAssetAsync(assetSymbol, ct);
        if (evidence == null)
            return NotFound($"No evidence found for '{assetSymbol}'");

        return Ok(new EvidenceResponse
        {
            Id = evidence.Id,
            AssetSymbol = evidence.Asset.Symbol,
            AssetName = evidence.Asset.Name,
            AssembledAt = evidence.AssembledAt,
            IndicatorCount = evidence.IndicatorCount,
            NewsItemCount = evidence.NewsItemCount,
            DataSources = evidence.DataSources
        });
    }

    /// <summary>
    /// Get evidence history for an asset within a date range.
    /// </summary>
    [HttpGet("evidence/{assetSymbol}")]
    [ProducesResponseType(typeof(IReadOnlyList<EvidenceResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEvidenceHistory(
        string assetSymbol,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int maxResults = 50,
        CancellationToken ct = default)
    {
        var effectiveFrom = from ?? DateTimeOffset.UtcNow.AddDays(-30);
        var effectiveTo = to ?? DateTimeOffset.UtcNow;

        var evidence = await _evidenceStore.GetByAssetAndDateRangeAsync(
            assetSymbol, effectiveFrom, effectiveTo, maxResults, ct);

        var responses = evidence.Select(e => new EvidenceResponse
        {
            Id = e.Id,
            AssetSymbol = e.Asset.Symbol,
            AssetName = e.Asset.Name,
            AssembledAt = e.AssembledAt,
            IndicatorCount = e.IndicatorCount,
            NewsItemCount = e.NewsItemCount,
            DataSources = e.DataSources
        }).ToList();

        return Ok(responses);
    }

    /// <summary>
    /// Get the most recent strategy for an asset.
    /// </summary>
    [HttpGet("strategy/latest/{assetSymbol}")]
    [ProducesResponseType(typeof(StrategyHistoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLatestStrategy(
        string assetSymbol,
        CancellationToken ct)
    {
        var strategy = await _strategyStore.GetLatestByAssetAsync(assetSymbol, ct);
        if (strategy == null)
            return NotFound($"No strategy found for '{assetSymbol}'");

        return Ok(StrategyHistoryResponse.FromDomain(strategy));
    }

    /// <summary>
    /// Get strategy history for an asset within a date range.
    /// </summary>
    [HttpGet("strategy/{assetSymbol}")]
    [ProducesResponseType(typeof(IReadOnlyList<StrategyHistoryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStrategyHistory(
        string assetSymbol,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int maxResults = 50,
        CancellationToken ct = default)
    {
        var effectiveFrom = from ?? DateTimeOffset.UtcNow.AddDays(-30);
        var effectiveTo = to ?? DateTimeOffset.UtcNow;

        var strategies = await _strategyStore.GetByAssetAndDateRangeAsync(
            assetSymbol, effectiveFrom, effectiveTo, maxResults, ct);

        var responses = strategies.Select(s => StrategyHistoryResponse.FromDomain(s)).ToList();

        return Ok(responses);
    }

    /// <summary>
    /// Trigger an immediate intelligence collection run for specified assets.
    /// </summary>
    [HttpPost("run")]
    [ProducesResponseType(typeof(IntelligenceRunResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> TriggerRun(
        [FromBody] IntelligenceRunRequest request,
        CancellationToken ct)
    {
        var run = await _intelligenceEngine.RunAsync(
            request.AssetSymbols,
            request.GenerateStrategies,
            ct);

        return Ok(IntelligenceRunResponse.FromDomain(run));
    }

    /// <summary>
    /// Get the history of intelligence runs.
    /// </summary>
    [HttpGet("runs")]
    [ProducesResponseType(typeof(IReadOnlyList<IntelligenceRunResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRunHistory(
        [FromQuery] int maxResults = 50,
        CancellationToken ct = default)
    {
        var runs = await _intelligenceEngine.GetRunHistoryAsync(maxResults, ct);
        var responses = runs.Select(r => IntelligenceRunResponse.FromDomain(r)).ToList();
        return Ok(responses);
    }
}

// --- Response DTOs ---

public sealed class EvidenceResponse
{
    public Guid Id { get; init; }
    public string AssetSymbol { get; init; } = "";
    public string AssetName { get; init; } = "";
    public DateTimeOffset AssembledAt { get; init; }
    public int IndicatorCount { get; init; }
    public int NewsItemCount { get; init; }
    public IReadOnlyList<string> DataSources { get; init; } = [];
}

public sealed class StrategyHistoryResponse
{
    public Guid Id { get; init; }
    public string AssetSymbol { get; init; } = "";
    public string AssetName { get; init; } = "";
    public DateTimeOffset GeneratedAt { get; init; }
    public string OverallSentiment { get; init; } = "";
    public decimal? OverallConfidence { get; init; }
    public string PipelineState { get; init; } = "";
    public IReadOnlyList<string> ContributingAgents { get; init; } = [];
    public string? LlmModel { get; init; }
    public int? TokensUsed { get; init; }

    public static StrategyHistoryResponse FromDomain(Domain.Models.PersistedStrategy strategy) => new()
    {
        Id = strategy.Id,
        AssetSymbol = strategy.Asset.Symbol,
        AssetName = strategy.Asset.Name,
        GeneratedAt = strategy.GeneratedAt,
        OverallSentiment = strategy.OverallSentiment.ToString(),
        OverallConfidence = strategy.OverallConfidence,
        PipelineState = strategy.PipelineState.ToString(),
        ContributingAgents = strategy.ContributingAgents,
        LlmModel = strategy.LlmModel,
        TokensUsed = strategy.TokensUsed
    };
}

public sealed class IntelligenceRunRequest
{
    public IReadOnlyList<string>? AssetSymbols { get; init; }
    public bool GenerateStrategies { get; init; }
}

public sealed class IntelligenceRunResponse
{
    public Guid Id { get; init; }
    public DateTimeOffset ScheduledAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public string State { get; init; } = "";
    public int SuccessfulAssets { get; init; }
    public int FailedAssets { get; init; }
    public int TotalTokensUsed { get; init; }
    public TimeSpan? TotalDuration { get; init; }

    public static IntelligenceRunResponse FromDomain(Domain.Models.IntelligenceRun run) => new()
    {
        Id = run.Id,
        ScheduledAt = run.ScheduledAt,
        CompletedAt = run.CompletedAt,
        State = run.State.ToString(),
        SuccessfulAssets = run.SuccessfulAssets,
        FailedAssets = run.FailedAssets,
        TotalTokensUsed = run.TotalTokensUsed,
        TotalDuration = run.TotalDuration
    };
}

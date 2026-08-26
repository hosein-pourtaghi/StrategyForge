using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Interfaces.Providers;
using StrategyForge.Domain.Models;
using StrategyForge.Infrastructure.Data;
using StrategyForge.Infrastructure.Data.Entities;

namespace StrategyForge.Infrastructure.Repositories;

/// <summary>
/// PostgreSQL-backed implementation of IStrategyHistoryStore.
/// Stores strategy reports as JSON in the database with scalar columns for efficient querying.
/// </summary>
public sealed class StrategyHistoryStore : IStrategyHistoryStore
{
    private readonly StrategyForgeDbContext _db;
    private readonly ILogger<StrategyHistoryStore> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public StrategyHistoryStore(StrategyForgeDbContext db, ILogger<StrategyHistoryStore> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<PersistedStrategy> StoreAsync(
        PersistedStrategy strategy,
        CancellationToken cancellationToken = default)
    {
        var entity = new StrategyEntity
        {
            Id = strategy.Id,
            AssetSymbol = strategy.Asset.Symbol,
            AssetName = strategy.Asset.Name,
            AssetMarket = strategy.Asset.Market,
            GeneratedAt = strategy.GeneratedAt,
            AssetJson = JsonSerializer.Serialize(strategy.Asset, JsonOptions),
            ReportJson = JsonSerializer.Serialize(strategy.Report, JsonOptions),
            OverallSentiment = strategy.OverallSentiment.ToString(),
            OverallConfidence = strategy.OverallConfidence,
            PipelineState = strategy.PipelineState.ToString(),
            ContributingAgents = strategy.ContributingAgents.Count > 0
                ? string.Join(",", strategy.ContributingAgents)
                : null,
            LlmModel = strategy.LlmModel,
            TokensUsed = strategy.TokensUsed,
            GenerationDurationMs = strategy.GenerationDuration.HasValue
                ? (long)strategy.GenerationDuration.Value.TotalMilliseconds
                : null,
            EvidenceId = strategy.EvidenceId
        };

        _db.Strategies.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogDebug(
            "Stored strategy {Id} for {Symbol} at {GeneratedAt} (sentiment={Sentiment})",
            entity.Id, strategy.Asset.Symbol, strategy.GeneratedAt, strategy.OverallSentiment);

        return strategy;
    }

    /// <inheritdoc/>
    public async Task<PersistedStrategy?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.Strategies
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        return entity == null ? null : MapToDomain(entity);
    }

    /// <inheritdoc/>
    public async Task<PersistedStrategy?> GetLatestByAssetAsync(
        string assetSymbol,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.Strategies
            .AsNoTracking()
            .Where(e => e.AssetSymbol == assetSymbol)
            .OrderByDescending(e => e.GeneratedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return entity == null ? null : MapToDomain(entity);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PersistedStrategy>> GetByAssetAndDateRangeAsync(
        string assetSymbol,
        DateTimeOffset from,
        DateTimeOffset to,
        int maxResults = 100,
        CancellationToken cancellationToken = default)
    {
        var entities = await _db.Strategies
            .AsNoTracking()
            .Where(e => e.AssetSymbol == assetSymbol
                && e.GeneratedAt >= from
                && e.GeneratedAt <= to)
            .OrderByDescending(e => e.GeneratedAt)
            .Take(maxResults)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDomain).ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PersistedStrategy>> GetByStateAsync(
        PipelineState state,
        int maxResults = 100,
        CancellationToken cancellationToken = default)
    {
        var stateStr = state.ToString();
        var entities = await _db.Strategies
            .AsNoTracking()
            .Where(e => e.PipelineState == stateStr)
            .OrderByDescending(e => e.GeneratedAt)
            .Take(maxResults)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDomain).ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PersistedStrategy>> GetRecentAsync(
        int maxResults = 50,
        CancellationToken cancellationToken = default)
    {
        var entities = await _db.Strategies
            .AsNoTracking()
            .OrderByDescending(e => e.GeneratedAt)
            .Take(maxResults)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDomain).ToList();
    }

    /// <inheritdoc/>
    public async Task<int> CountByAssetAsync(
        string assetSymbol,
        CancellationToken cancellationToken = default)
    {
        return await _db.Strategies
            .AsNoTracking()
            .CountAsync(e => e.AssetSymbol == assetSymbol, cancellationToken);
    }

    private static PersistedStrategy MapToDomain(StrategyEntity entity)
    {
        var asset = JsonSerializer.Deserialize<Asset>(entity.AssetJson, JsonOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize Asset for strategy {entity.Id}");

        var report = JsonSerializer.Deserialize<StrategyReport>(entity.ReportJson, JsonOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize StrategyReport for strategy {entity.Id}");

        var sentiment = Enum.TryParse<Sentiment>(entity.OverallSentiment, out var s)
            ? s : Sentiment.Unknown;

        var pipelineState = Enum.TryParse<PipelineState>(entity.PipelineState, out var ps)
            ? ps : PipelineState.Completed;

        var agents = string.IsNullOrEmpty(entity.ContributingAgents)
            ? Array.Empty<string>()
            : entity.ContributingAgents.Split(',', StringSplitOptions.RemoveEmptyEntries);

        return new PersistedStrategy
        {
            Id = entity.Id,
            Asset = asset,
            GeneratedAt = entity.GeneratedAt,
            Report = report,
            OverallSentiment = sentiment,
            OverallConfidence = entity.OverallConfidence,
            PipelineState = pipelineState,
            ContributingAgents = agents,
            LlmModel = entity.LlmModel,
            TokensUsed = entity.TokensUsed,
            GenerationDuration = entity.GenerationDurationMs.HasValue
                ? TimeSpan.FromMilliseconds(entity.GenerationDurationMs.Value)
                : null,
            EvidenceId = entity.EvidenceId
        };
    }
}

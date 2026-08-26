using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StrategyForge.Domain.Interfaces.Providers;
using StrategyForge.Domain.Models;
using StrategyForge.Infrastructure.Data;
using StrategyForge.Infrastructure.Data.Entities;

namespace StrategyForge.Infrastructure.Repositories;

/// <summary>
/// PostgreSQL-backed implementation of IIntelligenceRunStore.
/// Tracks background intelligence collection and analysis runs.
/// </summary>
public sealed class IntelligenceRunStore : IIntelligenceRunStore
{
    private readonly StrategyForgeDbContext _db;
    private readonly ILogger<IntelligenceRunStore> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public IntelligenceRunStore(StrategyForgeDbContext db, ILogger<IntelligenceRunStore> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IntelligenceRun> StoreAsync(
        IntelligenceRun run,
        CancellationToken cancellationToken = default)
    {
        var entity = new IntelligenceRunEntity
        {
            Id = run.Id,
            ScheduledAt = run.ScheduledAt,
            StartedAt = run.StartedAt,
            CompletedAt = run.CompletedAt,
            State = run.State.ToString(),
            TargetAssetsJson = JsonSerializer.Serialize(run.TargetAssets, JsonOptions),
            SuccessfulAssets = run.SuccessfulAssets,
            FailedAssets = run.FailedAssets,
            EvidenceIdsJson = run.EvidenceIds.Count > 0
                ? JsonSerializer.Serialize(run.EvidenceIds, JsonOptions)
                : null,
            StrategyIdsJson = run.StrategyIds.Count > 0
                ? JsonSerializer.Serialize(run.StrategyIds, JsonOptions)
                : null,
            GenerateStrategies = run.GenerateStrategies,
            ErrorMessage = run.ErrorMessage,
            TotalTokensUsed = run.TotalTokensUsed,
            TotalDurationMs = run.TotalDuration.HasValue
                ? (long)run.TotalDuration.Value.TotalMilliseconds
                : null
        };

        _db.IntelligenceRuns.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogDebug(
            "Stored intelligence run {Id} (state={State}, assets={AssetCount})",
            entity.Id, entity.State, run.TargetAssets.Count);

        return run;
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(
        IntelligenceRun run,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.IntelligenceRuns
            .FirstOrDefaultAsync(e => e.Id == run.Id, cancellationToken);

        if (entity == null)
        {
            _logger.LogWarning("Intelligence run {Id} not found for update", run.Id);
            return;
        }

        entity.StartedAt = run.StartedAt;
        entity.CompletedAt = run.CompletedAt;
        entity.State = run.State.ToString();
        entity.SuccessfulAssets = run.SuccessfulAssets;
        entity.FailedAssets = run.FailedAssets;
        entity.EvidenceIdsJson = run.EvidenceIds.Count > 0
            ? JsonSerializer.Serialize(run.EvidenceIds, JsonOptions)
            : null;
        entity.StrategyIdsJson = run.StrategyIds.Count > 0
            ? JsonSerializer.Serialize(run.StrategyIds, JsonOptions)
            : null;
        entity.ErrorMessage = run.ErrorMessage;
        entity.TotalTokensUsed = run.TotalTokensUsed;
        entity.TotalDurationMs = run.TotalDuration.HasValue
            ? (long)run.TotalDuration.Value.TotalMilliseconds
            : null;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogDebug(
            "Updated intelligence run {Id} to state={State}",
            run.Id, entity.State);
    }

    /// <inheritdoc/>
    public async Task<IntelligenceRun?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.IntelligenceRuns
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        return entity == null ? null : MapToDomain(entity);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IntelligenceRun>> GetRecentAsync(
        int maxResults = 50,
        CancellationToken cancellationToken = default)
    {
        var entities = await _db.IntelligenceRuns
            .AsNoTracking()
            .OrderByDescending(e => e.ScheduledAt)
            .Take(maxResults)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDomain).ToList();
    }

    private static IntelligenceRun MapToDomain(IntelligenceRunEntity entity)
    {
        var state = Enum.TryParse<IntelligenceRunState>(entity.State, out var s)
            ? s : IntelligenceRunState.Scheduled;

        var targetAssets = JsonSerializer.Deserialize<List<string>>(entity.TargetAssetsJson, JsonOptions)
            ?? [];

        var evidenceIds = string.IsNullOrEmpty(entity.EvidenceIdsJson)
            ? (IReadOnlyList<Guid>)Array.Empty<Guid>()
            : (IReadOnlyList<Guid>)(JsonSerializer.Deserialize<List<Guid>>(entity.EvidenceIdsJson, JsonOptions) ?? []);

        var strategyIds = string.IsNullOrEmpty(entity.StrategyIdsJson)
            ? (IReadOnlyList<Guid>)Array.Empty<Guid>()
            : (IReadOnlyList<Guid>)(JsonSerializer.Deserialize<List<Guid>>(entity.StrategyIdsJson, JsonOptions) ?? []);

        return new IntelligenceRun
        {
            Id = entity.Id,
            ScheduledAt = entity.ScheduledAt,
            StartedAt = entity.StartedAt,
            CompletedAt = entity.CompletedAt,
            State = state,
            TargetAssets = targetAssets,
            SuccessfulAssets = entity.SuccessfulAssets,
            FailedAssets = entity.FailedAssets,
            EvidenceIds = evidenceIds,
            StrategyIds = strategyIds,
            GenerateStrategies = entity.GenerateStrategies,
            ErrorMessage = entity.ErrorMessage,
            TotalTokensUsed = entity.TotalTokensUsed,
            TotalDuration = entity.TotalDurationMs.HasValue
                ? TimeSpan.FromMilliseconds(entity.TotalDurationMs.Value)
                : null
        };
    }
}

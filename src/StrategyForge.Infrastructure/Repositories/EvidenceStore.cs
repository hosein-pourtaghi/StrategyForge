using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StrategyForge.Domain.Interfaces.Providers;
using StrategyForge.Domain.Models;
using StrategyForge.Infrastructure.Data;
using StrategyForge.Infrastructure.Data.Entities;

namespace StrategyForge.Infrastructure.Repositories;

/// <summary>
/// PostgreSQL-backed implementation of IEvidenceStore.
/// Stores analysis evidence as JSON in the database with scalar columns for efficient querying.
/// 
/// JSON serialization is used for complex record types (Asset, AnalysisEvidence)
/// to avoid brittle relational mappings against mutable record types.
/// Scalar columns (AssetSymbol, AssembledAt, etc.) support efficient indexed queries.
/// </summary>
public sealed class EvidenceStore : IEvidenceStore
{
    private readonly StrategyForgeDbContext _db;
    private readonly ILogger<EvidenceStore> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public EvidenceStore(StrategyForgeDbContext db, ILogger<EvidenceStore> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<PersistedEvidence> StoreAsync(
        PersistedEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        var entity = new EvidenceEntity
        {
            Id = evidence.Id,
            AssetSymbol = evidence.Asset.Symbol,
            AssetName = evidence.Asset.Name,
            AssetMarket = evidence.Asset.Market,
            AssembledAt = evidence.AssembledAt,
            AssetJson = JsonSerializer.Serialize(evidence.Asset, JsonOptions),
            EvidenceJson = JsonSerializer.Serialize(evidence.Evidence, JsonOptions),
            DataSources = evidence.DataSources.Count > 0
                ? string.Join(",", evidence.DataSources)
                : null,
            IndicatorCount = evidence.IndicatorCount,
            NewsItemCount = evidence.NewsItemCount,
            DataQualityScore = evidence.DataQualityScore,
            ExecutionId = evidence.ExecutionId
        };

        _db.Evidence.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogDebug(
            "Stored evidence {Id} for {Symbol} at {AssembledAt}",
            entity.Id, evidence.Asset.Symbol, evidence.AssembledAt);

        return evidence;
    }

    /// <inheritdoc/>
    public async Task<PersistedEvidence?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.Evidence
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        return entity == null ? null : MapToDomain(entity);
    }

    /// <inheritdoc/>
    public async Task<PersistedEvidence?> GetLatestByAssetAsync(
        string assetSymbol,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.Evidence
            .AsNoTracking()
            .Where(e => e.AssetSymbol == assetSymbol)
            .OrderByDescending(e => e.AssembledAt)
            .FirstOrDefaultAsync(cancellationToken);

        return entity == null ? null : MapToDomain(entity);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PersistedEvidence>> GetByAssetAndDateRangeAsync(
        string assetSymbol,
        DateTimeOffset from,
        DateTimeOffset to,
        int maxResults = 100,
        CancellationToken cancellationToken = default)
    {
        var entities = await _db.Evidence
            .AsNoTracking()
            .Where(e => e.AssetSymbol == assetSymbol
                && e.AssembledAt >= from
                && e.AssembledAt <= to)
            .OrderByDescending(e => e.AssembledAt)
            .Take(maxResults)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDomain).ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PersistedEvidence>> GetRecentAsync(
        int maxResults = 50,
        CancellationToken cancellationToken = default)
    {
        var entities = await _db.Evidence
            .AsNoTracking()
            .OrderByDescending(e => e.AssembledAt)
            .Take(maxResults)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDomain).ToList();
    }

    /// <inheritdoc/>
    public async Task<int> CountByAssetAsync(
        string assetSymbol,
        CancellationToken cancellationToken = default)
    {
        return await _db.Evidence
            .AsNoTracking()
            .CountAsync(e => e.AssetSymbol == assetSymbol, cancellationToken);
    }

    private static PersistedEvidence MapToDomain(EvidenceEntity entity)
    {
        var asset = JsonSerializer.Deserialize<Asset>(entity.AssetJson, JsonOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize Asset for evidence {entity.Id}");

        var evidence = JsonSerializer.Deserialize<AnalysisEvidence>(entity.EvidenceJson, JsonOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize AnalysisEvidence for evidence {entity.Id}");

        var dataSources = string.IsNullOrEmpty(entity.DataSources)
            ? Array.Empty<string>()
            : entity.DataSources.Split(',', StringSplitOptions.RemoveEmptyEntries);

        return new PersistedEvidence
        {
            Id = entity.Id,
            Asset = asset,
            AssembledAt = entity.AssembledAt,
            Evidence = evidence,
            DataSources = dataSources,
            IndicatorCount = entity.IndicatorCount,
            NewsItemCount = entity.NewsItemCount,
            DataQualityScore = entity.DataQualityScore,
            ExecutionId = entity.ExecutionId
        };
    }
}

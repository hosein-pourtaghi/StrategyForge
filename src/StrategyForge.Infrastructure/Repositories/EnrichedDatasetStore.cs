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
/// EF Core (PostgreSQL) implementation of <see cref="IEnrichedDatasetStore"/>.
///
/// Idempotency is enforced by the unique index on (InstrumentId, Source, ObservationDate):
/// each batch first loads the existing derived rows for the dates it is about to write,
/// then inserts only genuinely new rows, updates changed rows, and counts unchanged rows
/// explicitly. Re-processing the same request never creates duplicate derived observations.
///
/// The raw historical dataset is never touched by this store.
/// </summary>
public sealed class EnrichedDatasetStore : IEnrichedDatasetStore
{
    private readonly StrategyForgeDbContext _db;
    private readonly ILogger<EnrichedDatasetStore> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public EnrichedDatasetStore(
        StrategyForgeDbContext db,
        ILogger<EnrichedDatasetStore> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<EnrichedUpsertOutcome> UpsertEnrichedAsync(
        string instrumentId,
        SourceAdapterType source,
        IReadOnlyList<EnrichedObservation> observations,
        CancellationToken cancellationToken = default)
    {
        if (observations.Count == 0)
        {
            return EnrichedUpsertOutcome.Empty;
        }

        // Collapse same-date rows within the batch (last wins) so the batch
        // itself can never violate the identity constraint.
        var distinct = observations
            .GroupBy(o => o.ObservationDate)
            .ToDictionary(g => g.Key, g => g.Last());

        var sourceStr = source.ToString();
        var now = DateTimeOffset.UtcNow;

        var dates = distinct.Keys.ToList();
        var existing = await _db.EnrichedObservations
            .Where(e => e.InstrumentId == instrumentId
                && e.Source == sourceStr
                && dates.Contains(e.ObservationDate))
            .ToDictionaryAsync(e => e.ObservationDate, cancellationToken);

        var outcome = new EnrichedUpsertOutcome
        {
            Inserted = 0,
            Changed = 0,
            Unchanged = 0,
            DuplicateInBatch = observations.Count - distinct.Count
        };

        foreach (var (date, observation) in distinct)
        {
            if (existing.TryGetValue(date, out var entity))
            {
                if (SameValues(entity, observation))
                {
                    outcome = outcome with { Unchanged = outcome.Unchanged + 1 };
                    continue;
                }

                ApplyObservation(entity, observation, now);
                outcome = outcome with { Changed = outcome.Changed + 1 };
            }
            else
            {
                entity = CreateEntity(instrumentId, sourceStr, date, observation, now);
                _db.EnrichedObservations.Add(entity);
                outcome = outcome with { Inserted = outcome.Inserted + 1 };
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogDebug(
            "Upserted {Count} enriched observations for {Instrument}/{Source}: {Inserted} inserted, {Changed} changed, {Unchanged} unchanged",
            distinct.Count, instrumentId, sourceStr, outcome.Inserted, outcome.Changed, outcome.Unchanged);

        return outcome;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<EnrichedObservation>> GetEnrichedAsync(
        string instrumentId,
        SourceAdapterType? source = null,
        DateOnly? from = null,
        DateOnly? to = null,
        int skip = 0,
        int take = 0,
        CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(instrumentId, source, from, to);

        if (skip > 0)
        {
            query = query.Skip(skip);
        }

        if (take > 0)
        {
            query = query.Take(take);
        }

        var entities = await query
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDomain).ToList();
    }

    /// <inheritdoc/>
    public async Task<int> CountEnrichedAsync(
        string instrumentId,
        SourceAdapterType? source = null,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken cancellationToken = default)
    {
        return await BuildQuery(instrumentId, source, from, to)
            .CountAsync(cancellationToken);
    }

    private IQueryable<EnrichedObservationEntity> BuildQuery(
        string instrumentId,
        SourceAdapterType? source,
        DateOnly? from,
        DateOnly? to)
    {
        var sourceStr = source?.ToString();

        var query = _db.EnrichedObservations
            .AsNoTracking()
            .Where(e => e.InstrumentId == instrumentId);

        if (sourceStr != null)
        {
            query = query.Where(e => e.Source == sourceStr);
        }

        if (from.HasValue)
        {
            query = query.Where(e => e.ObservationDate >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(e => e.ObservationDate <= to.Value);
        }

        // Deterministic ordering: (date) ascending — the identity includes
        // instrument and source, which are fixed for a single query.
        return query.OrderBy(e => e.ObservationDate);
    }

    // --- Entity mapping helpers ---

    private static EnrichedObservationEntity CreateEntity(
        string instrumentId,
        string sourceStr,
        DateOnly date,
        EnrichedObservation observation,
        DateTimeOffset now)
    {
        var sourceEnum = Enum.TryParse<SourceAdapterType>(sourceStr, out var parsed)
            ? parsed
            : default(SourceAdapterType);

        return new EnrichedObservationEntity
        {
            Id = Guid.NewGuid(),
            InstrumentId = instrumentId,
            Source = sourceStr,
            ObservationDate = date,
            Open = observation.Open,
            High = observation.High,
            Low = observation.Low,
            Close = observation.Close,
            Volume = observation.Volume,
            Change = observation.Change,
            ChangePercent = observation.ChangePercent,
            IndicatorsJson = JsonSerializer.Serialize(observation.Indicators, JsonOptions),
            QualityStatus = observation.QualityStatus.ToString(),
            WarningCodesJson = JsonSerializer.Serialize(observation.WarningCodes, JsonOptions),
            ProcessedBy = observation.ProcessedBy,
            ProcessedAtUtc = observation.ProcessedAtUtc,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }

    private static void ApplyObservation(EnrichedObservationEntity entity, EnrichedObservation observation, DateTimeOffset now)
    {
        entity.Open = observation.Open;
        entity.High = observation.High;
        entity.Low = observation.Low;
        entity.Close = observation.Close;
        entity.Volume = observation.Volume;
        entity.Change = observation.Change;
        entity.ChangePercent = observation.ChangePercent;
        entity.IndicatorsJson = JsonSerializer.Serialize(observation.Indicators, JsonOptions);
        entity.QualityStatus = observation.QualityStatus.ToString();
        entity.WarningCodesJson = JsonSerializer.Serialize(observation.WarningCodes, JsonOptions);
        entity.ProcessedBy = observation.ProcessedBy;
        entity.ProcessedAtUtc = observation.ProcessedAtUtc;
        entity.UpdatedAtUtc = now;
    }

    private static bool SameValues(EnrichedObservationEntity entity, EnrichedObservation observation)
    {
        return entity.Open == observation.Open
            && entity.High == observation.High
            && entity.Low == observation.Low
            && entity.Close == observation.Close
            && entity.Volume == observation.Volume
            && entity.Change == observation.Change
            && entity.ChangePercent == observation.ChangePercent
            && entity.IndicatorsJson == JsonSerializer.Serialize(observation.Indicators, JsonOptions)
            && string.Equals(entity.QualityStatus, observation.QualityStatus.ToString(), StringComparison.Ordinal)
            && entity.WarningCodesJson == JsonSerializer.Serialize(observation.WarningCodes, JsonOptions)
            && string.Equals(entity.ProcessedBy, observation.ProcessedBy, StringComparison.Ordinal);
    }

    private static EnrichedObservation MapToDomain(EnrichedObservationEntity e)
    {
        var indicators = DeserializeIndicators(e.IndicatorsJson);
        var warningCodes = DeserializeWarningCodes(e.WarningCodesJson);

        return new EnrichedObservation
        {
            InstrumentId = e.InstrumentId,
            Source = Enum.TryParse<SourceAdapterType>(e.Source, out var source)
                ? source
                : default(SourceAdapterType),
            ObservationDate = e.ObservationDate,
            Open = e.Open,
            High = e.High,
            Low = e.Low,
            Close = e.Close,
            Volume = e.Volume,
            Change = e.Change,
            ChangePercent = e.ChangePercent,
            Indicators = indicators,
            QualityStatus = Enum.TryParse<HistoricalDataQualityStatus>(e.QualityStatus, out var status)
                ? status
                : HistoricalDataQualityStatus.Valid,
            WarningCodes = warningCodes,
            ProcessedBy = e.ProcessedBy,
            ProcessedAtUtc = e.ProcessedAtUtc
        };
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, decimal>> DeserializeIndicators(string json)
    {
        try
        {
            var raw = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, decimal>>>(json, JsonOptions);
            if (raw is null)
            {
                return new Dictionary<string, IReadOnlyDictionary<string, decimal>>();
            }

            return raw.ToDictionary(
                kv => kv.Key,
                kv => (IReadOnlyDictionary<string, decimal>)kv.Value);
        }
        catch (JsonException)
        {
            // A malformed payload must not break reading the derived observation itself.
            return new Dictionary<string, IReadOnlyDictionary<string, decimal>>();
        }
    }

    private static IReadOnlyList<string> DeserializeWarningCodes(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}

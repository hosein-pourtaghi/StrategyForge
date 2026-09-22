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
/// EF Core (PostgreSQL) implementation of <see cref="IHistoricalDatasetStore"/>.
///
/// Idempotency is enforced by the unique index on (InstrumentId, Source, ObservationDate):
/// each batch first loads the existing rows for the dates it is about to write,
/// then inserts only genuinely new observations and updates the rest in place.
///
/// Provenance is preserved per observation: source symbol/id, fetch timestamp,
/// source calendar fields, and extra source properties.
/// </summary>
public sealed class HistoricalDatasetStore : IHistoricalDatasetStore
{
    private readonly StrategyForgeDbContext _db;
    private readonly ILogger<HistoricalDatasetStore> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public HistoricalDatasetStore(
        StrategyForgeDbContext db,
        ILogger<HistoricalDatasetStore> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<HistoricalDatasetUpsertOutcome> UpsertCandlesAsync(
        InstrumentMapping instrument,
        SourceAdapterType source,
        IReadOnlyList<Candle> candles,
        CancellationToken cancellationToken = default)
    {
        if (candles.Count == 0)
        {
            return HistoricalDatasetUpsertOutcome.Empty;
        }

        // Collapse same-date rows within the batch (last wins) so the batch
        // itself can never violate the identity constraint.
        var distinct = candles
            .GroupBy(c => c.Date)
            .ToDictionary(g => g.Key, g => g.Last());

        var sourceStr = source.ToString();
        var now = DateTimeOffset.UtcNow;

        var dates = distinct.Keys.ToList();
        var existing = await _db.HistoricalDataset
            .Where(e => e.InstrumentId == instrument.InstrumentId
                && e.Source == sourceStr
                && dates.Contains(e.ObservationDate))
            .ToDictionaryAsync(e => e.ObservationDate, cancellationToken);

        var outcome = new HistoricalDatasetUpsertOutcome
        {
            Inserted = 0,
            Updated = 0,
            DuplicateInBatch = candles.Count - distinct.Count
        };

        foreach (var (date, candle) in distinct)
        {
            if (existing.TryGetValue(date, out var entity))
            {
                ApplyCandle(entity, candle, source);
                entity.UpdatedAtUtc = now;
                outcome = outcome with { Updated = outcome.Updated + 1 };
            }
            else
            {
                entity = CreateEntity(instrument, sourceStr, date, candle, now);
                _db.HistoricalDataset.Add(entity);
                outcome = outcome with { Inserted = outcome.Inserted + 1 };
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogDebug(
            "Upserted {Count} candles for {Instrument}/{Source}: {Inserted} inserted, {Updated} updated",
            distinct.Count, instrument.InstrumentId, sourceStr, outcome.Inserted, outcome.Updated);

        return outcome;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Candle>> GetCandlesAsync(
        string instrumentId,
        SourceAdapterType? source = null,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.HistoricalDataset
            .AsNoTracking()
            .Where(e => e.InstrumentId == instrumentId);

        if (source.HasValue)
        {
            var sourceStr = source.Value.ToString();
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

        var entities = await query
            .OrderBy(e => e.ObservationDate)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDomain).ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Candle>> GetLatestCandlesAsync(
        string instrumentId,
        int limit,
        SourceAdapterType? source = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.HistoricalDataset
            .AsNoTracking()
            .Where(e => e.InstrumentId == instrumentId);

        if (source.HasValue)
        {
            var sourceStr = source.Value.ToString();
            query = query.Where(e => e.Source == sourceStr);
        }

        var entities = await query
            .OrderByDescending(e => e.ObservationDate)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDomain).ToList();
    }

    /// <inheritdoc/>
    public async Task<int> CountCandlesAsync(
        string instrumentId,
        SourceAdapterType? source = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.HistoricalDataset
            .AsNoTracking()
            .Where(e => e.InstrumentId == instrumentId);

        if (source.HasValue)
        {
            var sourceStr = source.Value.ToString();
            query = query.Where(e => e.Source == sourceStr);
        }

        return await query.CountAsync(cancellationToken);
    }

    // --- Entity mapping helpers ---

    private static HistoricalDatasetEntity CreateEntity(
        InstrumentMapping instrument,
        string sourceStr,
        DateOnly date,
        Candle candle,
        DateTimeOffset now)
    {
        var sourceEnum = Enum.TryParse<SourceAdapterType>(sourceStr, out var parsed)
            ? parsed
            : default(SourceAdapterType);

        return new HistoricalDatasetEntity
        {
            Id = Guid.NewGuid(),
            InstrumentId = instrument.InstrumentId,
            Source = sourceStr,
        ObservationDate = date,
        Open = candle.Open,
        High = candle.High,
        Low = candle.Low,
        Close = candle.Close,
        Volume = candle.Volume,
        Value = candle.Value,
        TradeCount = candle.TradeCount,
        LastPrice = candle.LastPrice,
        Change = candle.Change,
        ChangePercent = candle.ChangePercent,
        MarketTimezone = candle.MarketTimezone,
        SourceDate = candle.SourceDate,
        SourceCalendar = candle.SourceCalendar,
            AdjustmentType = candle.Adjustment?.Type.ToString(),
            AdjustmentSource = candle.Adjustment?.AdjustmentSource,
            SourceSymbol = candle.Provenance?.SourceSymbol
                ?? instrument.SourceIdentifiers.GetValueOrDefault(sourceEnum)?.SourceSymbol,
            SourceInstrumentId = candle.Provenance?.SourceInstrumentId
                ?? instrument.SourceIdentifiers.GetValueOrDefault(sourceEnum)?.Id,
            FetchedAtUtc = candle.Provenance?.FetchedAtUtc ?? now,
            Endpoint = candle.Provenance?.Endpoint,
            ExtraPropertiesJson = candle.Provenance?.ExtraProperties is { Count: > 0 } extras
                ? JsonSerializer.Serialize(extras, JsonOptions)
                : null,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    private static void ApplyCandle(HistoricalDatasetEntity entity, Candle candle, SourceAdapterType source)
    {
        entity.Open = candle.Open;
        entity.High = candle.High;
        entity.Low = candle.Low;
        entity.Close = candle.Close;
        entity.Volume = candle.Volume;
        entity.Value = candle.Value;
        entity.TradeCount = candle.TradeCount;
        entity.LastPrice = candle.LastPrice;
        entity.Change = candle.Change;
        entity.ChangePercent = candle.ChangePercent;
        entity.MarketTimezone = candle.MarketTimezone;
        entity.SourceDate = candle.SourceDate;
        entity.SourceCalendar = candle.SourceCalendar;
        entity.AdjustmentType = candle.Adjustment?.Type.ToString();
        entity.AdjustmentSource = candle.Adjustment?.AdjustmentSource;
        entity.SourceSymbol = candle.Provenance?.SourceSymbol ?? entity.SourceSymbol;
        entity.SourceInstrumentId = candle.Provenance?.SourceInstrumentId ?? entity.SourceInstrumentId;
        entity.FetchedAtUtc = candle.Provenance?.FetchedAtUtc ?? entity.FetchedAtUtc;
        entity.Endpoint = candle.Provenance?.Endpoint ?? entity.Endpoint;
        entity.ExtraPropertiesJson = candle.Provenance?.ExtraProperties is { Count: > 0 } extras
            ? JsonSerializer.Serialize(extras, JsonOptions)
            : entity.ExtraPropertiesJson;
    }

    private static Candle MapToDomain(HistoricalDatasetEntity e)
    {
        DataProvenance? provenance = null;
        if (Enum.TryParse<SourceAdapterType>(e.Source, out var source))
        {
            Dictionary<string, string>? extras = null;
            if (!string.IsNullOrEmpty(e.ExtraPropertiesJson))
            {
                try
                {
                    extras = JsonSerializer.Deserialize<Dictionary<string, string>>(e.ExtraPropertiesJson, JsonOptions);
                }
                catch (JsonException)
                {
                    // Provenance extras are best-effort; a malformed payload must not
                    // break reading the observation itself.
                    extras = null;
                }
            }

            provenance = new DataProvenance
            {
                Source = source,
                SourceSymbol = e.SourceSymbol,
                SourceInstrumentId = e.SourceInstrumentId,
                FetchedAtUtc = e.FetchedAtUtc,
                IsCached = false,
                Endpoint = e.Endpoint,
                ExtraProperties = extras
            };
        }

        return new Candle
        {
            Date = e.ObservationDate,
            Open = e.Open,
            High = e.High,
            Low = e.Low,
            Close = e.Close,
            Volume = e.Volume,
            Value = e.Value,
            TradeCount = e.TradeCount,
            LastPrice = e.LastPrice,
            Change = e.Change,
            ChangePercent = e.ChangePercent,
            MarketTimezone = e.MarketTimezone,
            SourceDate = e.SourceDate,
            SourceCalendar = e.SourceCalendar,
            Adjustment = new DataAdjustment
            {
                IsAdjusted = e.AdjustmentType != null
                    && !string.Equals(e.AdjustmentType, DataAdjustmentType.None.ToString(), StringComparison.Ordinal),
                Type = Enum.TryParse<DataAdjustmentType>(e.AdjustmentType, out var adjType)
                    ? adjType
                    : DataAdjustmentType.None,
                AdjustmentSource = e.AdjustmentSource
            },
            Provenance = provenance
        };
    }
}

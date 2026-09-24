using Microsoft.EntityFrameworkCore;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Interfaces.Providers;
using StrategyForge.Domain.Models;
using StrategyForge.Infrastructure.Data;
using StrategyForge.Infrastructure.Data.Entities;

namespace StrategyForge.Infrastructure.Repositories;

/// <summary>
/// EF Core (PostgreSQL) keyset-cursor paged reader over the enriched dataset.
/// Mirrors <see cref="HistoricalDatasetPageReader"/> for the derived layer:
/// pages are consecutive, non-overlapping, and chronologically ordered, so
/// streaming consumers stay memory-bounded regardless of dataset size.
/// </summary>
public sealed class EnrichedDatasetPageReader : IEnrichedDatasetPageReader
{
    private readonly StrategyForgeDbContext _db;

    public EnrichedDatasetPageReader(StrategyForgeDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<EnrichedObservation>> ReadPageAsync(
        string instrumentId,
        SourceAdapterType source,
        DateOnly? from,
        DateOnly? to,
        DateOnly? afterDate,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var sourceStr = source.ToString();
        var query = _db.EnrichedObservations
            .AsNoTracking()
            .Where(e => e.InstrumentId == instrumentId && e.Source == sourceStr);

        if (from.HasValue)
        {
            query = query.Where(e => e.ObservationDate >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(e => e.ObservationDate <= to.Value);
        }

        if (afterDate.HasValue)
        {
            query = query.Where(e => e.ObservationDate > afterDate.Value);
        }

        var entities = await query
            .OrderBy(e => e.ObservationDate)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return entities.Select(EnrichedDatasetStore.MapToDomain).ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<EnrichedObservation>> ReadLastPageAsync(
        string instrumentId,
        SourceAdapterType source,
        DateOnly? beforeDate,
        int maxRows,
        CancellationToken cancellationToken = default)
    {
        var sourceStr = source.ToString();
        var query = _db.EnrichedObservations
            .AsNoTracking()
            .Where(e => e.InstrumentId == instrumentId && e.Source == sourceStr);

        if (beforeDate.HasValue)
        {
            query = query.Where(e => e.ObservationDate < beforeDate.Value);
        }

        var entities = await query
            .OrderByDescending(e => e.ObservationDate)
            .Take(maxRows)
            .ToListAsync(cancellationToken);

        // Contract requires ascending order (oldest first).
        return entities
            .Select(EnrichedDatasetStore.MapToDomain)
            .Reverse()
            .ToList();
    }
}

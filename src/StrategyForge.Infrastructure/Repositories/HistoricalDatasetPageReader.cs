using Microsoft.EntityFrameworkCore;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Interfaces.Providers;
using StrategyForge.Domain.Models;
using StrategyForge.Infrastructure.Data;
using StrategyForge.Infrastructure.Data.Entities;

namespace StrategyForge.Infrastructure.Repositories;

/// <summary>
/// EF Core keyset-paged reader over the raw historical dataset.
///
/// Bounded batches: each page loads at most <paramref name="pageSize"/> rows, so peak
/// memory scales with page size rather than dataset size. Pages are consecutive and
/// non-overlapping (keyset cursor on ObservationDate), preserving chronological order
/// across page boundaries — database batches never become indicator windows.
/// </summary>
public sealed class HistoricalDatasetPageReader : IHistoricalDatasetPageReader
{
    private readonly StrategyForgeDbContext _db;

    public HistoricalDatasetPageReader(StrategyForgeDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Candle>> ReadPageAsync(
        string instrumentId,
        SourceAdapterType source,
        DateOnly? from,
        DateOnly? to,
        DateOnly? afterDate,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (pageSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be positive.");
        }

        var sourceStr = source.ToString();

        // Inclusive lower bound:
        // - warm-up cursor mode (afterDate set): strictly after the cursor
        // - initial mode (afterDate null): at `from` when given, else unbounded
        var lowerExclusive = afterDate ?? (from.HasValue ? from.Value.AddDays(-1) : null);

        var query = _db.HistoricalDataset
            .AsNoTracking()
            .Where(e => e.InstrumentId == instrumentId && e.Source == sourceStr);

        if (lowerExclusive.HasValue)
        {
            query = query.Where(e => e.ObservationDate > lowerExclusive.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(e => e.ObservationDate <= to.Value);
        }

        var page = await query
            .OrderBy(e => e.ObservationDate)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return page.Select(MapToDomain).ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Candle>> ReadLastPageAsync(
        string instrumentId,
        SourceAdapterType source,
        DateOnly? beforeDate,
        int maxRows,
        CancellationToken cancellationToken = default)
    {
        if (maxRows <= 0)
        {
            return [];
        }

        var sourceStr = source.ToString();

        var query = _db.HistoricalDataset
            .AsNoTracking()
            .Where(e => e.InstrumentId == instrumentId && e.Source == sourceStr);

        if (beforeDate.HasValue)
        {
            query = query.Where(e => e.ObservationDate < beforeDate.Value);
        }

        var rows = await query
            .OrderByDescending(e => e.ObservationDate)
            .Take(maxRows)
            .ToListAsync(cancellationToken);

        // Return ascending so the caller receives a chronologically contiguous context window.
        rows.Reverse();

        return rows.Select(MapToDomain).ToList();
    }

    private static Candle MapToDomain(HistoricalDatasetEntity e)
    {
        DataProvenance? provenance = null;
        if (Enum.TryParse<SourceAdapterType>(e.Source, out var source))
        {
            provenance = new DataProvenance
            {
                Source = source,
                SourceSymbol = e.SourceSymbol,
                SourceInstrumentId = e.SourceInstrumentId,
                FetchedAtUtc = e.FetchedAtUtc,
                IsCached = false,
                Endpoint = e.Endpoint
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

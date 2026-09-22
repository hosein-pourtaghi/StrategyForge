using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StrategyForge.Domain.Configuration;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Interfaces.Providers;
using StrategyForge.Domain.Models;

namespace StrategyForge.Infrastructure.Services;

/// <summary>
/// Imports historical candles from the existing provider abstractions into the
/// idempotent historical dataset.
///
/// Pipeline (uses only <see cref="IDataSourceRegistry"/> — never TSETMC/TGJU directly):
///
///   date range → deterministic chunks → provider fetch → normalize (canonical Candle)
///   → validate → range-filter → idempotent upsert (instrument + source + date)
///
/// Design guarantees:
/// - Chunking is deterministic: fixed-size, non-overlapping date windows; no gaps.
/// - Retries are bounded: each chunk gets 1 + MaxChunkRetries attempts, then the
///   failure is terminal for that chunk. There is no infinite retry loop.
/// - Re-running an import never creates duplicate observations (store enforces the
///   identity); results report inserted vs updated rows explicitly.
/// - Invalid candles are rejected explicitly and counted in Rejected, never silently dropped.
/// - The importer decides nothing about trading; it is a data-quality pipeline only.
/// </summary>
public sealed class HistoricalIngestionService
{
    private readonly IDataSourceRegistry _registry;
    private readonly IHistoricalDatasetStore _store;
    private readonly HistoricalIngestionSettings _settings;
    private readonly ILogger<HistoricalIngestionService> _logger;

    public HistoricalIngestionService(
        IDataSourceRegistry registry,
        IHistoricalDatasetStore store,
        IOptions<HistoricalIngestionSettings> settings,
        ILogger<HistoricalIngestionService> logger)
    {
        _registry = registry;
        _store = store;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Imports historical candles for the requested instrument, source, and range.
    /// </summary>
    public async Task<HistoricalDatasetImportResult> ImportAsync(
        HistoricalDatasetRequest request,
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.UtcNow;

        if (request.From > request.To)
        {
            return Fail(request, startedAt, "INVALID_RANGE",
                $"Range start {request.From:yyyy-MM-dd} is after range end {request.To:yyyy-MM-dd}");
        }

        var chunkDays = Math.Max(1, _settings.ChunkDays);
        var chunks = BuildChunks(request.From, request.To, chunkDays);

        _logger.LogInformation(
            "Historical import started: {Instrument} from {Source} over {From}..{To} in {ChunkCount} chunk(s)",
            request.Instrument.InstrumentId, request.Source, request.From, request.To, chunks.Count);

        var totalInserted = 0;
        var totalUpdated = 0;
        var totalRejected = 0;
        var totalOutOfRange = 0;
        var chunksFailed = 0;
        var warnings = new List<string>();

        foreach (var chunk in chunks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fetch = await FetchChunkWithBoundedRetryAsync(request, chunk, cancellationToken);
            if (fetch.Error != null)
            {
                warnings.Add(fetch.Error);
                chunksFailed++;

                if (chunksFailed >= Math.Max(1, _settings.MaxFailedChunks))
                {
                    _logger.LogError(
                        "Historical import aborted: {FailedChunks} chunks failed (max {MaxFailed}) for {Instrument}",
                        chunksFailed, _settings.MaxFailedChunks, request.Instrument.InstrumentId);

                    return Fail(request, startedAt, "CHUNK_FAILURE_LIMIT_REACHED",
                        $"Import aborted after {chunksFailed} failed chunk(s). Last error: {fetch.Error}",
                        partial: new PartialCounts(chunks.Count, chunksFailed, totalInserted, totalUpdated, totalRejected, totalOutOfRange),
                        warnings: warnings);
                }

                continue;
            }

            // --- Validate: reject malformed observations explicitly ---
            List<Candle> accepted;
            if (_settings.RejectInvalidCandles)
            {
                var rejectedCount = fetch.Candles.Count(c => !c.IsValid);
                totalRejected += rejectedCount;
                accepted = fetch.Candles.Where(c => c.IsValid).ToList();
            }
            else
            {
                accepted = fetch.Candles.ToList();
            }

            // --- Range filter: providers may return rows outside the requested window ---
            var inRange = accepted.Count(c => c.Date < request.From || c.Date > request.To);
            totalOutOfRange += inRange;
            accepted = accepted.Where(c => c.Date >= request.From && c.Date <= request.To).ToList();

            if (accepted.Count == 0)
            {
                continue;
            }

            // --- Idempotent upsert ---
            var outcome = await _store.UpsertCandlesAsync(request.Instrument, request.Source, accepted, cancellationToken);
            totalInserted += outcome.Inserted;
            totalUpdated += outcome.Updated;
        }

        var duration = DateTimeOffset.UtcNow - startedAt;
        _logger.LogInformation(
            "Historical import finished for {Instrument}/{Source}: {Inserted} inserted, {Updated} updated, {Rejected} rejected, {OutOfRange} out-of-range in {Duration:F0}ms",
            request.Instrument.InstrumentId, request.Source, totalInserted, totalUpdated, totalRejected, totalOutOfRange, duration.TotalMilliseconds);

        return new HistoricalDatasetImportResult
        {
            InstrumentId = request.Instrument.InstrumentId,
            Source = request.Source,
            RequestedFrom = request.From,
            RequestedTo = request.To,
            EffectiveFrom = chunks[0].From,
            EffectiveTo = chunks[^1].To,
            Ok = true,
            ChunksFetched = chunks.Count,
            ChunksFailed = chunksFailed,
            Inserted = totalInserted,
            Updated = totalUpdated,
            Rejected = totalRejected,
            OutOfRange = totalOutOfRange,
            Duration = duration,
            Warnings = warnings
        };
    }

    // --- Chunking ---

    /// <summary>
    /// Splits [from, to] into consecutive fixed-size date windows.
    /// Deterministic: chunk count depends only on range length and chunk size.
    /// </summary>
    public static List<(DateOnly From, DateOnly To)> BuildChunks(DateOnly from, DateOnly to, int chunkDays)
    {
        var chunks = new List<(DateOnly From, DateOnly To)>();
        var cursor = from;

        while (cursor <= to)
        {
            var end = cursor.AddDays(chunkDays - 1);
            if (end > to)
            {
                end = to;
            }

            chunks.Add((cursor, end));
            cursor = end.AddDays(1);
        }

        return chunks;
    }

    // --- Bounded retry ---

    private sealed record ChunkFetchOutcome(IReadOnlyList<Candle> Candles, string? Error);

    private async Task<ChunkFetchOutcome> FetchChunkWithBoundedRetryAsync(
        HistoricalDatasetRequest request,
        (DateOnly From, DateOnly To) chunk,
        CancellationToken cancellationToken)
    {
        var maxAttempts = 1 + Math.Max(0, _settings.MaxChunkRetries);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            if (attempt > 1 && _settings.ChunkRetryBaseDelayMs > 0)
            {
                var delay = _settings.ChunkRetryBaseDelayMs * (int)Math.Pow(2, attempt - 2);
                await Task.Delay(delay, cancellationToken);
            }

            try
            {
                var result = await _registry.FetchHistoricalCandlesAsync(
                    request.Instrument,
                    chunk.From,
                    chunk.To,
                    preferredSource: request.Source,
                    selectionMode: SourceSelectionMode.PreferredOnly,
                    resolution: request.Resolution,
                    cancellationToken: cancellationToken);

                if (result.Ok)
                {
                    return new ChunkFetchOutcome(result.Data ?? [], null);
                }

                _logger.LogWarning(
                    "Chunk {From}..{To} attempt {Attempt}/{Max} failed: {Code} — {Message}",
                    chunk.From, chunk.To, attempt, maxAttempts, result.Error?.Code, result.Error?.Message);

                if (result.Error is { Retryable: false })
                {
                    // Terminal provider error (e.g., no mapping, unsupported capability):
                    // retrying the same request would fail identically.
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Chunk {From}..{To} attempt {Attempt}/{Max} threw {ExceptionType}",
                    chunk.From, chunk.To, attempt, maxAttempts, ex.GetType().Name);
            }
        }

        return new ChunkFetchOutcome(
            [],
            $"Chunk {chunk.From:yyyy-MM-dd}..{chunk.To:yyyy-MM-dd} failed after {maxAttempts} attempt(s)");
    }

    // --- Failure helpers ---

    private sealed record PartialCounts(
        int ChunksFetched,
        int ChunksFailed,
        int Inserted,
        int Updated,
        int Rejected,
        int OutOfRange);

    private static HistoricalDatasetImportResult Fail(
        HistoricalDatasetRequest request,
        DateTimeOffset startedAt,
        string code,
        string message,
        PartialCounts? partial = null,
        IReadOnlyList<string>? warnings = null)
    {
        return new HistoricalDatasetImportResult
        {
            InstrumentId = request.Instrument.InstrumentId,
            Source = request.Source,
            RequestedFrom = request.From,
            RequestedTo = request.To,
            EffectiveFrom = partial != null ? request.From : request.From,
            EffectiveTo = partial != null ? request.To : request.To,
            Ok = false,
            ChunksFetched = partial?.ChunksFetched ?? 0,
            ChunksFailed = partial?.ChunksFailed ?? 0,
            Inserted = partial?.Inserted ?? 0,
            Updated = partial?.Updated ?? 0,
            Rejected = partial?.Rejected ?? 0,
            OutOfRange = partial?.OutOfRange ?? 0,
            Duration = DateTimeOffset.UtcNow - startedAt,
            ErrorCode = code,
            ErrorMessage = message,
            Warnings = warnings ?? []
        };
    }
}

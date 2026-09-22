namespace StrategyForge.Domain.Configuration;

/// <summary>
/// Configuration for the historical market dataset ingestion pipeline.
/// Maps to the "HistoricalIngestion" section in appsettings.json.
///
/// Design rules enforced by this phase:
/// - Chunking is deterministic: fixed-size date chunks with no overlap and no gaps.
/// - Retries are bounded — there is no unbounded retry loop.
/// - Upserts are idempotent: (canonical instrument, source, observation date).
/// - Validation rejects malformed candles before persistence; nothing is silently dropped.
/// </summary>
public sealed record HistoricalIngestionSettings
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "HistoricalIngestion";

    /// <summary>
    /// Chunk size in calendar days for splitting a date range into provider requests.
    /// Must be at least 1. Providers never receive a request larger than this window.
    /// Default: 90 days.
    /// </summary>
    public int ChunkDays { get; init; } = 90;

    /// <summary>
    /// Number of retry attempts per failed chunk fetch (in addition to the initial attempt).
    /// Must be non-negative. Total attempts per chunk = 1 + MaxChunkRetries. Default: 2.
    /// </summary>
    public int MaxChunkRetries { get; init; } = 2;

    /// <summary>
    /// Base delay in milliseconds for exponential backoff between chunk retries.
    /// Default: 500ms → delays of 500ms, 1000ms.
    /// </summary>
    public int ChunkRetryBaseDelayMs { get; init; } = 500;

    /// <summary>
    /// Maximum number of chunks that may fail terminally before the import aborts.
    /// Must be at least 1. A chunk fails terminally after exhausting
    /// 1 + <see cref="MaxChunkRetries"/> attempts. Default: 3.
    /// </summary>
    public int MaxFailedChunks { get; init; } = 3;

    /// <summary>Whether to fetch each chunk even when cached provider data is available.</summary>
    public bool BypassProviderCache { get; init; } = false;

    /// <summary>When true, candles with invalid OHLC relationships are rejected before persistence.</summary>
    public bool RejectInvalidCandles { get; init; } = true;
}

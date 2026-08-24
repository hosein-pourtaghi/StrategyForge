using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StrategyForge.Domain.Configuration;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Interfaces.Providers;
using StrategyForge.Domain.Models;
using StrategyForge.Infrastructure.Authentication;
using StrategyForge.Infrastructure.Services;

namespace StrategyForge.Infrastructure.DataAdapters;

/// <summary>
/// Base class for all data source adapters.
/// Provides common HTTP handling, authentication, rate limiting, retry logic, caching,
/// and response normalization.
/// </summary>
public abstract class BaseDataSourceAdapter : IDataSourceAdapter
{
    protected readonly HttpClient HttpClient;
    protected readonly ILogger Logger;
    protected readonly RateLimiter RateLimiter;
    protected readonly InMemoryDataCache Cache;
    protected readonly DataQualityValidator QualityValidator;
    protected readonly IDataSourceAuthenticator Authenticator;
    protected readonly SourceAdapterConfig Config;
    protected readonly DataSourceSettings GlobalSettings;

    private readonly AdapterHealthStatus _health = new() { IsHealthy = true };

    public abstract SourceAdapterType SourceType { get; }
    public string Name { get; init; }
    public abstract IReadOnlyList<string> Domains { get; }
    public abstract IReadOnlyList<MarketDataType> SupportedCapabilities { get; }
    public bool IsEnabled => Config.Enabled;

    protected BaseDataSourceAdapter(
        HttpClient httpClient,
        IOptions<DataSourceSettings> settings,
        ILogger logger,
        RateLimiter rateLimiter,
        InMemoryDataCache cache,
        DataQualityValidator qualityValidator,
        IDataSourceAuthenticator authenticator,
        string configKey)
    {
        HttpClient = httpClient;
        Logger = logger;
        RateLimiter = rateLimiter;
        Cache = cache;
        QualityValidator = qualityValidator;
        Authenticator = authenticator;
        GlobalSettings = settings.Value;

        if (!settings.Value.Sources.TryGetValue(configKey, out var sourceConfig))
        {
            throw new ArgumentException($"No configuration found for source '{configKey}'");
        }
        Config = sourceConfig;
        Name = Config.Name;

        // Configure HttpClient
        HttpClient.BaseAddress = new Uri(Config.BaseUrl);
        HttpClient.Timeout = TimeSpan.FromSeconds(Config.TimeoutSeconds ?? GlobalSettings.HttpTimeoutSeconds);
        if (!HttpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            HttpClient.DefaultRequestHeaders.Add("User-Agent", GlobalSettings.UserAgent);
        }
    }

    /// <summary>Get the source key for rate limiting.</summary>
    protected string GetSourceKey() => SourceType.ToString().ToLowerInvariant();

    /// <summary>
    /// Authenticates an HTTP request if the source requires it.
    /// Returns an error result if authentication fails; null if successful.
    /// </summary>
    protected async Task<DataResult<T>?> AuthenticateRequestAsync<T>(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var authResult = await Authenticator.AuthenticateAsync(request, Config.Authentication, cancellationToken);
        if (!authResult.Success)
        {
            Logger.LogWarning(
                "Authentication failed for {Source}: {Code} - {Message}",
                Name, authResult.ErrorCode, authResult.ErrorMessage);

            return DataResult<T>.Failure(new DataCollectionError2
            {
                Code = authResult.ErrorCode ?? "AUTHENTICATION_FAILED",
                Message = authResult.ErrorMessage ?? "Authentication failed",
                Retryable = authResult.Retryable,
                OccurredAtUtc = DateTimeOffset.UtcNow
            });
        }

        return null; // null means auth succeeded, continue with request
    }

    // --- Abstract methods for subclasses ---

    /// <summary>
    /// Default order book fetch returns unsupported. Override in adapters that provide order books.
    /// </summary>
    protected virtual Task<DataResult<OrderBook>> FetchOrderBookFromSourceAsync(
        InstrumentMapping instrument,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(DataResult<OrderBook>.Failure(new DataCollectionError2
        {
            Code = "UNSUPPORTED_CAPABILITY",
            Message = $"{Name} does not support order book data",
            Retryable = false
        }));
    }

    /// <summary>
    /// Public order book fetch with full pipeline (auth, rate limit, cache, quality).
    /// </summary>
    public virtual Task<DataResult<OrderBook>> GetOrderBookAsync(
        InstrumentMapping instrument,
        CancellationToken cancellationToken = default)
    {
        return ExecuteWithResilienceAsync(
            instrument,
            "order_book",
            async () =>
            {
                var sourceId = instrument.SourceIdentifiers.GetValueOrDefault(SourceType);
                if (sourceId == null)
                {
                    return DataResult<OrderBook>.Failure(new DataCollectionError2
                    {
                        Code = "PROVIDER_IDENTIFIER_NOT_FOUND",
                        Message = $"No {Name} identifier found for instrument {instrument.Symbol}",
                        Retryable = false
                    });
                }

                var cacheKey = $"orderbook:{instrument.InstrumentId}:{SourceType}";
                if (Cache.TryGet<OrderBook>(cacheKey, out var cached) && cached != null)
                {
                    Logger.LogDebug("Cache hit for {Source} order book: {Symbol}", SourceType, instrument.Symbol);
                    return DataResult<OrderBook>.Success(cached, freshness: DataFreshness.Cached(DateTimeOffset.UtcNow.AddMinutes(Math.Min(Config.CacheMinutes, 2))), quality: DataQuality.Perfect, metadata: new AcquisitionMetadata { CacheHit = true, Sources = [Name] });
                }

                await RateLimiter.WaitForSlotAsync(GetSourceKey(), cancellationToken);

                var sw = System.Diagnostics.Stopwatch.StartNew();
                var result = await FetchOrderBookFromSourceAsync(instrument, cancellationToken);
                sw.Stop();

                if (result.Ok && result.Data != null)
                {
                    Cache.Set(cacheKey, result.Data, TimeSpan.FromMinutes(Math.Min(Config.CacheMinutes, 2)));
                }

                return result;
            },
            cancellationToken);
    }

    protected abstract Task<IReadOnlyList<Candle>> FetchCandlesFromSourceAsync(
        string sourceInstrumentId,
        DateOnly from,
        DateOnly to,
        CandleResolution? resolution,
        CancellationToken cancellationToken);

    protected abstract Task<Candle?> FetchLatestCandleFromSourceAsync(
        string sourceInstrumentId,
        CancellationToken cancellationToken);

    protected abstract bool CanSupportInstrument(InstrumentMapping instrument);

    // --- IDataSourceAdapter implementation ---

    public virtual Task<DataResult<IReadOnlyList<Candle>>> GetHistoricalCandlesAsync(
        InstrumentMapping instrument,
        DateOnly from,
        DateOnly to,
        CandleResolution? resolution = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteWithResilienceAsync(
            instrument,
            "daily_ohlc",
            async () =>
            {
                var sourceId = instrument.SourceIdentifiers.GetValueOrDefault(SourceType);
                if (sourceId == null)
                {
                    return DataResult<IReadOnlyList<Candle>>.Failure(new DataCollectionError2
                    {
                        Code = "INSTRUMENT_NOT_FOUND",
                        Message = $"No {Name} identifier found for instrument {instrument.Symbol}",
                        Retryable = false
                    });
                }

                // Check cache
                var cacheKey = InMemoryDataCache.MarketDataKey(instrument.InstrumentId, SourceType.ToString(), from, to);
                if (Cache.TryGet<IReadOnlyList<Candle>>(cacheKey, out var cached) && cached != null)
                {
                    Logger.LogDebug("Cache hit for {Source} candles: {Symbol}", SourceType, instrument.Symbol);
                    return CreateCandleResult(cached, instrument, from, to, isCached: true);
                }

                // Rate limit
                await RateLimiter.WaitForSlotAsync(GetSourceKey(), cancellationToken);

                // Fetch from source
                var sw = Stopwatch.StartNew();
                var candles = await FetchCandlesFromSourceAsync(sourceId.Id, from, to, resolution, cancellationToken);
                sw.Stop();

                // Cache result
                Cache.Set(cacheKey, candles, TimeSpan.FromMinutes(Config.CacheMinutes));

                Logger.LogInformation(
                    "Fetched {Count} candles from {Source} for {Symbol} in {Elapsed}ms",
                    candles.Count, Name, instrument.Symbol, sw.ElapsedMilliseconds);

                return CreateCandleResult(candles, instrument, from, to, isCached: false, elapsed: sw.Elapsed);
            },
            cancellationToken);
    }

    public virtual Task<DataResult<Candle>> GetLatestCandleAsync(
        InstrumentMapping instrument,
        CancellationToken cancellationToken = default)
    {
        return ExecuteWithResilienceAsync(
            instrument,
            "latest_candle",
            async () =>
            {
                var sourceId = instrument.SourceIdentifiers.GetValueOrDefault(SourceType);
                if (sourceId == null)
                {
                    return DataResult<Candle>.Failure(new DataCollectionError2
                    {
                        Code = "INSTRUMENT_NOT_FOUND",
                        Message = $"No {Name} identifier found for instrument {instrument.Symbol}",
                        Retryable = false
                    });
                }

                // Check cache
                var cacheKey = InMemoryDataCache.LatestCandleKey(instrument.InstrumentId, SourceType.ToString());
                if (Cache.TryGet<Candle>(cacheKey, out var cached) && cached != null)
                {
                    return CreateLatestResult(cached, instrument, isCached: true);
                }

                // Rate limit
                await RateLimiter.WaitForSlotAsync(GetSourceKey(), cancellationToken);

                // Fetch from source
                var sw = Stopwatch.StartNew();
                var candle = await FetchLatestCandleFromSourceAsync(sourceId.Id, cancellationToken);
                sw.Stop();

                if (candle == null)
                {
                    return DataResult<Candle>.Failure(new DataCollectionError2
                    {
                        Code = "DATA_VALIDATION_FAILED",
                        Message = $"No data returned from {Name} for {instrument.Symbol}",
                        Retryable = true,
                        SourceHttpStatus = 200
                    });
                }

                // Cache for a shorter time for latest data
                Cache.Set(cacheKey, candle, TimeSpan.FromMinutes(Math.Min(Config.CacheMinutes, 5)));

                return CreateLatestResult(candle, instrument, isCached: false, elapsed: sw.Elapsed);
            },
            cancellationToken);
    }

    public virtual bool Supports(InstrumentMapping instrument) =>
        IsEnabled && instrument.SourceIdentifiers.ContainsKey(SourceType) && CanSupportInstrument(instrument);

    public virtual Task<AdapterHealthStatus> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_health);
    }

    // --- Resilience and helpers ---

    protected async Task<DataResult<T>> ExecuteWithResilienceAsync<T>(
        InstrumentMapping instrument,
        string dataType,
        Func<Task<DataResult<T>>> action,
        CancellationToken cancellationToken)
    {
        var maxRetries = Config.MaxRetries;
        DataResult<T>? lastResult = null;
        var sw = Stopwatch.StartNew();

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                lastResult = await action();
                if (lastResult.Ok)
                {
                    _health.LastSuccessfulRequest = DateTimeOffset.UtcNow;
                    _health.ConsecutiveFailures = 0;
                    return lastResult;
                }

                // If the error is not retryable, return immediately
                if (lastResult.Error != null && !lastResult.Error.Retryable)
                    break;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex,
                    "Attempt {Attempt}/{MaxRetries} failed for {Source} {DataType} on {Symbol}",
                    attempt + 1, maxRetries, Name, dataType, instrument.Symbol);

                _health.ConsecutiveFailures++;
                _health.LastError = ex.Message;

                lastResult = DataResult<T>.Failure(new DataCollectionError2
                {
                    Code = "SOURCE_UNAVAILABLE",
                    Message = $"Request failed after {attempt + 1} attempts: {ex.Message}",
                    Retryable = true
                });
            }

            if (attempt < maxRetries)
            {
                var delay = CalculateBackoff(attempt);
                Logger.LogDebug("Retrying in {Delay}ms (attempt {Attempt}/{MaxRetries})",
                    delay, attempt + 2, maxRetries);
                await Task.Delay(delay, cancellationToken);
            }
        }

        sw.Stop();
        _health.ConsecutiveFailures++;

        return lastResult ?? DataResult<T>.Failure(new DataCollectionError2
        {
            Code = "INTERNAL_ERROR",
            Message = "Unexpected null result from retry loop",
            Retryable = false
        });
    }

    protected int CalculateBackoff(int attempt)
    {
        var baseDelay = GlobalSettings.RetryBaseDelayMs;
        var maxDelay = GlobalSettings.RetryMaxDelayMs;
        var delay = baseDelay * (int)Math.Pow(2, attempt);

        // Add bounded jitter (±25%)
        var jitter = Random.Shared.Next(-(delay / 4), delay / 4 + 1);
        return Math.Min(delay + jitter, maxDelay);
    }

    protected static async Task<JsonDocument> ParseJsonResponseAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(content);
    }

    private DataResult<IReadOnlyList<Candle>> CreateCandleResult(
        IReadOnlyList<Candle> candles,
        InstrumentMapping instrument,
        DateOnly from,
        DateOnly to,
        bool isCached,
        TimeSpan? elapsed = null)
    {
        var freshness = isCached
            ? DataFreshness.Cached(DateTimeOffset.UtcNow.AddMinutes(-Config.CacheMinutes))
            : DataFreshness.Fresh(TimeSpan.FromMinutes(Config.CacheMinutes));

        var quality = QualityValidator.ValidateCandles(candles, freshness);

        var summary = new DataSummary
        {
            Count = candles.Count,
            StartDate = candles.Count > 0 ? candles[0].Date : null,
            EndDate = candles.Count > 0 ? candles[^1].Date : null,
            QuoteCurrency = instrument.QuoteCurrency,
            Description = $"Historical OHLC evidence from {Name}"
        };

        return DataResult<IReadOnlyList<Candle>>.Success(
            candles,
            summary: summary,
            freshness: freshness,
            quality: quality,
            metadata: new AcquisitionMetadata
            {
                Elapsed = elapsed ?? TimeSpan.Zero,
                CacheHit = isCached,
                Sources = [Name]
            });
    }

    private DataResult<Candle> CreateLatestResult(
        Candle candle,
        InstrumentMapping instrument,
        bool isCached,
        TimeSpan? elapsed = null)
    {
        var freshness = isCached
            ? DataFreshness.Cached(DateTimeOffset.UtcNow.AddMinutes(-Config.CacheMinutes))
            : DataFreshness.Fresh(TimeSpan.FromMinutes(Config.CacheMinutes));

        var quality = QualityValidator.ValidateCandle(candle);

        return DataResult<Candle>.Success(
            candle,
            freshness: freshness,
            quality: quality,
            metadata: new AcquisitionMetadata
            {
                Elapsed = elapsed ?? TimeSpan.Zero,
                CacheHit = isCached,
                Sources = [Name]
            });
    }

    protected DataCollectionError2 CreateError(string code, string message, bool retryable, int? httpStatus = null) => new()
    {
        Code = code,
        Message = message,
        Retryable = retryable,
        SourceHttpStatus = httpStatus,
        OccurredAtUtc = DateTimeOffset.UtcNow
    };
}

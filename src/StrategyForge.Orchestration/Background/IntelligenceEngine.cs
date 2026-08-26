using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StrategyForge.Domain.Configuration;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Interfaces.Background;
using StrategyForge.Domain.Interfaces.Orchestration;
using StrategyForge.Domain.Interfaces.Providers;
using StrategyForge.Domain.Models;

namespace StrategyForge.Orchestration.Background;

/// <summary>
/// Background Intelligence Engine — a hosted service that periodically collects data,
/// assembles evidence, and optionally generates strategies for registered assets.
/// 
/// The engine:
/// - Runs on a configurable schedule (default: every 6 hours)
/// - Processes each registered asset independently (partial failure is tolerated)
/// - Persists evidence and strategies for historical tracking
/// - Tracks run history for observability
/// 
/// The engine does NOT:
/// - Execute trades or make investment decisions
/// - Override user-initiated pipeline runs
/// - Block or interfere with manual API requests
/// 
/// Design notes:
/// - Uses scoped services (scoped IServiceProvider) to create per-run DI scope
/// - IStrategyOrchestrator is resolved per asset within the scope
/// - IEvidenceStore/IStrategyHistoryStore are resolved per run
/// - All operations are cancellation-aware
/// </summary>
public sealed class IntelligenceEngine : BackgroundService, IIntelligenceEngine
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<BackgroundSettings> _settings;
    private readonly ILogger<IntelligenceEngine> _logger;

    // In-memory run history (persists for process lifetime)
    // This provides fast reads without requiring a database for run tracking.
    private readonly List<IntelligenceRun> _runHistory = new();
    private readonly object _historyLock = new();

    public IntelligenceEngine(
        IServiceScopeFactory scopeFactory,
        IOptions<BackgroundSettings> settings,
        ILogger<IntelligenceEngine> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IntelligenceRun> RunAsync(
        IReadOnlyList<string>? assetSymbols = null,
        bool generateStrategies = false,
        CancellationToken cancellationToken = default)
    {
        var run = new IntelligenceRun
        {
            ScheduledAt = DateTimeOffset.UtcNow,
            State = IntelligenceRunState.Running,
            TargetAssets = assetSymbols ?? [],
            GenerateStrategies = generateStrategies
        };

        lock (_historyLock)
        {
            _runHistory.Add(run);
        }

        _logger.LogInformation(
            "Intelligence run {RunId} starting ({AssetCount} assets, strategies={GenerateStrategies})",
            run.Id, run.TargetAssets.Count, generateStrategies);

        var startTime = DateTimeOffset.UtcNow;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var orchestrator = scope.ServiceProvider.GetRequiredService<IStrategyOrchestrator>();

            // If no specific assets requested, we'd need an asset registry.
            // For now, if no assets provided, the run completes immediately.
            if (run.TargetAssets.Count == 0)
            {
                _logger.LogWarning(
                    "Intelligence run {RunId}: no assets specified, completing with no work",
                    run.Id);

                var emptyRun = run with
                {
                    CompletedAt = DateTimeOffset.UtcNow,
                    State = IntelligenceRunState.Completed,
                    TotalDuration = DateTimeOffset.UtcNow - startTime
                };

                UpdateHistory(emptyRun);
                return emptyRun;
            }

            var successfulAssets = 0;
            var failedAssets = 0;
            var evidenceIds = new List<Guid>();
            var strategyIds = new List<Guid>();
            var totalTokens = 0;

            foreach (var symbol in run.TargetAssets)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Intelligence run {RunId} cancelled at asset {Symbol}", run.Id, symbol);
                    break;
                }

                try
                {
                    // Resolve the asset — for now we create a placeholder Asset
                    // In a full implementation, this would look up from an asset registry
                    var asset = new Asset
                    {
                        Symbol = symbol,
                        Name = symbol,
                        Market = "TSE",
                        AssetType = AssetType.Stock
                    };

                    _logger.LogDebug(
                        "Intelligence run {RunId}: processing {Symbol}",
                        run.Id, symbol);

                    // Step 1: Collect data and run analysis
                    var evidence = await orchestrator.AnalyzeAsync(
                        await orchestrator.CollectDataAsync(asset, cancellationToken),
                        cancellationToken);

                    // Step 2: Persist evidence
                    var evidenceRecord = new PersistedEvidence
                    {
                        Asset = asset,
                        AssembledAt = DateTimeOffset.UtcNow,
                        Evidence = evidence,
                        DataSources = evidence.DataSources,
                        IndicatorCount = evidence.IndicatorValues.Count,
                        NewsItemCount = evidence.RecentNews.Count,
                        ExecutionId = run.Id.ToString("N")[..12]
                    };

                    var evidenceStore = scope.ServiceProvider.GetRequiredService<IEvidenceStore>();
                    var storedEvidence = await evidenceStore.StoreAsync(evidenceRecord, cancellationToken);
                    evidenceIds.Add(storedEvidence.Id);

                    // Step 3: Optionally generate strategy
                    if (generateStrategies)
                    {
                        var report = await orchestrator.GenerateStrategyAsync(asset, cancellationToken);

                        var strategyRecord = new PersistedStrategy
                        {
                            Asset = asset,
                            GeneratedAt = DateTimeOffset.UtcNow,
                            Report = report,
                            OverallSentiment = report.ExecutiveSummary.OverallSentiment,
                            OverallConfidence = report.Confidence?.OverallConfidence,
                            PipelineState = report.PipelineState,
                            ContributingAgents = report.ContributingAgents,
                            TokensUsed = report.TotalTokensUsed,
                            GenerationDuration = report.GenerationDuration,
                            LlmModel = report.LlmModel,
                            EvidenceId = storedEvidence.Id
                        };

                        var strategyStore = scope.ServiceProvider.GetRequiredService<IStrategyHistoryStore>();
                        var storedStrategy = await strategyStore.StoreAsync(strategyRecord, cancellationToken);
                        strategyIds.Add(storedStrategy.Id);
                        totalTokens += report.TotalTokensUsed ?? 0;
                    }

                    successfulAssets++;
                }
                catch (OperationCanceledException)
                {
                    throw; // Propagate cancellation
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Intelligence run {RunId}: failed to process {Symbol}: {Error}",
                        run.Id, symbol, ex.Message);
                    failedAssets++;
                }
            }

            var completedRun = run with
            {
                CompletedAt = DateTimeOffset.UtcNow,
                State = failedAssets == 0
                    ? IntelligenceRunState.Completed
                    : successfulAssets > 0
                        ? IntelligenceRunState.PartiallyCompleted
                        : IntelligenceRunState.Failed,
                SuccessfulAssets = successfulAssets,
                FailedAssets = failedAssets,
                EvidenceIds = evidenceIds,
                StrategyIds = strategyIds,
                TotalTokensUsed = totalTokens,
                TotalDuration = DateTimeOffset.UtcNow - startTime
            };

            UpdateHistory(completedRun);

            _logger.LogInformation(
                "Intelligence run {RunId} completed: {Success}/{Total} assets, state={State}",
                completedRun.Id, successfulAssets, run.TargetAssets.Count, completedRun.State);

            return completedRun;
        }
        catch (OperationCanceledException)
        {
            var cancelledRun = run with
            {
                CompletedAt = DateTimeOffset.UtcNow,
                State = IntelligenceRunState.Cancelled,
                TotalDuration = DateTimeOffset.UtcNow - startTime
            };

            UpdateHistory(cancelledRun);
            return cancelledRun;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Intelligence run {RunId} failed unexpectedly", run.Id);

            var failedRun = run with
            {
                CompletedAt = DateTimeOffset.UtcNow,
                State = IntelligenceRunState.Failed,
                ErrorMessage = ex.Message,
                TotalDuration = DateTimeOffset.UtcNow - startTime
            };

            UpdateHistory(failedRun);
            return failedRun;
        }
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<IntelligenceRun>> GetRunHistoryAsync(
        int maxResults = 50,
        CancellationToken cancellationToken = default)
    {
        lock (_historyLock)
        {
            var results = _runHistory
                .OrderByDescending(r => r.ScheduledAt)
                .Take(maxResults)
                .ToList();

            return Task.FromResult<IReadOnlyList<IntelligenceRun>>(results);
        }
    }

    /// <inheritdoc/>
    public Task<IntelligenceRun?> GetRunByIdAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        lock (_historyLock)
        {
            var run = _runHistory.FirstOrDefault(r => r.Id == runId);
            return Task.FromResult(run);
        }
    }

    /// <summary>
    /// Background service execution loop. Runs on a configurable schedule.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.Value.Enabled)
        {
            _logger.LogInformation("Background Intelligence Engine is disabled");
            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(_settings.Value.IntervalMinutes, 60));
        _logger.LogInformation(
            "Background Intelligence Engine starting (interval={IntervalMinutes}min, autoStrategies={AutoStrategies})",
            _settings.Value.IntervalMinutes, _settings.Value.AutoGenerateStrategies);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                _logger.LogInformation("Background Intelligence Engine: starting scheduled run");

                // Run for all registered assets (empty list = no specific targets)
                // In a full implementation, this would query an asset registry
                await RunAsync(
                    assetSymbols: null,
                    generateStrategies: _settings.Value.AutoGenerateStrategies,
                    cancellationToken: stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background Intelligence Engine: scheduled run failed");
            }
        }

        _logger.LogInformation("Background Intelligence Engine stopped");
    }

    private void UpdateHistory(IntelligenceRun updatedRun)
    {
        lock (_historyLock)
        {
            var index = _runHistory.FindIndex(r => r.Id == updatedRun.Id);
            if (index >= 0)
            {
                _runHistory[index] = updatedRun;
            }
            else
            {
                _runHistory.Add(updatedRun);
            }
        }
    }
}

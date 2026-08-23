using Microsoft.Extensions.Logging;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Interfaces.AI;
using StrategyForge.Domain.Interfaces.Analysis;
using StrategyForge.Domain.Interfaces.Orchestration;
using StrategyForge.Domain.Interfaces.Providers;
using StrategyForge.Domain.Models;

namespace StrategyForge.Orchestration;

/// <summary>
/// Coordinates the full analysis pipeline with observability and partial failure handling:
/// Data Collection → Indicator Analysis → AI Agent Analysis → Strategy Synthesis
/// 
/// Phase 7 enhancements:
/// - Pipeline execution state tracking
/// - Structured diagnostics with timing and counts
/// - Individual agent execution status (success, failure, timeout, cancelled)
/// - Correlation ID for tracing across the pipeline
/// - Cancellation propagation
/// - Partial result handling (failed agents don't block successful ones)
/// </summary>
public sealed class StrategyOrchestrator : IStrategyOrchestrator
{
    private readonly IEnumerable<IMarketDataProvider> _marketDataProviders;
    private readonly IEnumerable<INewsProvider> _newsProviders;
    private readonly IEnumerable<IEconomicDataProvider> _economicProviders;
    private readonly IEnumerable<ICompanyDataProvider> _companyProviders;
    private readonly IEnumerable<ICurrencyProvider> _currencyProviders;
    private readonly IEnumerable<IGoldPriceProvider> _goldProviders;
    private readonly IIndicatorEngine _indicatorEngine;
    private readonly IEnumerable<IAgent> _agents;
    private readonly IStrategySynthesisService _synthesisService;
    private readonly ILogger<StrategyOrchestrator> _logger;

    public StrategyOrchestrator(
        IEnumerable<IMarketDataProvider> marketDataProviders,
        IEnumerable<INewsProvider> newsProviders,
        IEnumerable<IEconomicDataProvider> economicProviders,
        IEnumerable<ICompanyDataProvider> companyProviders,
        IEnumerable<ICurrencyProvider> currencyProviders,
        IEnumerable<IGoldPriceProvider> goldProviders,
        IIndicatorEngine indicatorEngine,
        IEnumerable<IAgent> agents,
        IStrategySynthesisService synthesisService,
        ILogger<StrategyOrchestrator> logger)
    {
        _marketDataProviders = marketDataProviders;
        _newsProviders = newsProviders;
        _economicProviders = economicProviders;
        _companyProviders = companyProviders;
        _currencyProviders = currencyProviders;
        _goldProviders = goldProviders;
        _indicatorEngine = indicatorEngine;
        _agents = agents;
        _synthesisService = synthesisService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<StrategyReport> GenerateStrategyAsync(
        Asset asset,
        CancellationToken cancellationToken = default)
    {
        var executionId = Guid.NewGuid().ToString("N")[..12];
        var pipelineStart = DateTimeOffset.UtcNow;
        var warnings = new List<string>();

        _logger.LogInformation(
            "[{ExecutionId}] Starting strategy generation for {Symbol} ({Name})",
            executionId, asset.Symbol, asset.Name);

        var diagnostics = new PipelineDiagnostics
        {
            ExecutionId = executionId,
            State = PipelineState.Running,
            StartedAt = pipelineStart
        };

        try
        {
            // --- Stage 1: Data Collection ---
            _logger.LogDebug("[{ExecutionId}] Step 1: Collecting market data", executionId);
            var dataCollectionStart = DateTimeOffset.UtcNow;
            var dataBundle = await CollectDataAsync(asset, cancellationToken);
            diagnostics = diagnostics with
            {
                DataCollectionDuration = DateTimeOffset.UtcNow - dataCollectionStart,
                SuccessfulDataProviders = dataBundle.SuccessfulProviders.Count,
                FailedDataProviders = dataBundle.FailedProviders.Count
            };

            if (dataBundle.FailedProviders.Count > 0)
            {
                warnings.Add($"Data providers failed: {string.Join(", ", dataBundle.FailedProviders)}");
            }

            // --- Stage 2: Analysis ---
            _logger.LogDebug("[{ExecutionId}] Step 2: Running indicator analysis", executionId);
            var analysisStart = DateTimeOffset.UtcNow;
            var evidence = await AnalyzeAsync(dataBundle, cancellationToken);
            diagnostics = diagnostics with
            {
                AnalysisDuration = DateTimeOffset.UtcNow - analysisStart,
                EvidenceCount = evidence.IndicatorValues.Count + evidence.RecentNews.Count
            };

            // --- Stage 3: Agent Execution (parallel) ---
            _logger.LogDebug(
                "[{ExecutionId}] Step 3: Running {AgentCount} AI agents in parallel",
                executionId, _agents.Count());

            var agentExecutionStart = DateTimeOffset.UtcNow;
            var agentExecutionResults = await ExecuteAgentsAsync(
                evidence, executionId, cancellationToken);
            diagnostics = diagnostics with
            {
                AgentExecutionDuration = DateTimeOffset.UtcNow - agentExecutionStart,
                AgentResults = agentExecutionResults
            };

            var successfulResults = agentExecutionResults
                .Where(r => r.Status == AgentExecutionStatus.Success && r.Result != null)
                .Select(r => r.Result!)
                .ToList();

            var failedAgents = agentExecutionResults
                .Where(r => r.Status is AgentExecutionStatus.Failed
                    or AgentExecutionStatus.Timeout)
                .Select(r => r.AgentName)
                .ToList();

            if (failedAgents.Count > 0)
            {
                warnings.Add($"Agent failures: {string.Join(", ", failedAgents)}");
            }

            _logger.LogInformation(
                "[{ExecutionId}] Agent execution complete: {Success}/{Total} succeeded",
                executionId, successfulResults.Count, agentExecutionResults.Count);

            // --- Stage 4: Strategy Synthesis ---
            _logger.LogDebug(
                "[{ExecutionId}] Step 4: Running strategy synthesis with {AgentCount} agent results",
                executionId, successfulResults.Count);

            var synthesisStart = DateTimeOffset.UtcNow;
            var synthesisContext = new StrategyContext
            {
                Asset = asset,
                AssembledAt = DateTimeOffset.UtcNow,
                Evidence = evidence,
                AgentResults = successfulResults,
                RequestedHorizons = [TimeHorizon.ShortTerm, TimeHorizon.MediumTerm, TimeHorizon.LongTerm]
            };

            var synthesisOutcome = await _synthesisService.SynthesizeAsync(synthesisContext, cancellationToken);
            diagnostics = diagnostics with
            {
                SynthesisDuration = DateTimeOffset.UtcNow - synthesisStart
            };

            var totalDuration = DateTimeOffset.UtcNow - pipelineStart;

            // Determine final pipeline state
            var finalState = DeterminePipelineState(
                synthesisOutcome, successfulResults.Count, agentExecutionResults.Count,
                dataBundle.SuccessfulProviders.Count, dataBundle.FailedProviders.Count);

            diagnostics = diagnostics with
            {
                CompletedAt = DateTimeOffset.UtcNow,
                TotalDuration = totalDuration,
                State = finalState,
                Warnings = warnings
            };

            _logger.LogInformation(
                "[{ExecutionId}] Strategy generation for {Symbol} completed in {Duration}ms with state {State}",
                executionId, asset.Symbol, totalDuration.TotalMilliseconds, finalState);

            if (synthesisOutcome.Success && synthesisOutcome.Report != null)
            {
                return synthesisOutcome.Report with
                {
                    GenerationDuration = totalDuration,
                    PipelineState = finalState,
                    Diagnostics = diagnostics
                };
            }

            // Synthesis failed — return minimal report with diagnostics
            _logger.LogWarning(
                "[{ExecutionId}] Strategy synthesis failed for {Symbol}: {Error}. Returning minimal report.",
                executionId, asset.Symbol, synthesisOutcome.ErrorMessage);

            return new StrategyReport
            {
                Asset = asset,
                GeneratedAt = DateTimeOffset.UtcNow,
                DataAsOf = dataBundle.DataEndTime ?? DateTimeOffset.UtcNow,
                ExecutiveSummary = new ExecutiveSummary
                {
                    OverallSentiment = Sentiment.Unknown,
                    Summary = $"Strategy synthesis failed: {synthesisOutcome.ErrorMessage}. " +
                        $"Analysis evidence and {successfulResults.Count} agent results are available."
                },
                MarketContext = new MarketContext
                {
                    Regime = MarketRegime.Unknown,
                    Description = "Market context analysis unavailable due to synthesis failure.",
                    CurrentPrice = evidence.CurrentPrice
                },
                ContributingAgents = successfulResults.Select(a => a.AgentName).ToList(),
                DataProvidersUsed = dataBundle.SuccessfulProviders.ToList(),
                GenerationDuration = totalDuration,
                PipelineState = finalState,
                Diagnostics = diagnostics
            };
        }
        catch (OperationCanceledException)
        {
            var totalDuration = DateTimeOffset.UtcNow - pipelineStart;
            diagnostics = diagnostics with
            {
                CompletedAt = DateTimeOffset.UtcNow,
                TotalDuration = totalDuration,
                State = PipelineState.Cancelled,
                Warnings = warnings
            };

            _logger.LogWarning(
                "[{ExecutionId}] Strategy generation for {Symbol} was cancelled after {Duration}ms",
                executionId, asset.Symbol, totalDuration.TotalMilliseconds);

            return new StrategyReport
            {
                Asset = asset,
                GeneratedAt = DateTimeOffset.UtcNow,
                DataAsOf = DateTimeOffset.UtcNow,
                ExecutiveSummary = new ExecutiveSummary
                {
                    OverallSentiment = Sentiment.Unknown,
                    Summary = "Strategy generation was cancelled."
                },
                MarketContext = new MarketContext
                {
                    Regime = MarketRegime.Unknown,
                    Description = "Pipeline was cancelled before completion."
                },
                GenerationDuration = totalDuration,
                PipelineState = PipelineState.Cancelled,
                Diagnostics = diagnostics
            };
        }
        catch (Exception ex)
        {
            var totalDuration = DateTimeOffset.UtcNow - pipelineStart;
            diagnostics = diagnostics with
            {
                CompletedAt = DateTimeOffset.UtcNow,
                TotalDuration = totalDuration,
                State = PipelineState.Failed,
                Warnings = [.. warnings, $"Unexpected error: {ex.Message}"]
            };

            _logger.LogError(ex,
                "[{ExecutionId}] Strategy generation for {Symbol} failed unexpectedly",
                executionId, asset.Symbol);

            return new StrategyReport
            {
                Asset = asset,
                GeneratedAt = DateTimeOffset.UtcNow,
                DataAsOf = DateTimeOffset.UtcNow,
                ExecutiveSummary = new ExecutiveSummary
                {
                    OverallSentiment = Sentiment.Unknown,
                    Summary = $"Strategy generation failed: {ex.Message}"
                },
                MarketContext = new MarketContext
                {
                    Regime = MarketRegime.Unknown,
                    Description = "Pipeline failed due to an unexpected error."
                },
                GenerationDuration = totalDuration,
                PipelineState = PipelineState.Failed,
                Diagnostics = diagnostics
            };
        }
    }

    /// <inheritdoc/>
    public async Task<MarketDataBundle> CollectDataAsync(
        Asset asset,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<DataCollectionError>();
        var successfulProviders = new List<string>();
        var failedProviders = new List<string>();

        // Collect market data from the first available provider
        IReadOnlyList<Candle> candles = [];
        foreach (var provider in _marketDataProviders.Where(p => p.Supports(asset)))
        {
            try
            {
                var to = DateOnly.FromDateTime(DateTime.UtcNow);
                var from = to.AddDays(-365);
                candles = await provider.GetHistoricalDataAsync(asset, from, to, cancellationToken);
                successfulProviders.Add(provider.Name);
                break;
            }
            catch (OperationCanceledException)
            {
                throw; // Propagate cancellation
            }
            catch (Exception ex)
            {
                failedProviders.Add(provider.Name);
                errors.Add(new DataCollectionError
                {
                    ProviderName = provider.Name,
                    ErrorMessage = ex.Message,
                    OccurredAt = DateTimeOffset.UtcNow,
                    ExceptionMessage = ex.InnerException?.Message
                });
            }
        }

        return new MarketDataBundle
        {
            Asset = asset,
            CollectedAt = DateTimeOffset.UtcNow,
            Candles = candles,
            SuccessfulProviders = successfulProviders,
            FailedProviders = failedProviders,
            Errors = errors,
            DataStartTime = candles.Count > 0 ? candles[0].Metadata?.DataTimestamp : null,
            DataEndTime = candles.Count > 0 ? candles[^1].Metadata?.DataTimestamp : null
        };
    }

    /// <inheritdoc/>
    public Task<AnalysisEvidence> AnalyzeAsync(
        MarketDataBundle dataBundle,
        CancellationToken cancellationToken = default)
    {
        // Run indicator engine on the candle data
        var indicatorResult = _indicatorEngine.ComputeAll(dataBundle.Candles);

        // Build the evidence bundle
        var evidence = new AnalysisEvidence
        {
            Asset = dataBundle.Asset,
            AssembledAt = DateTimeOffset.UtcNow,
            DataStartDate = indicatorResult.DataStartDate,
            DataEndDate = indicatorResult.DataEndDate,
            CurrentPrice = dataBundle.Candles.Count > 0
                ? dataBundle.Candles[^1].Close
                : null,
            IndicatorValues = indicatorResult.GetLatestValues(),
            IndicatorHistory = indicatorResult.Results,
            CompanyInfo = dataBundle.CompanyInfo,
            EconomicIndicators = dataBundle.EconomicIndicators,
            CurrencyRates = dataBundle.CurrencyRates,
            GoldPrices = dataBundle.GoldPrices,
            RecentNews = dataBundle.News,
            DataSources = dataBundle.SuccessfulProviders.ToList(),
            MissingData = dataBundle.FailedProviders.ToList()
        };

        return Task.FromResult(evidence);
    }

    /// <summary>
    /// Executes all specialist agents in parallel with individual failure handling.
    /// Each agent's execution is tracked independently — one failure does not affect others.
    /// </summary>
    private async Task<IReadOnlyList<AgentExecutionResult>> ExecuteAgentsAsync(
        AnalysisEvidence evidence,
        string executionId,
        CancellationToken cancellationToken)
    {
        var agentList = _agents.ToList();
        var results = new AgentExecutionResult[agentList.Count];

        // Use index-based parallel execution to maintain agent-result association
        var tasks = agentList.Select((agent, index) => Task.Run(async () =>
        {
            var agentStart = DateTimeOffset.UtcNow;

            _logger.LogDebug("[{ExecutionId}] Running agent: {AgentName}", executionId, agent.Name);

            try
            {
                var result = await agent.AnalyzeAsync(evidence, cancellationToken);

                var agentDuration = DateTimeOffset.UtcNow - agentStart;

                // Distinguish success from insufficient evidence
                var status = result.Sentiment == Sentiment.Unknown && result.Confidence == 0m
                    ? AgentExecutionStatus.InsufficientEvidence
                    : AgentExecutionStatus.Success;

                results[index] = new AgentExecutionResult
                {
                    Result = result,
                    AgentName = agent.Name,
                    Status = status,
                    Duration = agentDuration,
                    StartedAt = agentStart,
                    CompletedAt = DateTimeOffset.UtcNow
                };

                _logger.LogInformation(
                    "[{ExecutionId}] Agent {AgentName} completed: {Status} in {Duration}ms",
                    executionId, agent.Name, status, agentDuration.TotalMilliseconds);
            }
            catch (OperationCanceledException)
            {
                results[index] = new AgentExecutionResult
                {
                    AgentName = agent.Name,
                    Status = AgentExecutionStatus.Cancelled,
                    Duration = DateTimeOffset.UtcNow - agentStart,
                    ErrorMessage = "Agent execution was cancelled",
                    StartedAt = agentStart,
                    CompletedAt = DateTimeOffset.UtcNow
                };

                _logger.LogWarning(
                    "[{ExecutionId}] Agent {AgentName} was cancelled",
                    executionId, agent.Name);
            }
            catch (Exception ex)
            {
                results[index] = new AgentExecutionResult
                {
                    AgentName = agent.Name,
                    Status = AgentExecutionStatus.Failed,
                    Duration = DateTimeOffset.UtcNow - agentStart,
                    ErrorMessage = ex.Message,
                    StartedAt = agentStart,
                    CompletedAt = DateTimeOffset.UtcNow
                };

                _logger.LogError(ex,
                    "[{ExecutionId}] Agent {AgentName} failed: {Error}",
                    executionId, agent.Name, ex.Message);
            }
        }));

        await Task.WhenAll(tasks);

        return results;
    }

    /// <summary>
    /// Determines the final pipeline state based on synthesis outcome and agent results.
    /// </summary>
    private static PipelineState DeterminePipelineState(
        StrategySynthesisOutcome synthesisOutcome,
        int successfulAgents,
        int totalAgents,
        int successfulProviders,
        int failedProviders)
    {
        // No agents produced results — critical input missing
        if (totalAgents > 0 && successfulAgents == 0)
            return PipelineState.PartiallyCompleted;

        // Synthesis succeeded
        if (synthesisOutcome.Success && synthesisOutcome.Report != null)
        {
            // Some agents or providers failed — completed with warnings
            if (successfulAgents < totalAgents || failedProviders > 0)
                return PipelineState.CompletedWithWarnings;

            return PipelineState.Completed;
        }

        // Synthesis failed but we have some agent results
        if (successfulAgents > 0)
            return PipelineState.PartiallyCompleted;

        // Nothing worked
        return PipelineState.Failed;
    }
}

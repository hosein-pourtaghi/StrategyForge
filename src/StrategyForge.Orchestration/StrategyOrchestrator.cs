using Microsoft.Extensions.Logging;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Interfaces.AI;
using StrategyForge.Domain.Interfaces.Analysis;
using StrategyForge.Domain.Interfaces.Orchestration;
using StrategyForge.Domain.Interfaces.Providers;
using StrategyForge.Domain.Models;

namespace StrategyForge.Orchestration;

/// <summary>
/// Coordinates the full analysis pipeline:
/// Data Collection → Indicator Analysis → AI Agent Analysis → Strategy Synthesis
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
        _logger.LogInformation(
            "Starting strategy generation for {Symbol} ({Name})",
            asset.Symbol, asset.Name);

        var startTime = DateTimeOffset.UtcNow;

        // Step 1: Collect data
        _logger.LogDebug("Step 1: Collecting market data");
        var dataBundle = await CollectDataAsync(asset, cancellationToken);

        // Step 2: Run analysis
        _logger.LogDebug("Step 2: Running indicator analysis");
        var evidence = await AnalyzeAsync(dataBundle, cancellationToken);

        // Step 3: Run AI agents in parallel
        // Agents are independent — parallel execution reduces total latency.
        // Each agent failure is handled individually; one failure does not affect others.
        _logger.LogDebug("Step 3: Running {AgentCount} AI agents in parallel", _agents.Count());

        var agentTasks = _agents.Select(async agent =>
        {
            try
            {
                _logger.LogDebug("Running agent: {AgentName}", agent.Name);
                return await agent.AnalyzeAsync(evidence, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Agent {AgentName} failed", agent.Name);
                return (AgentAnalysisResult?)null;
            }
        });

        var agentResults = (await Task.WhenAll(agentTasks))
            .Where(r => r != null)
            .Cast<AgentAnalysisResult>()
            .ToList();

        // Step 4: Strategy Synthesis
        _logger.LogDebug("Step 4: Running strategy synthesis with {AgentCount} agent results", agentResults.Count);

        var synthesisContext = new StrategyContext
        {
            Asset = asset,
            AssembledAt = DateTimeOffset.UtcNow,
            Evidence = evidence,
            AgentResults = agentResults,
            RequestedHorizons = [TimeHorizon.ShortTerm, TimeHorizon.MediumTerm, TimeHorizon.LongTerm]
        };

        var synthesisOutcome = await _synthesisService.SynthesizeAsync(synthesisContext, cancellationToken);

        var duration = DateTimeOffset.UtcNow - startTime;

        if (synthesisOutcome.Success && synthesisOutcome.Report != null)
        {
            _logger.LogInformation(
                "Strategy generation for {Symbol} completed in {Duration}ms",
                asset.Symbol, duration.TotalMilliseconds);

            return synthesisOutcome.Report with
            {
                GenerationDuration = duration
            };
        }

        // Fallback: Synthesis failed, return a minimal report with what we have
        _logger.LogWarning(
            "Strategy synthesis failed for {Symbol}: {Error}. Returning minimal report.",
            asset.Symbol, synthesisOutcome.ErrorMessage);

        return new StrategyReport
        {
            Asset = asset,
            GeneratedAt = DateTimeOffset.UtcNow,
            DataAsOf = dataBundle.DataEndTime ?? DateTimeOffset.UtcNow,
            ExecutiveSummary = new ExecutiveSummary
            {
                OverallSentiment = Sentiment.Unknown,
                Summary = $"Strategy synthesis failed: {synthesisOutcome.ErrorMessage}. " +
                    $"Analysis evidence and {agentResults.Count} agent results are available."
            },
            MarketContext = new MarketContext
            {
                Regime = MarketRegime.Unknown,
                Description = "Market context analysis unavailable due to synthesis failure.",
                CurrentPrice = evidence.CurrentPrice
            },
            ContributingAgents = agentResults.Select(a => a.AgentName).ToList(),
            DataProvidersUsed = dataBundle.SuccessfulProviders.ToList(),
            GenerationDuration = duration
        };
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
}

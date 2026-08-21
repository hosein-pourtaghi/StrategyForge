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

        // Step 3: Run AI agents
        _logger.LogDebug("Step 3: Running {AgentCount} AI agents", _agents.Count());
        var agentResults = new List<AgentAnalysisResult>();
        foreach (var agent in _agents)
        {
            try
            {
                _logger.LogDebug("Running agent: {AgentName}", agent.Name);
                var result = await agent.AnalyzeAsync(evidence, cancellationToken);
                agentResults.Add(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Agent {AgentName} failed", agent.Name);
            }
        }

        // Step 4: Build strategy report
        // TODO: Implement Strategy Agent synthesis in Phase 7
        _logger.LogDebug("Step 4: Building strategy report");

        var duration = DateTimeOffset.UtcNow - startTime;
        _logger.LogInformation(
            "Strategy generation for {Symbol} completed in {Duration}ms",
            asset.Symbol, duration.TotalMilliseconds);

        return new StrategyReport
        {
            Asset = asset,
            GeneratedAt = DateTimeOffset.UtcNow,
            DataAsOf = dataBundle.DataEndTime ?? DateTimeOffset.UtcNow,
            ExecutiveSummary = new ExecutiveSummary
            {
                OverallSentiment = Sentiment.Unknown,
                Summary = "Strategy generation pipeline is not yet fully implemented."
            },
            MarketContext = new MarketContext
            {
                Regime = MarketRegime.Unknown,
                Description = "Market context analysis not yet implemented.",
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

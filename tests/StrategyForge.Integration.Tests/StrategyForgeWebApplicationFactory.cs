using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Moq;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Interfaces.AI;
using StrategyForge.Domain.Interfaces.Analysis;
using StrategyForge.Domain.Interfaces.Orchestration;
using StrategyForge.Domain.Interfaces.Providers;
using StrategyForge.Domain.Models;

namespace StrategyForge.Integration.Tests;

/// <summary>
/// Test factory that replaces external dependencies with deterministic fakes.
/// Used by all E2E integration tests to ensure repeatability.
/// </summary>
public class StrategyForgeWebApplicationFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// Mock LLM provider used across tests. Configure responses per-test.
    /// </summary>
    public Mock<ILLMProvider> MockLlmProvider { get; } = new();

    /// <summary>
    /// Mock market data provider used across tests.
    /// </summary>
    public Mock<IMarketDataProvider> MockMarketDataProvider { get; } = new();

    /// <summary>
    /// Mock instrument resolver used across tests.
    /// </summary>
    public Mock<IInstrumentResolver> MockInstrumentResolver { get; } = new();

    /// <summary>
    /// Mock strategy synthesis service used across tests.
    /// </summary>
    public Mock<IStrategySynthesisService> MockSynthesisService { get; } = new();

    /// <summary>
    /// Resets all mock setups to their default configuration.
    /// Call this in test constructors to prevent mock state leakage between tests.
    /// </summary>
    public void ResetMocks()
    {
        MockLlmProvider.Reset();
        MockLlmProvider.Setup(p => p.Name).Returns("TestLLM");
        MockLlmProvider.Setup(p => p.Model).Returns("test-model");
        MockLlmProvider.Setup(p => p.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        MockMarketDataProvider.Reset();
        MockMarketDataProvider.Setup(p => p.Name).Returns("TestMarketData");
        MockMarketDataProvider.Setup(p => p.Supports(It.IsAny<Asset>())).Returns(true);
        MockMarketDataProvider.Setup(p => p.GetHistoricalDataAsync(
                It.IsAny<Asset>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Candle>());
        MockMarketDataProvider.Setup(p => p.GetLatestCandleAsync(
                It.IsAny<Asset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Candle?)null);

        MockInstrumentResolver.Reset();
        MockInstrumentResolver.Setup(r => r.ResolveAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string query, CancellationToken _) =>
                new InstrumentMapping
                {
                    InstrumentId = "test-001",
                    Symbol = query,
                    LatinSymbol = query,
                    DisplayName = $"Test {query}",
                    AssetClass = AssetType.Stock,
                    Exchange = "TSE",
                    QuoteCurrency = "IRR",
                    SourceIdentifiers = new Dictionary<SourceAdapterType, SourceIdentifier>()
                });

        MockSynthesisService.Reset();
        MockSynthesisService.Setup(s => s.SynthesizeAsync(
                It.IsAny<StrategyContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StrategySynthesisOutcome
            {
                Success = true,
                Report = new StrategyReport
                {
                    Asset = new Asset
                    {
                        Symbol = "TEST",
                        Name = "Test Asset",
                        Market = "TSE",
                        AssetType = AssetType.Stock
                    },
                    GeneratedAt = DateTimeOffset.UtcNow,
                    DataAsOf = DateTimeOffset.UtcNow,
                    ExecutiveSummary = new ExecutiveSummary
                    {
                        OverallSentiment = Sentiment.Bullish,
                        Summary = "Test strategy from synthesis"
                    },
                    MarketContext = new MarketContext
                    {
                        Regime = MarketRegime.Uptrend,
                        Description = "Test market context"
                    }
                }
            });
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove all real IAgent registrations
            services.RemoveAll<IAgent>();

            // Remove real LLM provider
            services.RemoveAll<ILLMProvider>();
            services.RemoveAll<HttpClient>();

            // Remove real instrument resolver
            services.RemoveAll<IInstrumentResolver>();

            // Remove real market data providers and all provider collections
            services.RemoveAll<IMarketDataProvider>();
            services.RemoveAll<IEnumerable<IMarketDataProvider>>();
            services.RemoveAll<INewsProvider>();
            services.RemoveAll<IEnumerable<INewsProvider>>();
            services.RemoveAll<IEconomicDataProvider>();
            services.RemoveAll<IEnumerable<IEconomicDataProvider>>();
            services.RemoveAll<ICompanyDataProvider>();
            services.RemoveAll<IEnumerable<ICompanyDataProvider>>();
            services.RemoveAll<ICurrencyProvider>();
            services.RemoveAll<IEnumerable<ICurrencyProvider>>();
            services.RemoveAll<IGoldPriceProvider>();
            services.RemoveAll<IEnumerable<IGoldPriceProvider>>();

            // Remove and replace the synthesis service
            services.RemoveAll<IStrategySynthesisService>();

            // Remove the indicator engine and replace with mock
            services.RemoveAll<IIndicatorEngine>();

            // Remove data source adapters and related services
            var dataSourceAdapterType = Type.GetType("StrategyForge.Domain.Interfaces.Providers.IDataSourceAdapter, StrategyForge.Domain");
            if (dataSourceAdapterType != null)
            {
                services.RemoveAll(dataSourceAdapterType);
            }

            // Configure mock LLM provider
            MockLlmProvider.Setup(p => p.Name).Returns("TestLLM");
            MockLlmProvider.Setup(p => p.Model).Returns("test-model");
            MockLlmProvider.Setup(p => p.IsAvailableAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            services.AddSingleton<ILLMProvider>(MockLlmProvider.Object);

            // Configure mock synthesis service to return success by default
            MockSynthesisService.Setup(s => s.SynthesizeAsync(
                    It.IsAny<StrategyContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new StrategySynthesisOutcome
                {
                    Success = true,
                    Report = new StrategyReport
                    {
                        Asset = new Asset
                        {
                            Symbol = "TEST",
                            Name = "Test Asset",
                            Market = "TSE",
                            AssetType = AssetType.Stock
                        },
                        GeneratedAt = DateTimeOffset.UtcNow,
                        DataAsOf = DateTimeOffset.UtcNow,
                        ExecutiveSummary = new ExecutiveSummary
                        {
                            OverallSentiment = Sentiment.Bullish,
                            Summary = "Test strategy from synthesis"
                        },
                        MarketContext = new MarketContext
                        {
                            Regime = MarketRegime.Uptrend,
                            Description = "Test market context"
                        }
                    }
                });
            services.AddSingleton<IStrategySynthesisService>(MockSynthesisService.Object);

            // Configure mock instrument resolver to return InstrumentMapping
            MockInstrumentResolver.Setup(r => r.ResolveAsync(
                    It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string query, CancellationToken _) =>
                    new InstrumentMapping
                    {
                        InstrumentId = "test-001",
                        Symbol = query,
                        LatinSymbol = query,
                        DisplayName = $"Test {query}",
                        AssetClass = AssetType.Stock,
                        Exchange = "TSE",
                        QuoteCurrency = "IRR",
                        SourceIdentifiers = new Dictionary<SourceAdapterType, SourceIdentifier>()
                    });
            services.AddSingleton<IInstrumentResolver>(MockInstrumentResolver.Object);

            // Configure mock market data provider
            MockMarketDataProvider.Setup(p => p.Name).Returns("TestMarketData");
            MockMarketDataProvider.Setup(p => p.Supports(It.IsAny<Asset>())).Returns(true);
            MockMarketDataProvider.Setup(p => p.GetHistoricalDataAsync(
                    It.IsAny<Asset>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Candle>());
            MockMarketDataProvider.Setup(p => p.GetLatestCandleAsync(
                    It.IsAny<Asset>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Candle?)null);
            services.AddSingleton<IMarketDataProvider>(MockMarketDataProvider.Object);

            // Mock remaining provider collections as empty
            services.AddSingleton<IEnumerable<INewsProvider>>([]);
            services.AddSingleton<IEnumerable<IEconomicDataProvider>>([]);
            services.AddSingleton<IEnumerable<ICompanyDataProvider>>([]);
            services.AddSingleton<IEnumerable<ICurrencyProvider>>([]);
            services.AddSingleton<IEnumerable<IGoldPriceProvider>>([]);

            // Mock indicator engine
            var mockIndicatorEngine = new Mock<IIndicatorEngine>();
            mockIndicatorEngine.Setup(e => e.ComputeAll(
                    It.IsAny<IReadOnlyList<Candle>>(),
                    It.IsAny<IndicatorConfiguration?>()))
                .Returns(new IndicatorEngineResult
                {
                    Results = new Dictionary<string, IReadOnlyList<IndicatorResult>>(),
                    SuccessfulIndicators = [],
                    FailedIndicators = [],
                    CandleCount = 0
                });
            services.AddSingleton<IIndicatorEngine>(mockIndicatorEngine.Object);

            // Register all 6 specialist agents with mock LLM and typed logger
            services.AddSingleton<IAgent>(sp =>
                new AI.Agents.TechnicalAnalyst(
                    MockLlmProvider.Object,
                    sp.GetRequiredService<ILoggerFactory>().CreateLogger<AI.Agents.TechnicalAnalyst>()));
            services.AddSingleton<IAgent>(sp =>
                new AI.Agents.FundamentalAnalyst(
                    MockLlmProvider.Object,
                    sp.GetRequiredService<ILoggerFactory>().CreateLogger<AI.Agents.FundamentalAnalyst>()));
            services.AddSingleton<IAgent>(sp =>
                new AI.Agents.MacroAnalyst(
                    MockLlmProvider.Object,
                    sp.GetRequiredService<ILoggerFactory>().CreateLogger<AI.Agents.MacroAnalyst>()));
            services.AddSingleton<IAgent>(sp =>
                new AI.Agents.NewsAnalyst(
                    MockLlmProvider.Object,
                    sp.GetRequiredService<ILoggerFactory>().CreateLogger<AI.Agents.NewsAnalyst>()));
            services.AddSingleton<IAgent>(sp =>
                new AI.Agents.PoliticalRiskAnalyst(
                    MockLlmProvider.Object,
                    sp.GetRequiredService<ILoggerFactory>().CreateLogger<AI.Agents.PoliticalRiskAnalyst>()));
            services.AddSingleton<IAgent>(sp =>
                new AI.Agents.RiskAnalyst(
                    MockLlmProvider.Object,
                    sp.GetRequiredService<ILoggerFactory>().CreateLogger<AI.Agents.RiskAnalyst>()));
        });
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StrategyForge.Domain.Configuration;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Interfaces.Background;
using StrategyForge.Domain.Interfaces.Orchestration;
using StrategyForge.Domain.Interfaces.Providers;
using StrategyForge.Domain.Models;
using StrategyForge.Infrastructure.Services;
using StrategyForge.Orchestration.Background;
using Xunit;

namespace StrategyForge.Orchestration.Tests;

public class IntelligenceEngineTests
{
    private static Mock<IStrategyOrchestrator> CreateDefaultMockOrchestrator()
    {
        var mock = new Mock<IStrategyOrchestrator>();

        mock.Setup(o => o.CollectDataAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Asset asset, CancellationToken ct) => new MarketDataBundle
            {
                Asset = asset,
                CollectedAt = DateTimeOffset.UtcNow
            });

        mock.Setup(o => o.AnalyzeAsync(It.IsAny<MarketDataBundle>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MarketDataBundle bundle, CancellationToken ct) => new AnalysisEvidence
            {
                Asset = bundle.Asset,
                AssembledAt = DateTimeOffset.UtcNow
            });

        mock.Setup(o => o.GenerateStrategyAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Asset asset, CancellationToken ct) => new StrategyReport
            {
                Asset = asset,
                GeneratedAt = DateTimeOffset.UtcNow,
                DataAsOf = DateTimeOffset.UtcNow,
                ExecutiveSummary = new ExecutiveSummary
                {
                    OverallSentiment = Sentiment.Neutral,
                    Summary = "Test strategy"
                },
                MarketContext = new MarketContext
                {
                    Regime = MarketRegime.Unknown,
                    Description = "Test"
                },
                LlmModel = "test-model",
                TotalTokensUsed = 100,
                GenerationDuration = TimeSpan.FromMilliseconds(50)
            });

        return mock;
    }

    private static IServiceProvider CreateServiceProvider(
        IStrategyOrchestrator? orchestrator = null,
        IEvidenceStore? evidenceStore = null,
        IStrategyHistoryStore? strategyStore = null)
    {
        var mockOrchestrator = orchestrator ?? CreateDefaultMockOrchestrator().Object;
        var mockEvidenceStore = evidenceStore ?? new InMemoryEvidenceStore();
        var mockStrategyStore = strategyStore ?? new InMemoryStrategyHistoryStore();

        var services = new ServiceCollection();
        services.AddSingleton(mockOrchestrator);
        services.AddSingleton<IEvidenceStore>(mockEvidenceStore);
        services.AddSingleton<IStrategyHistoryStore>(mockStrategyStore);
        return services.BuildServiceProvider();
    }

    private static IntelligenceEngine CreateEngine(IServiceProvider serviceProvider)
    {
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var settings = Options.Create(new BackgroundSettings { Enabled = false });
        var logger = Mock.Of<ILogger<IntelligenceEngine>>();

        return new IntelligenceEngine(scopeFactory, settings, logger);
    }

    [Fact]
    public async Task RunAsync_WithNoAssets_CompletesImmediately()
    {
        var engine = CreateEngine(CreateServiceProvider());

        var run = await engine.RunAsync(assetSymbols: []);

        Assert.Equal(IntelligenceRunState.Completed, run.State);
        Assert.Null(run.ErrorMessage);
    }

    [Fact]
    public async Task RunAsync_TracksRunInHistory()
    {
        var engine = CreateEngine(CreateServiceProvider());

        await engine.RunAsync(assetSymbols: []);
        var history = await engine.GetRunHistoryAsync();

        Assert.Single(history);
    }

    [Fact]
    public async Task RunAsync_GeneratesEvidenceForEachAsset()
    {
        var mockEvidenceStore = new InMemoryEvidenceStore();
        var engine = CreateEngine(CreateServiceProvider(evidenceStore: mockEvidenceStore));

        var run = await engine.RunAsync(
            assetSymbols: ["TEST1", "TEST2"],
            generateStrategies: false);

        Assert.Equal(IntelligenceRunState.Completed, run.State);
        Assert.Equal(2, run.SuccessfulAssets);
        Assert.Empty(run.StrategyIds);
    }

    [Fact]
    public async Task RunAsync_GeneratesStrategiesWhenRequested()
    {
        var mockStrategyStore = new InMemoryStrategyHistoryStore();
        var engine = CreateEngine(CreateServiceProvider(strategyStore: mockStrategyStore));

        var run = await engine.RunAsync(
            assetSymbols: ["TEST1"],
            generateStrategies: true);

        Assert.Equal(IntelligenceRunState.Completed, run.State);
        Assert.Single(run.StrategyIds);
    }

    [Fact]
    public async Task RunAsync_HandlesPartialFailure()
    {
        var mockOrchestrator = new Mock<IStrategyOrchestrator>();
        var callCount = 0;
        mockOrchestrator
            .Setup(o => o.CollectDataAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Asset asset, CancellationToken ct) =>
            {
                callCount++;
                if (callCount == 1)
                    throw new InvalidOperationException("Provider failed");
                return new MarketDataBundle
                {
                    Asset = asset,
                    CollectedAt = DateTimeOffset.UtcNow
                };
            });
        mockOrchestrator
            .Setup(o => o.AnalyzeAsync(It.IsAny<MarketDataBundle>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MarketDataBundle bundle, CancellationToken ct) => new AnalysisEvidence
            {
                Asset = bundle.Asset,
                AssembledAt = DateTimeOffset.UtcNow
            });

        var engine = CreateEngine(CreateServiceProvider(orchestrator: mockOrchestrator.Object));

        var run = await engine.RunAsync(
            assetSymbols: ["FAIL", "OK"],
            generateStrategies: false);

        Assert.Equal(IntelligenceRunState.PartiallyCompleted, run.State);
        Assert.Equal(1, run.SuccessfulAssets);
        Assert.Equal(1, run.FailedAssets);
    }

    [Fact]
    public async Task RunAsync_HandlesCompleteFailure()
    {
        var mockOrchestrator = new Mock<IStrategyOrchestrator>();
        mockOrchestrator
            .Setup(o => o.CollectDataAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("All providers down"));

        var engine = CreateEngine(CreateServiceProvider(orchestrator: mockOrchestrator.Object));

        var run = await engine.RunAsync(
            assetSymbols: ["FAIL1", "FAIL2"],
            generateStrategies: false);

        Assert.Equal(IntelligenceRunState.Failed, run.State);
        Assert.Equal(0, run.SuccessfulAssets);
        Assert.Equal(2, run.FailedAssets);
    }

    [Fact]
    public async Task RunAsync_RecordsDuration()
    {
        var engine = CreateEngine(CreateServiceProvider());

        var run = await engine.RunAsync(assetSymbols: []);

        Assert.NotNull(run.TotalDuration);
        Assert.True(run.TotalDuration!.Value.TotalMilliseconds >= 0);
        Assert.NotNull(run.CompletedAt);
    }

    [Fact]
    public async Task GetRunHistoryAsync_ReturnsEmptyWhenNoRuns()
    {
        var engine = CreateEngine(CreateServiceProvider());

        var history = await engine.GetRunHistoryAsync();

        Assert.Empty(history);
    }

    [Fact]
    public async Task GetRunByIdAsync_ReturnsCorrectRun()
    {
        var engine = CreateEngine(CreateServiceProvider());

        var run = await engine.RunAsync(assetSymbols: []);
        var retrieved = await engine.GetRunByIdAsync(run.Id);

        Assert.NotNull(retrieved);
        Assert.Equal(run.Id, retrieved!.Id);
    }

    [Fact]
    public async Task GetRunByIdAsync_ReturnsNullForUnknownId()
    {
        var engine = CreateEngine(CreateServiceProvider());

        var result = await engine.GetRunByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetRunHistoryAsync_RespectsMaxResults()
    {
        var engine = CreateEngine(CreateServiceProvider());

        for (int i = 0; i < 5; i++)
            await engine.RunAsync(assetSymbols: []);

        var history = await engine.GetRunHistoryAsync(maxResults: 2);

        Assert.Equal(2, history.Count);
    }

    [Fact]
    public void Engine_ImplementsIIntelligenceEngine()
    {
        var engine = CreateEngine(CreateServiceProvider());

        Assert.IsAssignableFrom<IIntelligenceEngine>(engine);
    }

    [Fact]
    public void Engine_ImplementsBackgroundService()
    {
        var engine = CreateEngine(CreateServiceProvider());

        Assert.IsAssignableFrom<Microsoft.Extensions.Hosting.BackgroundService>(engine);
    }
}

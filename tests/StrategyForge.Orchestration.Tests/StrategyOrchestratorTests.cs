using Microsoft.Extensions.Logging;
using Moq;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Interfaces.AI;
using StrategyForge.Domain.Interfaces.Analysis;
using StrategyForge.Domain.Interfaces.Orchestration;
using StrategyForge.Domain.Interfaces.Providers;
using StrategyForge.Domain.Models;
using StrategyForge.Orchestration;

namespace StrategyForge.Orchestration.Tests;

/// <summary>
/// Phase 7 tests: Pipeline state, diagnostics, partial failure handling,
/// agent execution tracking, cancellation, and error handling.
/// </summary>
public class StrategyOrchestratorTests
{
    private static Asset TestAsset => new()
    {
        Symbol = "TEST",
        Name = "Test Asset",
        Market = "TSE",
        AssetType = AssetType.Stock
    };

    private static StrategyReport CreateSuccessReport() => new()
    {
        Asset = TestAsset,
        GeneratedAt = DateTimeOffset.UtcNow,
        DataAsOf = DateTimeOffset.UtcNow,
        ExecutiveSummary = new ExecutiveSummary
        {
            OverallSentiment = Sentiment.Bullish,
            Summary = "Test strategy"
        },
        MarketContext = new MarketContext
        {
            Regime = MarketRegime.Uptrend,
            Description = "Strong uptrend"
        }
    };

    // ============================================================
    // Pipeline State Tests
    // ============================================================

    [Fact]
    public async Task GenerateStrategy_AllAgentsSucceed_PipelineStateIsCompleted()
    {
        var mockSynthesis = new Mock<IStrategySynthesisService>();
        mockSynthesis.Setup(s => s.SynthesizeAsync(It.IsAny<StrategyContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StrategySynthesisOutcome { Success = true, Report = CreateSuccessReport() });

        var orchestrator = CreateOrchestrator(
            synthesisService: mockSynthesis.Object,
            agents: CreateSuccessfulAgents(3));

        var report = await orchestrator.GenerateStrategyAsync(TestAsset);

        Assert.Equal(PipelineState.Completed, report.PipelineState);
        Assert.NotNull(report.Diagnostics);
        Assert.Equal(PipelineState.Completed, report.Diagnostics!.State);
    }

    [Fact]
    public async Task GenerateStrategy_SomeAgentsFail_PipelineStateIsCompletedWithWarnings()
    {
        var mockSynthesis = new Mock<IStrategySynthesisService>();
        mockSynthesis.Setup(s => s.SynthesizeAsync(It.IsAny<StrategyContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StrategySynthesisOutcome { Success = true, Report = CreateSuccessReport() });

        var agents = new List<IAgent>
        {
            CreateSuccessfulAgent("TechAnalyst"),
            CreateFailingAgent("FailAnalyst"),
            CreateSuccessfulAgent("MacroAnalyst")
        };

        var orchestrator = CreateOrchestrator(synthesisService: mockSynthesis.Object, agents: agents);
        var report = await orchestrator.GenerateStrategyAsync(TestAsset);

        Assert.Equal(PipelineState.CompletedWithWarnings, report.PipelineState);
        Assert.NotNull(report.Diagnostics);
        Assert.Equal(2, report.Diagnostics!.SuccessfulAgentCount);
        Assert.Equal(1, report.Diagnostics.FailedAgentCount);
        Assert.NotEmpty(report.Diagnostics.Warnings);
    }

    [Fact]
    public async Task GenerateStrategy_AllAgentsFail_PipelineStateIsPartiallyCompleted()
    {
        var mockSynthesis = new Mock<IStrategySynthesisService>();
        mockSynthesis.Setup(s => s.SynthesizeAsync(It.IsAny<StrategyContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StrategySynthesisOutcome { Success = true, Report = CreateSuccessReport() });

        var orchestrator = CreateOrchestrator(
            synthesisService: mockSynthesis.Object,
            agents: CreateFailingAgents(3));

        var report = await orchestrator.GenerateStrategyAsync(TestAsset);

        Assert.Equal(PipelineState.PartiallyCompleted, report.PipelineState);
        Assert.NotNull(report.Diagnostics);
        Assert.Equal(0, report.Diagnostics!.SuccessfulAgentCount);
        Assert.Equal(3, report.Diagnostics.FailedAgentCount);
    }

    [Fact]
    public async Task GenerateStrategy_SynthesisFails_ReturnsMinimalReport()
    {
        var mockSynthesis = new Mock<IStrategySynthesisService>();
        mockSynthesis.Setup(s => s.SynthesizeAsync(It.IsAny<StrategyContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StrategySynthesisOutcome
            {
                Success = false,
                ErrorMessage = "LLM unavailable"
            });

        var orchestrator = CreateOrchestrator(
            synthesisService: mockSynthesis.Object,
            agents: CreateSuccessfulAgents(2));

        var report = await orchestrator.GenerateStrategyAsync(TestAsset);

        Assert.NotNull(report);
        Assert.Contains("synthesis failed", report.ExecutiveSummary.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(report.Diagnostics);
    }

    // ============================================================
    // Diagnostics Tests
    // ============================================================

    [Fact]
    public async Task GenerateStrategy_Diagnostics_ContainsExecutionId()
    {
        var mockSynthesis = new Mock<IStrategySynthesisService>();
        mockSynthesis.Setup(s => s.SynthesizeAsync(It.IsAny<StrategyContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StrategySynthesisOutcome { Success = true, Report = CreateSuccessReport() });

        var orchestrator = CreateOrchestrator(
            synthesisService: mockSynthesis.Object,
            agents: CreateSuccessfulAgents(2));

        var report = await orchestrator.GenerateStrategyAsync(TestAsset);

        Assert.NotNull(report.Diagnostics);
        Assert.False(string.IsNullOrEmpty(report.Diagnostics!.ExecutionId));
        Assert.Equal(12, report.Diagnostics.ExecutionId.Length);
    }

    [Fact]
    public async Task GenerateStrategy_Diagnostics_ContainsAgentTimings()
    {
        var mockSynthesis = new Mock<IStrategySynthesisService>();
        mockSynthesis.Setup(s => s.SynthesizeAsync(It.IsAny<StrategyContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StrategySynthesisOutcome { Success = true, Report = CreateSuccessReport() });

        var orchestrator = CreateOrchestrator(
            synthesisService: mockSynthesis.Object,
            agents: CreateSuccessfulAgents(3));

        var report = await orchestrator.GenerateStrategyAsync(TestAsset);

        Assert.NotNull(report.Diagnostics);
        Assert.Equal(3, report.Diagnostics!.AgentResults.Count);
        Assert.All(report.Diagnostics.AgentResults, r =>
        {
            Assert.False(string.IsNullOrEmpty(r.AgentName));
            Assert.True(r.Duration >= TimeSpan.Zero);
            Assert.True(r.StartedAt <= r.CompletedAt);
        });
    }

    [Fact]
    public async Task GenerateStrategy_Diagnostics_ContainsStageDurations()
    {
        var mockSynthesis = new Mock<IStrategySynthesisService>();
        mockSynthesis.Setup(s => s.SynthesizeAsync(It.IsAny<StrategyContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StrategySynthesisOutcome { Success = true, Report = CreateSuccessReport() });

        var orchestrator = CreateOrchestrator(
            synthesisService: mockSynthesis.Object,
            agents: CreateSuccessfulAgents(1));

        var report = await orchestrator.GenerateStrategyAsync(TestAsset);

        Assert.NotNull(report.Diagnostics);
        Assert.True(report.Diagnostics!.TotalDuration >= TimeSpan.Zero);
        Assert.NotNull(report.Diagnostics.DataCollectionDuration);
        Assert.NotNull(report.Diagnostics.AnalysisDuration);
        Assert.NotNull(report.Diagnostics.AgentExecutionDuration);
        Assert.NotNull(report.Diagnostics.SynthesisDuration);
    }

    [Fact]
    public async Task GenerateStrategy_Diagnostics_ContainsDataProviders()
    {
        var mockSynthesis = new Mock<IStrategySynthesisService>();
        mockSynthesis.Setup(s => s.SynthesizeAsync(It.IsAny<StrategyContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StrategySynthesisOutcome { Success = true, Report = CreateSuccessReport() });

        var orchestrator = CreateOrchestrator(
            synthesisService: mockSynthesis.Object,
            agents: CreateSuccessfulAgents(1));

        var report = await orchestrator.GenerateStrategyAsync(TestAsset);

        Assert.NotNull(report.Diagnostics);
        Assert.True(report.Diagnostics!.SuccessfulDataProviders >= 0);
        Assert.True(report.Diagnostics.FailedDataProviders >= 0);
    }

    // ============================================================
    // Agent Execution Status Tests
    // ============================================================

    [Fact]
    public async Task GenerateStrategy_FailedAgent_HasFailedStatus()
    {
        var mockSynthesis = new Mock<IStrategySynthesisService>();
        mockSynthesis.Setup(s => s.SynthesizeAsync(It.IsAny<StrategyContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StrategySynthesisOutcome { Success = true, Report = CreateSuccessReport() });

        var orchestrator = CreateOrchestrator(
            synthesisService: mockSynthesis.Object,
            agents: new List<IAgent> { CreateFailingAgent("FailAgent") });

        var report = await orchestrator.GenerateStrategyAsync(TestAsset);

        var failResult = report.Diagnostics!.AgentResults.Single(a => a.AgentName == "FailAgent");
        Assert.Equal(AgentExecutionStatus.Failed, failResult.Status);
        Assert.Null(failResult.Result);
        Assert.NotNull(failResult.ErrorMessage);
    }

    [Fact]
    public async Task GenerateStrategy_SuccessfulAgent_HasSuccessStatus()
    {
        var mockSynthesis = new Mock<IStrategySynthesisService>();
        mockSynthesis.Setup(s => s.SynthesizeAsync(It.IsAny<StrategyContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StrategySynthesisOutcome { Success = true, Report = CreateSuccessReport() });

        var orchestrator = CreateOrchestrator(
            synthesisService: mockSynthesis.Object,
            agents: new List<IAgent> { CreateSuccessfulAgent("GoodAgent") });

        var report = await orchestrator.GenerateStrategyAsync(TestAsset);

        var goodResult = report.Diagnostics!.AgentResults.Single(a => a.AgentName == "GoodAgent");
        Assert.Equal(AgentExecutionStatus.Success, goodResult.Status);
        Assert.NotNull(goodResult.Result);
        Assert.Null(goodResult.ErrorMessage);
    }

    // ============================================================
    // Cancellation Tests
    // ============================================================

    [Fact]
    public async Task GenerateStrategy_Cancelled_ReturnsCancelledState()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var mockSynthesis = new Mock<IStrategySynthesisService>();
        mockSynthesis.Setup(s => s.SynthesizeAsync(It.IsAny<StrategyContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Create a provider that checks cancellation token
        var mockMarketProvider = new Mock<IMarketDataProvider>();
        mockMarketProvider.Setup(p => p.Name).Returns("TestProvider");
        mockMarketProvider.Setup(p => p.Supports(It.IsAny<Asset>())).Returns(true);
        mockMarketProvider.Setup(p => p.GetHistoricalDataAsync(
                It.IsAny<Asset>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var mockIndicatorEngine = new Mock<IIndicatorEngine>();
        mockIndicatorEngine.Setup(e => e.ComputeAll(It.IsAny<IReadOnlyList<Candle>>()))
            .Returns(new IndicatorEngineResult());

        var orchestrator = new StrategyOrchestrator(
            marketDataProviders: new[] { mockMarketProvider.Object },
            newsProviders: [],
            economicProviders: [],
            companyProviders: [],
            currencyProviders: [],
            goldProviders: [],
            indicatorEngine: mockIndicatorEngine.Object,
            agents: CreateSuccessfulAgents(1),
            synthesisService: mockSynthesis.Object,
            logger: Mock.Of<ILogger<StrategyOrchestrator>>());

        var report = await orchestrator.GenerateStrategyAsync(TestAsset, cts.Token);

        Assert.Equal(PipelineState.Cancelled, report.PipelineState);
        Assert.Contains("cancelled", report.ExecutiveSummary.Summary, StringComparison.OrdinalIgnoreCase);
    }

    // ============================================================
    // Error Handling Tests
    // ============================================================

    [Fact]
    public async Task GenerateStrategy_UnexpectedException_ReturnsFailedState()
    {
        var mockSynthesis = new Mock<IStrategySynthesisService>();
        mockSynthesis.Setup(s => s.SynthesizeAsync(It.IsAny<StrategyContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Unexpected internal error"));

        var orchestrator = CreateOrchestrator(
            synthesisService: mockSynthesis.Object,
            agents: CreateSuccessfulAgents(1));

        var report = await orchestrator.GenerateStrategyAsync(TestAsset);

        Assert.Equal(PipelineState.Failed, report.PipelineState);
        Assert.Contains("Unexpected internal error", report.ExecutiveSummary.Summary);
        Assert.NotNull(report.Diagnostics);
        Assert.Equal(PipelineState.Failed, report.Diagnostics!.State);
    }

    // ============================================================
    // Pipeline State Enum Tests
    // ============================================================

    [Fact]
    public void PipelineState_HasAllRequiredValues()
    {
        Assert.Equal(0, (int)PipelineState.NotStarted);
        Assert.Equal(1, (int)PipelineState.Running);
        Assert.Equal(2, (int)PipelineState.Completed);
        Assert.Equal(3, (int)PipelineState.CompletedWithWarnings);
        Assert.Equal(4, (int)PipelineState.PartiallyCompleted);
        Assert.Equal(5, (int)PipelineState.Failed);
        Assert.Equal(6, (int)PipelineState.Cancelled);
    }

    [Fact]
    public void AgentExecutionStatus_HasAllRequiredValues()
    {
        Assert.Equal(0, (int)AgentExecutionStatus.Success);
        Assert.Equal(1, (int)AgentExecutionStatus.InsufficientEvidence);
        Assert.Equal(2, (int)AgentExecutionStatus.Failed);
        Assert.Equal(3, (int)AgentExecutionStatus.Timeout);
        Assert.Equal(4, (int)AgentExecutionStatus.Cancelled);
        Assert.Equal(5, (int)AgentExecutionStatus.NotExecuted);
    }

    // ============================================================
    // Domain Model Tests
    // ============================================================

    [Fact]
    public void PipelineDiagnostics_CalculatesCountsCorrectly()
    {
        var diagnostics = new PipelineDiagnostics
        {
            ExecutionId = "test123",
            State = PipelineState.Completed,
            StartedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow,
            TotalDuration = TimeSpan.FromSeconds(5),
            AgentResults =
            [
                new() { AgentName = "A", Status = AgentExecutionStatus.Success, Duration = TimeSpan.FromSeconds(1) },
                new() { AgentName = "B", Status = AgentExecutionStatus.Success, Duration = TimeSpan.FromSeconds(1) },
                new() { AgentName = "C", Status = AgentExecutionStatus.Failed, Duration = TimeSpan.FromSeconds(1), ErrorMessage = "err" },
                new() { AgentName = "D", Status = AgentExecutionStatus.Cancelled, Duration = TimeSpan.FromSeconds(0) }
            ]
        };

        Assert.Equal(2, diagnostics.SuccessfulAgentCount);
        Assert.Equal(1, diagnostics.FailedAgentCount);
        Assert.Equal(1, diagnostics.UnavailableAgentCount);
    }

    [Fact]
    public void AgentExecutionResult_DistinguishesSuccessFromFailure()
    {
        var success = new AgentExecutionResult
        {
            AgentName = "A",
            Status = AgentExecutionStatus.Success,
            Result = new AgentAnalysisResult
            {
                AgentName = "A",
                AssetSymbol = "T",
                GeneratedAt = DateTimeOffset.UtcNow,
                Sentiment = Sentiment.Bullish,
                Confidence = 0.7m,
                Summary = "Test"
            }
        };

        var failed = new AgentExecutionResult
        {
            AgentName = "B",
            Status = AgentExecutionStatus.Failed,
            ErrorMessage = "LLM unavailable"
        };

        Assert.NotNull(success.Result);
        Assert.Null(failed.Result);
        Assert.Equal(AgentExecutionStatus.Success, success.Status);
        Assert.Equal(AgentExecutionStatus.Failed, failed.Status);
    }

    [Fact]
    public async Task GenerateStrategy_ReportContainsDiagnosticsAndState()
    {
        var mockSynthesis = new Mock<IStrategySynthesisService>();
        mockSynthesis.Setup(s => s.SynthesizeAsync(It.IsAny<StrategyContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StrategySynthesisOutcome { Success = true, Report = CreateSuccessReport() });

        var orchestrator = CreateOrchestrator(
            synthesisService: mockSynthesis.Object,
            agents: CreateSuccessfulAgents(2));

        var report = await orchestrator.GenerateStrategyAsync(TestAsset);

        Assert.NotNull(report.Diagnostics);
        Assert.NotEqual(PipelineState.NotStarted, report.PipelineState);
        Assert.NotNull(report.Diagnostics!.ExecutionId);
        Assert.True(report.Diagnostics.TotalDuration > TimeSpan.Zero);
    }

    // ============================================================
    // Architecture Tests
    // ============================================================

    [Fact]
    public void Orchestration_DoesNotReferenceInfrastructure()
    {
        var assembly = typeof(StrategyOrchestrator).Assembly;
        var refs = assembly.GetReferencedAssemblies().Select(a => a.Name);
        Assert.DoesNotContain("StrategyForge.Infrastructure", refs);
    }

    [Fact]
    public void Domain_DoesNotReferenceOrchestration()
    {
        var assembly = typeof(PipelineState).Assembly;
        var refs = assembly.GetReferencedAssemblies().Select(a => a.Name);
        Assert.DoesNotContain("StrategyForge.Orchestration", refs);
    }

    // ============================================================
    // Helpers
    // ============================================================

    private static IAgent CreateSuccessfulAgent(string name)
    {
        var mock = new Mock<IAgent>();
        mock.Setup(a => a.Name).Returns(name);
        mock.Setup(a => a.AnalyzeAsync(It.IsAny<AnalysisEvidence>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentAnalysisResult
            {
                AgentName = name,
                AssetSymbol = "TEST",
                GeneratedAt = DateTimeOffset.UtcNow,
                Sentiment = Sentiment.Bullish,
                Confidence = 0.7m,
                Summary = $"Analysis from {name}"
            });
        return mock.Object;
    }

    private static IAgent CreateFailingAgent(string name)
    {
        var mock = new Mock<IAgent>();
        mock.Setup(a => a.Name).Returns(name);
        mock.Setup(a => a.AnalyzeAsync(It.IsAny<AnalysisEvidence>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Agent failed"));
        return mock.Object;
    }

    private static List<IAgent> CreateSuccessfulAgents(int count) =>
        Enumerable.Range(0, count)
            .Select(i => CreateSuccessfulAgent($"Agent{i}"))
            .Cast<IAgent>()
            .ToList();

    private static List<IAgent> CreateFailingAgents(int count) =>
        Enumerable.Range(0, count)
            .Select(i => CreateFailingAgent($"FailAgent{i}"))
            .Cast<IAgent>()
            .ToList();

    private static StrategyOrchestrator CreateOrchestrator(
        IStrategySynthesisService? synthesisService = null,
        IEnumerable<IAgent>? agents = null)
    {
        var mockMarketProvider = new Mock<IMarketDataProvider>();
        mockMarketProvider.Setup(p => p.Name).Returns("TestProvider");
        mockMarketProvider.Setup(p => p.Supports(It.IsAny<Asset>())).Returns(true);
        mockMarketProvider.Setup(p => p.GetHistoricalDataAsync(
                It.IsAny<Asset>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Candle>());

        var mockIndicatorEngine = new Mock<IIndicatorEngine>();
        mockIndicatorEngine.Setup(e => e.ComputeAll(It.IsAny<IReadOnlyList<Candle>>()))
            .Returns(new IndicatorEngineResult());

        return new StrategyOrchestrator(
            marketDataProviders: new[] { mockMarketProvider.Object },
            newsProviders: [],
            economicProviders: [],
            companyProviders: [],
            currencyProviders: [],
            goldProviders: [],
            indicatorEngine: mockIndicatorEngine.Object,
            agents: agents ?? CreateSuccessfulAgents(2),
            synthesisService: synthesisService ?? new Mock<IStrategySynthesisService>().Object,
            logger: Mock.Of<ILogger<StrategyOrchestrator>>());
    }
}

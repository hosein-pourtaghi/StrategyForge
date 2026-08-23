using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Models;

namespace StrategyForge.Domain.Tests.Models;

/// <summary>
/// Phase 7 domain model tests for pipeline state, agent execution tracking, and diagnostics.
/// </summary>
public class Phase7Tests
{
    // ============================================================
    // PipelineState Tests
    // ============================================================

    [Fact]
    public void PipelineState_AllValuesAreDistinct()
    {
        var values = Enum.GetValues<PipelineState>().Cast<int>().ToList();
        Assert.Equal(values.Count, values.Distinct().Count());
    }

    [Fact]
    public void PipelineState_DefaultIsNotStarted()
    {
        var state = default(PipelineState);
        Assert.Equal(PipelineState.NotStarted, state);
    }

    [Fact]
    public void PipelineState_AllValuesCoverPipelineLifecycle()
    {
        var expected = new[]
        {
            PipelineState.NotStarted,
            PipelineState.Running,
            PipelineState.Completed,
            PipelineState.CompletedWithWarnings,
            PipelineState.PartiallyCompleted,
            PipelineState.Failed,
            PipelineState.Cancelled
        };

        Assert.Equal(expected.Length, Enum.GetValues<PipelineState>().Length);
        foreach (var value in expected)
        {
            Assert.True(Enum.IsDefined(value), $"PipelineState.{value} should be defined");
        }
    }

    // ============================================================
    // AgentExecutionStatus Tests
    // ============================================================

    [Fact]
    public void AgentExecutionStatus_AllValuesAreDistinct()
    {
        var values = Enum.GetValues<AgentExecutionStatus>().Cast<int>().ToList();
        Assert.Equal(values.Count, values.Distinct().Count());
    }

    [Fact]
    public void AgentExecutionStatus_DefaultIsSuccess()
    {
        var status = default(AgentExecutionStatus);
        Assert.Equal(AgentExecutionStatus.Success, status);
    }

    // ============================================================
    // AgentExecutionResult Tests
    // ============================================================

    [Fact]
    public void AgentExecutionResult_SuccessHasResult()
    {
        var result = new AgentExecutionResult
        {
            AgentName = "Test",
            Status = AgentExecutionStatus.Success,
            Result = new AgentAnalysisResult
            {
                AgentName = "Test",
                AssetSymbol = "T",
                GeneratedAt = DateTimeOffset.UtcNow,
                Sentiment = Sentiment.Bullish,
                Confidence = 0.7m,
                Summary = "Test"
            }
        };

        Assert.NotNull(result.Result);
        Assert.Equal(AgentExecutionStatus.Success, result.Status);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void AgentExecutionResult_FailureHasNoResult()
    {
        var result = new AgentExecutionResult
        {
            AgentName = "Test",
            Status = AgentExecutionStatus.Failed,
            ErrorMessage = "LLM unavailable"
        };

        Assert.Null(result.Result);
        Assert.Equal(AgentExecutionStatus.Failed, result.Status);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void AgentExecutionResult_CancelledHasNoResult()
    {
        var result = new AgentExecutionResult
        {
            AgentName = "Test",
            Status = AgentExecutionStatus.Cancelled,
            ErrorMessage = "Cancelled"
        };

        Assert.Null(result.Result);
        Assert.Equal(AgentExecutionStatus.Cancelled, result.Status);
    }

    // ============================================================
    // PipelineDiagnostics Tests
    // ============================================================

    [Fact]
    public void PipelineDiagnostics_CalculatesSuccessfulAgentCount()
    {
        var diagnostics = new PipelineDiagnostics
        {
            ExecutionId = "test",
            State = PipelineState.Completed,
            AgentResults =
            [
                new() { AgentName = "A", Status = AgentExecutionStatus.Success },
                new() { AgentName = "B", Status = AgentExecutionStatus.Success },
                new() { AgentName = "C", Status = AgentExecutionStatus.Failed, ErrorMessage = "err" }
            ]
        };

        Assert.Equal(2, diagnostics.SuccessfulAgentCount);
    }

    [Fact]
    public void PipelineDiagnostics_CalculatesFailedAgentCount()
    {
        var diagnostics = new PipelineDiagnostics
        {
            ExecutionId = "test",
            State = PipelineState.CompletedWithWarnings,
            AgentResults =
            [
                new() { AgentName = "A", Status = AgentExecutionStatus.Success },
                new() { AgentName = "B", Status = AgentExecutionStatus.Failed, ErrorMessage = "err" },
                new() { AgentName = "C", Status = AgentExecutionStatus.Timeout, ErrorMessage = "timeout" }
            ]
        };

        Assert.Equal(2, diagnostics.FailedAgentCount);
    }

    [Fact]
    public void PipelineDiagnostics_CalculatesUnavailableAgentCount()
    {
        var diagnostics = new PipelineDiagnostics
        {
            ExecutionId = "test",
            State = PipelineState.CompletedWithWarnings,
            AgentResults =
            [
                new() { AgentName = "A", Status = AgentExecutionStatus.Success },
                new() { AgentName = "B", Status = AgentExecutionStatus.InsufficientEvidence },
                new() { AgentName = "C", Status = AgentExecutionStatus.Cancelled },
                new() { AgentName = "D", Status = AgentExecutionStatus.NotExecuted }
            ]
        };

        Assert.Equal(3, diagnostics.UnavailableAgentCount);
    }

    [Fact]
    public void PipelineDiagnostics_EmptyAgentResults()
    {
        var diagnostics = new PipelineDiagnostics
        {
            ExecutionId = "test",
            State = PipelineState.Failed
        };

        Assert.Equal(0, diagnostics.SuccessfulAgentCount);
        Assert.Equal(0, diagnostics.FailedAgentCount);
        Assert.Equal(0, diagnostics.UnavailableAgentCount);
    }

    [Fact]
    public void StrategyReport_ContainsPipelineStateAndDiagnostics()
    {
        var report = new StrategyReport
        {
            Asset = new Asset { Symbol = "T", Name = "T", Market = "M", AssetType = AssetType.Stock },
            GeneratedAt = DateTimeOffset.UtcNow,
            DataAsOf = DateTimeOffset.UtcNow,
            ExecutiveSummary = new ExecutiveSummary { OverallSentiment = Sentiment.Neutral, Summary = "Test" },
            MarketContext = new MarketContext { Regime = MarketRegime.Unknown, Description = "Test" },
            PipelineState = PipelineState.CompletedWithWarnings,
            Diagnostics = new PipelineDiagnostics
            {
                ExecutionId = "abc123",
                State = PipelineState.CompletedWithWarnings,
                TotalDuration = TimeSpan.FromSeconds(5)
            }
        };

        Assert.Equal(PipelineState.CompletedWithWarnings, report.PipelineState);
        Assert.NotNull(report.Diagnostics);
        Assert.Equal("abc123", report.Diagnostics!.ExecutionId);
    }
}

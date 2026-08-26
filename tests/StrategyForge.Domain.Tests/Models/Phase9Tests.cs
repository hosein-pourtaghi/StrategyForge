using StrategyForge.Domain.Configuration;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Models;
using Xunit;

namespace StrategyForge.Domain.Tests.Models;

// ============================================================
// PersistedEvidence Tests
// ============================================================

public class PersistedEvidenceTests
{
    private static Asset CreateTestAsset() => new()
    {
        Symbol = "TEST",
        Name = "Test Asset",
        Market = "TSE",
        AssetType = AssetType.Stock
    };

    private static AnalysisEvidence CreateTestEvidence(Asset asset) => new()
    {
        Asset = asset,
        AssembledAt = DateTimeOffset.UtcNow,
        CurrentPrice = 15000m,
        IndicatorValues = new Dictionary<string, IndicatorResult>
        {
            ["RSI"] = new() { IndicatorName = "RSI", Date = DateOnly.FromDateTime(DateTime.Today), Value = 55m, Period = 14 }
        }
    };

    [Fact]
    public void PersistedEvidence_HasDefaultId()
    {
        var evidence = new PersistedEvidence
        {
            Asset = CreateTestAsset(),
            AssembledAt = DateTimeOffset.UtcNow,
            Evidence = CreateTestEvidence(CreateTestAsset())
        };

        Assert.NotEqual(Guid.Empty, evidence.Id);
    }

    [Fact]
    public void PersistedEvidence_PreservesAllFields()
    {
        var asset = CreateTestAsset();
        var evidence = new PersistedEvidence
        {
            Id = Guid.NewGuid(),
            Asset = asset,
            AssembledAt = DateTimeOffset.UtcNow,
            Evidence = CreateTestEvidence(asset),
            DataSources = ["TSETMC", "TGJU"],
            IndicatorCount = 5,
            NewsItemCount = 10,
            DataQualityScore = 0.85m,
            ExecutionId = "abc123"
        };

        Assert.Equal("TEST", evidence.Asset.Symbol);
        Assert.Equal(2, evidence.DataSources.Count);
        Assert.Equal(5, evidence.IndicatorCount);
        Assert.Equal(10, evidence.NewsItemCount);
        Assert.Equal(0.85m, evidence.DataQualityScore);
        Assert.Equal("abc123", evidence.ExecutionId);
    }

    [Fact]
    public void PersistedEvidence_DefaultCollectionsEmpty()
    {
        var evidence = new PersistedEvidence
        {
            Asset = CreateTestAsset(),
            AssembledAt = DateTimeOffset.UtcNow,
            Evidence = CreateTestEvidence(CreateTestAsset())
        };

        Assert.Empty(evidence.DataSources);
    }

    [Fact]
    public void PersistedEvidence_IsRecordType_SupportsWithExpression()
    {
        var original = new PersistedEvidence
        {
            Id = Guid.NewGuid(),
            Asset = CreateTestAsset(),
            AssembledAt = DateTimeOffset.UtcNow,
            Evidence = CreateTestEvidence(CreateTestAsset()),
            IndicatorCount = 3
        };

        var modified = original with { IndicatorCount = 7 };

        Assert.Equal(3, original.IndicatorCount);
        Assert.Equal(7, modified.IndicatorCount);
        Assert.Equal(original.Id, modified.Id);
    }
}

// ============================================================
// PersistedStrategy Tests
// ============================================================

public class PersistedStrategyTests
{
    private static Asset CreateTestAsset() => new()
    {
        Symbol = "TEST",
        Name = "Test Asset",
        Market = "TSE",
        AssetType = AssetType.Stock
    };

    private static StrategyReport CreateTestReport(Asset asset) => new()
    {
        Asset = asset,
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

    [Fact]
    public void PersistedStrategy_HasDefaultId()
    {
        var strategy = new PersistedStrategy
        {
            Asset = CreateTestAsset(),
            GeneratedAt = DateTimeOffset.UtcNow,
            Report = CreateTestReport(CreateTestAsset())
        };

        Assert.NotEqual(Guid.Empty, strategy.Id);
    }

    [Fact]
    public void PersistedStrategy_PreservesAllFields()
    {
        var strategy = new PersistedStrategy
        {
            Id = Guid.NewGuid(),
            Asset = CreateTestAsset(),
            GeneratedAt = DateTimeOffset.UtcNow,
            Report = CreateTestReport(CreateTestAsset()),
            OverallSentiment = Sentiment.Bullish,
            OverallConfidence = 0.75m,
            PipelineState = PipelineState.Completed,
            ContributingAgents = ["TechnicalAnalyst", "MacroAnalyst"],
            TokensUsed = 1500,
            GenerationDuration = TimeSpan.FromSeconds(5),
            LlmModel = "llama3",
            EvidenceId = Guid.NewGuid()
        };

        Assert.Equal(Sentiment.Bullish, strategy.OverallSentiment);
        Assert.Equal(0.75m, strategy.OverallConfidence);
        Assert.Equal(PipelineState.Completed, strategy.PipelineState);
        Assert.Equal(2, strategy.ContributingAgents.Count);
        Assert.Equal(1500, strategy.TokensUsed);
        Assert.Equal("llama3", strategy.LlmModel);
        Assert.NotNull(strategy.EvidenceId);
    }

    [Fact]
    public void PersistedStrategy_DefaultsEmptyCollections()
    {
        var strategy = new PersistedStrategy
        {
            Asset = CreateTestAsset(),
            GeneratedAt = DateTimeOffset.UtcNow,
            Report = CreateTestReport(CreateTestAsset())
        };

        Assert.Empty(strategy.ContributingAgents);
    }
}

// ============================================================
// IntelligenceRun Tests
// ============================================================

public class IntelligenceRunTests
{
    [Fact]
    public void IntelligenceRun_HasDefaultId()
    {
        var run = new IntelligenceRun
        {
            ScheduledAt = DateTimeOffset.UtcNow
        };

        Assert.NotEqual(Guid.Empty, run.Id);
    }

    [Fact]
    public void IntelligenceRun_DefaultStateIsScheduled()
    {
        var run = new IntelligenceRun
        {
            ScheduledAt = DateTimeOffset.UtcNow
        };

        Assert.Equal(IntelligenceRunState.Scheduled, run.State);
    }

    [Fact]
    public void IntelligenceRun_PreservesAllFields()
    {
        var run = new IntelligenceRun
        {
            Id = Guid.NewGuid(),
            ScheduledAt = DateTimeOffset.UtcNow,
            StartedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow.AddMinutes(5),
            State = IntelligenceRunState.Completed,
            TargetAssets = ["TEST", "FOLD"],
            SuccessfulAssets = 2,
            FailedAssets = 0,
            EvidenceIds = [Guid.NewGuid(), Guid.NewGuid()],
            StrategyIds = [Guid.NewGuid()],
            GenerateStrategies = true,
            TotalTokensUsed = 3000,
            TotalDuration = TimeSpan.FromMinutes(5)
        };

        Assert.Equal(IntelligenceRunState.Completed, run.State);
        Assert.Equal(2, run.TargetAssets.Count);
        Assert.Equal(2, run.SuccessfulAssets);
        Assert.Equal(2, run.EvidenceIds.Count);
        Assert.Equal(1, run.StrategyIds.Count);
        Assert.True(run.GenerateStrategies);
        Assert.Equal(3000, run.TotalTokensUsed);
    }

    [Fact]
    public void IntelligenceRun_DefaultCollectionsEmpty()
    {
        var run = new IntelligenceRun
        {
            ScheduledAt = DateTimeOffset.UtcNow
        };

        Assert.Empty(run.TargetAssets);
        Assert.Empty(run.EvidenceIds);
        Assert.Empty(run.StrategyIds);
    }

    [Fact]
    public void IntelligenceRun_AllStatesRepresented()
    {
        var states = Enum.GetValues<IntelligenceRunState>();
        Assert.Equal(6, states.Length);
        Assert.Contains(IntelligenceRunState.Scheduled, states);
        Assert.Contains(IntelligenceRunState.Running, states);
        Assert.Contains(IntelligenceRunState.Completed, states);
        Assert.Contains(IntelligenceRunState.PartiallyCompleted, states);
        Assert.Contains(IntelligenceRunState.Failed, states);
        Assert.Contains(IntelligenceRunState.Cancelled, states);
    }
}

// ============================================================
// BackgroundSettings Tests
// ============================================================

public class BackgroundSettingsTests
{
    [Fact]
    public void BackgroundSettings_DefaultsAreSafe()
    {
        var settings = new BackgroundSettings();

        Assert.False(settings.Enabled);
        Assert.Equal(360, settings.IntervalMinutes);
        Assert.False(settings.AutoGenerateStrategies);
        Assert.Equal(10, settings.MaxAssetsPerRun);
        Assert.Equal(600, settings.RunTimeoutSeconds);
        Assert.Equal(500, settings.MaxEvidenceRetention);
        Assert.Equal(200, settings.MaxStrategyRetention);
    }

    [Fact]
    public void BackgroundSettings_SectionNameIsCorrect()
    {
        Assert.Equal("BackgroundSettings", BackgroundSettings.SectionName);
    }

    [Fact]
    public void BackgroundSettings_SupportsWithExpression()
    {
        var original = new BackgroundSettings();
        var modified = original with { Enabled = true, IntervalMinutes = 60 };

        Assert.False(original.Enabled);
        Assert.True(modified.Enabled);
        Assert.Equal(60, modified.IntervalMinutes);
    }
}

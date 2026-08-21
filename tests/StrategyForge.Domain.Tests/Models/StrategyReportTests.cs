using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Models;
using Xunit;

namespace StrategyForge.Domain.Tests.Models;

public class StrategyReportTests
{
    [Fact]
    public void StrategyReport_Creation_SetsRequiredProperties()
    {
        // Arrange
        var asset = new Asset
        {
            Symbol = "فولاد",
            Name = "Foolad Mobarakeh",
            Market = "TSE",
            AssetType = AssetType.Stock
        };

        // Act
        var report = new StrategyReport
        {
            Asset = asset,
            GeneratedAt = DateTimeOffset.UtcNow,
            DataAsOf = DateTimeOffset.UtcNow.AddDays(-1),
            ExecutiveSummary = new ExecutiveSummary
            {
                OverallSentiment = Sentiment.Bullish,
                Summary = "Test summary"
            },
            MarketContext = new MarketContext
            {
                Regime = MarketRegime.Uptrend,
                Description = "Test context"
            }
        };

        // Assert
        Assert.Equal(asset, report.Asset);
        Assert.NotNull(report.ExecutiveSummary);
        Assert.NotNull(report.MarketContext);
        Assert.Equal(Sentiment.Bullish, report.ExecutiveSummary.OverallSentiment);
        Assert.Equal(MarketRegime.Uptrend, report.MarketContext.Regime);
    }

    [Fact]
    public void StrategyReport_WithScenarios_PreservesScenarios()
    {
        // Arrange
        var asset = CreateTestAsset();
        var bullish = new Scenario
        {
            Name = "Bullish",
            Description = "Price breaks above resistance",
            ProbabilityAssessment = "Possible"
        };
        var bearish = new Scenario
        {
            Name = "Bearish",
            Description = "Price breaks below support",
            ProbabilityAssessment = "Unlikely"
        };

        // Act
        var report = CreateMinimalReport(asset);
        var reportWithScenarios = report with
        {
            BullishScenario = bullish,
            BearishScenario = bearish
        };

        // Assert
        Assert.NotNull(reportWithScenarios.BullishScenario);
        Assert.NotNull(reportWithScenarios.BearishScenario);
        Assert.Equal("Bullish", reportWithScenarios.BullishScenario.Name);
        Assert.Equal("Bearish", reportWithScenarios.BearishScenario.Name);
    }

    [Fact]
    public void StrategyReport_WithAgentResults_PreservesResults()
    {
        // Arrange
        var asset = CreateTestAsset();
        var agentResult = new AgentAnalysisResult
        {
            AgentName = "TechnicalAnalyst",
            AssetSymbol = asset.Symbol,
            GeneratedAt = DateTimeOffset.UtcNow,
            Sentiment = Sentiment.Bullish,
            Confidence = 0.7m,
            Summary = "RSI shows momentum"
        };

        // Act
        var report = CreateMinimalReport(asset);
        var reportWithAgent = report with
        {
            TechnicalAnalysis = agentResult
        };

        // Assert
        Assert.NotNull(reportWithAgent.TechnicalAnalysis);
        Assert.Equal("TechnicalAnalyst", reportWithAgent.TechnicalAnalysis.AgentName);
        Assert.Equal(0.7m, reportWithAgent.TechnicalAnalysis.Confidence);
    }

    [Fact]
    public void StrategyReport_EvidenceLists_DefaultToEmpty()
    {
        // Arrange
        var asset = CreateTestAsset();

        // Act
        var report = CreateMinimalReport(asset);

        // Assert
        Assert.NotNull(report.SupportingEvidence);
        Assert.Empty(report.SupportingEvidence);
        Assert.NotNull(report.ContradictingEvidence);
        Assert.Empty(report.ContradictingEvidence);
        Assert.NotNull(report.MissingInformation);
        Assert.Empty(report.MissingInformation);
        Assert.NotNull(report.InvalidationConditions);
        Assert.Empty(report.InvalidationConditions);
        Assert.NotNull(report.MonitoringRecommendations);
        Assert.Empty(report.MonitoringRecommendations);
    }

    private static Asset CreateTestAsset() => new()
    {
        Symbol = "فولاد",
        Name = "Foolad Mobarakeh",
        Market = "TSE",
        AssetType = AssetType.Stock
    };

    private static StrategyReport CreateMinimalReport(Asset asset) => new()
    {
        Asset = asset,
        GeneratedAt = DateTimeOffset.UtcNow,
        DataAsOf = DateTimeOffset.UtcNow,
        ExecutiveSummary = new ExecutiveSummary
        {
            OverallSentiment = Sentiment.Unknown,
            Summary = "Test"
        },
        MarketContext = new MarketContext
        {
            Regime = MarketRegime.Unknown,
            Description = "Test"
        }
    };
}

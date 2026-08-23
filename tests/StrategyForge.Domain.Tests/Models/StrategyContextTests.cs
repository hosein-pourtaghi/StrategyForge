using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Models;
using Xunit;

namespace StrategyForge.Domain.Tests.Models;

public class StrategyContextTests
{
    [Fact]
    public void StrategyContext_Creation_SetsRequiredProperties()
    {
        var asset = CreateTestAsset();
        var evidence = CreateTestEvidence(asset);

        var context = new StrategyContext
        {
            Asset = asset,
            AssembledAt = DateTimeOffset.UtcNow,
            Evidence = evidence
        };

        Assert.Equal(asset, context.Asset);
        Assert.Equal(evidence, context.Evidence);
        Assert.Empty(context.AgentResults);
        Assert.Empty(context.Constraints);
        Assert.Null(context.FocusArea);
    }

    [Fact]
    public void StrategyContext_WithAgentResults_PreservesResults()
    {
        var asset = CreateTestAsset();
        var evidence = CreateTestEvidence(asset);
        var agentResult = CreateTestAgentResult("TechnicalAnalyst");

        var context = new StrategyContext
        {
            Asset = asset,
            AssembledAt = DateTimeOffset.UtcNow,
            Evidence = evidence,
            AgentResults = [agentResult]
        };

        Assert.Single(context.AgentResults);
        Assert.Equal("TechnicalAnalyst", context.AgentResults[0].AgentName);
    }

    [Fact]
    public void StrategyContext_WithHorizons_PreservesHorizons()
    {
        var context = new StrategyContext
        {
            Asset = CreateTestAsset(),
            AssembledAt = DateTimeOffset.UtcNow,
            Evidence = CreateTestEvidence(CreateTestAsset()),
            RequestedHorizons = [TimeHorizon.ShortTerm, TimeHorizon.MediumTerm]
        };

        Assert.Equal(2, context.RequestedHorizons.Count);
        Assert.Contains(TimeHorizon.ShortTerm, context.RequestedHorizons);
        Assert.Contains(TimeHorizon.MediumTerm, context.RequestedHorizons);
    }

    [Fact]
    public void StrategyContext_WithConstraints_PreservesConstraints()
    {
        var context = new StrategyContext
        {
            Asset = CreateTestAsset(),
            AssembledAt = DateTimeOffset.UtcNow,
            Evidence = CreateTestEvidence(CreateTestAsset()),
            Constraints = ["Max risk: Moderate", "Focus on technical analysis"],
            FocusArea = "Risk assessment"
        };

        Assert.Equal(2, context.Constraints.Count);
        Assert.Equal("Risk assessment", context.FocusArea);
    }

    [Fact]
    public void StrategyContext_EvidenceLists_DefaultToEmpty()
    {
        var context = new StrategyContext
        {
            Asset = CreateTestAsset(),
            AssembledAt = DateTimeOffset.UtcNow,
            Evidence = CreateTestEvidence(CreateTestAsset())
        };

        Assert.NotNull(context.AgentResults);
        Assert.NotNull(context.RequestedHorizons);
        Assert.NotNull(context.Constraints);
    }

    private static Asset CreateTestAsset() => new()
    {
        Symbol = "\u0641\u0648\u0644\u0627\u062f",
        Name = "Foolad Mobarakeh",
        Market = "TSE",
        AssetType = AssetType.Stock
    };

    private static AnalysisEvidence CreateTestEvidence(Asset asset) => new()
    {
        Asset = asset,
        AssembledAt = DateTimeOffset.UtcNow,
        CurrentPrice = 15000m,
        DailyChangePercent = 2.5m
    };

    private static AgentAnalysisResult CreateTestAgentResult(string agentName) => new()
    {
        AgentName = agentName,
        AssetSymbol = "\u0641\u0648\u0644\u0627\u062f",
        GeneratedAt = DateTimeOffset.UtcNow,
        Sentiment = Sentiment.Bullish,
        Confidence = 0.7m,
        Summary = "Test analysis"
    };
}

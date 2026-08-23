using Microsoft.Extensions.Logging;
using Moq;
using StrategyForge.AI.Agents;
using StrategyForge.AI.Services;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Interfaces.AI;
using StrategyForge.Domain.Models;
using Xunit;

namespace StrategyForge.AI.Tests;

// ============================================================
// AgentPromptBuilder Tests
// ============================================================

public class AgentPromptBuilderTests
{
    private static readonly Asset TestAsset = new()
    {
        Symbol = "TEST",
        Name = "Test Asset",
        Market = "TSE",
        AssetType = AssetType.Stock
    };

    private static AnalysisEvidence CreateFullEvidence() => new()
    {
        Asset = TestAsset,
        AssembledAt = DateTimeOffset.UtcNow,
        DataStartDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-180)),
        DataEndDate = DateOnly.FromDateTime(DateTime.Today),
        CurrentPrice = 15000m,
        DailyChangePercent = 2.5m,
        LatestVolume = 1_500_000,
        AverageVolume = 1_200_000,
        VolumeRatio = 1.25m,
        MarketRegime = MarketRegime.Uptrend,
        PriceActionSummary = "Strong upward momentum",
        SupportLevels = [14000m, 13500m],
        ResistanceLevels = [15500m, 16000m],
        IndicatorValues = new Dictionary<string, IndicatorResult>
        {
            ["RSI"] = new() { IndicatorName = "RSI", Date = DateOnly.FromDateTime(DateTime.Today), Value = 62m, Period = 14, Signal = "Neutral" },
            ["MACD"] = new() { IndicatorName = "MACD", Date = DateOnly.FromDateTime(DateTime.Today), Value = 125m, Period = 26 }
        },
        CompanyInfo = new CompanyInfo
        {
            Symbol = "TEST",
            CompanyName = "Test Corp",
            Sector = "Mining",
            MarketCap = 50_000_000_000m,
            Pe = 8.5m,
            Eps = 1765m,
            Revenue = 25_000_000_000m,
            NetProfit = 5_000_000_000m,
            GrossMargin = 35m,
            NetMargin = 20m
        },
        EconomicIndicators =
        [
            new() { Name = "Inflation Rate", Value = 42m, Unit = "%", Period = "2024-06" },
            new() { Name = "Central Bank Rate", Value = 23m, Unit = "%" }
        ],
        CurrencyRates =
        [
            new() { BaseCurrency = "USD", QuoteCurrency = "IRR", Rate = 580000m, Timestamp = DateTimeOffset.UtcNow },
            new() { BaseCurrency = "USDT", QuoteCurrency = "IRR", Rate = 595000m, Timestamp = DateTimeOffset.UtcNow }
        ],
        GoldPrices =
        [
            new() { Price = 3_400_000m, Unit = "IRR/mithqal", GoldType = "18K", Timestamp = DateTimeOffset.UtcNow }
        ],
        RecentNews =
        [
            new() { Title = "Mining sector sees increased output", Source = "IRNA", PublishedAt = DateTimeOffset.UtcNow.AddDays(-1), Sentiment = Sentiment.Bullish },
            new() { Title = "Currency volatility continues", Source = "Donya-e-Eqtesad", PublishedAt = DateTimeOffset.UtcNow.AddHours(-6), Sentiment = Sentiment.Neutral }
        ],
        MissingData = ["Sector peers comparison unavailable"],
        Warnings = ["Volume data from single source"],
        DataSources = ["TSETMC", "TGJU"]
    };

    [Fact]
    public void BuildEvidenceSection_TechnicalScope_IncludesIndicators()
    {
        var evidence = CreateFullEvidence();
        var section = AgentPromptBuilder.BuildEvidenceSection(evidence, EvidenceScope.Technical);

        Assert.Contains("RSI", section);
        Assert.Contains("MACD", section);
        Assert.Contains("Support levels", section);
        Assert.Contains("Resistance levels", section);
        Assert.Contains("Market regime", section);
        // Should NOT include company fundamentals
        Assert.DoesNotContain("Company Fundamentals", section);
        Assert.DoesNotContain("Economic Indicators", section);
        Assert.DoesNotContain("Recent News", section);
    }

    [Fact]
    public void BuildEvidenceSection_FundamentalScope_IncludesCompanyInfo()
    {
        var evidence = CreateFullEvidence();
        var section = AgentPromptBuilder.BuildEvidenceSection(evidence, EvidenceScope.Fundamental);

        Assert.Contains("Company Fundamentals", section);
        Assert.Contains("Test Corp", section);
        Assert.Contains("P/E Ratio", section);
        Assert.Contains("Market Cap", section);
        // Should NOT include indicators
        Assert.DoesNotContain("Deterministic Technical Indicators", section);
        Assert.DoesNotContain("Economic Indicators", section);
    }

    [Fact]
    public void BuildEvidenceSection_MacroScope_IncludesEconomicData()
    {
        var evidence = CreateFullEvidence();
        var section = AgentPromptBuilder.BuildEvidenceSection(evidence, EvidenceScope.Macro);

        Assert.Contains("Economic Indicators", section);
        Assert.Contains("Inflation Rate", section);
        Assert.Contains("Currency Rates", section);
        Assert.Contains("USD/IRR", section);
        Assert.Contains("Gold Prices", section);
        Assert.Contains("18K", section);
        // Should NOT include indicators or company info
        Assert.DoesNotContain("Deterministic Technical Indicators", section);
        Assert.DoesNotContain("Company Fundamentals", section);
        Assert.DoesNotContain("Recent News", section);
    }

    [Fact]
    public void BuildEvidenceSection_NewsScope_IncludesNews()
    {
        var evidence = CreateFullEvidence();
        var section = AgentPromptBuilder.BuildEvidenceSection(evidence, EvidenceScope.News);

        Assert.Contains("Recent News", section);
        Assert.Contains("Mining sector sees increased output", section);
        Assert.Contains("Currency volatility continues", section);
        // Should NOT include indicators or fundamentals
        Assert.DoesNotContain("Deterministic Technical Indicators", section);
        Assert.DoesNotContain("Company Fundamentals", section);
    }

    [Fact]
    public void BuildEvidenceSection_RiskScope_IncludesAllEvidence()
    {
        var evidence = CreateFullEvidence();
        var section = AgentPromptBuilder.BuildEvidenceSection(evidence, EvidenceScope.Risk);

        Assert.Contains("Deterministic Technical Indicators", section);
        Assert.Contains("Company Fundamentals", section);
        Assert.Contains("Economic Indicators", section);
        Assert.Contains("Recent News", section);
        Assert.Contains("Currency Rates", section);
        Assert.Contains("Gold Prices", section);
    }

    [Fact]
    public void BuildEvidenceSection_AlwaysIncludesMissingData()
    {
        var evidence = CreateFullEvidence();
        var section = AgentPromptBuilder.BuildEvidenceSection(evidence, EvidenceScope.Technical);

        Assert.Contains("Missing Data", section);
        Assert.Contains("Sector peers comparison unavailable", section);
        Assert.Contains("Data Warnings", section);
        Assert.Contains("Volume data from single source", section);
    }

    [Fact]
    public void BuildEvidenceSection_MissingFundamentals_AcknowledgesMissing()
    {
        var evidence = CreateFullEvidence() with { CompanyInfo = null };
        var section = AgentPromptBuilder.BuildEvidenceSection(evidence, EvidenceScope.Fundamental);

        Assert.Contains("No fundamental data available", section);
    }

    [Fact]
    public void BuildEvidenceSection_MissingNews_AcknowledgesMissing()
    {
        var evidence = CreateFullEvidence() with { RecentNews = [] };
        var section = AgentPromptBuilder.BuildEvidenceSection(evidence, EvidenceScope.News);

        Assert.Contains("No recent news available", section);
    }

    [Fact]
    public void BuildUserPrompt_IncludesTaskInstruction()
    {
        var evidence = CreateFullEvidence();
        var prompt = AgentPromptBuilder.BuildUserPrompt(
            evidence, EvidenceScope.Technical, "Analyze the technical indicators.");

        Assert.Contains("Analyze the technical indicators.", prompt);
        Assert.Contains("Your Task", prompt);
        Assert.Contains("Do not fabricate data", prompt);
    }

    [Fact]
    public void BuildUserPrompt_IncludesAdditionalContext()
    {
        var evidence = CreateFullEvidence();
        var prompt = AgentPromptBuilder.BuildUserPrompt(
            evidence, EvidenceScope.Risk, "Assess risk.", "Focus on downside risk.");

        Assert.Contains("Focus on downside risk.", prompt);
    }

    [Fact]
    public void EvidenceOnlyRules_ContainsCriticalConstraints()
    {
        Assert.Contains("ONLY from the provided evidence", AgentPromptBuilder.EvidenceOnlyRules);
        Assert.Contains("Never invent facts", AgentPromptBuilder.EvidenceOnlyRules);
        Assert.Contains("Never produce trading signals", AgentPromptBuilder.EvidenceOnlyRules);
        Assert.Contains("Mark uncertainty explicitly", AgentPromptBuilder.EvidenceOnlyRules);
    }
}

// ============================================================
// EvidenceScope Tests
// ============================================================

public class EvidenceScopeTests
{
    [Fact]
    public void TechnicalScope_IncludesOnlyMarketAndIndicators()
    {
        var scope = EvidenceScope.Technical;
        Assert.True(scope.IncludeMarketData);
        Assert.True(scope.IncludeTechnicalIndicators);
        Assert.False(scope.IncludeFundamentals);
        Assert.False(scope.IncludeEconomic);
        Assert.False(scope.IncludeNews);
    }

    [Fact]
    public void FundamentalScope_IncludesMarketAndFundamentals()
    {
        var scope = EvidenceScope.Fundamental;
        Assert.True(scope.IncludeMarketData);
        Assert.False(scope.IncludeTechnicalIndicators);
        Assert.True(scope.IncludeFundamentals);
        Assert.False(scope.IncludeEconomic);
        Assert.False(scope.IncludeNews);
    }

    [Fact]
    public void MacroScope_IncludesMarketAndEconomic()
    {
        var scope = EvidenceScope.Macro;
        Assert.True(scope.IncludeMarketData);
        Assert.False(scope.IncludeTechnicalIndicators);
        Assert.False(scope.IncludeFundamentals);
        Assert.True(scope.IncludeEconomic);
        Assert.False(scope.IncludeNews);
    }

    [Fact]
    public void NewsScope_IncludesMarketAndNews()
    {
        var scope = EvidenceScope.News;
        Assert.True(scope.IncludeMarketData);
        Assert.False(scope.IncludeTechnicalIndicators);
        Assert.False(scope.IncludeFundamentals);
        Assert.False(scope.IncludeEconomic);
        Assert.True(scope.IncludeNews);
    }

    [Fact]
    public void PoliticalRiskScope_IncludesMarketEconomicAndNews()
    {
        var scope = EvidenceScope.PoliticalRisk;
        Assert.True(scope.IncludeMarketData);
        Assert.False(scope.IncludeTechnicalIndicators);
        Assert.False(scope.IncludeFundamentals);
        Assert.True(scope.IncludeEconomic);
        Assert.True(scope.IncludeNews);
    }

    [Fact]
    public void RiskScope_IncludesEverything()
    {
        var scope = EvidenceScope.Risk;
        Assert.True(scope.IncludeMarketData);
        Assert.True(scope.IncludeTechnicalIndicators);
        Assert.True(scope.IncludeFundamentals);
        Assert.True(scope.IncludeEconomic);
        Assert.True(scope.IncludeNews);
    }
}

// ============================================================
// Specialist Agent Naming & Contract Tests
// ============================================================

public class SpecialistAgentContractTests
{
    private static Mock<ILLMProvider> CreateMockProvider(string responseJson, bool success = true)
    {
        var mock = new Mock<ILLMProvider>();
        mock.Setup(p => p.Name).Returns("TestProvider");
        mock.Setup(p => p.Model).Returns("test-model");
        mock.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = responseJson,
                Model = "test-model",
                Success = success,
                PromptTokens = 100,
                CompletionTokens = 200
            });
        return mock;
    }

    private static Asset CreateTestAsset() => new()
    {
        Symbol = "TEST",
        Name = "Test Asset",
        Market = "TSE",
        AssetType = AssetType.Stock
    };

    private static AnalysisEvidence CreateTestEvidence() => new()
    {
        Asset = CreateTestAsset(),
        AssembledAt = DateTimeOffset.UtcNow,
        CurrentPrice = 15000m,
        IndicatorValues = new Dictionary<string, IndicatorResult>
        {
            ["RSI"] = new() { IndicatorName = "RSI", Date = DateOnly.FromDateTime(DateTime.Today), Value = 55m, Period = 14 }
        }
    };

    private static string ValidAgentResponse(string agentName, Sentiment sentiment = Sentiment.Bullish, decimal confidence = 0.7m) =>
        $@"{{
            ""agentName"": ""{agentName}"",
            ""sentiment"": ""{sentiment}"",
            ""confidence"": {confidence},
            ""summary"": ""Test analysis from {agentName}"",
            ""detailedAnalysis"": ""Detailed findings"",
            ""supportingEvidence"": [{{""content"": ""Evidence 1"", ""type"": ""Fact"", ""source"": ""Test"", ""confidence"": 0.8}}],
            ""contradictingEvidence"": [],
            ""identifiedRisks"": [""Risk 1""],
            ""informationGaps"": [""Gap 1""],
            ""agentSpecificData"": {{""key"": ""value""}}
        }}";

    // --- TechnicalAnalyst Tests ---

    [Fact]
    public void TechnicalAnalyst_HasCorrectName()
    {
        var agent = new TechnicalAnalyst(
            Mock.Of<ILLMProvider>(),
            Mock.Of<ILogger<TechnicalAnalyst>>());
        Assert.Equal("TechnicalAnalyst", agent.Name);
    }

    [Fact]
    public async Task TechnicalAnalyst_ValidResponse_ReturnsResult()
    {
        var provider = CreateMockProvider(ValidAgentResponse("TechnicalAnalyst"));
        var agent = new TechnicalAnalyst(provider.Object, Mock.Of<ILogger<TechnicalAnalyst>>());

        var result = await agent.AnalyzeAsync(CreateTestEvidence());

        Assert.Equal("TechnicalAnalyst", result.AgentName);
        Assert.Equal(Sentiment.Bullish, result.Sentiment);
        Assert.Equal(0.7m, result.Confidence);
        Assert.Contains("Evidence 1", result.SupportingEvidence[0].Content);
        Assert.True(result.LlmDuration.HasValue);
    }

    [Fact]
    public async Task TechnicalAnalyst_SystemPrompt_ContainsTechnicalInstructions()
    {
        var agent = new TechnicalAnalyst(
            Mock.Of<ILLMProvider>(),
            Mock.Of<ILogger<TechnicalAnalyst>>());

        // Use reflection to get the system prompt for inspection
        var method = typeof(SpecialistAgentBase).GetMethod("GetSystemPrompt",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var prompt = (string)method!.Invoke(agent, [])!;

        Assert.Contains("Technical Analyst", prompt);
        Assert.Contains("Trend", prompt);
        Assert.Contains("Momentum", prompt);
        Assert.Contains("Volatility", prompt);
        Assert.Contains("Support", prompt);
        Assert.Contains("agentName", prompt);
        Assert.Contains("sentiment", prompt);
        Assert.Contains("confidence", prompt);
    }

    // --- FundamentalAnalyst Tests ---

    [Fact]
    public void FundamentalAnalyst_HasCorrectName()
    {
        var agent = new FundamentalAnalyst(
            Mock.Of<ILLMProvider>(),
            Mock.Of<ILogger<FundamentalAnalyst>>());
        Assert.Equal("FundamentalAnalyst", agent.Name);
    }

    [Fact]
    public async Task FundamentalAnalyst_ValidResponse_ReturnsResult()
    {
        var provider = CreateMockProvider(ValidAgentResponse("FundamentalAnalyst"));
        var agent = new FundamentalAnalyst(provider.Object, Mock.Of<ILogger<FundamentalAnalyst>>());

        var result = await agent.AnalyzeAsync(CreateTestEvidence());

        Assert.Equal("FundamentalAnalyst", result.AgentName);
        Assert.Equal(Sentiment.Bullish, result.Sentiment);
    }

    [Fact]
    public async Task FundamentalAnalyst_MissingData_LowConfidence()
    {
        // LLM returns low confidence due to missing data
        var response = ValidAgentResponse("FundamentalAnalyst", confidence: 0.2m);
        var provider = CreateMockProvider(response);
        var agent = new FundamentalAnalyst(provider.Object, Mock.Of<ILogger<FundamentalAnalyst>>());

        var evidence = CreateTestEvidence() with { CompanyInfo = null };
        var result = await agent.AnalyzeAsync(evidence);

        Assert.Equal("FundamentalAnalyst", result.AgentName);
        Assert.Equal(0.2m, result.Confidence);
    }

    // --- MacroAnalyst Tests ---

    [Fact]
    public void MacroAnalyst_HasCorrectName()
    {
        var agent = new MacroAnalyst(
            Mock.Of<ILLMProvider>(),
            Mock.Of<ILogger<MacroAnalyst>>());
        Assert.Equal("MacroAnalyst", agent.Name);
    }

    [Fact]
    public async Task MacroAnalyst_ValidResponse_ReturnsResult()
    {
        var provider = CreateMockProvider(ValidAgentResponse("MacroAnalyst", Sentiment.Bearish, 0.55m));
        var agent = new MacroAnalyst(provider.Object, Mock.Of<ILogger<MacroAnalyst>>());

        var result = await agent.AnalyzeAsync(CreateTestEvidence());

        Assert.Equal("MacroAnalyst", result.AgentName);
        Assert.Equal(Sentiment.Bearish, result.Sentiment);
        Assert.Equal(0.55m, result.Confidence);
    }

    [Fact]
    public async Task MacroAnalyst_SystemPrompt_ContainsIranianContext()
    {
        var agent = new MacroAnalyst(
            Mock.Of<ILLMProvider>(),
            Mock.Of<ILogger<MacroAnalyst>>());

        var method = typeof(SpecialistAgentBase).GetMethod("GetSystemPrompt",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var prompt = (string)method!.Invoke(agent, [])!;

        Assert.Contains("Iranian", prompt);
        Assert.Contains("IRR", prompt);
        Assert.Contains("inflation", prompt, StringComparison.OrdinalIgnoreCase);
    }

    // --- NewsAnalyst Tests ---

    [Fact]
    public void NewsAnalyst_HasCorrectName()
    {
        var agent = new NewsAnalyst(
            Mock.Of<ILLMProvider>(),
            Mock.Of<ILogger<NewsAnalyst>>());
        Assert.Equal("NewsAnalyst", agent.Name);
    }

    [Fact]
    public async Task NewsAnalyst_ValidResponse_ReturnsResult()
    {
        var provider = CreateMockProvider(ValidAgentResponse("NewsAnalyst", Sentiment.Neutral, 0.6m));
        var agent = new NewsAnalyst(provider.Object, Mock.Of<ILogger<NewsAnalyst>>());

        var result = await agent.AnalyzeAsync(CreateTestEvidence());

        Assert.Equal("NewsAnalyst", result.AgentName);
        Assert.Equal(Sentiment.Neutral, result.Sentiment);
    }

    [Fact]
    public async Task NewsAnalyst_SystemPrompt_DistinguishesFactFromInterpretation()
    {
        var agent = new NewsAnalyst(
            Mock.Of<ILLMProvider>(),
            Mock.Of<ILogger<NewsAnalyst>>());

        var method = typeof(SpecialistAgentBase).GetMethod("GetSystemPrompt",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var prompt = (string)method!.Invoke(agent, [])!;

        Assert.Contains("Reported facts", prompt);
        Assert.Contains("interpretation", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Potential scenarios", prompt);
    }

    // --- PoliticalRiskAnalyst Tests ---

    [Fact]
    public void PoliticalRiskAnalyst_HasCorrectName()
    {
        var agent = new PoliticalRiskAnalyst(
            Mock.Of<ILLMProvider>(),
            Mock.Of<ILogger<PoliticalRiskAnalyst>>());
        Assert.Equal("PoliticalRiskAnalyst", agent.Name);
    }

    [Fact]
    public async Task PoliticalRiskAnalyst_ValidResponse_ReturnsResult()
    {
        var provider = CreateMockProvider(ValidAgentResponse("PoliticalRiskAnalyst", Sentiment.Bearish, 0.45m));
        var agent = new PoliticalRiskAnalyst(provider.Object, Mock.Of<ILogger<PoliticalRiskAnalyst>>());

        var result = await agent.AnalyzeAsync(CreateTestEvidence());

        Assert.Equal("PoliticalRiskAnalyst", result.AgentName);
        Assert.Equal(Sentiment.Bearish, result.Sentiment);
    }

    [Fact]
    public async Task PoliticalRiskAnalyst_SystemPrompt_ContainsSanctionsAndGeopolitical()
    {
        var agent = new PoliticalRiskAnalyst(
            Mock.Of<ILLMProvider>(),
            Mock.Of<ILogger<PoliticalRiskAnalyst>>());

        var method = typeof(SpecialistAgentBase).GetMethod("GetSystemPrompt",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var prompt = (string)method!.Invoke(agent, [])!;

        Assert.Contains("Sanctions", prompt);
        Assert.Contains("Geopolitical", prompt);
        Assert.Contains("probabilistic", prompt, StringComparison.OrdinalIgnoreCase);
    }

    // --- RiskAnalyst Tests ---

    [Fact]
    public void RiskAnalyst_HasCorrectName()
    {
        var agent = new RiskAnalyst(
            Mock.Of<ILLMProvider>(),
            Mock.Of<ILogger<RiskAnalyst>>());
        Assert.Equal("RiskAnalyst", agent.Name);
    }

    [Fact]
    public async Task RiskAnalyst_ValidResponse_ReturnsResult()
    {
        var provider = CreateMockProvider(ValidAgentResponse("RiskAnalyst", Sentiment.Bearish, 0.5m));
        var agent = new RiskAnalyst(provider.Object, Mock.Of<ILogger<RiskAnalyst>>());

        var result = await agent.AnalyzeAsync(CreateTestEvidence());

        Assert.Equal("RiskAnalyst", result.AgentName);
        Assert.Equal(Sentiment.Bearish, result.Sentiment);
    }

    [Fact]
    public async Task RiskAnalyst_SystemPrompt_SynthesizesAcrossDomains()
    {
        var agent = new RiskAnalyst(
            Mock.Of<ILLMProvider>(),
            Mock.Of<ILogger<RiskAnalyst>>());

        var method = typeof(SpecialistAgentBase).GetMethod("GetSystemPrompt",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var prompt = (string)method!.Invoke(agent, [])!;

        Assert.Contains("Technical risks", prompt);
        Assert.Contains("Fundamental risks", prompt);
        Assert.Contains("Macro risks", prompt);
        Assert.Contains("Political risks", prompt);
        Assert.Contains("Invalidation", prompt);
    }
}

// ============================================================
// Specialist Agent Failure Handling Tests
// ============================================================

public class SpecialistAgentFailureTests
{
    private static Asset CreateTestAsset() => new()
    {
        Symbol = "TEST",
        Name = "Test Asset",
        Market = "TSE",
        AssetType = AssetType.Stock
    };

    private static AnalysisEvidence CreateTestEvidence() => new()
    {
        Asset = CreateTestAsset(),
        AssembledAt = DateTimeOffset.UtcNow,
        CurrentPrice = 15000m
    };

    [Fact]
    public async Task Agent_LLMFailure_ReturnsFailureResult_NotException()
    {
        var mockProvider = new Mock<ILLMProvider>();
        mockProvider.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = "",
                Model = "test",
                Success = false,
                Error = "Connection refused"
            });

        var agent = new TechnicalAnalyst(mockProvider.Object, Mock.Of<ILogger<TechnicalAnalyst>>());
        var result = await agent.AnalyzeAsync(CreateTestEvidence());

        // Should NOT throw — failure is represented in the result
        Assert.Equal("TechnicalAnalyst", result.AgentName);
        Assert.Equal(Sentiment.Unknown, result.Sentiment);
        Assert.Equal(0m, result.Confidence);
        Assert.Contains("Connection refused", result.Summary);
        Assert.Contains(result.InformationGaps, g => g.Contains("Connection refused"));
    }

    [Fact]
    public async Task Agent_InvalidJson_ReturnsFailureResult()
    {
        var mockProvider = new Mock<ILLMProvider>();
        mockProvider.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = "not valid json {{{",
                Model = "test",
                Success = true
            });

        var agent = new TechnicalAnalyst(mockProvider.Object, Mock.Of<ILogger<TechnicalAnalyst>>());
        var result = await agent.AnalyzeAsync(CreateTestEvidence());

        Assert.Equal(Sentiment.Unknown, result.Sentiment);
        Assert.Equal(0m, result.Confidence);
        Assert.Contains("failed", result.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Agent_EmptyContent_ReturnsFailureResult()
    {
        var mockProvider = new Mock<ILLMProvider>();
        mockProvider.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = "",
                Model = "test",
                Success = true
            });

        var agent = new FundamentalAnalyst(mockProvider.Object, Mock.Of<ILogger<FundamentalAnalyst>>());
        var result = await agent.AnalyzeAsync(CreateTestEvidence());

        Assert.Equal(Sentiment.Unknown, result.Sentiment);
        Assert.Equal(0m, result.Confidence);
    }

    [Fact]
    public async Task Agent_Cancellation_ReturnsFailureResult()
    {
        var mockProvider = new Mock<ILLMProvider>();
        mockProvider.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var agent = new MacroAnalyst(mockProvider.Object, Mock.Of<ILogger<MacroAnalyst>>());
        var result = await agent.AnalyzeAsync(CreateTestEvidence());

        Assert.Equal(Sentiment.Unknown, result.Sentiment);
        Assert.Contains("cancelled", result.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Agent_MissingRequiredFields_ReturnsFailureResult()
    {
        // Response missing 'sentiment' and 'summary' fields
        var mockProvider = new Mock<ILLMProvider>();
        mockProvider.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = @"{""agentName"": ""Test"", ""confidence"": 0.5}",
                Model = "test",
                Success = true
            });

        var agent = new NewsAnalyst(mockProvider.Object, Mock.Of<ILogger<NewsAnalyst>>());
        var result = await agent.AnalyzeAsync(CreateTestEvidence());

        // Should handle gracefully — missing fields default or result is failure
        Assert.NotNull(result);
        // agentName from JSON ("Test") takes precedence over expected name
        Assert.Equal("Test", result.AgentName);
    }

    [Fact]
    public async Task Agent_InvalidConfidence_ClampedToValidRange()
    {
        var mockProvider = new Mock<ILLMProvider>();
        mockProvider.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = @"{
                    ""agentName"": ""RiskAnalyst"",
                    ""sentiment"": ""Neutral"",
                    ""confidence"": 2.5,
                    ""summary"": ""Test""
                }",
                Model = "test",
                Success = true
            });

        var agent = new RiskAnalyst(mockProvider.Object, Mock.Of<ILogger<RiskAnalyst>>());
        var result = await agent.AnalyzeAsync(CreateTestEvidence());

        Assert.Equal(1.0m, result.Confidence); // Clamped
    }

    [Fact]
    public async Task Agent_NegativeConfidence_ClampedToZero()
    {
        var mockProvider = new Mock<ILLMProvider>();
        mockProvider.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = @"{
                    ""agentName"": ""PoliticalRiskAnalyst"",
                    ""sentiment"": ""Bearish"",
                    ""confidence"": -0.3,
                    ""summary"": ""Test""
                }",
                Model = "test",
                Success = true
            });

        var agent = new PoliticalRiskAnalyst(mockProvider.Object, Mock.Of<ILogger<PoliticalRiskAnalyst>>());
        var result = await agent.AnalyzeAsync(CreateTestEvidence());

        Assert.Equal(0.0m, result.Confidence); // Clamped to 0
    }
}

// ============================================================
// Specialist Agent Evidence Traceability Tests
// ============================================================

public class SpecialistAgentTraceabilityTests
{
    private static Asset CreateTestAsset() => new()
    {
        Symbol = "TEST",
        Name = "Test Asset",
        Market = "TSE",
        AssetType = AssetType.Stock
    };

    [Fact]
    public async Task Agent_PreservesSupportingEvidence()
    {
        var responseJson = @"{
            ""agentName"": ""TechnicalAnalyst"",
            ""sentiment"": ""Bullish"",
            ""confidence"": 0.75,
            ""summary"": ""Strong technical setup"",
            ""supportingEvidence"": [
                {""content"": ""RSI above 50"", ""type"": ""Calculation"", ""source"": ""Indicator Engine"", ""confidence"": 0.9},
                {""content"": ""Volume increasing"", ""type"": ""Fact"", ""source"": ""Market Data"", ""confidence"": 0.8}
            ],
            ""contradictingEvidence"": [
                {""content"": ""Resistance nearby"", ""type"": ""Interpretation"", ""source"": ""Technical Analyst"", ""confidence"": 0.6}
            ],
            ""identifiedRisks"": [""Volume could decline"", ""Resistance at 15500""],
            ""informationGaps"": [""Sector comparison unavailable""]
        }";

        var mockProvider = new Mock<ILLMProvider>();
        mockProvider.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse { Content = responseJson, Model = "test", Success = true });

        var agent = new TechnicalAnalyst(mockProvider.Object, Mock.Of<ILogger<TechnicalAnalyst>>());
        var result = await agent.AnalyzeAsync(new AnalysisEvidence
        {
            Asset = CreateTestAsset(),
            AssembledAt = DateTimeOffset.UtcNow
        });

        Assert.Equal(2, result.SupportingEvidence.Count);
        Assert.Equal("RSI above 50", result.SupportingEvidence[0].Content);
        Assert.Equal(EvidenceType.Calculation, result.SupportingEvidence[0].Type);
        Assert.Equal("Indicator Engine", result.SupportingEvidence[0].Source);
        Assert.Single(result.ContradictingEvidence);
        Assert.Equal(2, result.IdentifiedRisks.Count);
        Assert.Single(result.InformationGaps);
    }

    [Fact]
    public async Task Agent_PreservesAgentSpecificData()
    {
        var responseJson = @"{
            ""agentName"": ""FundamentalAnalyst"",
            ""sentiment"": ""Neutral"",
            ""confidence"": 0.5,
            ""summary"": ""Limited data"",
            ""agentSpecificData"": {
                ""valuationAssessment"": ""P/E within range"",
                ""profitabilityAssessment"": ""Stable margins"",
                ""missingDataSummary"": ""Peer comparison unavailable""
            }
        }";

        var mockProvider = new Mock<ILLMProvider>();
        mockProvider.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse { Content = responseJson, Model = "test", Success = true });

        var agent = new FundamentalAnalyst(mockProvider.Object, Mock.Of<ILogger<FundamentalAnalyst>>());
        var result = await agent.AnalyzeAsync(new AnalysisEvidence
        {
            Asset = CreateTestAsset(),
            AssembledAt = DateTimeOffset.UtcNow
        });

        Assert.NotNull(result.AgentSpecificData);
        Assert.Equal("P/E within range", result.AgentSpecificData!["valuationAssessment"]);
        Assert.Equal("Stable margins", result.AgentSpecificData["profitabilityAssessment"]);
        Assert.Equal("Peer comparison unavailable", result.AgentSpecificData["missingDataSummary"]);
    }

    [Fact]
    public async Task Agent_SetsTimestampAndAssetSymbol()
    {
        var mockProvider = new Mock<ILLMProvider>();
        mockProvider.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = @"{""agentName"":""Test"",""sentiment"":""Neutral"",""confidence"":0.5,""summary"":""Test""}",
                Model = "test",
                Success = true
            });

        var agent = new RiskAnalyst(mockProvider.Object, Mock.Of<ILogger<RiskAnalyst>>());
        var result = await agent.AnalyzeAsync(new AnalysisEvidence
        {
            Asset = CreateTestAsset(),
            AssembledAt = DateTimeOffset.UtcNow
        });

        Assert.Equal("TEST", result.AssetSymbol);
        Assert.True(result.GeneratedAt > DateTimeOffset.UtcNow.AddMinutes(-1));
    }
}

// ============================================================
// Specialist Agent Evidence Scoping Tests (via Prompt Capture)
// ============================================================

public class SpecialistAgentScopingTests
{
    private static Asset CreateTestAsset() => new()
    {
        Symbol = "TEST",
        Name = "Test Asset",
        Market = "TSE",
        AssetType = AssetType.Stock
    };

    private static AnalysisEvidence CreateFullEvidence() => new()
    {
        Asset = CreateTestAsset(),
        AssembledAt = DateTimeOffset.UtcNow,
        CurrentPrice = 15000m,
        IndicatorValues = new Dictionary<string, IndicatorResult>
        {
            ["RSI"] = new() { IndicatorName = "RSI", Date = DateOnly.FromDateTime(DateTime.Today), Value = 55m, Period = 14 }
        },
        CompanyInfo = new CompanyInfo { Symbol = "TEST", CompanyName = "Test Corp" },
        EconomicIndicators = [new() { Name = "Inflation", Value = 40m, Unit = "%" }],
        RecentNews = [new() { Title = "Test News", Source = "IRNA", PublishedAt = DateTimeOffset.UtcNow }],
        CurrencyRates = [new() { BaseCurrency = "USD", QuoteCurrency = "IRR", Rate = 580000m, Timestamp = DateTimeOffset.UtcNow }]
    };

    [Fact]
    public async Task TechnicalAnalyst_ReceivesOnlyTechnicalEvidence()
    {
        LlmRequest? capturedRequest = null;
        var mockProvider = new Mock<ILLMProvider>();
        mockProvider.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LlmRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new LlmResponse
            {
                Content = @"{""agentName"":""TechnicalAnalyst"",""sentiment"":""Neutral"",""confidence"":0.5,""summary"":""Test""}",
                Model = "test",
                Success = true
            });

        var agent = new TechnicalAnalyst(mockProvider.Object, Mock.Of<ILogger<TechnicalAnalyst>>());
        await agent.AnalyzeAsync(CreateFullEvidence());

        Assert.NotNull(capturedRequest);
        Assert.Contains("RSI", capturedRequest!.UserPrompt);
        // Should NOT contain fundamental data
        Assert.DoesNotContain("Test Corp", capturedRequest.UserPrompt);
        Assert.DoesNotContain("Inflation", capturedRequest.UserPrompt);
        Assert.DoesNotContain("IRNA", capturedRequest.UserPrompt);
    }

    [Fact]
    public async Task FundamentalAnalyst_ReceivesOnlyFundamentalEvidence()
    {
        LlmRequest? capturedRequest = null;
        var mockProvider = new Mock<ILLMProvider>();
        mockProvider.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LlmRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new LlmResponse
            {
                Content = @"{""agentName"":""FundamentalAnalyst"",""sentiment"":""Neutral"",""confidence"":0.5,""summary"":""Test""}",
                Model = "test",
                Success = true
            });

        var agent = new FundamentalAnalyst(mockProvider.Object, Mock.Of<ILogger<FundamentalAnalyst>>());
        await agent.AnalyzeAsync(CreateFullEvidence());

        Assert.NotNull(capturedRequest);
        Assert.Contains("Test Corp", capturedRequest!.UserPrompt);
        // Should NOT contain indicator values
        Assert.DoesNotContain("RSI", capturedRequest.UserPrompt);
        Assert.DoesNotContain("Inflation", capturedRequest.UserPrompt);
        Assert.DoesNotContain("IRNA", capturedRequest.UserPrompt);
    }

    [Fact]
    public async Task MacroAnalyst_ReceivesMacroEvidence()
    {
        LlmRequest? capturedRequest = null;
        var mockProvider = new Mock<ILLMProvider>();
        mockProvider.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LlmRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new LlmResponse
            {
                Content = @"{""agentName"":""MacroAnalyst"",""sentiment"":""Neutral"",""confidence"":0.5,""summary"":""Test""}",
                Model = "test",
                Success = true
            });

        var agent = new MacroAnalyst(mockProvider.Object, Mock.Of<ILogger<MacroAnalyst>>());
        await agent.AnalyzeAsync(CreateFullEvidence());

        Assert.NotNull(capturedRequest);
        Assert.Contains("Inflation", capturedRequest!.UserPrompt);
        Assert.Contains("USD/IRR", capturedRequest.UserPrompt);
        // Should NOT contain fundamentals or news
        Assert.DoesNotContain("Test Corp", capturedRequest.UserPrompt);
        Assert.DoesNotContain("Test News", capturedRequest.UserPrompt);
    }

    [Fact]
    public async Task NewsAnalyst_ReceivesNewsEvidence()
    {
        LlmRequest? capturedRequest = null;
        var mockProvider = new Mock<ILLMProvider>();
        mockProvider.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LlmRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new LlmResponse
            {
                Content = @"{""agentName"":""NewsAnalyst"",""sentiment"":""Neutral"",""confidence"":0.5,""summary"":""Test""}",
                Model = "test",
                Success = true
            });

        var agent = new NewsAnalyst(mockProvider.Object, Mock.Of<ILogger<NewsAnalyst>>());
        await agent.AnalyzeAsync(CreateFullEvidence());

        Assert.NotNull(capturedRequest);
        Assert.Contains("Test News", capturedRequest!.UserPrompt);
        Assert.Contains("IRNA", capturedRequest.UserPrompt);
        // Should NOT contain indicators or fundamentals
        Assert.DoesNotContain("RSI", capturedRequest.UserPrompt);
        Assert.DoesNotContain("Test Corp", capturedRequest.UserPrompt);
    }

    [Fact]
    public async Task RiskAnalyst_ReceivesAllEvidence()
    {
        LlmRequest? capturedRequest = null;
        var mockProvider = new Mock<ILLMProvider>();
        mockProvider.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LlmRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new LlmResponse
            {
                Content = @"{""agentName"":""RiskAnalyst"",""sentiment"":""Neutral"",""confidence"":0.5,""summary"":""Test""}",
                Model = "test",
                Success = true
            });

        var agent = new RiskAnalyst(mockProvider.Object, Mock.Of<ILogger<RiskAnalyst>>());
        await agent.AnalyzeAsync(CreateFullEvidence());

        Assert.NotNull(capturedRequest);
        // Risk analyst receives all evidence
        Assert.Contains("RSI", capturedRequest!.UserPrompt);
        Assert.Contains("Test Corp", capturedRequest.UserPrompt);
        Assert.Contains("Inflation", capturedRequest.UserPrompt);
        Assert.Contains("Test News", capturedRequest.UserPrompt);
        Assert.Contains("USD/IRR", capturedRequest.UserPrompt);
    }
}

// ============================================================
// Integration: All Agents → StrategyAgent → StrategyContext
// ============================================================

public class SpecialistAgentIntegrationTests
{
    private static Asset CreateTestAsset() => new()
    {
        Symbol = "TEST",
        Name = "Test Asset",
        Market = "TSE",
        AssetType = AssetType.Stock
    };

    private static AnalysisEvidence CreateTestEvidence() => new()
    {
        Asset = CreateTestAsset(),
        AssembledAt = DateTimeOffset.UtcNow,
        CurrentPrice = 15000m,
        IndicatorValues = new Dictionary<string, IndicatorResult>
        {
            ["RSI"] = new() { IndicatorName = "RSI", Date = DateOnly.FromDateTime(DateTime.Today), Value = 55m, Period = 14 }
        }
    };

    private static Mock<ILLMProvider> CreateMockProvider(string responseJson)
    {
        var mock = new Mock<ILLMProvider>();
        mock.Setup(p => p.Name).Returns("TestProvider");
        mock.Setup(p => p.Model).Returns("test-model");
        mock.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = responseJson,
                Model = "test-model",
                Success = true,
                PromptTokens = 100,
                CompletionTokens = 200
            });
        return mock;
    }

    private static string ValidResponse(string agentName) =>
        $@"{{
            ""agentName"": ""{agentName}"",
            ""sentiment"": ""Bullish"",
            ""confidence"": 0.65,
            ""summary"": ""{agentName} analysis complete"",
            ""supportingEvidence"": [{{""content"": ""Evidence from {agentName}"", ""type"": ""Interpretation"", ""source"": ""{agentName}""}}],
            ""identifiedRisks"": [""Risk from {agentName}""]
        }}";

    [Fact]
    public async Task AllAgents_CanRunInParallel()
    {
        var provider = CreateMockProvider(ValidResponse("TestAgent"));

        var agents = new List<IAgent>
        {
            new TechnicalAnalyst(provider.Object, Mock.Of<ILogger<TechnicalAnalyst>>()),
            new FundamentalAnalyst(provider.Object, Mock.Of<ILogger<FundamentalAnalyst>>()),
            new MacroAnalyst(provider.Object, Mock.Of<ILogger<MacroAnalyst>>()),
            new NewsAnalyst(provider.Object, Mock.Of<ILogger<NewsAnalyst>>()),
            new PoliticalRiskAnalyst(provider.Object, Mock.Of<ILogger<PoliticalRiskAnalyst>>()),
            new RiskAnalyst(provider.Object, Mock.Of<ILogger<RiskAnalyst>>())
        };

        var evidence = CreateTestEvidence();

        // Run all agents in parallel (simulating orchestrator behavior)
        var tasks = agents.Select(async agent =>
        {
            return await agent.AnalyzeAsync(evidence);
        });

        var results = await Task.WhenAll(tasks);

        Assert.Equal(6, results.Length);
        Assert.All(results, r => Assert.Equal(Sentiment.Bullish, r.Sentiment));
        Assert.All(results, r => Assert.True(r.LlmDuration.HasValue));
    }

    [Fact]
    public async Task OneAgentFailure_DoesNotAffectOthers()
    {
        var goodProvider = CreateMockProvider(ValidResponse("TestAgent"));

        var failProvider = new Mock<ILLMProvider>();
        failProvider.Setup(p => p.Name).Returns("FailProvider");
        failProvider.Setup(p => p.Model).Returns("fail-model");
        failProvider.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse { Content = "", Model = "fail", Success = false, Error = "LLM down" });

        var agents = new List<IAgent>
        {
            new TechnicalAnalyst(goodProvider.Object, Mock.Of<ILogger<TechnicalAnalyst>>()),
            new FundamentalAnalyst(failProvider.Object, Mock.Of<ILogger<FundamentalAnalyst>>()), // Will fail
            new MacroAnalyst(goodProvider.Object, Mock.Of<ILogger<MacroAnalyst>>())
        };

        var evidence = CreateTestEvidence();
        var results = new List<AgentAnalysisResult>();

        // Simulate orchestrator: one failure doesn't stop others
        foreach (var agent in agents)
        {
            try
            {
                results.Add(await agent.AnalyzeAsync(evidence));
            }
            catch
            {
                // Should not happen — agents handle failures internally
            }
        }

        Assert.Equal(3, results.Count);
        // Technical and Macro should succeed
        Assert.Equal(Sentiment.Bullish, results[0].Sentiment);
        Assert.Equal(Sentiment.Bullish, results[2].Sentiment);
        // Fundamental should have Unknown sentiment from failure
        Assert.Equal(Sentiment.Unknown, results[1].Sentiment);
        Assert.Contains("LLM down", results[1].Summary);
    }

    [Fact]
    public async Task AgentResults_FeedIntoStrategyAgent()
    {
        var provider = CreateMockProvider(ValidResponse("TestAgent"));
        var evidence = CreateTestEvidence();

        // Run all agents
        var agents = new List<IAgent>
        {
            new TechnicalAnalyst(provider.Object, Mock.Of<ILogger<TechnicalAnalyst>>()),
            new FundamentalAnalyst(provider.Object, Mock.Of<ILogger<FundamentalAnalyst>>()),
            new MacroAnalyst(provider.Object, Mock.Of<ILogger<MacroAnalyst>>()),
            new NewsAnalyst(provider.Object, Mock.Of<ILogger<NewsAnalyst>>()),
            new PoliticalRiskAnalyst(provider.Object, Mock.Of<ILogger<PoliticalRiskAnalyst>>()),
            new RiskAnalyst(provider.Object, Mock.Of<ILogger<RiskAnalyst>>())
        };

        var agentResults = new List<AgentAnalysisResult>();
        foreach (var agent in agents)
            agentResults.Add(await agent.AnalyzeAsync(evidence));

        // Feed into StrategyAgent via context builder
        var contextBuilder = new StrategyContextBuilder();
        var context = contextBuilder.Build(evidence, agentResults);

        Assert.Equal(6, context.AgentResults.Count);
        // All agents use the same mock which returns "TestAgent" as agentName in JSON
        Assert.All(context.AgentResults, a => Assert.Equal("TestAgent", a.AgentName));
    }
}

// ============================================================
// Architecture Boundary Tests
// ============================================================

public class SpecialistAgentArchitectureTests
{
    [Fact]
    public void SpecialistAgents_DoNotReferenceInfrastructure()
    {
        var aiAssembly = typeof(SpecialistAgentBase).Assembly;
        var refs = aiAssembly.GetReferencedAssemblies().Select(a => a.Name);
        Assert.DoesNotContain("StrategyForge.Infrastructure", refs);
    }

    [Fact]
    public void SpecialistAgents_DoNotReferenceApi()
    {
        var aiAssembly = typeof(SpecialistAgentBase).Assembly;
        var refs = aiAssembly.GetReferencedAssemblies().Select(a => a.Name);
        Assert.DoesNotContain("StrategyForge.Api", refs);
    }

    [Fact]
    public void Domain_DoesNotReferenceAI()
    {
        var domainAssembly = typeof(IAgent).Assembly;
        var refs = domainAssembly.GetReferencedAssemblies().Select(a => a.Name);
        Assert.DoesNotContain("StrategyForge.AI", refs);
    }

    [Fact]
    public void SpecialistAgents_ImplementIAgent()
    {
        Assert.IsAssignableFrom<IAgent>(new TechnicalAnalyst(Mock.Of<ILLMProvider>(), Mock.Of<ILogger<TechnicalAnalyst>>()));
        Assert.IsAssignableFrom<IAgent>(new FundamentalAnalyst(Mock.Of<ILLMProvider>(), Mock.Of<ILogger<FundamentalAnalyst>>()));
        Assert.IsAssignableFrom<IAgent>(new MacroAnalyst(Mock.Of<ILLMProvider>(), Mock.Of<ILogger<MacroAnalyst>>()));
        Assert.IsAssignableFrom<IAgent>(new NewsAnalyst(Mock.Of<ILLMProvider>(), Mock.Of<ILogger<NewsAnalyst>>()));
        Assert.IsAssignableFrom<IAgent>(new PoliticalRiskAnalyst(Mock.Of<ILLMProvider>(), Mock.Of<ILogger<PoliticalRiskAnalyst>>()));
        Assert.IsAssignableFrom<IAgent>(new RiskAnalyst(Mock.Of<ILLMProvider>(), Mock.Of<ILogger<RiskAnalyst>>()));
    }

    [Fact]
    public void SpecialistAgents_ExtendSpecialistAgentBase()
    {
        Assert.IsAssignableFrom<SpecialistAgentBase>(new TechnicalAnalyst(Mock.Of<ILLMProvider>(), Mock.Of<ILogger<TechnicalAnalyst>>()));
        Assert.IsAssignableFrom<SpecialistAgentBase>(new FundamentalAnalyst(Mock.Of<ILLMProvider>(), Mock.Of<ILogger<FundamentalAnalyst>>()));
        Assert.IsAssignableFrom<SpecialistAgentBase>(new MacroAnalyst(Mock.Of<ILLMProvider>(), Mock.Of<ILogger<MacroAnalyst>>()));
        Assert.IsAssignableFrom<SpecialistAgentBase>(new NewsAnalyst(Mock.Of<ILLMProvider>(), Mock.Of<ILogger<NewsAnalyst>>()));
        Assert.IsAssignableFrom<SpecialistAgentBase>(new PoliticalRiskAnalyst(Mock.Of<ILLMProvider>(), Mock.Of<ILogger<PoliticalRiskAnalyst>>()));
        Assert.IsAssignableFrom<SpecialistAgentBase>(new RiskAnalyst(Mock.Of<ILLMProvider>(), Mock.Of<ILogger<RiskAnalyst>>()));
    }

    [Fact]
    public void SpecialistAgents_AllHaveDistinctNames()
    {
        var names = new IAgent[]
        {
            new TechnicalAnalyst(Mock.Of<ILLMProvider>(), Mock.Of<ILogger<TechnicalAnalyst>>()),
            new FundamentalAnalyst(Mock.Of<ILLMProvider>(), Mock.Of<ILogger<FundamentalAnalyst>>()),
            new MacroAnalyst(Mock.Of<ILLMProvider>(), Mock.Of<ILogger<MacroAnalyst>>()),
            new NewsAnalyst(Mock.Of<ILLMProvider>(), Mock.Of<ILogger<NewsAnalyst>>()),
            new PoliticalRiskAnalyst(Mock.Of<ILLMProvider>(), Mock.Of<ILogger<PoliticalRiskAnalyst>>()),
            new RiskAnalyst(Mock.Of<ILLMProvider>(), Mock.Of<ILogger<RiskAnalyst>>())
        }.Select(a => a.Name).ToList();

        Assert.Equal(6, names.Distinct().Count());
    }
}

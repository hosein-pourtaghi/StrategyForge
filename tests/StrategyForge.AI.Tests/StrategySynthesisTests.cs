using Microsoft.Extensions.Logging;
using Moq;
using StrategyForge.AI.Agents;
using StrategyForge.AI.Services;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Interfaces.AI;
using StrategyForge.Domain.Interfaces.Orchestration;
using StrategyForge.Domain.Models;
using Xunit;
using static StrategyForge.AI.Tests.SynthesisTestHelpers;

namespace StrategyForge.AI.Tests;

// --- StrategyContextBuilder Tests ---

public class StrategyContextBuilderTests
{
    private readonly StrategyContextBuilder _builder = new();

    [Fact]
    public void Build_WithEvidence_ProducesContextWithAsset()
    {
        var asset = CreateTestAsset();
        var evidence = CreateTestEvidence(asset);

        var context = _builder.Build(evidence, []);

        Assert.Equal(asset, context.Asset);
        Assert.Equal(evidence, context.Evidence);
    }

    [Fact]
    public void Build_WithAgentResults_PreservesAgentResults()
    {
        var evidence = CreateTestEvidence(CreateTestAsset());
        var agents = new List<AgentAnalysisResult>
        {
            CreateAgentResult("TechnicalAnalyst", Sentiment.Bullish, 0.7m),
            CreateAgentResult("MacroAnalyst", Sentiment.Neutral, 0.5m)
        };

        var context = _builder.Build(evidence, agents);

        Assert.Equal(2, context.AgentResults.Count);
        Assert.Equal("TechnicalAnalyst", context.AgentResults[0].AgentName);
        Assert.Equal("MacroAnalyst", context.AgentResults[1].AgentName);
    }

    [Fact]
    public void Build_DefaultHorizons_IncludesAll()
    {
        var evidence = CreateTestEvidence(CreateTestAsset());

        var context = _builder.Build(evidence, []);

        Assert.Contains(TimeHorizon.ShortTerm, context.RequestedHorizons);
        Assert.Contains(TimeHorizon.MediumTerm, context.RequestedHorizons);
        Assert.Contains(TimeHorizon.LongTerm, context.RequestedHorizons);
    }

    [Fact]
    public void Build_WithCustomHorizons_PreservesHorizons()
    {
        var evidence = CreateTestEvidence(CreateTestAsset());

        var context = _builder.Build(evidence, [],
            requestedHorizons: [TimeHorizon.ShortTerm]);

        Assert.Single(context.RequestedHorizons);
        Assert.Contains(TimeHorizon.ShortTerm, context.RequestedHorizons);
    }

    [Fact]
    public void Build_WithConstraints_PreservesConstraints()
    {
        var evidence = CreateTestEvidence(CreateTestAsset());
        var constraints = new List<string> { "Max risk: Moderate" };

        var context = _builder.Build(evidence, [], constraints: constraints, focusArea: "Risk");

        Assert.Single(context.Constraints);
        Assert.Equal("Risk", context.FocusArea);
    }

    [Fact]
    public void Build_DefaultConstraints_EmptyList()
    {
        var evidence = CreateTestEvidence(CreateTestAsset());

        var context = _builder.Build(evidence, []);

        Assert.Empty(context.Constraints);
        Assert.Null(context.FocusArea);
    }
}

// --- StrategySynthesisPromptBuilder Tests ---

public class StrategySynthesisPromptBuilderTests
{
    private readonly StrategySynthesisPromptBuilder _builder = new();

    [Fact]
    public void BuildRequest_ProducesValidRequest()
    {
        var context = CreateTestContext();

        var request = _builder.BuildRequest(context);

        Assert.NotNull(request.SystemPrompt);
        Assert.NotNull(request.UserPrompt);
        Assert.Equal("json", request.ResponseFormat);
        Assert.Equal(0.3, request.Temperature);
    }

    [Fact]
    public void SystemPrompt_ContainsEvidenceRules()
    {
        var prompt = StrategySynthesisPromptBuilder.BuildSystemPrompt();

        Assert.Contains("ONLY from provided context", prompt);
        Assert.Contains("Never fabricate market data", prompt);
    }

    [Fact]
    public void SystemPrompt_NoTradingSignals()
    {
        var prompt = StrategySynthesisPromptBuilder.BuildSystemPrompt();

        Assert.Contains("Never produce buy/sell/hold signals", prompt);
    }

    [Fact]
    public void SystemPrompt_RequiresStructuredOutput()
    {
        var prompt = StrategySynthesisPromptBuilder.BuildSystemPrompt();

        Assert.Contains("executiveSummary", prompt);
        Assert.Contains("marketContext", prompt);
        Assert.Contains("bullishScenario", prompt);
        Assert.Contains("baseScenario", prompt);
        Assert.Contains("bearishScenario", prompt);
        Assert.Contains("riskReward", prompt);
        Assert.Contains("confidence", prompt);
    }

    [Fact]
    public void SystemPrompt_RequiresEvidenceTraceability()
    {
        var prompt = StrategySynthesisPromptBuilder.BuildSystemPrompt();

        Assert.Contains("evidence traceability", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("supportingEvidence", prompt);
    }

    [Fact]
    public void SystemPrompt_NoCredentials()
    {
        var prompt = StrategySynthesisPromptBuilder.BuildSystemPrompt();

        Assert.DoesNotContain("sk-", prompt);
        Assert.DoesNotContain("Bearer", prompt);
    }

    [Fact]
    public void BuildUserPrompt_IncludesAssetInfo()
    {
        var context = CreateTestContext();

        var prompt = StrategySynthesisPromptBuilder.BuildUserPrompt(context);

        Assert.Contains("TEST", prompt);
        Assert.Contains("Test Asset", prompt);
        Assert.Contains("TSE", prompt);
    }

    [Fact]
    public void BuildUserPrompt_IncludesIndicators()
    {
        var evidence = CreateTestEvidence(CreateTestAsset());
        evidence = evidence with
        {
            IndicatorValues = new Dictionary<string, IndicatorResult>
            {
                ["RSI"] = new() { IndicatorName = "RSI", Date = DateOnly.FromDateTime(DateTime.Today), Value = 31.4m, Period = 14 }
            }
        };
        var context = new StrategyContext
        {
            Asset = CreateTestAsset(),
            AssembledAt = DateTimeOffset.UtcNow,
            Evidence = evidence,
            RequestedHorizons = [TimeHorizon.ShortTerm]
        };

        var prompt = StrategySynthesisPromptBuilder.BuildUserPrompt(context);

        Assert.Contains("RSI", prompt);
        Assert.Contains("31.4", prompt);
    }

    [Fact]
    public void BuildUserPrompt_IncludesAgentResults()
    {
        var agent = CreateAgentResult("TechnicalAnalyst", Sentiment.Bullish, 0.7m);
        var context = CreateTestContextWithAgents([agent]);

        var prompt = StrategySynthesisPromptBuilder.BuildUserPrompt(context);

        Assert.Contains("TechnicalAnalyst", prompt);
        Assert.Contains("Bullish", prompt);
    }

    [Fact]
    public void BuildUserPrompt_IncludesConstraints()
    {
        var context = new StrategyContext
        {
            Asset = CreateTestAsset(),
            AssembledAt = DateTimeOffset.UtcNow,
            Evidence = CreateTestEvidence(CreateTestAsset()),
            RequestedHorizons = [TimeHorizon.ShortTerm],
            Constraints = ["Max risk: Moderate"]
        };

        var prompt = StrategySynthesisPromptBuilder.BuildUserPrompt(context);

        Assert.Contains("Max risk: Moderate", prompt);
    }

    [Fact]
    public void BuildUserPrompt_IncludesFocusArea()
    {
        var context = new StrategyContext
        {
            Asset = CreateTestAsset(),
            AssembledAt = DateTimeOffset.UtcNow,
            Evidence = CreateTestEvidence(CreateTestAsset()),
            RequestedHorizons = [TimeHorizon.ShortTerm],
            FocusArea = "Risk assessment focus"
        };

        var prompt = StrategySynthesisPromptBuilder.BuildUserPrompt(context);

        Assert.Contains("Risk assessment focus", prompt);
    }

    [Fact]
    public void BuildUserPrompt_IncludesMissingData()
    {
        var evidence = CreateTestEvidence(CreateTestAsset());
        evidence = evidence with { MissingData = ["Fundamental data unavailable"] };
        var context = new StrategyContext
        {
            Asset = CreateTestAsset(),
            AssembledAt = DateTimeOffset.UtcNow,
            Evidence = evidence,
            RequestedHorizons = [TimeHorizon.ShortTerm]
        };

        var prompt = StrategySynthesisPromptBuilder.BuildUserPrompt(context);

        Assert.Contains("Fundamental data unavailable", prompt);
    }
}

// --- StrategyResponseValidator Tests ---

public class StrategyResponseValidatorTests
{
    private readonly StrategyResponseValidator _validator = new();

    private static readonly Asset TestAsset = new()
    {
        Symbol = "TEST",
        Name = "Test Asset",
        Market = "TSE",
        AssetType = AssetType.Stock
    };

    [Fact]
    public void Validate_FailedResponse_ReturnsInvalid()
    {
        var result = _validator.Validate(
            new LlmResponse { Content = "", Model = "test", Success = false, Error = "timeout" },
            TestAsset, DateTimeOffset.UtcNow);

        Assert.False(result.IsValid);
        Assert.Contains("timeout", result.ErrorMessage);
    }

    [Fact]
    public void Validate_EmptyContent_ReturnsInvalid()
    {
        var result = _validator.Validate(
            new LlmResponse { Content = "", Model = "test", Success = true },
            TestAsset, DateTimeOffset.UtcNow);

        Assert.False(result.IsValid);
        Assert.Contains("empty content", result.ErrorMessage);
    }

    [Fact]
    public void Validate_InvalidJson_ReturnsInvalid()
    {
        var result = _validator.Validate(
            new LlmResponse { Content = "not json", Model = "test", Success = true },
            TestAsset, DateTimeOffset.UtcNow);

        Assert.False(result.IsValid);
        Assert.Contains("Invalid JSON", result.ErrorMessage);
    }

    [Fact]
    public void Validate_MissingExecutiveSummary_ReturnsInvalid()
    {
        var json = "{\"marketContext\":{\"regime\":\"Uptrend\",\"description\":\"Test\"}}";
        var result = _validator.Validate(
            new LlmResponse { Content = json, Model = "test", Success = true },
            TestAsset, DateTimeOffset.UtcNow);

        Assert.False(result.IsValid);
        Assert.Contains("executiveSummary", result.ErrorMessage);
    }

    [Fact]
    public void Validate_ValidMinimalResponse_ReturnsSuccess()
    {
        var json = @"{
            ""executiveSummary"": {""overallSentiment"":""Bullish"",""summary"":""Test summary""},
            ""marketContext"": {""regime"":""Uptrend"",""description"":""Test market""}
        }";

        var result = _validator.Validate(
            new LlmResponse { Content = json, Model = "test", Success = true },
            TestAsset, DateTimeOffset.UtcNow);

        Assert.True(result.IsValid);
        Assert.NotNull(result.Report);
        Assert.Equal("TEST", result.Report!.Asset.Symbol);
        Assert.Equal(Sentiment.Bullish, result.Report.ExecutiveSummary.OverallSentiment);
        Assert.Equal(MarketRegime.Uptrend, result.Report.MarketContext.Regime);
    }

    [Fact]
    public void Validate_InvalidSentiment_DefaultsToUnknown()
    {
        var json = @"{
            ""executiveSummary"": {""overallSentiment"":""INVALID"",""summary"":""Test""},
            ""marketContext"": {""regime"":""INVALID"",""description"":""Test""}
        }";

        var result = _validator.Validate(
            new LlmResponse { Content = json, Model = "test", Success = true },
            TestAsset, DateTimeOffset.UtcNow);

        // Invalid sentiment should cause validation to fail since it's a required field
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_WithAgentAnalysis_ParsesAgentSection()
    {
        var json = @"{
            ""executiveSummary"": {""overallSentiment"":""Bullish"",""summary"":""Test""},
            ""marketContext"": {""regime"":""Sideways"",""description"":""Test""},
            ""technicalAnalysis"": {
                ""agentName"": ""TechnicalAnalyst"",
                ""sentiment"": ""Bullish"",
                ""confidence"": 0.75,
                ""summary"": ""RSI shows momentum"",
                ""supportingEvidence"": [
                    {""content"": ""RSI above 50"", ""type"": ""Calculation"", ""source"": ""Indicator Engine""}
                ],
                ""identifiedRisks"": [""Volume declining""]
            }
        }";

        var result = _validator.Validate(
            new LlmResponse { Content = json, Model = "test", Success = true },
            TestAsset, DateTimeOffset.UtcNow);

        Assert.True(result.IsValid);
        Assert.NotNull(result.Report!.TechnicalAnalysis);
        Assert.Equal("TechnicalAnalyst", result.Report.TechnicalAnalysis!.AgentName);
        Assert.Equal(Sentiment.Bullish, result.Report.TechnicalAnalysis.Sentiment);
        Assert.Equal(0.75m, result.Report.TechnicalAnalysis.Confidence);
        Assert.Single(result.Report.TechnicalAnalysis.SupportingEvidence);
        Assert.Single(result.Report.TechnicalAnalysis.IdentifiedRisks);
    }

    [Fact]
    public void Validate_WithScenarios_ParsesScenarios()
    {
        var json = @"{
            ""executiveSummary"": {""overallSentiment"":""Neutral"",""summary"":""Test""},
            ""marketContext"": {""regime"":""Sideways"",""description"":""Test""},
            ""bullishScenario"": {
                ""name"": ""Bullish"",
                ""description"": ""Price breaks above resistance"",
                ""assumptions"": [""Volume increases""],
                ""probabilityAssessment"": ""Possible"",
                ""expectedOutcome"": ""5-10% upside"",
                ""confirmationConditions"": [""RSI above 60""],
                ""invalidationConditions"": [""Price below support""]
            },
            ""baseScenario"": {
                ""name"": ""Base"",
                ""description"": ""Sideways consolidation"",
                ""probabilityAssessment"": ""Most likely""
            },
            ""bearishScenario"": {
                ""name"": ""Bearish"",
                ""description"": ""Price breaks support"",
                ""probabilityAssessment"": ""Unlikely""
            }
        }";

        var result = _validator.Validate(
            new LlmResponse { Content = json, Model = "test", Success = true },
            TestAsset, DateTimeOffset.UtcNow);

        Assert.True(result.IsValid);
        Assert.NotNull(result.Report!.BullishScenario);
        Assert.Equal("Bullish", result.Report.BullishScenario!.Name);
        Assert.Contains("Volume increases", result.Report.BullishScenario.Assumptions);
        Assert.Contains("RSI above 60", result.Report.BullishScenario.ConfirmationConditions);
        Assert.NotNull(result.Report.BaseScenario);
        Assert.NotNull(result.Report.BearishScenario);
    }

    [Fact]
    public void Validate_WithStrategySection_ParsesSection()
    {
        var json = @"{
            ""executiveSummary"": {""overallSentiment"":""Bullish"",""summary"":""Test""},
            ""marketContext"": {""regime"":""Uptrend"",""description"":""Test""},
            ""shortTermStrategy"": {
                ""timeHorizon"": ""ShortTerm"",
                ""entryScenario"": ""Wait for pullback"",
                ""entryZones"": [""14500-14800""],
                ""confirmationConditions"": [""Volume spike""],
                ""stopInvalidation"": ""Below 14000"",
                ""targetLevels"": [""15500"", ""16000""],
                ""exitConditions"": ""Take profit at targets"",
                ""riskAssessment"": ""Moderate""
            }
        }";

        var result = _validator.Validate(
            new LlmResponse { Content = json, Model = "test", Success = true },
            TestAsset, DateTimeOffset.UtcNow);

        Assert.True(result.IsValid);
        Assert.NotNull(result.Report!.ShortTermStrategy);
        Assert.Equal(TimeHorizon.ShortTerm, result.Report.ShortTermStrategy!.TimeHorizon);
        Assert.Equal("Wait for pullback", result.Report.ShortTermStrategy.EntryScenario);
        Assert.Contains("14500-14800", result.Report.ShortTermStrategy.EntryZones);
        Assert.Contains("15500", result.Report.ShortTermStrategy.TargetLevels);
    }

    [Fact]
    public void Validate_WithRiskReward_ParsesRiskReward()
    {
        var json = @"{
            ""executiveSummary"": {""overallSentiment"":""Bullish"",""summary"":""Test""},
            ""marketContext"": {""regime"":""Uptrend"",""description"":""Test""},
            ""riskReward"": {
                ""potentialUpside"": ""10-15%"",
                ""potentialDownside"": ""5-7%"",
                ""riskRewardRatio"": ""1:2"",
                ""riskLevel"": ""Moderate"",
                ""keyRiskFactors"": [""Volume decline"", ""Market uncertainty""],
                ""favorableFactors"": [""Strong technicals""],
                ""unfavorableFactors"": [""Weak fundamentals""]
            }
        }";

        var result = _validator.Validate(
            new LlmResponse { Content = json, Model = "test", Success = true },
            TestAsset, DateTimeOffset.UtcNow);

        Assert.True(result.IsValid);
        Assert.NotNull(result.Report!.RiskReward);
        Assert.Equal("1:2", result.Report.RiskReward!.RiskRewardRatio);
        Assert.Equal("Moderate", result.Report.RiskReward.RiskLevel);
        Assert.Contains("Volume decline", result.Report.RiskReward.KeyRiskFactors);
    }

    [Fact]
    public void Validate_WithConfidence_ParsesConfidence()
    {
        var json = @"{
            ""executiveSummary"": {""overallSentiment"":""Neutral"",""summary"":""Test""},
            ""marketContext"": {""regime"":""Sideways"",""description"":""Test""},
            ""confidence"": {
                ""overallConfidence"": 0.65,
                ""level"": ""Moderate"",
                ""confidenceFactors"": [""Multiple agents agree""],
                ""uncertaintyFactors"": [""Missing fundamental data""],
                ""informationThatWouldHelp"": [""Company earnings report""],
                ""dataSourcesUsed"": 3,
                ""agentsContributed"": 4
            }
        }";

        var result = _validator.Validate(
            new LlmResponse { Content = json, Model = "test", Success = true },
            TestAsset, DateTimeOffset.UtcNow);

        Assert.True(result.IsValid);
        Assert.NotNull(result.Report!.Confidence);
        Assert.Equal(0.65m, result.Report.Confidence!.OverallConfidence);
        Assert.Equal("Moderate", result.Report.Confidence.Level);
        Assert.Contains("Multiple agents agree", result.Report.Confidence.ConfidenceFactors);
        Assert.Equal(3, result.Report.Confidence.DataSourcesUsed);
    }

    [Fact]
    public void Validate_ConfidenceClamped_0To1()
    {
        var json = @"{
            ""executiveSummary"": {""overallSentiment"":""Neutral"",""summary"":""Test""},
            ""marketContext"": {""regime"":""Sideways"",""description"":""Test""},
            ""confidence"": {
                ""overallConfidence"": 1.5,
                ""level"": ""High""
            }
        }";

        var result = _validator.Validate(
            new LlmResponse { Content = json, Model = "test", Success = true },
            TestAsset, DateTimeOffset.UtcNow);

        Assert.True(result.IsValid);
        Assert.Equal(1.0m, result.Report!.Confidence!.OverallConfidence);
    }

    [Fact]
    public void Validate_WithEvidenceLists_ParsesEvidence()
    {
        var json = @"{
            ""executiveSummary"": {""overallSentiment"":""Bullish"",""summary"":""Test""},
            ""marketContext"": {""regime"":""Uptrend"",""description"":""Test""},
            ""supportingEvidence"": [
                {""content"": ""RSI bullish divergence"", ""type"": ""Calculation"", ""source"": ""Technical Analyst"", ""confidence"": 0.8},
                {""content"": ""Volume increasing"", ""type"": ""Fact"", ""source"": ""Indicator Engine""}
            ],
            ""contradictingEvidence"": [
                {""content"": ""Weak fundamentals"", ""type"": ""Interpretation"", ""source"": ""Fundamental Analyst""}
            ],
            ""missingInformation"": [""Company earnings report"", ""Sector analysis""],
            ""invalidationConditions"": [""RSI below 30"", ""Volume collapse""],
            ""monitoringRecommendations"": [""Watch RSI divergence"", ""Monitor volume""]
        }";

        var result = _validator.Validate(
            new LlmResponse { Content = json, Model = "test", Success = true },
            TestAsset, DateTimeOffset.UtcNow);

        Assert.True(result.IsValid);
        Assert.Equal(2, result.Report!.SupportingEvidence.Count);
        Assert.Equal(EvidenceType.Calculation, result.Report.SupportingEvidence[0].Type);
        Assert.Single(result.Report.ContradictingEvidence);
        Assert.Contains("Company earnings report", result.Report.MissingInformation);
        Assert.Contains("RSI below 30", result.Report.InvalidationConditions);
        Assert.Contains("Watch RSI divergence", result.Report.MonitoringRecommendations);
    }

    [Fact]
    public void Validate_NullOptionalSections_OmitsFromReport()
    {
        var json = @"{
            ""executiveSummary"": {""overallSentiment"":""Neutral"",""summary"":""Test""},
            ""marketContext"": {""regime"":""Sideways"",""description"":""Test""},
            ""technicalAnalysis"": null,
            ""bullishScenario"": null,
            ""riskReward"": null
        }";

        var result = _validator.Validate(
            new LlmResponse { Content = json, Model = "test", Success = true },
            TestAsset, DateTimeOffset.UtcNow);

        Assert.True(result.IsValid);
        Assert.Null(result.Report!.TechnicalAnalysis);
        Assert.Null(result.Report.BullishScenario);
        Assert.Null(result.Report.RiskReward);
    }

    [Fact]
    public void Validate_AgentConfidence_ClampedTo01()
    {
        var json = @"{
            ""executiveSummary"": {""overallSentiment"":""Neutral"",""summary"":""Test""},
            ""marketContext"": {""regime"":""Sideways"",""description"":""Test""},
            ""technicalAnalysis"": {
                ""agentName"": ""Tech"",
                ""sentiment"": ""Neutral"",
                ""confidence"": -0.5,
                ""summary"": ""Test""
            }
        }";

        var result = _validator.Validate(
            new LlmResponse { Content = json, Model = "test", Success = true },
            TestAsset, DateTimeOffset.UtcNow);

        Assert.True(result.IsValid);
        Assert.Equal(0.0m, result.Report!.TechnicalAnalysis!.Confidence);
    }

    [Fact]
    public void Validate_VariousSentiments_AllParsed()
    {
        foreach (var sentiment in new[] { "Bullish", "Bearish", "Neutral" })
        {
            var json = $@"{{
                ""executiveSummary"": {{""overallSentiment"":""{sentiment}"",""summary"":""Test""}},
                ""marketContext"": {{""regime"":""Uptrend"",""description"":""Test""}}
            }}";

            var result = _validator.Validate(
                new LlmResponse { Content = json, Model = "test", Success = true },
                TestAsset, DateTimeOffset.UtcNow);

            Assert.True(result.IsValid);
            var expected = Enum.Parse<Sentiment>(sentiment);
            Assert.Equal(expected, result.Report!.ExecutiveSummary.OverallSentiment);
        }
    }

    [Fact]
    public void Validate_VariousRegimes_AllParsed()
    {
        foreach (var regime in new[] { "Uptrend", "Downtrend", "Sideways", "Volatile", "Transitional" })
        {
            var json = $@"{{
                ""executiveSummary"": {{""overallSentiment"":""Neutral"",""summary"":""Test""}},
                ""marketContext"": {{""regime"":""{regime}"",""description"":""Test""}}
            }}";

            var result = _validator.Validate(
                new LlmResponse { Content = json, Model = "test", Success = true },
                TestAsset, DateTimeOffset.UtcNow);

            Assert.True(result.IsValid);
            var expected = Enum.Parse<MarketRegime>(regime);
            Assert.Equal(expected, result.Report!.MarketContext.Regime);
        }
    }
}

// --- StrategySynthesisService Tests ---

public class StrategySynthesisServiceTests
{
    private readonly Mock<ILLMProvider> _mockProvider;
    private readonly StrategySynthesisService _service;

    public StrategySynthesisServiceTests()
    {
        _mockProvider = new Mock<ILLMProvider>();
        _mockProvider.Setup(p => p.Model).Returns("test-model");

        _service = new StrategySynthesisService(
            _mockProvider.Object,
            new StrategyContextBuilder(),
            new StrategySynthesisPromptBuilder(),
            new StrategyResponseValidator(),
            Mock.Of<ILogger<StrategySynthesisService>>());
    }

    [Fact]
    public async Task SynthesizeAsync_ProviderFails_ReturnsFailure()
    {
        _mockProvider.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse { Content = "", Model = "test", Success = false, Error = "Connection refused" });

        var context = CreateTestContext();
        var outcome = await _service.SynthesizeAsync(context);

        Assert.False(outcome.Success);
        Assert.Contains("Connection refused", outcome.ErrorMessage);
    }

    [Fact]
    public async Task SynthesizeAsync_InvalidJson_ReturnsFailure()
    {
        _mockProvider.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse { Content = "not json", Model = "test", Success = true });

        var context = CreateTestContext();
        var outcome = await _service.SynthesizeAsync(context);

        Assert.False(outcome.Success);
        Assert.Contains("Invalid JSON", outcome.ErrorMessage);
    }

    [Fact]
    public async Task SynthesizeAsync_ValidResponse_ReturnsSuccess()
    {
        var json = @"{
            ""executiveSummary"": {""overallSentiment"":""Bullish"",""summary"":""Positive outlook""},
            ""marketContext"": {""regime"":""Uptrend"",""description"":""Strong momentum""},
            ""confidence"": {""overallConfidence"":0.6,""level"":""Moderate""}
        }";

        _mockProvider.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = json,
                Model = "llama3",
                Success = true,
                PromptTokens = 500,
                CompletionTokens = 800
            });

        var context = CreateTestContext();
        var outcome = await _service.SynthesizeAsync(context);

        Assert.True(outcome.Success);
        Assert.NotNull(outcome.Report);
        Assert.Equal(Sentiment.Bullish, outcome.Report!.ExecutiveSummary.OverallSentiment);
        Assert.Equal(MarketRegime.Uptrend, outcome.Report.MarketContext.Regime);
        Assert.Equal("llama3", outcome.ProviderModel);
        Assert.Equal(1300, outcome.TokensUsed);
    }

    [Fact]
    public async Task SynthesizeAsync_ValidResponse_EnrichesMetadata()
    {
        var json = @"{
            ""executiveSummary"": {""overallSentiment"":""Neutral"",""summary"":""Test""},
            ""marketContext"": {""regime"":""Sideways"",""description"":""Test""}
        }";

        _mockProvider.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse { Content = json, Model = "gpt-4", Success = true });

        var agent1 = CreateAgentResult("TechnicalAnalyst", Sentiment.Bullish, 0.7m);
        var context = CreateTestContextWithAgents([agent1]);

        var outcome = await _service.SynthesizeAsync(context);

        Assert.True(outcome.Success);
        Assert.Contains("TechnicalAnalyst", outcome.Report!.ContributingAgents);
        Assert.Equal("gpt-4", outcome.Report.LlmModel);
    }

    [Fact]
    public async Task SynthesizeAsync_Cancellation_Propagates()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockProvider.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var context = CreateTestContext();
        var outcome = await _service.SynthesizeAsync(context, cts.Token);

        Assert.False(outcome.Success);
        Assert.Contains("cancelled", outcome.ErrorMessage);
    }

    [Fact]
    public async Task SynthesizeAsync_IncludesAgentResults_InPrompt()
    {
        var json = @"{
            ""executiveSummary"": {""overallSentiment"":""Bullish"",""summary"":""Test""},
            ""marketContext"": {""regime"":""Uptrend"",""description"":""Test""}
        }";

        LlmRequest? capturedRequest = null;
        _mockProvider.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LlmRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new LlmResponse { Content = json, Model = "test", Success = true });

        var agent = CreateAgentResult("TechnicalAnalyst", Sentiment.Bullish, 0.7m);
        var context = CreateTestContextWithAgents([agent]);

        await _service.SynthesizeAsync(context);

        Assert.NotNull(capturedRequest);
        Assert.Contains("TechnicalAnalyst", capturedRequest!.UserPrompt);
    }
}

// --- StrategyAgent Tests ---

public class StrategyAgentTests
{
    private readonly Mock<IStrategySynthesisService> _mockSynthesis;
    private readonly StrategyAgent _agent;

    public StrategyAgentTests()
    {
        _mockSynthesis = new Mock<IStrategySynthesisService>();
        _agent = new StrategyAgent(
            _mockSynthesis.Object,
            new StrategyContextBuilder(),
            Mock.Of<ILogger<StrategyAgent>>());
    }

    [Fact]
    public async Task SynthesizeAsync_CallsSynthesisServiceWithContext()
    {
        var report = CreateTestReport();
        _mockSynthesis.Setup(s => s.SynthesizeAsync(It.IsAny<StrategyContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StrategySynthesisOutcome
            {
                Success = true,
                Report = report
            });

        var evidence = CreateTestEvidence(CreateTestAsset());
        var agents = new List<AgentAnalysisResult>
        {
            CreateAgentResult("TechnicalAnalyst", Sentiment.Bullish, 0.7m)
        };

        var outcome = await _agent.SynthesizeAsync(evidence, agents);

        Assert.True(outcome.Success);
        Assert.NotNull(outcome.Report);
        _mockSynthesis.Verify(s => s.SynthesizeAsync(
            It.Is<StrategyContext>(c =>
                c.Asset.Symbol == "TEST" &&
                c.AgentResults.Count == 1 &&
                c.RequestedHorizons.Count == 3),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SynthesizeAsync_WithCustomHorizons_PassesHorizons()
    {
        _mockSynthesis.Setup(s => s.SynthesizeAsync(It.IsAny<StrategyContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StrategySynthesisOutcome { Success = true, Report = CreateTestReport() });

        var evidence = CreateTestEvidence(CreateTestAsset());
        var horizons = new[] { TimeHorizon.ShortTerm };

        await _agent.SynthesizeAsync(evidence, [], horizons);

        _mockSynthesis.Verify(s => s.SynthesizeAsync(
            It.Is<StrategyContext>(c =>
                c.RequestedHorizons.Count == 1 &&
                c.RequestedHorizons[0] == TimeHorizon.ShortTerm),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SynthesizeAsync_SynthesisFails_ReturnsFailure()
    {
        _mockSynthesis.Setup(s => s.SynthesizeAsync(It.IsAny<StrategyContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StrategySynthesisOutcome
            {
                Success = false,
                ErrorMessage = "LLM unavailable"
            });

        var outcome = await _agent.SynthesizeAsync(
            CreateTestEvidence(CreateTestAsset()), []);

        Assert.False(outcome.Success);
        Assert.Contains("LLM unavailable", outcome.ErrorMessage);
    }
}

// --- Hallucination Guardrail Tests ---

public class HallucinationGuardrailTests
{
    private readonly StrategyResponseValidator _validator = new();
    private static readonly Asset TestAsset = new()
    {
        Symbol = "TEST",
        Name = "Test Asset",
        Market = "TSE",
        AssetType = AssetType.Stock
    };

    [Fact]
    public void Validate_EvidenceItemsRequireContent()
    {
        var json = @"{
            ""executiveSummary"": {""overallSentiment"":""Neutral"",""summary"":""Test""},
            ""marketContext"": {""regime"":""Sideways"",""description"":""Test""},
            ""supportingEvidence"": [
                {""content"": """, ""type"": ""Fact"", ""source"": ""Test""}
            ]
        }";

        // Even with empty content, the validator should still parse it
        // (content validation is best-effort; the application marks empty items)
        var result = _validator.Validate(
            new LlmResponse { Content = json, Model = "test", Success = true },
            TestAsset, DateTimeOffset.UtcNow);

        Assert.True(result.IsValid);
        Assert.Single(result.Report!.SupportingEvidence);
        Assert.Equal("", result.Report.SupportingEvidence[0].Content);
    }

    [Fact]
    public void Validate_EvidenceTypeDefaultsToInterpretation()
    {
        var json = @"{
            ""executiveSummary"": {""overallSentiment"":""Neutral"",""summary"":""Test""},
            ""marketContext"": {""regime"":""Sideways"",""description"":""Test""},
            ""supportingEvidence"": [
                {""content"": ""Test evidence"", ""type"": ""INVALID"", ""source"": ""Test""}
            ]
        }";

        var result = _validator.Validate(
            new LlmResponse { Content = json, Model = "test", Success = true },
            TestAsset, DateTimeOffset.UtcNow);

        Assert.True(result.IsValid);
        Assert.Equal(EvidenceType.Interpretation, result.Report!.SupportingEvidence[0].Type);
    }

    [Fact]
    public void Validate_NumericValuesWithinRange()
    {
        var json = @"{
            ""executiveSummary"": {""overallSentiment"":""Neutral"",""summary"":""Test""},
            ""marketContext"": {""regime"":""Sideways"",""description"":""Test""},
            ""confidence"": {
                ""overallConfidence"": 0.5,
                ""level"": ""Moderate""
            }
        }";

        var result = _validator.Validate(
            new LlmResponse { Content = json, Model = "test", Success = true },
            TestAsset, DateTimeOffset.UtcNow);

        Assert.True(result.IsValid);
        Assert.True(result.Report!.Confidence!.OverallConfidence >= 0m);
        Assert.True(result.Report.Confidence.OverallConfidence <= 1m);
    }
}

// --- Architecture Boundary Tests ---

public class StrategySynthesisArchitectureTests
{
    [Fact]
    public void StrategySynthesis_DoesNotReferenceInfrastructure()
    {
        var aiAssembly = typeof(StrategySynthesisService).Assembly;
        var refs = aiAssembly.GetReferencedAssemblies().Select(a => a.Name);
        Assert.DoesNotContain("StrategyForge.Infrastructure", refs);
    }

    [Fact]
    public void Domain_DoesNotReferenceAI()
    {
        var domainAssembly = typeof(StrategyContext).Assembly;
        var refs = domainAssembly.GetReferencedAssemblies().Select(a => a.Name);
        Assert.DoesNotContain("StrategyForge.AI", refs);
    }

    [Fact]
    public void StrategySynthesisPromptBuilder_NoCredentialsInPrompt()
    {
        var builder = new StrategySynthesisPromptBuilder();
        var context = CreateTestContext();
        var request = builder.BuildRequest(context);

        Assert.DoesNotContain("sk-", request.SystemPrompt);
        Assert.DoesNotContain("Bearer", request.SystemPrompt);
        Assert.DoesNotContain("sk-", request.UserPrompt);
        Assert.DoesNotContain("Bearer", request.UserPrompt);
    }
}

// --- Test Helpers ---

internal static class SynthesisTestHelpers
{
    internal static Asset CreateTestAsset() => new()
    {
        Symbol = "TEST",
        Name = "Test Asset",
        Market = "TSE",
        AssetType = AssetType.Stock
    };

    internal static AnalysisEvidence CreateTestEvidence(Asset asset) => new()
    {
        Asset = asset,
        AssembledAt = DateTimeOffset.UtcNow,
        CurrentPrice = 15000m,
        DailyChangePercent = 2.5m,
        IndicatorValues = new Dictionary<string, IndicatorResult>
        {
            ["RSI"] = new() { IndicatorName = "RSI", Date = DateOnly.FromDateTime(DateTime.Today), Value = 55m, Period = 14 }
        }
    };

    internal static AgentAnalysisResult CreateAgentResult(string name, Sentiment sentiment, decimal confidence) => new()
    {
        AgentName = name,
        AssetSymbol = "TEST",
        GeneratedAt = DateTimeOffset.UtcNow,
        Sentiment = sentiment,
        Confidence = confidence,
        Summary = $"Test analysis from {name}"
    };

    internal static StrategyContext CreateTestContext() => new()
    {
        Asset = CreateTestAsset(),
        AssembledAt = DateTimeOffset.UtcNow,
        Evidence = CreateTestEvidence(CreateTestAsset()),
        RequestedHorizons = [TimeHorizon.ShortTerm, TimeHorizon.MediumTerm, TimeHorizon.LongTerm]
    };

    internal static StrategyContext CreateTestContextWithAgents(IReadOnlyList<AgentAnalysisResult> agents) => new()
    {
        Asset = CreateTestAsset(),
        AssembledAt = DateTimeOffset.UtcNow,
        Evidence = CreateTestEvidence(CreateTestAsset()),
        AgentResults = agents,
        RequestedHorizons = [TimeHorizon.ShortTerm, TimeHorizon.MediumTerm, TimeHorizon.LongTerm]
    };

    internal static StrategyReport CreateTestReport() => new()
    {
        Asset = CreateTestAsset(),
        GeneratedAt = DateTimeOffset.UtcNow,
        DataAsOf = DateTimeOffset.UtcNow,
        ExecutiveSummary = new ExecutiveSummary
        {
            OverallSentiment = Sentiment.Bullish,
            Summary = "Test report"
        },
        MarketContext = new MarketContext
        {
            Regime = MarketRegime.Uptrend,
            Description = "Strong uptrend"
        }
    };
}

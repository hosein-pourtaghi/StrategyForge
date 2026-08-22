using Microsoft.Extensions.Logging;
using Moq;
using StrategyForge.AI.Providers;
using StrategyForge.AI.Services;
using StrategyForge.Analysis;
using StrategyForge.Analysis.Indicators;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Interfaces.AI;
using StrategyForge.Domain.Interfaces.Analysis;
using StrategyForge.Domain.Models;
using Xunit;

namespace StrategyForge.AI.Tests;

public static class TestHelpers
{
    public static IReadOnlyList<Candle> CreateCandles(params decimal[] closes)
    {
        var list = new List<Candle>();
        for (int i = 0; i < closes.Length; i++)
            list.Add(new Candle { Date = new DateOnly(2024, 1, 1).AddDays(i), Open = closes[i], High = closes[i] + 1, Low = closes[i] - 1, Close = closes[i], Volume = 1000 });
        return list.AsReadOnly();
    }

    public static InstrumentMapping CreateFoolad() => new()
    {
        InstrumentId = "foolad-tse",
        Symbol = "\u0641\u0648\u0644\u0627\u062f",
        DisplayName = "Foolad Mobarakeh",
        AssetClass = AssetType.Stock,
        Exchange = "TSE",
        QuoteCurrency = "IRR",
        SourceIdentifiers = new Dictionary<SourceAdapterType, SourceIdentifier>()
    };
}

public class AnalysisContextBuilderTests
{
    [Fact]
    public void Build_EmptyCandles_HasMissingDataWarning()
    {
        var builder = new AnalysisContextBuilder();
        var indicatorResult = new IndicatorEngineResult();
        var evidence = builder.Build(TestHelpers.CreateFoolad(), [], indicatorResult);
        Assert.NotEmpty(evidence.MissingData);
    }

    [Fact]
    public void Build_WithCandles_ProducesCurrentPrice()
    {
        var builder = new AnalysisContextBuilder();
        var candles = TestHelpers.CreateCandles(10, 20, 30, 40, 50);
        var engine = new IndicatorEngine(new IIndicator[] { new SmIndicator() });
        var result = engine.ComputeAll(candles);
        var evidence = builder.Build(TestHelpers.CreateFoolad(), candles, result);
        Assert.Equal(50m, evidence.CurrentPrice);
    }

    [Fact]
    public void Build_IncludesIndicatorValues()
    {
        var builder = new AnalysisContextBuilder();
        var candles = TestHelpers.CreateCandles(Enumerable.Range(1, 30).Select(x => (decimal)x).ToArray());
        var engine = new IndicatorEngine(new IIndicator[] { new SmIndicator(), new RsiIndicator() });
        var result = engine.ComputeAll(candles);
        var evidence = builder.Build(TestHelpers.CreateFoolad(), candles, result);
        Assert.True(evidence.IndicatorValues.ContainsKey("SMA"));
        Assert.True(evidence.IndicatorValues.ContainsKey("RSI"));
    }
}

public class PromptBuilderTests
{
    [Fact]
    public void BuildRequest_ContainsEvidenceRules()
    {
        var builder = new PromptBuilder();
        var evidence = new AnalysisEvidence
        {
            Asset = new Asset { Symbol = "TEST", Name = "Test", Market = "TSE", AssetType = AssetType.Stock },
            AssembledAt = DateTimeOffset.UtcNow
        };
        var request = builder.BuildRequest(evidence);
        Assert.Contains("ONLY the evidence provided", request.SystemPrompt);
        Assert.Equal("json", request.ResponseFormat);
    }

    [Fact]
    public void BuildRequest_IncludesAssetInfo()
    {
        var builder = new PromptBuilder();
        var evidence = new AnalysisEvidence
        {
            Asset = new Asset { Symbol = "\u0641\u0648\u0644\u0627\u062f", Name = "Foolad", Market = "TSE", AssetType = AssetType.Stock },
            AssembledAt = DateTimeOffset.UtcNow,
            CurrentPrice = 15000m
        };
        var request = builder.BuildRequest(evidence);
        Assert.Contains("15000", request.UserPrompt);
    }

    [Fact]
    public void BuildRequest_IncludesIndicators()
    {
        var builder = new PromptBuilder();
        var evidence = new AnalysisEvidence
        {
            Asset = new Asset { Symbol = "TEST", Name = "Test", Market = "TSE", AssetType = AssetType.Stock },
            AssembledAt = DateTimeOffset.UtcNow,
            IndicatorValues = new Dictionary<string, IndicatorResult>
            {
                ["RSI"] = new() { IndicatorName = "RSI", Date = DateOnly.FromDateTime(DateTime.Today), Value = 31.4m, Period = 14 }
            }
        };
        var request = builder.BuildRequest(evidence);
        Assert.Contains("RSI", request.UserPrompt);
        Assert.Contains("31.4", request.UserPrompt);
    }

    [Fact]
    public void BuildRequest_IncludesMissingData()
    {
        var builder = new PromptBuilder();
        var evidence = new AnalysisEvidence
        {
            Asset = new Asset { Symbol = "TEST", Name = "Test", Market = "TSE", AssetType = AssetType.Stock },
            AssembledAt = DateTimeOffset.UtcNow,
            MissingData = new[] { "Fundamental data unavailable" }
        };
        var request = builder.BuildRequest(evidence);
        Assert.Contains("Fundamental data unavailable", request.UserPrompt);
    }

    [Fact]
    public void SystemPrompt_NoTradingSignals()
    {
        var prompt = PromptBuilder.BuildSystemPrompt();
        Assert.Contains("BUY/SELL", prompt);
    }

    [Fact]
    public void SystemPrompt_PreservesCurrencyDistinction()
    {
        var prompt = PromptBuilder.BuildSystemPrompt();
        Assert.Contains("USD/IRR is NOT USDT/IRR", prompt);
    }

    [Fact]
    public void SystemPrompt_NoCredentials()
    {
        var prompt = PromptBuilder.BuildSystemPrompt();
        Assert.DoesNotContain("sk-", prompt);
    }
}

public class LlmResponseValidatorTests
{
    private readonly LlmResponseValidator _validator = new();

    [Fact]
    public void Validate_FailedResponse_ReturnsInvalid()
    {
        var result = _validator.Validate(new LlmResponse { Content = "", Model = "test", Success = false, Error = "timeout" });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_EmptyContent_ReturnsInvalid()
    {
        var result = _validator.Validate(new LlmResponse { Content = "", Model = "test", Success = true });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_InvalidJson_ReturnsInvalid()
    {
        var result = _validator.Validate(new LlmResponse { Content = "not json", Model = "test", Success = true });
        Assert.False(result.IsValid);
        Assert.Contains("Invalid JSON", result.ErrorMessage);
    }

    [Fact]
    public void Validate_MissingSummary_ReturnsInvalid()
    {
        var result = _validator.Validate(new LlmResponse { Content = "{\"observations\": []}", Model = "test", Success = true });
        Assert.False(result.IsValid);
        Assert.Contains("summary", result.ErrorMessage);
    }

    [Fact]
    public void Validate_ValidResponse_ParsesCorrectly()
    {
        var json = "{\"summary\":\"Momentum positive\",\"observations\":[{\"category\":\"technical\",\"statement\":\"RSI 65\",\"evidenceType\":\"calculated\",\"indicatorName\":\"RSI\"}],\"interpretations\":[{\"topic\":\"Trend\",\"analysis\":\"Positive\",\"confidence\":0.7,\"basedOn\":[\"RSI\"]}],\"uncertainties\":[],\"warnings\":[]}";
        var result = _validator.Validate(new LlmResponse { Content = json, Model = "test", Success = true });
        Assert.True(result.IsValid);
        Assert.Equal("Momentum positive", result.ParsedResult!.Summary);
        Assert.Single(result.ParsedResult.Observations);
        Assert.Equal("RSI", result.ParsedResult.Observations[0].IndicatorName);
        Assert.Single(result.ParsedResult.Interpretations);
    }

    [Fact]
    public void Validate_MinimalValid_Works()
    {
        var result = _validator.Validate(new LlmResponse { Content = "{\"summary\":\"Done\"}", Model = "test", Success = true });
        Assert.True(result.IsValid);
        Assert.Equal("Done", result.ParsedResult!.Summary);
    }
}

public class LlmInterpretationServiceTests
{
    [Fact]
    public async Task InterpretAsync_ProviderFails_ReturnsFailure()
    {
        var provider = new Mock<ILLMProvider>();
        provider.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse { Content = "", Model = "test", Success = false, Error = "Connection refused" });

        var service = new LlmInterpretationService(provider.Object, new AnalysisContextBuilder(), new PromptBuilder(), new LlmResponseValidator(), Mock.Of<ILogger<LlmInterpretationService>>());
        var outcome = await service.InterpretAsync(new AnalysisEvidence { Asset = new Asset { Symbol = "T", Name = "T", Market = "M", AssetType = AssetType.Stock }, AssembledAt = DateTimeOffset.UtcNow });
        Assert.False(outcome.Success);
        Assert.Contains("Connection refused", outcome.ErrorMessage);
    }

    [Fact]
    public async Task InterpretAsync_InvalidJson_ReturnsFailure()
    {
        var provider = new Mock<ILLMProvider>();
        provider.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse { Content = "not json", Model = "test", Success = true });

        var service = new LlmInterpretationService(provider.Object, new AnalysisContextBuilder(), new PromptBuilder(), new LlmResponseValidator(), Mock.Of<ILogger<LlmInterpretationService>>());
        var outcome = await service.InterpretAsync(new AnalysisEvidence { Asset = new Asset { Symbol = "T", Name = "T", Market = "M", AssetType = AssetType.Stock }, AssembledAt = DateTimeOffset.UtcNow });
        Assert.False(outcome.Success);
        Assert.Contains("Invalid JSON", outcome.ErrorMessage);
    }

    [Fact]
    public async Task InterpretAsync_ValidResponse_ReturnsSuccess()
    {
        var json = "{\"summary\":\"Positive\",\"observations\":[],\"interpretations\":[],\"uncertainties\":[],\"warnings\":[]}";
        var provider = new Mock<ILLMProvider>();
        provider.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse { Content = json, Model = "llama3", Success = true, PromptTokens = 100, CompletionTokens = 50 });

        var service = new LlmInterpretationService(provider.Object, new AnalysisContextBuilder(), new PromptBuilder(), new LlmResponseValidator(), Mock.Of<ILogger<LlmInterpretationService>>());
        var outcome = await service.InterpretAsync(new AnalysisEvidence { Asset = new Asset { Symbol = "T", Name = "T", Market = "M", AssetType = AssetType.Stock }, AssembledAt = DateTimeOffset.UtcNow });
        Assert.True(outcome.Success);
        Assert.Equal("Positive", outcome.Result!.Summary);
        Assert.Equal(150, outcome.TokensUsed);
    }

    [Fact]
    public async Task InterpretAsync_Cancellation_Propagates()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var provider = new Mock<ILLMProvider>();
        provider.Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var service = new LlmInterpretationService(provider.Object, new AnalysisContextBuilder(), new PromptBuilder(), new LlmResponseValidator(), Mock.Of<ILogger<LlmInterpretationService>>());
        var outcome = await service.InterpretAsync(new AnalysisEvidence { Asset = new Asset { Symbol = "T", Name = "T", Market = "M", AssetType = AssetType.Stock }, AssembledAt = DateTimeOffset.UtcNow }, cts.Token);
        Assert.False(outcome.Success);
    }
}

public class ArchitectureBoundaryTests
{
    [Fact]
    public void AI_DoesNotReferenceInfrastructure()
    {
        var aiAssembly = typeof(OpenAiCompatibleLlmProvider).Assembly;
        var refs = aiAssembly.GetReferencedAssemblies().Select(a => a.Name);
        Assert.DoesNotContain("StrategyForge.Infrastructure", refs);
    }

    [Fact]
    public void Indicators_DoNotReferenceAI()
    {
        var analysisAssembly = typeof(SmIndicator).Assembly;
        var refs = analysisAssembly.GetReferencedAssemblies().Select(a => a.Name);
        Assert.DoesNotContain("StrategyForge.AI", refs);
    }

    [Fact]
    public void PromptBuilder_NoCredentialsInPrompt()
    {
        var builder = new PromptBuilder();
        var evidence = new AnalysisEvidence { Asset = new Asset { Symbol = "T", Name = "T", Market = "M", AssetType = AssetType.Stock }, AssembledAt = DateTimeOffset.UtcNow };
        var request = builder.BuildRequest(evidence);
        Assert.DoesNotContain("sk-", request.SystemPrompt);
        Assert.DoesNotContain("Bearer", request.SystemPrompt);
    }
}

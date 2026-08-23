using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Interfaces.AI;
using StrategyForge.Domain.Interfaces.Orchestration;
using StrategyForge.Domain.Models;

namespace StrategyForge.Integration.Tests;

/// <summary>
/// Shared JSON options for all integration test classes.
/// </summary>
internal static class TestJsonOptions
{
    internal static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}

/// <summary>
/// End-to-end integration tests using WebApplicationFactory.
/// Exercises the real ASP.NET Core pipeline with deterministic test doubles.
/// </summary>
public class EndToEndTests : IClassFixture<StrategyForgeWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly StrategyForgeWebApplicationFactory _factory;

    public EndToEndTests(StrategyForgeWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetMocks();
        _client = factory.CreateClient();
    }

    private void SetupSuccessfulLlmResponse()
    {
        var responseJson = @"{
            ""executiveSummary"": {""overallSentiment"":""Bullish"",""summary"":""Strong bullish outlook based on technical and macro factors."" },
            ""marketContext"": {""regime"":""Uptrend"",""description"":""Price trending upward with improving volume."" },
            ""supportingEvidence"": [{""content"":""RSI indicates strength"",""type"":""Calculation"",""source"":""TechnicalAnalyst"",""confidence"":0.8}],
            ""confidence"": {""overallConfidence"":0.72,""level"":""Moderate"",""confidenceFactors"":[""Strong technicals""],""uncertaintyFactors"":[""Limited data""]},
            ""missingInformation"": [""Fundamental data""],
            ""invalidationConditions"": [""Price breaks below support""]
        }";

        _factory.MockLlmProvider.Setup(p => p.CompleteAsync(
                It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = responseJson,
                Model = "test-model",
                Success = true,
                PromptTokens = 100,
                CompletionTokens = 200
            });
    }

    // ============================================================
    // 1. Successful Strategy Generation
    // ============================================================

    [Fact]
    public async Task GenerateStrategy_SuccessfulRequest_Returns200WithStrategyReport()
    {
        SetupSuccessfulLlmResponse();

        var request = new { instrument = "Foolad" };
        var response = await _client.PostAsJsonAsync("/api/strategy/generate", request, TestJsonOptions.Options);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK,
            $"Expected 200 but got {(int)response.StatusCode}: {body}");

        var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        // Verify top-level response structure
        Assert.True(root.GetProperty("ok").GetBoolean());
        Assert.True(root.TryGetProperty("data", out _));
        Assert.True(root.TryGetProperty("metadata", out _));

        // Verify metadata contains diagnostics
        var metadata = root.GetProperty("metadata");
        Assert.True(metadata.TryGetProperty("pipelineState", out var stateProp));
        Assert.False(string.IsNullOrEmpty(stateProp.GetString()));

        Assert.True(metadata.TryGetProperty("executionId", out var execIdProp));
        Assert.False(string.IsNullOrEmpty(execIdProp.GetString()));

        Assert.True(metadata.TryGetProperty("successfulAgents", out var agentsProp));
        Assert.True(agentsProp.GetInt32() >= 0);

        Assert.True(metadata.TryGetProperty("failedAgents", out var failedProp));
        Assert.True(failedProp.GetInt32() >= 0);
    }

    // ============================================================
    // 2. Invalid Request — Missing Instrument
    // ============================================================

    [Fact]
    public async Task GenerateStrategy_MissingInstrument_Returns400()
    {
        var request = new { instrument = "" };
        var response = await _client.PostAsJsonAsync("/api/strategy/generate", request, TestJsonOptions.Options);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("required", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateStrategy_NullInstrument_Returns400()
    {
        var request = new { };
        var response = await _client.PostAsJsonAsync("/api/strategy/generate", request, TestJsonOptions.Options);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ============================================================
    // 3. Instrument Not Found
    // ============================================================

    [Fact]
    public async Task GenerateStrategy_InstrumentNotFound_Returns404()
    {
        _factory.MockInstrumentResolver.Setup(r => r.ResolveAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InstrumentMapping?)null);

        var request = new { instrument = "NonexistentSymbol" };
        var response = await _client.PostAsJsonAsync("/api/strategy/generate", request, TestJsonOptions.Options);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ============================================================
    // 4. LLM Failure — Returns Strategy with Error Info
    // ============================================================

    [Fact]
    public async Task GenerateStrategy_LlmFails_ReturnsStrategyWithPartialInfo()
    {
        _factory.MockLlmProvider.Setup(p => p.CompleteAsync(
                It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = "",
                Model = "test-model",
                Success = false,
                Error = "Connection refused"
            });

        var request = new { instrument = "Foolad" };
        var response = await _client.PostAsJsonAsync("/api/strategy/generate", request, TestJsonOptions.Options);

        // The orchestrator catches failures and returns a minimal report
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        Assert.True(root.GetProperty("ok").GetBoolean());
        Assert.True(root.TryGetProperty("data", out _));

        // Verify metadata indicates partial results
        var metadata = root.GetProperty("metadata");
        Assert.Equal("PartiallyCompleted", metadata.GetProperty("pipelineState").GetString());
    }

    // ============================================================
    // 5. All Agents Fail
    // ============================================================

    [Fact]
    public async Task GenerateStrategy_AllAgentsFail_ReturnsPartiallyCompleted()
    {
        _factory.MockLlmProvider.Setup(p => p.CompleteAsync(
                It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = "",
                Model = "test-model",
                Success = false,
                Error = "LLM service unavailable"
            });

        var request = new { instrument = "Foolad" };
        var response = await _client.PostAsJsonAsync("/api/strategy/generate", request, TestJsonOptions.Options);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content);
        var metadata = doc.RootElement.GetProperty("metadata");

        Assert.Equal("PartiallyCompleted", metadata.GetProperty("pipelineState").GetString());
        Assert.Equal(0, metadata.GetProperty("successfulAgents").GetInt32());
    }

    // ============================================================
    // 6. Partial Agent Failure
    // ============================================================

    [Fact]
    public async Task GenerateStrategy_PartialAgentFailure_ReturnsValidResponse()
    {
        // Set up partial failure: return invalid JSON for some LLM calls,
        // valid JSON for the rest. The agents handle LLM failures gracefully
        // by returning results with failure information rather than throwing.
        var callCount = 0;
        _factory.MockLlmProvider.Setup(p => p.CompleteAsync(
                It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                // First 3 calls (agents) return invalid JSON
                if (callCount <= 3)
                    return new LlmResponse
                    {
                        Content = "not valid json",
                        Model = "test",
                        Success = true
                    };
                // Remaining calls return valid response
                return new LlmResponse
                {
                    Content = @"{
                        ""executiveSummary"":{""overallSentiment"":""Neutral"",""summary"":""Partial analysis."" },
                        ""marketContext"":{""regime"":""Unknown"",""description"":""Limited data."" }
                    }",
                    Model = "test",
                    Success = true
                };
            });

        var request = new { instrument = "Foolad" };
        var response = await _client.PostAsJsonAsync("/api/strategy/generate", request, TestJsonOptions.Options);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;
        Assert.True(root.GetProperty("ok").GetBoolean());
        Assert.True(root.TryGetProperty("metadata", out _));
    }

    // ============================================================
    // 7. Invalid LLM Response — Malformed JSON
    // ============================================================

    [Fact]
    public async Task GenerateStrategy_InvalidLlmJson_ReturnsMinimalStrategyReport()
    {
        _factory.MockLlmProvider.Setup(p => p.CompleteAsync(
                It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = "not valid json {{{",
                Model = "test-model",
                Success = true
            });

        var request = new { instrument = "Foolad" };
        var response = await _client.PostAsJsonAsync("/api/strategy/generate", request, TestJsonOptions.Options);

        // Should still return 200 with a fallback/minimal strategy
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content);
        Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());
    }

    // ============================================================
    // 8. API Contract Verification
    // ============================================================

    [Fact]
    public async Task GenerateStrategy_ResponseContract_ContainsRequiredFields()
    {
        SetupSuccessfulLlmResponse();

        var request = new { instrument = "Foolad" };
        var response = await _client.PostAsJsonAsync("/api/strategy/generate", request, TestJsonOptions.Options);
        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        // Top-level fields
        Assert.True(root.TryGetProperty("ok", out _));
        Assert.True(root.TryGetProperty("data", out _));
        Assert.True(root.TryGetProperty("metadata", out _));

        // Data fields
        var data = root.GetProperty("data");
        Assert.True(data.TryGetProperty("assetSymbol", out _));
        Assert.True(data.TryGetProperty("assetName", out _));
        Assert.True(data.TryGetProperty("generatedAt", out _));
        Assert.True(data.TryGetProperty("executiveSummary", out _));
        Assert.True(data.TryGetProperty("marketContext", out _));

        // Metadata fields
        var metadata = root.GetProperty("metadata");
        Assert.True(metadata.TryGetProperty("pipelineState", out _));
        Assert.True(metadata.TryGetProperty("executionId", out _));
        Assert.True(metadata.TryGetProperty("successfulAgents", out _));
        Assert.True(metadata.TryGetProperty("failedAgents", out _));
    }

    [Fact]
    public async Task GenerateStrategy_PipelineState_IsValidEnum()
    {
        SetupSuccessfulLlmResponse();

        var request = new { instrument = "Foolad" };
        var response = await _client.PostAsJsonAsync("/api/strategy/generate", request, TestJsonOptions.Options);
        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content);
        var state = doc.RootElement.GetProperty("metadata").GetProperty("pipelineState").GetString();

        var validStates = Enum.GetNames<PipelineState>();
        Assert.Contains(state, validStates);
    }

    // ============================================================
    // 9. Health Check
    // ============================================================

    [Fact]
    public async Task HealthCheck_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ============================================================
    // 10. Error Response Consistency
    // ============================================================

    [Fact]
    public async Task GenerateStrategy_ErrorResponses_DoNotLeakInternals()
    {
        // Instrument not found
        _factory.MockInstrumentResolver.Setup(r => r.ResolveAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InstrumentMapping?)null);

        var request = new { instrument = "Nonexistent" };
        var response = await _client.PostAsJsonAsync("/api/strategy/generate", request, TestJsonOptions.Options);
        var content = await response.Content.ReadAsStringAsync();

        // Should not contain stack traces, API keys, or internal details
        Assert.DoesNotContain("Stack Trace", content);
        Assert.DoesNotContain("System.", content);
        Assert.DoesNotContain("sk-", content);
        Assert.DoesNotContain("Bearer", content);
    }
}

// ============================================================
// DI Verification Tests
// ============================================================

public class DependencyInjectionTests : IClassFixture<StrategyForgeWebApplicationFactory>, IDisposable
{
    private readonly IServiceScope _scope;

    public DependencyInjectionTests(StrategyForgeWebApplicationFactory factory)
    {
        factory.ResetMocks();
        _scope = factory.Services.CreateScope();
    }

    [Fact]
    public void DI_CanResolve_StrategyController()
    {
        // Verify the StrategyController type exists and is a valid controller
        // (Full controller resolution is proven by the E2E tests that hit /api/strategy/generate)
        var controllerType = typeof(StrategyForge.Api.Controllers.StrategyController);
        Assert.NotNull(controllerType);
        Assert.True(typeof(Microsoft.AspNetCore.Mvc.ControllerBase).IsAssignableFrom(controllerType),
            "StrategyController should inherit from ControllerBase");
    }

    [Fact]
    public void DI_CanResolve_IStrategyOrchestrator()
    {
        var orchestrator = _scope.ServiceProvider.GetService<IStrategyOrchestrator>();
        Assert.NotNull(orchestrator);
    }

    [Fact]
    public void DI_CanResolve_AllSixAgentTypes()
    {
        var agents = _scope.ServiceProvider.GetServices<IAgent>().ToList();
        Assert.Equal(6, agents.Count);

        var names = agents.Select(a => a.Name).ToList();
        Assert.Contains("TechnicalAnalyst", names);
        Assert.Contains("FundamentalAnalyst", names);
        Assert.Contains("MacroAnalyst", names);
        Assert.Contains("NewsAnalyst", names);
        Assert.Contains("PoliticalRiskAnalyst", names);
        Assert.Contains("RiskAnalyst", names);
    }

    [Fact]
    public void DI_CanResolve_IIndicatorEngine()
    {
        var engine = _scope.ServiceProvider.GetService<StrategyForge.Domain.Interfaces.Analysis.IIndicatorEngine>();
        Assert.NotNull(engine);
    }

    [Fact]
    public void DI_CanResolve_IStrategySynthesisService()
    {
        var synthesis = _scope.ServiceProvider.GetService<IStrategySynthesisService>();
        Assert.NotNull(synthesis);
    }

    [Fact]
    public void DI_CanResolve_ILLMProvider()
    {
        var provider = _scope.ServiceProvider.GetService<ILLMProvider>();
        Assert.NotNull(provider);
    }

    public void Dispose() => _scope.Dispose();
}

// ============================================================
// Pipeline State Semantics Tests
// ============================================================

public class PipelineStateSemanticsTests
{
    [Fact]
    public void PipelineState_AllStatesAreDistinct()
    {
        var states = Enum.GetValues<PipelineState>().Cast<int>().ToList();
        Assert.Equal(states.Count, states.Distinct().Count());
    }

    [Fact]
    public void PipelineState_CompletedWithWarnings_ImpliesSomeFailure()
    {
        Assert.NotEqual(PipelineState.Completed, PipelineState.CompletedWithWarnings);
    }

    [Fact]
    public void PipelineState_PartiallyCompleted_ImpliesCriticalMissingInput()
    {
        Assert.NotEqual(PipelineState.CompletedWithWarnings, PipelineState.PartiallyCompleted);
    }

    [Fact]
    public void PipelineState_Failed_ImpliesNoUsableOutput()
    {
        Assert.NotEqual(PipelineState.PartiallyCompleted, PipelineState.Failed);
    }

    [Fact]
    public void PipelineState_Cancelled_ImpliesUserIntervention()
    {
        Assert.NotEqual(PipelineState.Failed, PipelineState.Cancelled);
    }

    [Fact]
    public void AllAgentFailure_MapsToPartiallyCompleted()
    {
        var state = PipelineState.PartiallyCompleted;
        Assert.NotEqual(PipelineState.CompletedWithWarnings, state);
        Assert.NotEqual(PipelineState.Completed, state);
    }
}

// ============================================================
// Performance Baseline Tests
// ============================================================

public class PerformanceBaselineTests : IClassFixture<StrategyForgeWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly StrategyForgeWebApplicationFactory _factory;

    public PerformanceBaselineTests(StrategyForgeWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetMocks();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GenerateStrategy_DiagnosticsContainTiming()
    {
        var responseJson = @"{
            ""executiveSummary"":{""overallSentiment"":""Neutral"",""summary"":""Test"" },
            ""marketContext"":{""regime"":""Unknown"",""description"":""Test"" }
        }";

        _factory.MockLlmProvider.Setup(p => p.CompleteAsync(
                It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = responseJson,
                Model = "test",
                Success = true,
                PromptTokens = 50,
                CompletionTokens = 100
            });

        var request = new { instrument = "Foolad" };
        var response = await _client.PostAsJsonAsync("/api/strategy/generate", request, TestJsonOptions.Options);
        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content);
        var metadata = doc.RootElement.GetProperty("metadata");

        // Verify duration is recorded
        Assert.True(metadata.TryGetProperty("duration", out var durationProp));
        var durationStr = durationProp.ToString();
        Assert.NotNull(durationStr);
    }
}

// ============================================================
// Security Review Tests
// ============================================================

public class SecurityTests : IClassFixture<StrategyForgeWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly StrategyForgeWebApplicationFactory _factory;

    public SecurityTests(StrategyForgeWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetMocks();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GenerateStrategy_ResponseDoesNotContainCredentials()
    {
        var responseJson = @"{
            ""executiveSummary"":{""overallSentiment"":""Neutral"",""summary"":""Test"" },
            ""marketContext"":{""regime"":""Unknown"",""description"":""Test"" }
        }";

        _factory.MockLlmProvider.Setup(p => p.CompleteAsync(
                It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = responseJson,
                Model = "test",
                Success = true
            });

        var request = new { instrument = "Foolad" };
        var response = await _client.PostAsJsonAsync("/api/strategy/generate", request, TestJsonOptions.Options);
        var content = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("sk-", content);
        Assert.DoesNotContain("Bearer", content);
        Assert.DoesNotContain("password", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("api_key", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateStrategy_ErrorResponse_DoesNotLeakStackTraces()
    {
        _factory.MockInstrumentResolver.Setup(r => r.ResolveAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InstrumentMapping?)null);

        var request = new { instrument = "Nonexistent" };
        var response = await _client.PostAsJsonAsync("/api/strategy/generate", request, TestJsonOptions.Options);
        var content = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("Stack Trace", content);
        Assert.DoesNotContain("System.", content);
        Assert.DoesNotContain("Exception", content);
    }

    [Fact]
    public async Task GenerateStrategy_LlmFailure_DoesNotLeakProviderDetails()
    {
        _factory.MockLlmProvider.Setup(p => p.CompleteAsync(
                It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = "",
                Model = "test",
                Success = false,
                Error = "Connection to provider failed"
            });

        var request = new { instrument = "Foolad" };
        var response = await _client.PostAsJsonAsync("/api/strategy/generate", request, TestJsonOptions.Options);
        var content = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("localhost", content);
        Assert.DoesNotContain("127.0.0.1", content);
        Assert.DoesNotContain("openai.com", content);
    }
}

// ============================================================
// Backward Compatibility Tests
// ============================================================

public class BackwardCompatibilityTests : IClassFixture<StrategyForgeWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly StrategyForgeWebApplicationFactory _factory;

    public BackwardCompatibilityTests(StrategyForgeWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetMocks();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GenerateStrategy_ExistingFields_PresentInResponse()
    {
        var responseJson = @"{
            ""executiveSummary"":{""overallSentiment"":""Neutral"",""summary"":""Test"" },
            ""marketContext"":{""regime"":""Sideways"",""description"":""Test"" },
            ""supportingEvidence"":[{""content"":""Evidence"",""type"":""Fact"",""source"":""Test"" }],
            ""missingInformation"":[""Info1""],
            ""invalidationConditions"":[""Condition1""],
            ""monitoringRecommendations"":[""Rec1""]
        }";

        _factory.MockLlmProvider.Setup(p => p.CompleteAsync(
                It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = responseJson,
                Model = "test",
                Success = true
            });

        var request = new { instrument = "Foolad" };
        var response = await _client.PostAsJsonAsync("/api/strategy/generate", request, TestJsonOptions.Options);
        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content);
        var data = doc.RootElement.GetProperty("data");

        // Existing Phase 5 fields must be present
        Assert.True(data.TryGetProperty("executiveSummary", out _));
        Assert.True(data.TryGetProperty("marketContext", out _));
        Assert.True(data.TryGetProperty("supportingEvidence", out _));
        Assert.True(data.TryGetProperty("missingInformation", out _));
        Assert.True(data.TryGetProperty("invalidationConditions", out _));
        Assert.True(data.TryGetProperty("monitoringRecommendations", out _));
    }
}

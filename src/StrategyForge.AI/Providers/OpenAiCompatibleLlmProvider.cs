using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StrategyForge.Domain.Interfaces.AI;
using StrategyForge.Domain.Models;

namespace StrategyForge.AI.Providers;

/// <summary>
/// Configuration for the OpenAI-compatible LLM provider.
/// All credentials come from IConfiguration — never hard-coded.
/// </summary>
public sealed record LlmProviderSettings
{
    public const string SectionName = "LlmProvider";

    /// <summary>Base URL for the OpenAI-compatible API (e.g., "http://localhost:3000/v1").</summary>
    public string BaseUrl { get; init; } = "http://localhost:3000/v1";

    /// <summary>Model identifier (e.g., "llama3", "gpt-4").</summary>
    public string Model { get; init; } = "llama3";

    /// <summary>API key. Null for local/free providers that don't require one.</summary>
    public string? ApiKey { get; init; }

    /// <summary>Request timeout in seconds.</summary>
    public int TimeoutSeconds { get; init; } = 120;

    /// <summary>Default temperature for completions.</summary>
    public double DefaultTemperature { get; init; } = 0.3;

    /// <summary>Default max tokens for completions.</summary>
    public int DefaultMaxTokens { get; init; } = 4096;
}

/// <summary>
/// OpenAI-compatible LLM provider. Works with:
/// - OpenAI API
/// - Ollama (http://localhost:3000/v1)
/// - FreeLLMApi
/// - Any OpenAI-compatible gateway
/// 
/// Security: API keys come from IConfiguration, are never logged,
/// never returned in responses, and never included in prompts.
/// </summary>
public sealed class OpenAiCompatibleLlmProvider : ILLMProvider
{
    private readonly HttpClient _httpClient;
    private readonly LlmProviderSettings _settings;
    private readonly ILogger<OpenAiCompatibleLlmProvider> _logger;

    public string Name => "OpenAI-Compatible";
    public string Model => _settings.Model;

    public OpenAiCompatibleLlmProvider(
        HttpClient httpClient,
        IOptions<LlmProviderSettings> settings,
        ILogger<OpenAiCompatibleLlmProvider> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;

        _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);

        if (!string.IsNullOrEmpty(_settings.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        }
    }

    public async Task<LlmResponse> CompleteAsync(
        LlmRequest request,
        CancellationToken cancellationToken = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var model = request.Model ?? _settings.Model;
            var messages = new List<ChatMessage>
            {
                new() { Role = "system", Content = request.SystemPrompt },
                new() { Role = "user", Content = request.UserPrompt }
            };

            var requestBody = new ChatCompletionRequest
            {
                Model = model,
                Messages = messages,
                Temperature = request.Temperature ?? _settings.DefaultTemperature,
                MaxTokens = request.MaxTokens ?? _settings.DefaultMaxTokens
            };

            if (request.ResponseFormat == "json")
            {
                requestBody.ResponseFormat = new ResponseFormat { Type = "json_object" };
            }

            _logger.LogDebug("Sending LLM request to {Model} ({Url})", model, _settings.BaseUrl);

            var httpResponse = await _httpClient.PostAsJsonAsync(
                "/chat/completions", requestBody, cancellationToken);

            sw.Stop();

            if (!httpResponse.IsSuccessStatusCode)
            {
                var errorBody = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("LLM request failed: HTTP {Status}", httpResponse.StatusCode);
                return new LlmResponse
                {
                    Content = "",
                    Model = model,
                    Success = false,
                    Error = $"HTTP {(int)httpResponse.StatusCode}: {errorBody}",
                    FinishReason = "error",
                    ReceivedAt = DateTimeOffset.UtcNow,
                    ResponseDuration = sw.Elapsed
                };
            }

            var response = await httpResponse.Content.ReadFromJsonAsync<ChatCompletionResponse>(
                cancellationToken: cancellationToken);

            if (response?.Choices == null || response.Choices.Count == 0)
            {
                return new LlmResponse
                {
                    Content = "",
                    Model = model,
                    Success = false,
                    Error = "Empty response from LLM provider",
                    FinishReason = "empty",
                    ReceivedAt = DateTimeOffset.UtcNow,
                    ResponseDuration = sw.Elapsed
                };
            }

            var content = response.Choices[0].Message?.Content ?? "";
            var finishReason = response.Choices[0].FinishReason;

            _logger.LogInformation("LLM response received: {Tokens} tokens in {Duration}ms",
                response.Usage?.TotalTokens ?? 0, sw.ElapsedMilliseconds);

            return new LlmResponse
            {
                Content = content,
                Model = response.Model ?? model,
                Success = true,
                PromptTokens = response.Usage?.PromptTokens ?? 0,
                CompletionTokens = response.Usage?.CompletionTokens ?? 0,
                FinishReason = finishReason,
                ReceivedAt = DateTimeOffset.UtcNow,
                ResponseDuration = sw.Elapsed
            };
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            return new LlmResponse
            {
                Content = "",
                Model = _settings.Model,
                Success = false,
                Error = "Request was cancelled",
                FinishReason = "cancelled",
                ReceivedAt = DateTimeOffset.UtcNow,
                ResponseDuration = sw.Elapsed
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "LLM request failed");
            return new LlmResponse
            {
                Content = "",
                Model = _settings.Model,
                Success = false,
                Error = $"LLM request failed: {ex.Message}",
                FinishReason = "error",
                ReceivedAt = DateTimeOffset.UtcNow,
                ResponseDuration = sw.Elapsed
            };
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/models", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}

// --- Internal OpenAI-compatible request/response models ---

internal sealed class ChatCompletionRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = "";

    [JsonPropertyName("messages")]
    public List<ChatMessage> Messages { get; set; } = [];

    [JsonPropertyName("temperature")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Temperature { get; set; }

    [JsonPropertyName("max_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxTokens { get; set; }

    [JsonPropertyName("response_format")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ResponseFormat? ResponseFormat { get; set; }
}

internal sealed class ChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "";

    [JsonPropertyName("content")]
    public string Content { get; set; } = "";
}

internal sealed class ResponseFormat
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";
}

internal sealed class ChatCompletionResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("choices")]
    public List<Choice>? Choices { get; set; }

    [JsonPropertyName("usage")]
    public UsageInfo? Usage { get; set; }
}

internal sealed class Choice
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("message")]
    public ChatMessage? Message { get; set; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

internal sealed class UsageInfo
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}

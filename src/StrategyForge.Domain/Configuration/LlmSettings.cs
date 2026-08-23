namespace StrategyForge.Domain.Configuration;

/// <summary>
/// Configuration for the LLM provider.
/// Maps to the "LlmSettings" section in appsettings.json.
/// </summary>
public sealed record LlmSettings
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "LlmSettings";

    /// <summary>The LLM provider to use (e.g., "OpenAiCompatible").</summary>
    public string Provider { get; init; } = "OpenAiCompatible";

    /// <summary>Base URL of the LLM API (e.g., "http://localhost:3000/v1").</summary>
    public string BaseUrl { get; init; } = "http://localhost:3000/v1";

    /// <summary>The model identifier to use (e.g., "llama3", "gpt-4").</summary>
    public string Model { get; init; } = "default";

    /// <summary>API key (if required). Use environment variables for production.</summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>Default maximum tokens for completions.</summary>
    public int DefaultMaxTokens { get; init; } = 4096;

    /// <summary>Default temperature for completions (0.0 = deterministic).</summary>
    public double DefaultTemperature { get; init; } = 0.3;

    /// <summary>Timeout in seconds for LLM requests.</summary>
    public int TimeoutSeconds { get; init; } = 120;

    /// <summary>Number of retry attempts for failed LLM requests.</summary>
    public int RetryAttempts { get; init; } = 2;
}

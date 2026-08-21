namespace StrategyForge.Domain.Models;

/// <summary>
/// Represents a request to an LLM provider.
/// Used by the ILLMProvider interface to abstract away provider-specific details.
/// </summary>
public sealed record LlmRequest
{
    /// <summary>The system prompt establishing the AI's role and instructions.</summary>
    public required string SystemPrompt { get; init; }

    /// <summary>The user prompt containing the specific question or task.</summary>
    public required string UserPrompt { get; init; }

    /// <summary>
    /// Desired response format (e.g., "json").
    /// Null means default text format.
    /// </summary>
    public string? ResponseFormat { get; init; }

    /// <summary>Maximum tokens to generate in the response.</summary>
    public int? MaxTokens { get; init; }

    /// <summary>Temperature for response generation (0.0 = deterministic, 1.0 = creative).</summary>
    public double? Temperature { get; init; }

    /// <summary>Optional model override (if the provider supports multiple models).</summary>
    public string? Model { get; init; }
}

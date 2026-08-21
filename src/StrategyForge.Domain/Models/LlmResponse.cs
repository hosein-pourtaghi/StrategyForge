namespace StrategyForge.Domain.Models;

/// <summary>
/// Represents a response from an LLM provider.
/// Used by the ILLMProvider interface to abstract away provider-specific details.
/// </summary>
public sealed record LlmResponse
{
    /// <summary>The generated text content from the LLM.</summary>
    public required string Content { get; init; }

    /// <summary>The model that generated this response.</summary>
    public required string Model { get; init; }

    /// <summary>Number of tokens in the prompt.</summary>
    public int PromptTokens { get; init; }

    /// <summary>Number of tokens in the completion.</summary>
    public int CompletionTokens { get; init; }

    /// <summary>Total tokens used (prompt + completion).</summary>
    public int TotalTokens => PromptTokens + CompletionTokens;

    /// <summary>Whether the request completed successfully.</summary>
    public bool Success { get; init; }

    /// <summary>Error message if the request failed.</summary>
    public string? Error { get; init; }

    /// <summary>Finish reason (e.g., "stop", "length", "content_filter").</summary>
    public string? FinishReason { get; init; }

    /// <summary>When the response was received.</summary>
    public DateTimeOffset ReceivedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>How long the LLM took to respond.</summary>
    public TimeSpan? ResponseDuration { get; init; }
}

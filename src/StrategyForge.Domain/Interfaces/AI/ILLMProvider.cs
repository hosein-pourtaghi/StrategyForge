using StrategyForge.Domain.Models;

namespace StrategyForge.Domain.Interfaces.AI;

/// <summary>
/// Interface for LLM (Large Language Model) providers.
/// Abstracts away provider-specific details so agents remain provider-agnostic.
/// 
/// To switch from FreeLLM to OpenAI to Anthropic: only implement a new ILLMProvider.
/// Agents never know which LLM is being used.
/// </summary>
public interface ILLMProvider
{
    /// <summary>Human-readable name of this provider (e.g., "FreeLLM", "OpenAI").</summary>
    string Name { get; }

    /// <summary>The model identifier being used (e.g., "llama3", "gpt-4").</summary>
    string Model { get; }

    /// <summary>
    /// Sends a structured request to the LLM and receives a completion.
    /// </summary>
    /// <param name="request">The request containing system/user prompts and parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The LLM response.</returns>
    Task<LlmResponse> CompleteAsync(
        LlmRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the LLM provider is available and responsive.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the provider is reachable and functional.</returns>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}

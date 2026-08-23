using StrategyForge.Domain.Models;

namespace StrategyForge.Domain.Interfaces.Orchestration;

/// <summary>
/// Interface for the strategy synthesis service that combines evidence,
/// analysis results, and agent outputs into a structured StrategyReport
/// through LLM reasoning and structured validation.
/// 
/// The service:
/// 1. Builds a deterministic context from inputs
/// 2. Constructs a prompt instructing the LLM to reason from the context
/// 3. Calls the LLM through the existing ILLMProvider abstraction
/// 4. Validates the LLM response against the application schema
/// 5. Produces a strongly typed, evidence-traceable StrategyReport
/// 
/// The LLM never becomes the source of truth for structure.
/// The application owns the schema; the LLM produces candidate content.
/// </summary>
public interface IStrategySynthesisService
{
    /// <summary>
    /// Synthesizes a StrategyReport from the provided context.
    /// </summary>
    /// <param name="context">The assembled strategy context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The synthesis outcome containing either a StrategyReport or error details.</returns>
    Task<StrategySynthesisOutcome> SynthesizeAsync(
        StrategyContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The outcome of a strategy synthesis attempt.
/// Contains either a successful StrategyReport or error information.
/// </summary>
public sealed class StrategySynthesisOutcome
{
    /// <summary>Whether synthesis was successful.</summary>
    public bool Success { get; init; }

    /// <summary>Error message if synthesis failed.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>The synthesized StrategyReport (only when Success is true).</summary>
    public StrategyReport? Report { get; init; }

    /// <summary>The LLM model used for synthesis.</summary>
    public string? ProviderModel { get; init; }

    /// <summary>Total tokens consumed.</summary>
    public int TokensUsed { get; init; }

    /// <summary>Duration of the synthesis call.</summary>
    public TimeSpan? Duration { get; init; }

    /// <summary>Raw LLM content if parsing failed (for debugging).</summary>
    public string? RawContent { get; init; }
}

namespace StrategyForge.AI.Services;

/// <summary>
/// Structured result from LLM interpretation.
/// Separates FACT from INTERPRETATION explicitly.
/// </summary>
public sealed class LlmInterpretationResult
{
    public string Summary { get; set; } = "";
    public List<LlmObservation> Observations { get; set; } = [];
    public List<LlmInterpretation> Interpretations { get; set; } = [];
    public List<LlmUncertainty> Uncertainties { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}

public sealed class LlmObservation
{
    public string? Category { get; set; }
    public string Statement { get; set; } = "";
    public string? EvidenceType { get; set; }
    public string? IndicatorName { get; set; }
}

public sealed class LlmInterpretation
{
    public string? Topic { get; set; }
    public string Analysis { get; set; } = "";
    public decimal Confidence { get; set; } = 0.5m;
    public IReadOnlyList<string> BasedOn { get; set; } = [];
}

public sealed class LlmUncertainty
{
    public string? Topic { get; set; }
    public string? Reason { get; set; }
    public string? WhatWouldHelp { get; set; }
}

/// <summary>
/// Result of validating an LLM response.
/// </summary>
public sealed class LlmValidationResult
{
    public bool IsValid { get; init; }
    public string? ErrorMessage { get; init; }
    public LlmInterpretationResult? ParsedResult { get; init; }
}

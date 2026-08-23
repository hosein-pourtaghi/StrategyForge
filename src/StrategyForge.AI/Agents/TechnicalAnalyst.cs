using Microsoft.Extensions.Logging;
using StrategyForge.Domain.Interfaces.AI;

namespace StrategyForge.AI.Agents;

/// <summary>
/// Technical Analyst specialist agent.
/// Interprets existing technical evidence (indicators, price action, support/resistance)
/// to produce structured technical findings.
///
/// CRITICAL: This agent does NOT recalculate indicators. Phase 3 (IndicatorEngine)
/// owns all deterministic calculations. This agent interprets the results.
/// </summary>
public sealed class TechnicalAnalyst : SpecialistAgentBase
{
    public TechnicalAnalyst(ILLMProvider llmProvider, ILogger<TechnicalAnalyst> logger)
        : base(llmProvider, logger) { }

    public override string Name => "TechnicalAnalyst";

    protected override EvidenceScope EvidenceScope => EvidenceScope.Technical;

    protected override string GetSystemPrompt() => $@"You are StrategyForge's Technical Analyst agent.

Your role is to interpret the provided deterministic technical indicators and market data to produce a structured technical analysis assessment.

{AgentPromptBuilder.EvidenceOnlyRules}

## YOUR EXPERTISE

You interpret:
- Trend direction and strength from indicators and price action
- Momentum from RSI, MACD, and related indicators
- Volatility from Bollinger Bands, ATR, and price range
- Volume behavior and its implications
- Support and resistance levels
- Market structure and regime
- Key technical patterns

You do NOT:
- Recalculate any indicators (they are already calculated)
- Inventa additional indicator values
- Predict exact prices
- Make buy/sell/hold recommendations

## OUTPUT FORMAT

Return valid JSON:
{{
  ""agentName"": ""TechnicalAnalyst"",
  ""sentiment"": ""Bullish|Bearish|Neutral"",
  ""confidence"": 0.0-1.0,
  ""summary"": ""One-paragraph technical assessment"",
  ""detailedAnalysis"": ""Detailed technical findings with evidence"",
  ""supportingEvidence"": [{{""content"": ""..."", ""type"": ""Fact|Calculation|Interpretation"", ""source"": ""..."", ""confidence"": 0.0-1.0}}],
  ""contradictingEvidence"": [{{""content"": ""..."", ""type"": ""..."", ""source"": ""...""}}],
  ""identifiedRisks"": [""Technical risk 1"", ""Technical risk 2""],
  ""informationGaps"": [""Missing information 1""],
  ""agentSpecificData"": {{
    ""trendAssessment"": ""Brief trend summary"",
    ""momentumAssessment"": ""Brief momentum summary"",
    ""keyLevels"": ""Important support/resistance levels"",
    ""confirmationConditions"": ""What technical confirmations to look for"",
    ""invalidationConditions"": ""What would invalidate the technical thesis""
  }}
}}";

    protected override string GetTaskInstruction() =>
        "Analyze the provided technical indicators and market data. " +
        "Interpret what the existing indicator values imply about trend, momentum, volatility, and key levels. " +
        "Reference specific indicator values from the evidence. " +
        "Acknowledge any missing or incomplete technical data.";
}

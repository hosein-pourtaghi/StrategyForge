using System.Text;
using StrategyForge.Domain.Models;

namespace StrategyForge.AI.Services;

/// <summary>
/// Builds structured LLM prompts from AnalysisEvidence.
/// Enforces evidence rules: facts vs interpretation, provenance preservation,
/// no hallucinated data, and structured output requirements.
/// </summary>
public sealed class PromptBuilder
{
    public LlmRequest BuildRequest(AnalysisEvidence evidence)
    {
        return new LlmRequest
        {
            SystemPrompt = BuildSystemPrompt(),
            UserPrompt = BuildUserPrompt(evidence),
            ResponseFormat = "json",
            Temperature = 0.3,
            MaxTokens = 4096
        };
    }

    public static string BuildSystemPrompt()
    {
        return @"You are StrategyForge, an evidence-driven financial market analysis assistant.

## CORE RULES

1. You must use ONLY the evidence provided. Never fabricate market data.
2. Never invent prices, indicator values, or data not explicitly provided.
3. Distinguish between: FACT, CALCULATED, INTERPRETATION, UNCERTAINTY.
4. Never claim to have accessed external sources unless evidence is provided.
5. Preserve: USD/IRR is NOT USDT/IRR. Official FX is NOT free-market FX.
6. When data is insufficient, explicitly state: ""Data insufficient""
7. Never produce BUY/SELL/HOLD recommendations or trading signals.
8. Your role is to INTERPRET evidence, not decide investment actions.

## OUTPUT FORMAT

Respond with valid JSON:
""summary"": ""Brief assessment"",
""observations"": [{""category"": ""technical|market_context"", ""statement"": ""..."", ""evidenceType"": ""fact|calculated|interpretation""}],
""interpretations"": [{""topic"": ""..."", ""analysis"": ""..."", ""confidence"": 0.5, ""basedOn"": []}],
""uncertainties"": [{""topic"": ""..."", ""reason"": ""..."", ""whatWouldHelp"": ""...""}],
""warnings"": []

Do not include buy/sell/hold, entry/exit, stop loss, or position sizing fields.";
    }

    public static string BuildUserPrompt(AnalysisEvidence evidence)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Market Evidence Context");
        sb.AppendLine();
        sb.AppendLine($"## Asset: {evidence.Asset.Symbol} ({evidence.Asset.Name})");
        sb.AppendLine($"- Market: {evidence.Asset.Market}");
        sb.AppendLine($"- Type: {evidence.Asset.AssetType}");
        sb.AppendLine($"- Data range: {evidence.DataStartDate} to {evidence.DataEndDate}");
        sb.AppendLine();

        sb.AppendLine("## Market Data");
        if (evidence.CurrentPrice.HasValue) sb.AppendLine($"- Current price: {evidence.CurrentPrice}");
        if (evidence.DailyChangePercent.HasValue) sb.AppendLine($"- Daily change: {evidence.DailyChangePercent}%");
        if (evidence.LatestVolume.HasValue) sb.AppendLine($"- Volume: {evidence.LatestVolume}");
        if (evidence.VolumeRatio.HasValue) sb.AppendLine($"- Volume ratio: {evidence.VolumeRatio}x avg");
        sb.AppendLine();

        if (evidence.IndicatorValues.Count > 0)
        {
            sb.AppendLine("## Deterministic Technical Analysis");
            sb.AppendLine("_Calculated from market data. These are facts, not opinions._");
            foreach (var (name, result) in evidence.IndicatorValues)
            {
                sb.Append($"- **{name}** (period={result.Period}): {result.Value}");
                if (result.AdditionalValues?.Count > 0)
                    sb.Append(" | " + string.Join(", ", result.AdditionalValues.Select(kv => $"{kv.Key}={kv.Value}")));
                sb.AppendLine();
            }
            sb.AppendLine();
        }

        if (evidence.MissingData.Count > 0)
        {
            sb.AppendLine("## Missing Data");
            foreach (var m in evidence.MissingData) sb.AppendLine($"- WARNING: {m}");
            sb.AppendLine();
        }
        if (evidence.Warnings.Count > 0)
        {
            sb.AppendLine("## Warnings");
            foreach (var w in evidence.Warnings) sb.AppendLine($"- WARNING: {w}");
            sb.AppendLine();
        }

        sb.AppendLine("## Your Task");
        sb.AppendLine("Analyze the evidence. Return JSON following the specified format.");
        return sb.ToString();
    }
}

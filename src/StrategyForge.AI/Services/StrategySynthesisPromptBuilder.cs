using System.Text;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Models;

namespace StrategyForge.AI.Services;

/// <summary>
/// Builds structured LLM prompts for strategy synthesis.
/// 
/// The prompt explicitly instructs the LLM to:
/// 1. Reason only from the supplied context
/// 2. Distinguish evidence from inference
/// 3. Avoid inventing market facts or values
/// 4. Clearly state uncertainty
/// 5. Produce machine-readable structured output
/// 6. Reference supporting evidence
/// 7. Provide scenario-based reasoning
/// 8. Respect risk constraints
/// 9. Avoid pretending to execute trades
/// </summary>
public sealed class StrategySynthesisPromptBuilder
{
    /// <summary>
    /// Builds an LlmRequest for strategy synthesis from the given context.
    /// </summary>
    public LlmRequest BuildRequest(StrategyContext context)
    {
        return new LlmRequest
        {
            SystemPrompt = BuildSystemPrompt(),
            UserPrompt = BuildUserPrompt(context),
            ResponseFormat = "json",
            Temperature = 0.3,
            MaxTokens = 6144
        };
    }

    /// <summary>
    /// The system prompt establishing the Strategy Synthesis agent's role and rules.
    /// </summary>
    public static string BuildSystemPrompt()
    {
        return @"You are StrategyForge's Strategy Synthesis Agent. Your role is to produce a structured, evidence-driven investment strategy proposal by reasoning over specialist agent analyses and market evidence.

## CRITICAL RULES

1. **Reason ONLY from provided context.** Never fabricate market data, prices, or indicators not present in the context.
2. **Distinguish evidence from inference.** Mark information as FACT (from data sources), CALCULATION (from indicators), INTERPRETATION (your reasoning), or UNCERTAIN (when data is insufficient).
3. **Never invent prices, levels, or indicators.** If a value is not in the context, state it is unavailable.
4. **Never produce buy/sell/hold signals.** You produce strategy *proposals* for human decision-making. You do NOT execute trades.
5. **Preserve evidence traceability.** Reference which agent findings or evidence items support each conclusion.
6. **Use scenarios, not predictions.** Present base, bull, and bear cases as conditional scenarios, not deterministic forecasts.
7. **Be honest about uncertainty.** Never overstate confidence. Financial analysis rarely exceeds moderate confidence.
8. **Respect the LLM's limitations.** You do not have real-time data. Your analysis is based on the provided context only.
9. **Do not include entry/exit/stop-loss prices** unless they are directly supported by evidence in the context (indicator values, support/resistance levels from analysis).
10. **Mark unsupported claims.** If you infer a numeric level not directly from evidence, mark it as ""LLM estimate"" not as a fact.

## OUTPUT FORMAT

Respond with valid JSON matching this structure:

{
  ""executiveSummary"": {
    ""overallSentiment"": ""Bullish|Bearish|Neutral"",
    ""summary"": ""One-paragraph strategy overview"",
    ""keyTakeaway"": ""Most important takeaway"",
    ""criticalLevel"": ""Key price level to watch"",
    ""urgency"": ""Action urgency assessment""
  },
  ""marketContext"": {
    ""regime"": ""Uptrend|Downtrend|Sideways|Volatile|Transitional"",
    ""description"": ""Market condition description"",
    ""currentPrice"": null,
    ""recentPriceChange"": null,
    ""volumeContext"": ""Volume description"",
    ""macroContext"": ""Macro context"",
    ""upcomingEvents"": []
  },
  ""technicalAnalysis"": {
    ""agentName"": ""TechnicalAnalyst"",
    ""sentiment"": ""Bullish|Bearish|Neutral"",
    ""confidence"": 0.0,
    ""summary"": ""Technical analysis summary"",
    ""detailedAnalysis"": ""Detailed technical findings"",
    ""supportingEvidence"": [{""content"": ""..."", ""type"": ""Fact|Calculation|Interpretation"", ""source"": ""...""}],
    ""contradictingEvidence"": [],
    ""identifiedRisks"": []
  },
  ""fundamentalAnalysis"": null,
  ""macroAnalysis"": null,
  ""newsAnalysis"": null,
  ""politicalRiskAnalysis"": null,
  ""riskAnalysis"": null,
  ""bullishScenario"": {
    ""name"": ""Bullish"",
    ""description"": ""What happens if bullish"",
    ""assumptions"": [],
    ""probabilityAssessment"": ""Qualitative probability"",
    ""expectedOutcome"": ""Expected result"",
    ""confirmationConditions"": [],
    ""invalidationConditions"": []
  },
  ""baseScenario"": {
    ""name"": ""Base"",
    ""description"": ""Most likely outcome"",
    ""assumptions"": [],
    ""probabilityAssessment"": ""Most likely"",
    ""expectedOutcome"": ""Expected result"",
    ""confirmationConditions"": [],
    ""invalidationConditions"": []
  },
  ""bearishScenario"": {
    ""name"": ""Bearish"",
    ""description"": ""What happens if bearish"",
    ""assumptions"": [],
    ""probabilityAssessment"": ""Qualitative probability"",
    ""expectedOutcome"": ""Expected result"",
    ""confirmationConditions"": [],
    ""invalidationConditions"": []
  },
  ""shortTermStrategy"": {
    ""timeHorizon"": ""ShortTerm"",
    ""entryScenario"": ""Entry conditions"",
    ""entryZones"": [],
    ""confirmationConditions"": [],
    ""stopInvalidation"": ""Invalidation level"",
    ""targetLevels"": [],
    ""exitConditions"": ""Exit conditions"",
    ""riskAssessment"": ""Risk for this horizon"",
    ""monitoringActions"": []
  },
  ""mediumTermStrategy"": null,
  ""longTermStrategy"": null,
  ""riskReward"": {
    ""potentialUpside"": ""Upside estimate"",
    ""potentialDownside"": ""Downside estimate"",
    ""riskRewardRatio"": ""Risk/reward ratio"",
    ""riskLevel"": ""Low|Moderate|High|Very High"",
    ""keyRiskFactors"": [],
    ""favorableFactors"": [],
    ""unfavorableFactors"": []
  },
  ""confidence"": {
    ""overallConfidence"": 0.0,
    ""level"": ""Qualitative confidence level"",
    ""confidenceFactors"": [],
    ""uncertaintyFactors"": [],
    ""informationThatWouldHelp"": [],
    ""dataSourcesUsed"": 0,
    ""agentsContributed"": 0
  },
  ""supportingEvidence"": [{""content"": ""..."", ""type"": ""..."", ""source"": ""...""}],
  ""contradictingEvidence"": [],
  ""missingInformation"": [],
  ""invalidationConditions"": [],
  ""monitoringRecommendations"": []
}

## IMPORTANT

- Include only scenarios and strategy sections that are relevant for the requested time horizons.
- Set null for scenarios or strategy sections not applicable.
- Use the sentiment values: Bullish, Bearish, Neutral (not Mixed or Unknown).
- Use the regime values: Uptrend, Downtrend, Sideways, Volatile, Transitional (not Unknown).
- The overallConfidence must be between 0.0 and 1.0.
- Every evidence item must have content, type, and source fields.
- Do not fabricate agent results for agents that did not contribute.";
    }

    /// <summary>
    /// Builds the user prompt containing the full synthesis context.
    /// </summary>
    public static string BuildUserPrompt(StrategyContext context)
    {
        var sb = new StringBuilder();

        // --- Asset Information ---
        sb.AppendLine("# Strategy Synthesis Request");
        sb.AppendLine();
        sb.AppendLine($"## Asset: {context.Asset.Symbol} ({context.Asset.Name})");
        sb.AppendLine($"- Market: {context.Asset.Market}");
        sb.AppendLine($"- Type: {context.Asset.AssetType}");
        if (!string.IsNullOrEmpty(context.Asset.Sector))
            sb.AppendLine($"- Sector: {context.Asset.Sector}");
        sb.AppendLine();

        // --- Requested Horizons ---
        sb.AppendLine($"## Requested Horizons: {string.Join(", ", context.RequestedHorizons)}");
        sb.AppendLine();

        // --- Evidence Summary ---
        sb.AppendLine("---");
        sb.AppendLine("## Market Evidence");
        sb.AppendLine();
        sb.AppendLine($"- Data range: {context.Evidence.DataStartDate} to {context.Evidence.DataEndDate}");
        if (context.Evidence.CurrentPrice.HasValue)
            sb.AppendLine($"- Current price: {context.Evidence.CurrentPrice}");
        if (context.Evidence.DailyChangePercent.HasValue)
            sb.AppendLine($"- Daily change: {context.Evidence.DailyChangePercent}%");
        if (context.Evidence.LatestVolume.HasValue)
            sb.AppendLine($"- Volume: {context.Evidence.LatestVolume}");
        if (context.Evidence.VolumeRatio.HasValue)
            sb.AppendLine($"- Volume ratio: {context.Evidence.VolumeRatio}x average");
        sb.AppendLine();

        // --- Technical Indicators ---
        if (context.Evidence.IndicatorValues.Count > 0)
        {
            sb.AppendLine("### Deterministic Technical Indicators");
            sb.AppendLine("_(Calculated from market data. These are facts, not opinions.)_");
            foreach (var (name, result) in context.Evidence.IndicatorValues)
            {
                sb.Append($"- **{name}** (period={result.Period}): {result.Value}");
                if (result.Signal != null)
                    sb.Append($" [{result.Signal}]");
                if (result.AdditionalValues?.Count > 0)
                    sb.Append(" | " + string.Join(", ", result.AdditionalValues.Select(kv => $"{kv.Key}={kv.Value}")));
                sb.AppendLine();
            }
            sb.AppendLine();
        }

        // --- Fundamental Data ---
        if (context.Evidence.CompanyInfo != null)
        {
            sb.AppendLine("### Company Fundamentals");
            sb.AppendLine($"- Name: {context.Evidence.CompanyInfo.Name}");
            if (context.Evidence.CompanyInfo.MarketCap.HasValue)
                sb.AppendLine($"- Market Cap: {context.Evidence.CompanyInfo.MarketCap}");
            if (context.Evidence.CompanyInfo.PE.HasValue)
                sb.AppendLine($"- P/E Ratio: {context.Evidence.CompanyInfo.PE}");
            sb.AppendLine();
        }

        // --- Economic Indicators ---
        if (context.Evidence.EconomicIndicators.Count > 0)
        {
            sb.AppendLine("### Economic Indicators");
            foreach (var indicator in context.Evidence.EconomicIndicators)
            {
                sb.AppendLine($"- {indicator.Name}: {indicator.Value} ({indicator.Unit})");
            }
            sb.AppendLine();
        }

        // --- Currency Rates ---
        if (context.Evidence.CurrencyRates.Count > 0)
        {
            sb.AppendLine("### Currency Rates");
            foreach (var rate in context.Evidence.CurrencyRates)
            {
                sb.AppendLine($"- {rate.BaseCurrency}/{rate.QuoteCurrency}: {rate.Rate}");
            }
            sb.AppendLine();
        }

        // --- News ---
        if (context.Evidence.RecentNews.Count > 0)
        {
            sb.AppendLine("### Recent News");
            foreach (var news in context.Evidence.RecentNews.Take(10))
            {
                sb.AppendLine($"- [{news.Source}] {news.Title}");
                if (!string.IsNullOrEmpty(news.Summary))
                    sb.AppendLine($"  {news.Summary}");
            }
            sb.AppendLine();
        }

        // --- Missing Data / Warnings ---
        if (context.Evidence.MissingData.Count > 0)
        {
            sb.AppendLine("### Missing Data");
            foreach (var m in context.Evidence.MissingData)
                sb.AppendLine($"- ⚠️ {m}");
            sb.AppendLine();
        }
        if (context.Evidence.Warnings.Count > 0)
        {
            sb.AppendLine("### Data Warnings");
            foreach (var w in context.Evidence.Warnings)
                sb.AppendLine($"- ⚠️ {w}");
            sb.AppendLine();
        }

        // --- Specialist Agent Results ---
        if (context.AgentResults.Count > 0)
        {
            sb.AppendLine("---");
            sb.AppendLine("## Specialist Agent Analyses");
            sb.AppendLine();
            foreach (var agent in context.AgentResults)
            {
                sb.AppendLine($"### {agent.AgentName}");
                sb.AppendLine($"- Sentiment: {agent.Sentiment}");
                sb.AppendLine($"- Confidence: {agent.Confidence}");
                sb.AppendLine($"- Summary: {agent.Summary}");
                if (!string.IsNullOrEmpty(agent.DetailedAnalysis))
                    sb.AppendLine($"- Analysis: {agent.DetailedAnalysis}");

                if (agent.SupportingEvidence.Count > 0)
                {
                    sb.AppendLine("- Supporting evidence:");
                    foreach (var ev in agent.SupportingEvidence)
                        sb.AppendLine($"  - [{ev.Type}] {ev.Content} (Source: {ev.Source})");
                }
                if (agent.ContradictingEvidence.Count > 0)
                {
                    sb.AppendLine("- Contradicting evidence:");
                    foreach (var ev in agent.ContradictingEvidence)
                        sb.AppendLine($"  - [{ev.Type}] {ev.Content} (Source: {ev.Source})");
                }
                if (agent.IdentifiedRisks.Count > 0)
                {
                    sb.AppendLine("- Identified risks:");
                    foreach (var risk in agent.IdentifiedRisks)
                        sb.AppendLine($"  - {risk}");
                }
                if (agent.KeyLevels.Count > 0)
                {
                    sb.AppendLine("- Key levels:");
                    foreach (var level in agent.KeyLevels)
                        sb.AppendLine($"  - {level.Label}: {level.Price} (Horizon: {level.TimeHorizon})");
                }
                if (agent.InformationGaps.Count > 0)
                {
                    sb.AppendLine("- Information gaps:");
                    foreach (var gap in agent.InformationGaps)
                        sb.AppendLine($"  - {gap}");
                }
                sb.AppendLine();
            }
        }

        // --- Constraints ---
        if (context.Constraints.Count > 0)
        {
            sb.AppendLine("---");
            sb.AppendLine("## Strategy Constraints");
            foreach (var constraint in context.Constraints)
                sb.AppendLine($"- {constraint}");
            sb.AppendLine();
        }

        // --- Focus Area ---
        if (!string.IsNullOrEmpty(context.FocusArea))
        {
            sb.AppendLine("---");
            sb.AppendLine($"## Focus Area: {context.FocusArea}");
            sb.AppendLine();
        }

        // --- Task ---
        sb.AppendLine("---");
        sb.AppendLine("## Your Task");
        sb.AppendLine();
        sb.AppendLine("Synthesize all provided evidence and agent analyses into a structured strategy report.");
        sb.AppendLine("Follow the output JSON format specified in the system prompt.");
        sb.AppendLine("Reference evidence items by their source and content when supporting conclusions.");
        sb.AppendLine("Be honest about uncertainty and missing information.");
        sb.AppendLine("Do NOT invent data, prices, or indicator values not present in the context.");
        sb.AppendLine("Produce scenarios based on the evidence, not predictions.");

        return sb.ToString();
    }
}

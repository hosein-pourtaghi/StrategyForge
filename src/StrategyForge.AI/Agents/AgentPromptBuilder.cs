using System.Text;
using StrategyForge.AI.Services;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Models;

namespace StrategyForge.AI.Agents;

/// <summary>
/// Shared prompt-building utilities for specialist AI agents.
/// Provides evidence scoping and common system prompt rules.
/// Each specialist agent provides its own focused system prompt
/// but shares the evidence serialization logic.
/// </summary>
public static class AgentPromptBuilder
{
    /// <summary>
    /// Common evidence-only rules injected into every specialist agent's system prompt.
    /// </summary>
    public const string EvidenceOnlyRules = @"
## CRITICAL RULES

1. **Reason ONLY from the provided evidence.** Do not use your pre-trained knowledge as current market data.
2. **Never invent facts.** Do not fabricate prices, indicator values, financial metrics, news, or political events.
3. **Distinguish facts from interpretations.** Use EvidenceType: Fact (verified data), Calculation (deterministic indicator), Interpretation (your reasoning), Uncertain (insufficient data).
4. **Mark uncertainty explicitly.** When evidence is insufficient, say so. Do not guess.
5. **Reference evidence.** Every conclusion should reference which evidence items support it.
6. **Never produce trading signals.** You produce analysis, not BUY/SELL recommendations.
7. **Do not pretend to have real-time data.** Your analysis is limited to the evidence provided.
8. **Acknowledge missing data.** If evidence categories are absent, explicitly state what is missing.";

    /// <summary>
    /// Builds the evidence section of a user prompt from AnalysisEvidence.
    /// Scoped to only include relevant evidence categories.
    /// </summary>
    public static string BuildEvidenceSection(AnalysisEvidence evidence, EvidenceScope scope)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"# Analysis Evidence for {evidence.Asset.Symbol} ({evidence.Asset.Name})");
        sb.AppendLine($"- Market: {evidence.Asset.Market}");
        sb.AppendLine($"- Type: {evidence.Asset.AssetType}");
        if (!string.IsNullOrEmpty(evidence.Asset.Sector))
            sb.AppendLine($"- Sector: {evidence.Asset.Sector}");
        sb.AppendLine($"- Data range: {evidence.DataStartDate} to {evidence.DataEndDate}");
        sb.AppendLine();

        if (scope.IncludeMarketData)
        {
            sb.AppendLine("## Market Data");
            if (evidence.CurrentPrice.HasValue)
                sb.AppendLine($"- Current price: {evidence.CurrentPrice}");
            if (evidence.DailyChangePercent.HasValue)
                sb.AppendLine($"- Daily change: {evidence.DailyChangePercent}%");
            if (evidence.LatestVolume.HasValue)
                sb.AppendLine($"- Volume: {evidence.LatestVolume}");
            if (evidence.AverageVolume.HasValue)
                sb.AppendLine($"- Average volume: {evidence.AverageVolume}");
            if (evidence.VolumeRatio.HasValue)
                sb.AppendLine($"- Volume ratio: {evidence.VolumeRatio}x average");
            if (evidence.SupportLevels.Count > 0)
                sb.AppendLine($"- Support levels: {string.Join(", ", evidence.SupportLevels)}");
            if (evidence.ResistanceLevels.Count > 0)
                sb.AppendLine($"- Resistance levels: {string.Join(", ", evidence.ResistanceLevels)}");
            if (evidence.MarketRegime.HasValue)
                sb.AppendLine($"- Market regime: {evidence.MarketRegime}");
            if (!string.IsNullOrEmpty(evidence.PriceActionSummary))
                sb.AppendLine($"- Price action: {evidence.PriceActionSummary}");
            sb.AppendLine();
        }

        if (scope.IncludeTechnicalIndicators && evidence.IndicatorValues.Count > 0)
        {
            sb.AppendLine("## Deterministic Technical Indicators");
            sb.AppendLine("_(Calculated from market data — these are facts, not opinions.)_");
            foreach (var (name, result) in evidence.IndicatorValues)
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

        if (scope.IncludeFundamentals && evidence.CompanyInfo != null)
        {
            sb.AppendLine("## Company Fundamentals");
            var c = evidence.CompanyInfo;
            sb.AppendLine($"- Name: {c.CompanyName}");
            if (c.MarketCap.HasValue) sb.AppendLine($"- Market Cap: {c.MarketCap}");
            if (c.Eps.HasValue) sb.AppendLine($"- EPS: {c.Eps}");
            if (c.Pe.HasValue) sb.AppendLine($"- P/E Ratio: {c.Pe}");
            if (c.Pb.HasValue) sb.AppendLine($"- P/B Ratio: {c.Pb}");
            if (c.DividendYield.HasValue) sb.AppendLine($"- Dividend Yield: {c.DividendYield}%");
            if (c.Revenue.HasValue) sb.AppendLine($"- Revenue: {c.Revenue}");
            if (c.RevenueGrowth.HasValue) sb.AppendLine($"- Revenue Growth: {c.RevenueGrowth}%");
            if (c.NetProfit.HasValue) sb.AppendLine($"- Net Profit: {c.NetProfit}");
            if (c.ProfitGrowth.HasValue) sb.AppendLine($"- Profit Growth: {c.ProfitGrowth}%");
            if (c.GrossMargin.HasValue) sb.AppendLine($"- Gross Margin: {c.GrossMargin}%");
            if (c.NetMargin.HasValue) sb.AppendLine($"- Net Margin: {c.NetMargin}%");
            if (c.TotalDebt.HasValue) sb.AppendLine($"- Total Debt: {c.TotalDebt}");
            if (c.Cash.HasValue) sb.AppendLine($"- Cash: {c.Cash}");
            if (!string.IsNullOrEmpty(c.Sector)) sb.AppendLine($"- Sector: {c.Sector}");
            if (!string.IsNullOrEmpty(c.Industry)) sb.AppendLine($"- Industry: {c.Industry}");
            sb.AppendLine();
        }
        else if (scope.IncludeFundamentals && evidence.CompanyInfo == null)
        {
            sb.AppendLine("## Company Fundamentals");
            sb.AppendLine("⚠️ No fundamental data available for this instrument.");
            sb.AppendLine();
        }

        if (scope.IncludeEconomic && evidence.EconomicIndicators.Count > 0)
        {
            sb.AppendLine("## Economic Indicators");
            foreach (var indicator in evidence.EconomicIndicators)
            {
                sb.Append($"- {indicator.Name}: {indicator.Value} ({indicator.Unit ?? "N/A"})");
                if (indicator.PreviousValue.HasValue)
                    sb.Append($" [prev: {indicator.PreviousValue}]");
                if (!string.IsNullOrEmpty(indicator.Period))
                    sb.Append($" [{indicator.Period}]");
                sb.AppendLine();
            }
            sb.AppendLine();
        }

        if (scope.IncludeEconomic && evidence.CurrencyRates.Count > 0)
        {
            sb.AppendLine("## Currency Rates");
            foreach (var rate in evidence.CurrencyRates)
            {
                sb.AppendLine($"- {rate.BaseCurrency}/{rate.QuoteCurrency}: {rate.Rate}");
            }
            sb.AppendLine();
        }

        if (scope.IncludeEconomic && evidence.GoldPrices.Count > 0)
        {
            sb.AppendLine("## Gold Prices");
            foreach (var gold in evidence.GoldPrices)
            {
                sb.AppendLine($"- {gold.GoldType ?? "Gold"}: {gold.Price} {gold.Unit}");
            }
            sb.AppendLine();
        }

        if (scope.IncludeNews && evidence.RecentNews.Count > 0)
        {
            sb.AppendLine("## Recent News");
            foreach (var news in evidence.RecentNews.Take(15))
            {
                sb.AppendLine($"- [{news.Source}] {news.Title}");
                if (!string.IsNullOrEmpty(news.Content))
                    sb.AppendLine($"  {news.Content}");
                if (news.Sentiment.HasValue)
                    sb.AppendLine($"  Sentiment: {news.Sentiment}");
            }
            sb.AppendLine();
        }

        if (scope.IncludeNews && evidence.RecentNews.Count == 0)
        {
            sb.AppendLine("## News");
            sb.AppendLine("⚠️ No recent news available.");
            sb.AppendLine();
        }

        // Always include missing data and warnings
        if (evidence.MissingData.Count > 0)
        {
            sb.AppendLine("## Missing Data");
            foreach (var m in evidence.MissingData)
                sb.AppendLine($"- ⚠️ {m}");
            sb.AppendLine();
        }

        if (evidence.Warnings.Count > 0)
        {
            sb.AppendLine("## Data Warnings");
            foreach (var w in evidence.Warnings)
                sb.AppendLine($"- ⚠️ {w}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Validates common fields in an LLM JSON response for agent analysis.
    /// </summary>
    public static (string? agentName, Sentiment sentiment, decimal confidence, string summary, string? detailedAnalysis,
        IReadOnlyList<EvidenceItem> supportingEvidence, IReadOnlyList<EvidenceItem> contradictingEvidence,
        IReadOnlyList<string> identifiedRisks, IReadOnlyList<string> informationGaps,
        IReadOnlyDictionary<string, string>? agentSpecificData)?
        ValidateCommonFields(System.Text.Json.JsonElement root, string expectedAgentName)
    {
        var agentName = root.TryGetString("agentName") ?? expectedAgentName;
        var summary = root.TryGetString("summary") ?? "";
        var sentimentStr = root.TryGetString("sentiment") ?? "Neutral";
        var confidence = ClampConfidence(root.TryGetDecimal("confidence") ?? 0.5m);

        var sentiment = sentimentStr.ToLowerInvariant() switch
        {
            "bullish" => Sentiment.Bullish,
            "bearish" => Sentiment.Bearish,
            "neutral" => Sentiment.Neutral,
            _ => Sentiment.Unknown
        };

        var detailedAnalysis = root.TryGetString("detailedAnalysis");
        var supportingEvidence = ParseEvidenceItems(root, "supportingEvidence");
        var contradictingEvidence = ParseEvidenceItems(root, "contradictingEvidence");
        var identifiedRisks = root.TryGetArrayStrings("identifiedRisks");
        var informationGaps = root.TryGetArrayStrings("informationGaps");

        IReadOnlyDictionary<string, string>? agentSpecificData = null;
        if (root.TryGetProperty("agentSpecificData", out var asdProp) && asdProp.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            var dict = new Dictionary<string, string>();
            foreach (var prop in asdProp.EnumerateObject())
            {
                dict[prop.Name] = prop.Value.GetString() ?? "";
            }
            agentSpecificData = dict;
        }

        return (agentName, sentiment, confidence, summary, detailedAnalysis,
            supportingEvidence, contradictingEvidence, identifiedRisks, informationGaps, agentSpecificData);
    }

    /// <summary>
    /// Builds the complete user prompt combining evidence section and task instruction.
    /// </summary>
    public static string BuildUserPrompt(AnalysisEvidence evidence, EvidenceScope scope, string taskInstruction, string? additionalContext = null)
    {
        var sb = new StringBuilder();
        sb.Append(BuildEvidenceSection(evidence, scope));

        if (!string.IsNullOrEmpty(additionalContext))
        {
            sb.AppendLine("---");
            sb.AppendLine(additionalContext);
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine("## Your Task");
        sb.AppendLine();
        sb.AppendLine(taskInstruction);
        sb.AppendLine();
        sb.AppendLine("Return your analysis as valid JSON matching the output format specified in the system prompt.");
        sb.AppendLine("Every conclusion must reference supporting evidence. Do not fabricate data.");

        return sb.ToString();
    }

    // --- Internal Helpers ---

    private static decimal ClampConfidence(decimal value)
    {
        if (value < 0m) return 0m;
        if (value > 1m) return 1m;
        return Math.Round(value, 2);
    }

    private static IReadOnlyList<EvidenceItem> ParseEvidenceItems(System.Text.Json.JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var prop) || prop.ValueKind != System.Text.Json.JsonValueKind.Array)
            return [];

        var items = new List<EvidenceItem>();
        foreach (var item in prop.EnumerateArray())
        {
            if (item.ValueKind != System.Text.Json.JsonValueKind.Object) continue;

            var content = item.TryGetString("content") ?? "";
            var typeStr = item.TryGetString("type") ?? "Interpretation";
            var source = item.TryGetString("source") ?? "Agent Analysis";

            var evidenceType = typeStr.ToLowerInvariant() switch
            {
                "fact" => EvidenceType.Fact,
                "calculation" => EvidenceType.Calculation,
                "interpretation" => EvidenceType.Interpretation,
                "scenario" => EvidenceType.Scenario,
                "uncertain" => EvidenceType.Uncertain,
                _ => EvidenceType.Interpretation
            };

            items.Add(new EvidenceItem
            {
                Content = content,
                Type = evidenceType,
                Source = source,
                Confidence = ClampConfidence(item.TryGetDecimal("confidence") ?? 0.5m)
            });
        }

        return items;
    }
}

/// <summary>
/// Defines which evidence categories an agent requires.
/// Used for evidence scoping — each specialist receives only relevant evidence.
/// </summary>
public sealed class EvidenceScope
{
    public bool IncludeMarketData { get; init; } = true;
    public bool IncludeTechnicalIndicators { get; init; }
    public bool IncludeFundamentals { get; init; }
    public bool IncludeEconomic { get; init; }
    public bool IncludeNews { get; init; }

    public static EvidenceScope Technical { get; } = new()
    {
        IncludeMarketData = true,
        IncludeTechnicalIndicators = true
    };

    public static EvidenceScope Fundamental { get; } = new()
    {
        IncludeMarketData = true,
        IncludeFundamentals = true
    };

    public static EvidenceScope Macro { get; } = new()
    {
        IncludeMarketData = true,
        IncludeEconomic = true
    };

    public static EvidenceScope News { get; } = new()
    {
        IncludeMarketData = true,
        IncludeNews = true
    };

    public static EvidenceScope PoliticalRisk { get; } = new()
    {
        IncludeMarketData = true,
        IncludeEconomic = true,
        IncludeNews = true
    };

    public static EvidenceScope Risk { get; } = new()
    {
        IncludeMarketData = true,
        IncludeTechnicalIndicators = true,
        IncludeFundamentals = true,
        IncludeEconomic = true,
        IncludeNews = true
    };
}

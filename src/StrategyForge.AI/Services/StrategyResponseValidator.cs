using System.Text.Json;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Models;

namespace StrategyForge.AI.Services;

/// <summary>
/// Validates and parses the LLM's structured strategy synthesis output into a StrategyReport.
/// 
/// Validation checks:
/// - Valid JSON
/// - Required strategy fields present
/// - Supported enum values (Sentiment, MarketRegime, etc.)
/// - Valid numeric ranges (confidence 0.0-1.0)
/// - Evidence items have required fields
/// - Scenarios are structurally valid
/// - Risk assessment is present
/// - Confidence assessment is present
/// 
/// Invalid LLM responses never silently become valid-looking strategies.
/// </summary>
public sealed class StrategyResponseValidator
{
    /// <summary>
    /// Validates and parses an LLM response into a StrategyReport.
    /// </summary>
    public StrategyValidationResult Validate(
        LlmResponse response,
        Asset asset,
        DateTimeOffset generatedAt)
    {
        if (!response.Success)
        {
            return StrategyValidationResult.Failure(
                $"LLM request failed: {response.Error}");
        }

        if (string.IsNullOrWhiteSpace(response.Content))
        {
            return StrategyValidationResult.Failure(
                "LLM returned empty content");
        }

        try
        {
            var doc = JsonDocument.Parse(response.Content);
            var root = doc.RootElement;

            // Validate and build ExecutiveSummary
            if (!root.TryGetProperty("executiveSummary", out var execSummaryProp))
            {
                return StrategyValidationResult.Failure(
                    "Missing required field: executiveSummary");
            }

            var executiveSummary = ParseExecutiveSummary(execSummaryProp);
            if (executiveSummary == null)
            {
                return StrategyValidationResult.Failure(
                    "Invalid executiveSummary: missing required fields (overallSentiment, summary)");
            }

            // Validate and build MarketContext
            var marketContext = ParseMarketContext(root);

            // Build the StrategyReport
            var report = new StrategyReport
            {
                Asset = asset,
                GeneratedAt = generatedAt,
                DataAsOf = DateTimeOffset.UtcNow,
                ExecutiveSummary = executiveSummary,
                MarketContext = marketContext
            };

            // Parse optional sections
            if (root.TryGetProperty("technicalAnalysis", out var techProp) && techProp.ValueKind != JsonValueKind.Null)
            {
                var techAnalysis = ParseAgentAnalysisResult(techProp);
                if (techAnalysis != null)
                    report = report with { TechnicalAnalysis = techAnalysis };
            }

            if (root.TryGetProperty("fundamentalAnalysis", out var fundProp) && fundProp.ValueKind != JsonValueKind.Null)
            {
                var fundAnalysis = ParseAgentAnalysisResult(fundProp);
                if (fundAnalysis != null)
                    report = report with { FundamentalAnalysis = fundAnalysis };
            }

            if (root.TryGetProperty("macroAnalysis", out var macroProp) && macroProp.ValueKind != JsonValueKind.Null)
            {
                var macroAnalysis = ParseAgentAnalysisResult(macroProp);
                if (macroAnalysis != null)
                    report = report with { MacroAnalysis = macroAnalysis };
            }

            if (root.TryGetProperty("newsAnalysis", out var newsProp) && newsProp.ValueKind != JsonValueKind.Null)
            {
                var newsAnalysis = ParseAgentAnalysisResult(newsProp);
                if (newsAnalysis != null)
                    report = report with { NewsAnalysis = newsAnalysis };
            }

            if (root.TryGetProperty("politicalRiskAnalysis", out var polProp) && polProp.ValueKind != JsonValueKind.Null)
            {
                var polAnalysis = ParseAgentAnalysisResult(polProp);
                if (polAnalysis != null)
                    report = report with { PoliticalRiskAnalysis = polAnalysis };
            }

            if (root.TryGetProperty("riskAnalysis", out var riskProp) && riskProp.ValueKind != JsonValueKind.Null)
            {
                var riskAnalysis = ParseAgentAnalysisResult(riskProp);
                if (riskAnalysis != null)
                    report = report with { RiskAnalysis = riskAnalysis };
            }

            // Parse scenarios
            if (root.TryGetProperty("bullishScenario", out var bullProp) && bullProp.ValueKind != JsonValueKind.Null)
            {
                var bullish = ParseScenario(bullProp);
                if (bullish != null)
                    report = report with { BullishScenario = bullish };
            }

            if (root.TryGetProperty("baseScenario", out var baseProp) && baseProp.ValueKind != JsonValueKind.Null)
            {
                var baseline = ParseScenario(baseProp);
                if (baseline != null)
                    report = report with { BaseScenario = baseline };
            }

            if (root.TryGetProperty("bearishScenario", out var bearProp) && bearProp.ValueKind != JsonValueKind.Null)
            {
                var bearish = ParseScenario(bearProp);
                if (bearish != null)
                    report = report with { BearishScenario = bearish };
            }

            // Parse strategy sections
            if (root.TryGetProperty("shortTermStrategy", out var stProp) && stProp.ValueKind != JsonValueKind.Null)
            {
                var st = ParseStrategySection(stProp, TimeHorizon.ShortTerm);
                if (st != null)
                    report = report with { ShortTermStrategy = st };
            }

            if (root.TryGetProperty("mediumTermStrategy", out var mtProp) && mtProp.ValueKind != JsonValueKind.Null)
            {
                var mt = ParseStrategySection(mtProp, TimeHorizon.MediumTerm);
                if (mt != null)
                    report = report with { MediumTermStrategy = mt };
            }

            if (root.TryGetProperty("longTermStrategy", out var ltProp) && ltProp.ValueKind != JsonValueKind.Null)
            {
                var lt = ParseStrategySection(ltProp, TimeHorizon.LongTerm);
                if (lt != null)
                    report = report with { LongTermStrategy = lt };
            }

            // Parse risk/reward
            if (root.TryGetProperty("riskReward", out var rrProp) && rrProp.ValueKind != JsonValueKind.Null)
            {
                var riskReward = ParseRiskRewardAssessment(rrProp);
                if (riskReward != null)
                    report = report with { RiskReward = riskReward };
            }

            // Parse confidence
            if (root.TryGetProperty("confidence", out var confProp) && confProp.ValueKind != JsonValueKind.Null)
            {
                var confidence = ParseConfidenceAssessment(confProp);
                if (confidence != null)
                    report = report with { Confidence = confidence };
            }

            // Parse evidence lists
            if (root.TryGetProperty("supportingEvidence", out var suppEvProp) && suppEvProp.ValueKind == JsonValueKind.Array)
            {
                var evidence = ParseEvidenceList(suppEvProp);
                report = report with { SupportingEvidence = evidence };
            }

            if (root.TryGetProperty("contradictingEvidence", out var contEvProp) && contEvProp.ValueKind == JsonValueKind.Array)
            {
                var evidence = ParseEvidenceList(contEvProp);
                report = report with { ContradictingEvidence = evidence };
            }

            if (root.TryGetProperty("missingInformation", out var missProp) && missProp.ValueKind == JsonValueKind.Array)
            {
                report = report with { MissingInformation = missProp.TryGetArrayStrings() };
            }

            if (root.TryGetProperty("invalidationConditions", out var invProp) && invProp.ValueKind == JsonValueKind.Array)
            {
                report = report with { InvalidationConditions = invProp.TryGetArrayStrings() };
            }

            if (root.TryGetProperty("monitoringRecommendations", out var monProp) && monProp.ValueKind == JsonValueKind.Array)
            {
                report = report with { MonitoringRecommendations = monProp.TryGetArrayStrings() };
            }

            return StrategyValidationResult.Success(report);
        }
        catch (JsonException ex)
        {
            return StrategyValidationResult.Failure(
                $"Invalid JSON from LLM: {ex.Message}");
        }
        catch (Exception ex)
        {
            return StrategyValidationResult.Failure(
                $"Failed to parse strategy: {ex.Message}");
        }
    }

    // --- Private Parsing Methods ---

    private static ExecutiveSummary? ParseExecutiveSummary(JsonElement prop)
    {
        if (prop.ValueKind != JsonValueKind.Object)
            return null;

        var sentimentStr = prop.TryGetString("overallSentiment");
        var summary = prop.TryGetString("summary");

        if (sentimentStr == null || summary == null)
            return null;

        if (!TryParseSentiment(sentimentStr, out var sentiment))
            return null;

        return new ExecutiveSummary
        {
            OverallSentiment = sentiment,
            Summary = summary,
            KeyTakeaway = prop.TryGetString("keyTakeaway"),
            CriticalLevel = prop.TryGetString("criticalLevel"),
            Urgency = prop.TryGetString("urgency")
        };
    }

    private static MarketContext ParseMarketContext(JsonElement root)
    {
        if (!root.TryGetProperty("marketContext", out var mcProp) || mcProp.ValueKind != JsonValueKind.Object)
        {
            return new MarketContext
            {
                Regime = MarketRegime.Unknown,
                Description = "Market context not available"
            };
        }

        var regimeStr = mcProp.TryGetString("regime") ?? "Unknown";
        TryParseMarketRegime(regimeStr, out var regime);

        return new MarketContext
        {
            Regime = regime,
            Description = mcProp.TryGetString("description") ?? "No description",
            CurrentPrice = mcProp.TryGetDecimal("currentPrice"),
            RecentPriceChange = mcProp.TryGetDecimal("recentPriceChange"),
            VolumeContext = mcProp.TryGetString("volumeContext"),
            MacroContext = mcProp.TryGetString("macroContext"),
            UpcomingEvents = mcProp.TryGetArrayStrings("upcomingEvents")
        };
    }

    private static AgentAnalysisResult? ParseAgentAnalysisResult(JsonElement prop)
    {
        if (prop.ValueKind != JsonValueKind.Object)
            return null;

        var agentName = prop.TryGetString("agentName") ?? "Unknown";
        var summary = prop.TryGetString("summary") ?? "";
        var sentimentStr = prop.TryGetString("sentiment") ?? "Neutral";

        if (!TryParseSentiment(sentimentStr, out var sentiment))
            sentiment = Sentiment.Neutral;

        var confidence = ClampConfidence(prop.TryGetDecimal("confidence") ?? 0.5m);

        return new AgentAnalysisResult
        {
            AgentName = agentName,
            AssetSymbol = "",
            GeneratedAt = DateTimeOffset.UtcNow,
            Sentiment = sentiment,
            Confidence = confidence,
            Summary = summary,
            DetailedAnalysis = prop.TryGetString("detailedAnalysis"),
            SupportingEvidence = ParseEvidenceItems(prop, "supportingEvidence"),
            ContradictingEvidence = ParseEvidenceItems(prop, "contradictingEvidence"),
            IdentifiedRisks = prop.TryGetArrayStrings("identifiedRisks"),
            InformationGaps = prop.TryGetArrayStrings("informationGaps")
        };
    }

    private static Scenario? ParseScenario(JsonElement prop)
    {
        if (prop.ValueKind != JsonValueKind.Object)
            return null;

        var name = prop.TryGetString("name");
        var description = prop.TryGetString("description");

        if (name == null || description == null)
            return null;

        return new Scenario
        {
            Name = name,
            Description = description,
            Assumptions = prop.TryGetArrayStrings("assumptions"),
            SupportingEvidence = ParseEvidenceItems(prop, "supportingEvidence"),
            WeakeningEvidence = ParseEvidenceItems(prop, "weakeningEvidence"),
            ProbabilityAssessment = prop.TryGetString("probabilityAssessment"),
            ExpectedOutcome = prop.TryGetString("expectedOutcome"),
            ConfirmationConditions = prop.TryGetArrayStrings("confirmationConditions"),
            InvalidationConditions = prop.TryGetArrayStrings("invalidationConditions")
        };
    }

    private static StrategySection? ParseStrategySection(JsonElement prop, TimeHorizon defaultHorizon)
    {
        if (prop.ValueKind != JsonValueKind.Object)
            return null;

        var horizonStr = prop.TryGetString("timeHorizon");
        var horizon = defaultHorizon;
        if (horizonStr != null)
            TryParseTimeHorizon(horizonStr, out horizon);

        return new StrategySection
        {
            TimeHorizon = horizon,
            EntryScenario = prop.TryGetString("entryScenario"),
            EntryZones = prop.TryGetArrayStrings("entryZones"),
            ConfirmationConditions = prop.TryGetArrayStrings("confirmationConditions"),
            StopInvalidation = prop.TryGetString("stopInvalidation"),
            TargetLevels = prop.TryGetArrayStrings("targetLevels"),
            ExitConditions = prop.TryGetString("exitConditions"),
            RiskAssessment = prop.TryGetString("riskAssessment"),
            MonitoringActions = prop.TryGetArrayStrings("monitoringActions")
        };
    }

    private static RiskRewardAssessment? ParseRiskRewardAssessment(JsonElement prop)
    {
        if (prop.ValueKind != JsonValueKind.Object)
            return null;

        return new RiskRewardAssessment
        {
            PotentialUpside = prop.TryGetString("potentialUpside"),
            PotentialDownside = prop.TryGetString("potentialDownside"),
            RiskRewardRatio = prop.TryGetString("riskRewardRatio"),
            RiskLevel = prop.TryGetString("riskLevel"),
            KeyRiskFactors = prop.TryGetArrayStrings("keyRiskFactors"),
            FavorableFactors = prop.TryGetArrayStrings("favorableFactors"),
            UnfavorableFactors = prop.TryGetArrayStrings("unfavorableFactors")
        };
    }

    private static ConfidenceAssessment? ParseConfidenceAssessment(JsonElement prop)
    {
        if (prop.ValueKind != JsonValueKind.Object)
            return null;

        var overall = ClampConfidence(prop.TryGetDecimal("overallConfidence") ?? 0.5m);
        var level = prop.TryGetString("level") ?? "Unknown";

        return new ConfidenceAssessment
        {
            OverallConfidence = overall,
            Level = level,
            ConfidenceFactors = prop.TryGetArrayStrings("confidenceFactors"),
            UncertaintyFactors = prop.TryGetArrayStrings("uncertaintyFactors"),
            InformationThatWouldHelp = prop.TryGetArrayStrings("informationThatWouldHelp"),
            DataSourcesUsed = (int)(prop.TryGetDecimal("dataSourcesUsed") ?? 0),
            AgentsContributed = (int)(prop.TryGetDecimal("agentsContributed") ?? 0)
        };
    }

    private static IReadOnlyList<EvidenceItem> ParseEvidenceItems(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var prop) || prop.ValueKind != JsonValueKind.Array)
            return [];

        var items = new List<EvidenceItem>();
        foreach (var item in prop.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;

            var content = item.TryGetString("content") ?? "";
            var typeStr = item.TryGetString("type") ?? "Interpretation";
            var source = item.TryGetString("source") ?? "LLM Analysis";

            if (!TryParseEvidenceType(typeStr, out var evidenceType))
                evidenceType = EvidenceType.Interpretation;

            items.Add(new EvidenceItem
            {
                Content = content,
                Type = evidenceType,
                Source = source,
                Timestamp = DateTimeOffset.UtcNow,
                Confidence = ClampConfidence(item.TryGetDecimal("confidence") ?? 0.5m)
            });
        }

        return items;
    }

    private static IReadOnlyList<EvidenceItem> ParseEvidenceList(JsonElement prop)
    {
        if (prop.ValueKind != JsonValueKind.Array)
            return [];

        var items = new List<EvidenceItem>();
        foreach (var item in prop.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;

            var content = item.TryGetString("content") ?? "";
            var typeStr = item.TryGetString("type") ?? "Interpretation";
            var source = item.TryGetString("source") ?? "Unknown";

            if (!TryParseEvidenceType(typeStr, out var evidenceType))
                evidenceType = EvidenceType.Interpretation;

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

    // --- Enum Parsers ---

    private static bool TryParseSentiment(string value, out Sentiment result)
    {
        result = value.ToLowerInvariant() switch
        {
            "bullish" => Sentiment.Bullish,
            "bearish" => Sentiment.Bearish,
            "neutral" => Sentiment.Neutral,
            _ => Sentiment.Unknown
        };
        return result != Sentiment.Unknown || value.Equals("Unknown", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseMarketRegime(string value, out MarketRegime result)
    {
        result = value.ToLowerInvariant() switch
        {
            "uptrend" => MarketRegime.Uptrend,
            "downtrend" => MarketRegime.Downtrend,
            "sideways" => MarketRegime.Sideways,
            "volatile" => MarketRegime.Volatile,
            "transitional" => MarketRegime.Transitional,
            _ => MarketRegime.Unknown
        };
        return result != MarketRegime.Unknown || value.Equals("Unknown", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseEvidenceType(string value, out EvidenceType result)
    {
        result = value.ToLowerInvariant() switch
        {
            "fact" => EvidenceType.Fact,
            "calculation" => EvidenceType.Calculation,
            "interpretation" => EvidenceType.Interpretation,
            "scenario" => EvidenceType.Scenario,
            "uncertain" => EvidenceType.Uncertain,
            _ => EvidenceType.Interpretation
        };
        return true;
    }

    private static bool TryParseTimeHorizon(string value, out TimeHorizon result)
    {
        result = value.ToLowerInvariant() switch
        {
            "shortterm" or "short_term" or "short term" => TimeHorizon.ShortTerm,
            "mediumterm" or "medium_term" or "medium term" => TimeHorizon.MediumTerm,
            "longterm" or "long_term" or "long term" => TimeHorizon.LongTerm,
            _ => TimeHorizon.ShortTerm
        };
        return true;
    }

    private static decimal ClampConfidence(decimal value)
    {
        if (value < 0m) return 0m;
        if (value > 1m) return 1m;
        return Math.Round(value, 2);
    }
}

/// <summary>
/// Result of validating a strategy synthesis LLM response.
/// </summary>
public sealed class StrategyValidationResult
{
    public bool IsValid { get; init; }
    public string? ErrorMessage { get; init; }
    public StrategyReport? Report { get; init; }

    public static StrategyValidationResult Success(StrategyReport report) => new()
    {
        IsValid = true,
        Report = report
    };

    public static StrategyValidationResult Failure(string errorMessage) => new()
    {
        IsValid = false,
        ErrorMessage = errorMessage
    };
}

/// <summary>
/// Additional JsonElement extension methods for strategy parsing.
/// </summary>
internal static class StrategyJsonExtensions
{
    public static IReadOnlyList<string> TryGetArrayStrings(this JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop) || prop.ValueKind != JsonValueKind.Array)
            return [];

        return prop.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString() ?? "")
            .ToList();
    }
}

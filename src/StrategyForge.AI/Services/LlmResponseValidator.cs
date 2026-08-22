using System.Text.Json;
using StrategyForge.Domain.Models;

namespace StrategyForge.AI.Services;

/// <summary>
/// Validates structured LLM output. Ensures JSON is valid,
/// required fields exist, and data types are correct.
/// </summary>
public sealed class LlmResponseValidator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Validates and parses an LLM response into a structured result.
    /// Returns a validation result indicating success or specific failures.
    /// </summary>
    public LlmValidationResult Validate(LlmResponse response)
    {
        if (!response.Success)
        {
            return new LlmValidationResult
            {
                IsValid = false,
                ErrorMessage = $"LLM request failed: {response.Error}"
            };
        }

        if (string.IsNullOrWhiteSpace(response.Content))
        {
            return new LlmValidationResult
            {
                IsValid = false,
                ErrorMessage = "LLM returned empty content"
            };
        }

        try
        {
            var doc = JsonDocument.Parse(response.Content);
            var root = doc.RootElement;

            var result = new LlmInterpretationResult();

            // Parse summary (required)
            if (root.TryGetProperty("summary", out var summaryProp) && summaryProp.ValueKind == JsonValueKind.String)
                result.Summary = summaryProp.GetString() ?? "";
            else
                return new LlmValidationResult { IsValid = false, ErrorMessage = "Missing required field: summary" };

            // Parse observations (optional)
            if (root.TryGetProperty("observations", out var obsProp) && obsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var obs in obsProp.EnumerateArray())
                {
                    result.Observations.Add(new LlmObservation
                    {
                        Category = obs.TryGetString("category"),
                        Statement = obs.TryGetString("statement") ?? "",
                        EvidenceType = obs.TryGetString("evidenceType"),
                        IndicatorName = obs.TryGetString("indicatorName")
                    });
                }
            }

            // Parse interpretations (optional)
            if (root.TryGetProperty("interpretations", out var interpProp) && interpProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var interp in interpProp.EnumerateArray())
                {
                    result.Interpretations.Add(new LlmInterpretation
                    {
                        Topic = interp.TryGetString("topic"),
                        Analysis = interp.TryGetString("analysis") ?? "",
                        Confidence = interp.TryGetDecimal("confidence") ?? 0.5m,
                        BasedOn = interp.TryGetStringList("basedOn")
                    });
                }
            }

            // Parse uncertainties (optional)
            if (root.TryGetProperty("uncertainties", out var uncProp) && uncProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var unc in uncProp.EnumerateArray())
                {
                    result.Uncertainties.Add(new LlmUncertainty
                    {
                        Topic = unc.TryGetString("topic"),
                        Reason = unc.TryGetString("reason"),
                        WhatWouldHelp = unc.TryGetString("whatWouldHelp")
                    });
                }
            }

            // Parse warnings (optional)
            if (root.TryGetProperty("warnings", out var warnProp) && warnProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var w in warnProp.EnumerateArray())
                {
                    if (w.ValueKind == JsonValueKind.String)
                        result.Warnings.Add(w.GetString() ?? "");
                }
            }

            return new LlmValidationResult
            {
                IsValid = true,
                ParsedResult = result
            };
        }
        catch (JsonException ex)
        {
            return new LlmValidationResult
            {
                IsValid = false,
                ErrorMessage = $"Invalid JSON from LLM: {ex.Message}"
            };
        }
    }
}

// --- Extension helpers ---

internal static class JsonElementExtensions
{
    public static string? TryGetString(this JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;
    }

    public static decimal? TryGetDecimal(this JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.Number
            ? prop.GetDecimal()
            : null;
    }

    public static IReadOnlyList<string> TryGetStringList(this JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop) || prop.ValueKind != JsonValueKind.Array)
            return [];
        return prop.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString() ?? "")
            .ToList();
    }
}

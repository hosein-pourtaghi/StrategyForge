using System.Security.Cryptography;
using System.Text;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Models;

namespace StrategyForge.Domain.Models;

/// <summary>
/// Deterministic dataset identity (Step 19): an application-level fingerprint over
/// configuration, feature policy, range, and upstream pipeline versions.
/// Same dataset + same configuration → same DatasetId/Version (reproducibility).
/// No Git-LFS/DVC/object storage — versioning stays application-level.
/// </summary>
public static class AiReadyDatasetFingerprint
{
    /// <summary>Short hash prefix length.</summary>
    private const int PrefixLength = 12;

    /// <summary>
    /// Builds the deterministic dataset id and version string.
    /// </summary>
    public static (string DatasetId, string DatasetVersion) Compute(
        string instrumentId,
        SourceAdapterType source,
        DateOnly from,
        DateOnly to,
        int contextWindowObservations,
        IReadOnlyList<int> forwardHorizons,
        string processingPipelineVersion,
        string preparationPipelineVersion)
    {
        var horizons = string.Join(",", forwardHorizons);
        var payload = string.Join("|",
            instrumentId,
            source.ToString(),
            from.ToString("yyyy-MM-dd"),
            to.ToString("yyyy-MM-dd"),
            contextWindowObservations,
            horizons,
            string.Join(",", AiReadyFeaturePolicy.IncludedFeatures),
            processingPipelineVersion,
            preparationPipelineVersion);

        var hash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(payload)))
            .ToLowerInvariant();

        var id = $"ai-ready-{instrumentId}-{source}-{from:yyyyMMdd}-{to:yyyyMMdd}"
            .ToLowerInvariant();
        var version = $"v1-{hash[..PrefixLength]}";

        return (id, version);
    }
}

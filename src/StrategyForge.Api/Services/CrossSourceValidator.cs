using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StrategyForge.Domain.Configuration;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Interfaces.Providers;
using StrategyForge.Domain.Models;

namespace StrategyForge.Api.Services;

/// <summary>
/// Result of a cross-source comparison, flattened into the primary
/// DataResult flow: the primary data plus a validation verdict and,
/// when the secondary source was queried, its observations.
/// </summary>
public sealed record CrossValidatedSnapshot
{
    public required DataResult<Candle> PrimaryResult { get; init; }

    public required CrossValidationResult Validation { get; init; }

    /// <summary>The secondary observation, when a comparison actually happened.</summary>
    public Candle? SecondaryCandle { get; init; }
}

public sealed record CrossValidatedCandles
{
    public required DataResult<IReadOnlyList<Candle>> PrimaryResult { get; init; }

    public required CrossValidationResult Validation { get; init; }

    /// <summary>Secondary observations returned by the secondary source, when it was queried.</summary>
    public IReadOnlyList<Candle> SecondaryCandles { get; init; } = [];
}

/// <summary>
/// Cross-source market-data validation: compares compatible independent
/// sources for the same instrument and observation date to detect
/// discrepancies before downstream analysis consumes the data.
///
/// Responsibilities — data consistency and quality only:
/// - verifies instrument identity (canonical identity semantics; provider
///   identifiers may differ and are never used to match instruments)
/// - verifies observations refer to the same trading date
/// - compares applicable numeric fields within a configured tolerance
/// - classifies outcomes as Consistent / Discrepancy / Incomplete /
///   Unavailable / Invalid / NotAttempted (see CrossValidationStatus)
///
/// Explicitly out of scope: buy/sell signals, entry/TP/SL, forecasts,
/// strategy recommendations. Never merges or averages prices — the
/// primary source always remains canonical.
///
/// Availability semantics:
/// - a provider that legitimately does not expose an optional field
///   (e.g. TGJU does not provide volume in its verified response) is NOT
///   a provider failure and never a discrepancy on its own;
/// - a missing required price or date marks the comparison Invalid —
///   validation never manufactures confidence from incomplete data.
///
/// The comparison core (CompareSnapshot / CompareCandleSets) is a pure
/// function of its inputs: deterministic and unit-testable without any
/// adapter or network.
/// </summary>
public sealed class CrossSourceValidator
{
    private readonly IDataSourceRegistry _registry;
    private readonly ILogger<CrossSourceValidator> _logger;
    private readonly CrossValidationSettings _settings;

    public CrossSourceValidator(
        IDataSourceRegistry registry,
        IOptions<DataSourceSettings> settings,
        ILogger<CrossSourceValidator> logger)
    {
        _registry = registry;
        _logger = logger;
        _settings = settings.Value.CrossValidation;
    }

    /// <summary>
    /// Fetches the same observation from a secondary source and compares it
    /// with the primary snapshot. The primary result is returned unchanged
    /// as the canonical data; only a warning may be attached on discrepancy.
    /// </summary>
    public async Task<CrossValidatedSnapshot> ValidateSnapshotAsync(
        DataResult<Candle> primaryResult,
        InstrumentMapping instrument,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabledFor(MarketDataType.Snapshot) || !primaryResult.Ok || primaryResult.Data == null)
        {
            return new CrossValidatedSnapshot
            {
                PrimaryResult = primaryResult,
                Validation = NotAttempted(
                    primaryResult.Ok ? primaryResult.Data?.Provenance?.Source : null,
                    instrument.InstrumentId)
            };
        }

        var primary = primaryResult.Data;

        if (!HasRequiredFields(primary))
        {
            _logger.LogWarning(
                "Cross-validation skipped: primary snapshot from {Source} for {Symbol} is missing required fields",
                primary.Provenance?.Source, instrument.Symbol);
            return new CrossValidatedSnapshot
            {
                PrimaryResult = primaryResult,
                Validation = new CrossValidationResult
                {
                    Status = CrossValidationStatus.Invalid,
                    InstrumentId = instrument.InstrumentId,
                    PrimarySource = primary.Provenance?.Source,
                    Reasons = ["Primary observation is missing required price or date fields"]
                }
            };
        }

        var secondaryAdapter = FindSecondaryAdapter(instrument, primary.Provenance?.Source);
        if (secondaryAdapter == null)
        {
            _logger.LogDebug("No secondary source available for cross-validation of {Symbol}", instrument.Symbol);
            return new CrossValidatedSnapshot
            {
                PrimaryResult = primaryResult,
                Validation = NotAttempted(primary.Provenance?.Source, instrument.InstrumentId)
            };
        }

        _logger.LogInformation(
            "Cross-validating {Symbol} snapshot: primary={Primary}, secondary={Secondary}",
            instrument.Symbol, primary.Provenance?.Source, secondaryAdapter.SourceType);

        var secondaryResult = await secondaryAdapter.GetLatestCandleAsync(instrument, cancellationToken);
        var verdict = CompareSnapshot(primary, secondaryResult, instrument, secondaryAdapter.SourceType);

        return new CrossValidatedSnapshot
        {
            PrimaryResult = AddWarningIfNeeded(primaryResult, verdict),
            Validation = verdict,
            SecondaryCandle = secondaryResult.Ok ? secondaryResult.Data : null
        };
    }

    /// <summary>
    /// Fetches the same observation period from a secondary source and compares
    /// it with the primary candle set, trading date by trading date.
    /// </summary>
    public async Task<CrossValidatedCandles> ValidateCandlesAsync(
        DataResult<IReadOnlyList<Candle>> primaryResult,
        InstrumentMapping instrument,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabledFor(MarketDataType.HistoricalCandles) ||
            !primaryResult.Ok || primaryResult.Data == null || primaryResult.Data.Count == 0)
        {
            return new CrossValidatedCandles
            {
                PrimaryResult = primaryResult,
                Validation = NotAttempted(
                    primaryResult.Ok && primaryResult.Data is { Count: > 0 }
                        ? primaryResult.Data[0].Provenance?.Source
                        : null,
                    instrument.InstrumentId)
            };
        }

        var primaryCandles = primaryResult.Data;

        var missingRequired = primaryCandles.Where(c => !HasRequiredFields(c)).ToList();
        if (missingRequired.Count > 0)
        {
            _logger.LogWarning(
                "Cross-validation skipped: {Count} primary candles for {Symbol} are missing required fields",
                missingRequired.Count, instrument.Symbol);
            return new CrossValidatedCandles
            {
                PrimaryResult = primaryResult,
                Validation = new CrossValidationResult
                {
                    Status = CrossValidationStatus.Invalid,
                    InstrumentId = instrument.InstrumentId,
                    PrimarySource = primaryCandles[0].Provenance?.Source,
                    ObservationDate = missingRequired[0].Date,
                    Reasons = [$"{missingRequired.Count} primary candles are missing required price or date fields"]
                }
            };
        }

        var secondaryAdapter = FindSecondaryAdapter(instrument, primaryCandles[0].Provenance?.Source);
        if (secondaryAdapter == null)
        {
            _logger.LogDebug("No secondary source for cross-validation of {Symbol} candles", instrument.Symbol);
            return new CrossValidatedCandles
            {
                PrimaryResult = primaryResult,
                Validation = NotAttempted(primaryCandles[0].Provenance?.Source, instrument.InstrumentId)
            };
        }

        var from = primaryCandles.Min(c => c.Date);
        var to = primaryCandles.Max(c => c.Date);

        _logger.LogInformation(
            "Cross-validating {Symbol} candles {From}..{To}: primary={Primary}, secondary={Secondary}",
            instrument.Symbol, from, to, primaryCandles[0].Provenance?.Source, secondaryAdapter.SourceType);

        var secondaryResult = await secondaryAdapter.GetHistoricalCandlesAsync(
            instrument, from, to, null, cancellationToken);
        var verdict = CompareCandleSets(primaryCandles, secondaryResult, instrument, secondaryAdapter.SourceType);

        return new CrossValidatedCandles
        {
            PrimaryResult = AddWarningIfNeeded(primaryResult, verdict),
            Validation = verdict,
            SecondaryCandles = secondaryResult.Ok ? secondaryResult.Data ?? [] : []
        };
    }

    /// <summary>
    /// Whether cross-validation is enabled for the given data type.
    /// </summary>
    public bool IsEnabledFor(MarketDataType dataType)
    {
        if (!_settings.Enabled) return false;
        if (_settings.EnabledDataTypes.Count == 0) return true;
        return _settings.EnabledDataTypes.Contains(dataType.ToString());
    }

    // ============================================================
    // Comparison core (pure, deterministic, no I/O)
    // ============================================================

    /// <summary>
    /// Compares a primary snapshot with a secondary fetch result and produces
    /// a verdict. Pure function of its inputs.
    /// </summary>
    public CrossValidationResult CompareSnapshot(
        Candle primary,
        DataResult<Candle> secondaryResult,
        InstrumentMapping instrument,
        SourceAdapterType secondaryType)
    {
        if (!secondaryResult.Ok || secondaryResult.Data == null)
        {
            return new CrossValidationResult
            {
                Status = CrossValidationStatus.Incomplete,
                InstrumentId = instrument.InstrumentId,
                PrimarySource = primary.Provenance?.Source,
                SecondarySource = secondaryType,
                ObservationDate = primary.Date,
                PrimaryPrice = PositiveOrNull(primary.Close),
                Reasons = [$"Secondary source {secondaryType} unavailable: {DescribeSecondaryError(secondaryResult)}"]
            };
        }

        var secondary = secondaryResult.Data;

        var identityReason = DescribeNotComparableReason(primary, secondary);
        if (identityReason != null)
        {
            return Invalid(primary, secondary, instrument, secondaryType, identityReason);
        }

        if (primary.Date != secondary.Date)
        {
            return new CrossValidationResult
            {
                Status = CrossValidationStatus.Invalid,
                InstrumentId = instrument.InstrumentId,
                PrimarySource = primary.Provenance?.Source,
                SecondarySource = secondaryType,
                ObservationDate = primary.Date,
                PrimaryPrice = PositiveOrNull(primary.Close),
                SecondaryPrice = PositiveOrNull(secondary.Close),
                Reasons = [$"Observation dates differ: primary {FormatDate(primary.Date)}, secondary {FormatDate(secondary.Date)}"]
            };
        }

        // A secondary observation without a usable headline price is unusable,
        // not merely discrepant — never manufacture confidence from partial data.
        if (!HasRequiredFields(secondary))
        {
            return new CrossValidationResult
            {
                Status = CrossValidationStatus.Invalid,
                InstrumentId = instrument.InstrumentId,
                PrimarySource = primary.Provenance?.Source,
                SecondarySource = secondaryType,
                ObservationDate = primary.Date,
                PrimaryPrice = PositiveOrNull(primary.Close),
                SecondaryPrice = PositiveOrNull(secondary.Close),
                Reasons = [$"Secondary observation from {secondaryType} is missing required price or date fields"]
            };
        }

        var reasons = new List<string>();
        var priceDiff = CompareHeadlinePrice(primary, secondary, primary.Date, reasons);
        CompareOptional(primary.Change, secondary.Change, "change", reasons);
        CompareOptional(primary.ChangePercent, secondary.ChangePercent, "changePercent", reasons);

        // Volume: only flagged when BOTH sources actually report volume (> 0).
        // TGJU does not provide volume (Volume == 0 by verified contract) and
        // must never be treated as a discrepancy for it.
        if (primary.Volume > 0 && secondary.Volume > 0)
        {
            var volDiff = RelativeDiff(primary.Volume, secondary.Volume, reference: primary.Volume);
            if (volDiff != null && volDiff > _settings.MaximumPriceDeviationPercent)
            {
                reasons.Add($"volume differs by {FormatPercent(volDiff.Value)} (primary {primary.Volume}, secondary {secondary.Volume})");
            }
        }

        return new CrossValidationResult
        {
            Status = reasons.Count > 0 ? CrossValidationStatus.Discrepancy : CrossValidationStatus.Consistent,
            InstrumentId = instrument.InstrumentId,
            PrimarySource = primary.Provenance?.Source,
            SecondarySource = secondaryType,
            ObservationDate = primary.Date,
            PrimaryPrice = PositiveOrNull(primary.Close),
            SecondaryPrice = PositiveOrNull(secondary.Close),
            DifferencePercent = priceDiff,
            TolerancePercent = _settings.MaximumPriceDeviationPercent,
            Reasons = reasons.AsReadOnly()
        };
    }

    /// <summary>
    /// Compares two candle sets on the intersection of their trading dates.
    /// Dates present in only one set are counted but not compared; if nothing
    /// comparable remains the outcome is Invalid (no confidence manufactured).
    /// </summary>
    public CrossValidationResult CompareCandleSets(
        IReadOnlyList<Candle> primary,
        DataResult<IReadOnlyList<Candle>> secondaryResult,
        InstrumentMapping instrument,
        SourceAdapterType secondaryType)
    {
        var primarySource = primary.Count > 0 ? primary[0].Provenance?.Source : null;

        if (!secondaryResult.Ok || secondaryResult.Data == null || secondaryResult.Data.Count == 0)
        {
            return new CrossValidationResult
            {
                Status = CrossValidationStatus.Incomplete,
                InstrumentId = instrument.InstrumentId,
                PrimarySource = primarySource,
                SecondarySource = secondaryType,
                Reasons = [$"Secondary source {secondaryType} unavailable: {DescribeSecondaryError(secondaryResult)}"]
            };
        }

        var secondary = secondaryResult.Data;
        var secondaryByDate = secondary.GroupBy(c => c.Date).ToDictionary(g => g.Key, g => g.Last());
        var primaryDates = primary.Select(c => c.Date).ToHashSet();

        var reasons = new List<string>();
        var notComparable = false;
        DateOnly? firstComparedDate = null;
        decimal? firstPriceDiff = null;
        decimal? primaryPrice = null;
        decimal? secondaryPrice = null;
        int compared = 0;
        int primaryOnly = 0;

        foreach (var p in primary)
        {
            if (!secondaryByDate.TryGetValue(p.Date, out var s))
            {
                primaryOnly++;
                continue;
            }

            var notComparableReason = DescribeNotComparableReason(p, s);
            if (notComparableReason != null)
            {
                reasons.Add(notComparableReason);
                notComparable = true;
                continue;
            }

            compared++;
            firstComparedDate ??= p.Date;

            var diff = CompareHeadlinePrice(p, s, p.Date, reasons);
            if (diff == null)
            {
                // Missing/invalid price on either side — data unusable for this date.
                notComparable = true;
                continue;
            }

            primaryPrice ??= PositiveOrNull(p.Close);
            secondaryPrice ??= PositiveOrNull(s.Close);
            firstPriceDiff ??= diff;

            CompareOptional(p.Change, s.Change, $"change on {FormatDate(p.Date)}", reasons);
            CompareOptional(p.ChangePercent, s.ChangePercent, $"changePercent on {FormatDate(p.Date)}", reasons);
        }

        var secondaryOnly = secondary.Count(c => !primaryDates.Contains(c.Date));

        if (compared == 0)
        {
            return new CrossValidationResult
            {
                Status = CrossValidationStatus.Invalid,
                InstrumentId = instrument.InstrumentId,
                PrimarySource = primarySource,
                SecondarySource = secondaryType,
                TolerancePercent = _settings.MaximumPriceDeviationPercent,
                Reasons = reasons.Count > 0
                    ? reasons.AsReadOnly()
                    : (IReadOnlyList<string>)["No overlapping observation dates between primary and secondary sources"]
            };
        }

        var status = notComparable
            ? CrossValidationStatus.Invalid
            : reasons.Count > 0 ? CrossValidationStatus.Discrepancy : CrossValidationStatus.Consistent;

        var finalReasons = new List<string>(reasons);
        if (primaryOnly > 0)
            finalReasons.Add($"{primaryOnly} primary-only date(s) had no secondary counterpart");
        if (secondaryOnly > 0)
            finalReasons.Add($"{secondaryOnly} secondary-only date(s) had no primary counterpart");

        return new CrossValidationResult
        {
            Status = status,
            InstrumentId = instrument.InstrumentId,
            PrimarySource = primarySource,
            SecondarySource = secondaryType,
            ObservationDate = firstComparedDate,
            PrimaryPrice = primaryPrice,
            SecondaryPrice = secondaryPrice,
            DifferencePercent = firstPriceDiff,
            TolerancePercent = _settings.MaximumPriceDeviationPercent,
            Reasons = finalReasons.AsReadOnly()
        };
    }

    // ============================================================
    // Helpers
    // ============================================================

    private static CrossValidationResult NotAttempted(SourceAdapterType? primarySource, string? instrumentId) => new()
    {
        Status = CrossValidationStatus.NotAttempted,
        InstrumentId = instrumentId,
        PrimarySource = primarySource,
        Reasons = ["Cross-validation did not run (disabled, primary unavailable, or no secondary source for this instrument)"]
    };

    private static CrossValidationResult Invalid(
        Candle primary,
        Candle secondary,
        InstrumentMapping instrument,
        SourceAdapterType secondaryType,
        string reason) => new()
    {
        Status = CrossValidationStatus.Invalid,
        InstrumentId = instrument.InstrumentId,
        PrimarySource = primary.Provenance?.Source,
        SecondarySource = secondaryType,
        ObservationDate = primary.Date,
        PrimaryPrice = PositiveOrNull(primary.Close),
        SecondaryPrice = PositiveOrNull(secondary.Close),
        Reasons = [reason]
    };

    private IDataSourceAdapter? FindSecondaryAdapter(InstrumentMapping instrument, SourceAdapterType? primarySource)
    {
        // Capability-aware selection via the existing registry; deterministic
        // ordering comes from the registry (healthy first, then SourceType).
        return _registry.GetAdaptersForCapability(instrument, MarketDataType.Snapshot)
            .Concat(_registry.GetAdaptersForCapability(instrument, MarketDataType.HistoricalCandles))
            .Where(a => a.SourceType != primarySource)
            .GroupBy(a => a.SourceType)
            .Select(g => g.First())
            .FirstOrDefault();
    }

    /// <summary>
    /// Returns a reason when the two observations are semantically not
    /// comparable, or null when comparison may proceed.
    ///
    /// Canonical identity is the StrategyForge instrument; provider-specific
    /// identifiers (TSETMC InsCode, TGJU slug) legitimately differ and are
    /// never used to match. Two observations are not comparable when they
    /// declare different canonical instruments or different rate semantics
    /// (e.g. free-market vs official USD/IRR describe different markets).
    /// </summary>
    private static string? DescribeNotComparableReason(Candle primary, Candle secondary)
    {
        var primaryInstrument = primary.ExtraFields?.GetValueOrDefault("instrumentId");
        var secondaryInstrument = secondary.ExtraFields?.GetValueOrDefault("instrumentId");
        if (primaryInstrument != null &&
            secondaryInstrument != null &&
            !string.Equals(primaryInstrument, secondaryInstrument, StringComparison.Ordinal))
        {
            return $"Observations declare different instruments: '{primaryInstrument}' vs '{secondaryInstrument}'";
        }

        var primaryRateType = primary.ExtraFields?.GetValueOrDefault("rateType");
        var secondaryRateType = secondary.ExtraFields?.GetValueOrDefault("rateType");
        if (primaryRateType != null &&
            secondaryRateType != null &&
            !string.Equals(primaryRateType, secondaryRateType, StringComparison.Ordinal))
        {
            return $"Rate types differ: primary '{primaryRateType}' vs secondary '{secondaryRateType}'";
        }

        return null;
    }

    /// <summary>
    /// Compares headline (close/last) prices. Appends a diagnosable reason and
    /// returns the relative difference. Returns null (with a reason) when a
    /// required price is missing or non-positive on either side — comparison
    /// is impossible, never "valid by default".
    /// </summary>
    private decimal? CompareHeadlinePrice(Candle primary, Candle secondary, DateOnly date, List<string> reasons)
    {
        var p = PositiveOrNull(primary.Close);
        var s = PositiveOrNull(secondary.Close);

        if (p == null && s == null)
        {
            reasons.Add($"close price missing or invalid on {FormatDate(date)} for both sources");
            return null;
        }
        if (p == null)
        {
            reasons.Add($"close price missing or invalid on {FormatDate(date)} for primary source ({primary.Provenance?.Source})");
            return null;
        }
        if (s == null)
        {
            reasons.Add($"close price missing or invalid on {FormatDate(date)} for secondary source ({secondary.Provenance?.Source})");
            return null;
        }

        var diff = RelativeDiff(p.Value, s.Value, reference: p.Value);
        if (diff != null && diff > _settings.MaximumPriceDeviationPercent)
        {
            reasons.Add(
                $"close differs by {FormatPercent(diff.Value)} on {FormatDate(date)} " +
                $"(primary {FormatNumber(p.Value)} vs secondary {FormatNumber(s.Value)}, tolerance {FormatPercent(_settings.MaximumPriceDeviationPercent)})");
        }

        return diff;
    }

    /// <summary>
    /// Compares an optional numeric field only when BOTH sources provide it.
    /// A field absent on either side is skipped — provider-specific optional
    /// fields never invalidate otherwise comparable observations.
    /// </summary>
    private void CompareOptional(decimal? primaryValue, decimal? secondaryValue, string field, List<string> reasons)
    {
        if (!primaryValue.HasValue || !secondaryValue.HasValue)
            return;

        var p = Math.Abs(primaryValue.Value);
        var s = Math.Abs(secondaryValue.Value);
        if (p == 0 && s == 0)
            return;

        // Reference is the larger magnitude — non-zero by construction,
        // deterministic, and symmetric in the compared sources.
        var reference = Math.Max(p, s);
        var diff = Math.Abs(p - s) / reference * 100m;
        if (diff > _settings.MaximumPriceDeviationPercent)
        {
            reasons.Add(
                $"{field} differs by {FormatPercent(diff)} " +
                $"(primary {FormatNumber(primaryValue.Value)} vs secondary {FormatNumber(secondaryValue.Value)}, tolerance {FormatPercent(_settings.MaximumPriceDeviationPercent)})");
        }
    }

    /// <summary>
    /// abs(a-b)/reference*100, guarding division by zero. Returns null when
    /// the reference is zero (comparison impossible — caller must handle).
    /// </summary>
    private static decimal? RelativeDiff(decimal a, decimal b, decimal reference)
    {
        if (reference == 0)
            return null;
        return Math.Abs(a - b) / reference * 100m;
    }

    /// <summary>Required fields for a meaningful comparison: a usable date and a positive price.</summary>
    private static bool HasRequiredFields(Candle c) =>
        c.Date != default && PositiveOrNull(c.Close) != null;

    private static decimal? PositiveOrNull(decimal value) => value > 0 ? value : null;

    private static string FormatDate(DateOnly date) => date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    private static string FormatNumber(decimal value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string FormatPercent(decimal value) => value.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);

    private static string DescribeSecondaryError<T>(DataResult<T> result) =>
        result.Error != null ? $"{result.Error.Code}: {result.Error.Message}" : "no data returned";

    private DataResult<Candle> AddWarningIfNeeded(DataResult<Candle> primaryResult, CrossValidationResult verdict)
    {
        if (verdict.Status != CrossValidationStatus.Discrepancy)
            return primaryResult;

        var warnings = primaryResult.Warnings.ToList();
        warnings.Add(new DataWarning
        {
            Code = "CROSS_VALIDATION_DISCREPANCY",
            Message = $"Cross-source validation detected discrepancies: {string.Join("; ", verdict.Reasons)}",
            Severity = WarningSeverity.Warning
        });
        return primaryResult with { Warnings = warnings.AsReadOnly() };
    }

    private DataResult<IReadOnlyList<Candle>> AddWarningIfNeeded(DataResult<IReadOnlyList<Candle>> primaryResult, CrossValidationResult verdict)
    {
        if (verdict.Status != CrossValidationStatus.Discrepancy)
            return primaryResult;

        var warnings = primaryResult.Warnings.ToList();
        warnings.Add(new DataWarning
        {
            Code = "CROSS_VALIDATION_DISCREPANCY",
            Message = $"Cross-source validation detected discrepancies: {string.Join("; ", verdict.Reasons)}",
            Severity = WarningSeverity.Warning
        });
        return primaryResult with { Warnings = warnings.AsReadOnly() };
    }
}

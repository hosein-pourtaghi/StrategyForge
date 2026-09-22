using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StrategyForge.Api.Services;
using StrategyForge.Domain.Configuration;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Interfaces.Providers;
using StrategyForge.Domain.Models;

namespace StrategyForge.Api.Tests.Services;

/// <summary>
/// Phase 4 tests: cross-source market-data validation.
/// Covers the pure comparison core (CompareSnapshot / CompareCandleSets),
/// async validation flow over the registry, and configuration gating.
/// The validator's responsibility is data consistency/quality only — never
/// trading decisions — so tests assert statuses, reasons, and provenance,
/// and that primary data is never altered apart from a warning.
/// </summary>
public class CrossSourceValidatorTests
{
    private const string InstrumentId = "iran-equity-foolad-test";
    private static readonly DateOnly Day1 = new(2025, 1, 15);
    private static readonly DateOnly Day2 = new(2025, 1, 16);
    private static readonly DateOnly Day3 = new(2025, 1, 17);

    // ============================================================
    // Builders
    // ============================================================

    private static InstrumentMapping CreateInstrument() => new()
    {
        InstrumentId = InstrumentId,
        Symbol = "فولاد",
        DisplayName = "Foolad Test",
        AssetClass = AssetType.Stock,
        Exchange = "TSE",
        QuoteCurrency = "IRR"
    };

    private static DataProvenance CreateProvenance(SourceAdapterType source) => new()
    {
        Source = source,
        FetchedAtUtc = DateTimeOffset.Parse("2025-01-15T12:00:00Z"),
        IsCached = false
    };

    private static Candle CreateCandle(
        DateOnly date,
        decimal close,
        SourceAdapterType source,
        long volume = 1_000_000,
        decimal? change = null,
        decimal? changePercent = null,
        IReadOnlyDictionary<string, string>? extraFields = null) => new()
        {
            Date = date,
            Open = close,
            High = close,
            Low = close,
            Close = close,
            Volume = volume,
            Change = change,
            ChangePercent = changePercent,
            Provenance = CreateProvenance(source),
            ExtraFields = extraFields
        };

    private static CrossSourceValidator CreateValidator(
        Mock<IDataSourceRegistry> registryMock,
        bool enabled = true)
    {
        var settings = new DataSourceSettings
        {
            CrossValidation = new CrossValidationSettings
            {
                Enabled = enabled,
                EnabledDataTypes = ["Snapshot", "HistoricalCandles"]
            }
        };
        return new CrossSourceValidator(
            registryMock.Object,
            Options.Create(settings),
            Mock.Of<ILogger<CrossSourceValidator>>());
    }

    private static Mock<IDataSourceAdapter> CreateAdapter(
        SourceAdapterType type,
        DataResult<Candle>? latest = null,
        DataResult<IReadOnlyList<Candle>>? historical = null)
    {
        var mock = new Mock<IDataSourceAdapter>();
        mock.SetupGet(a => a.SourceType).Returns(type);
        if (latest != null)
        {
            mock.Setup(a => a.GetLatestCandleAsync(It.IsAny<InstrumentMapping>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(latest);
        }
        if (historical != null)
        {
            mock.Setup(a => a.GetHistoricalCandlesAsync(
                    It.IsAny<InstrumentMapping>(),
                    It.IsAny<DateOnly>(),
                    It.IsAny<DateOnly>(),
                    It.IsAny<CandleResolution?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(historical);
        }
        return mock;
    }

    private static void SetupRegistry(
        Mock<IDataSourceRegistry> registryMock,
        params Mock<IDataSourceAdapter>[] adapters)
    {
        registryMock
            .Setup(r => r.GetAdaptersForCapability(It.IsAny<InstrumentMapping>(), It.IsAny<MarketDataType>()))
            .Returns(adapters.Select(a => a.Object).ToArray());
    }

    // ============================================================
    // CompareSnapshot — price comparison and tolerance
    // ============================================================

    [Fact]
    public void CompareSnapshot_IdenticalPrices_IsConsistent()
    {
        var validator = CreateValidator(new Mock<IDataSourceRegistry>());
        var primary = CreateCandle(Day1, 100m, SourceAdapterType.Tsetmc);
        var secondary = DataResult<Candle>.Success(CreateCandle(Day1, 100m, SourceAdapterType.Tgju, volume: 0));

        var verdict = validator.CompareSnapshot(primary, secondary, CreateInstrument(), SourceAdapterType.Tgju);

        Assert.Equal(CrossValidationStatus.Consistent, verdict.Status);
        Assert.Empty(verdict.Reasons);
        Assert.Equal(0m, verdict.DifferencePercent);
    }

    [Fact]
    public void CompareSnapshot_WithinTolerance_IsConsistent()
    {
        var validator = CreateValidator(new Mock<IDataSourceRegistry>());
        var primary = CreateCandle(Day1, 100m, SourceAdapterType.Tsetmc);
        var secondary = DataResult<Candle>.Success(CreateCandle(Day1, 101m, SourceAdapterType.Tgju, volume: 0));

        var verdict = validator.CompareSnapshot(primary, secondary, CreateInstrument(), SourceAdapterType.Tgju);

        Assert.Equal(CrossValidationStatus.Consistent, verdict.Status);
        Assert.Equal(1m, verdict.DifferencePercent);
    }

    [Fact]
    public void CompareSnapshot_DifferenceExactlyAtTolerance_IsConsistent()
    {
        // Documented contract: a difference exactly equal to the tolerance
        // is within tolerance (strictly-greater-than triggers a discrepancy).
        var validator = CreateValidator(new Mock<IDataSourceRegistry>());
        var primary = CreateCandle(Day1, 100m, SourceAdapterType.Tsetmc);
        var secondary = DataResult<Candle>.Success(CreateCandle(Day1, 102m, SourceAdapterType.Tgju, volume: 0));

        var verdict = validator.CompareSnapshot(primary, secondary, CreateInstrument(), SourceAdapterType.Tgju);

        Assert.Equal(CrossValidationStatus.Consistent, verdict.Status);
        Assert.Equal(2m, verdict.DifferencePercent);
    }

    [Fact]
    public void CompareSnapshot_BeyondTolerance_IsDiscrepancyWithDiagnosableReason()
    {
        var validator = CreateValidator(new Mock<IDataSourceRegistry>());
        var primary = CreateCandle(Day1, 100m, SourceAdapterType.Tsetmc);
        var secondary = DataResult<Candle>.Success(CreateCandle(Day1, 105m, SourceAdapterType.Tgju, volume: 0));

        var verdict = validator.CompareSnapshot(primary, secondary, CreateInstrument(), SourceAdapterType.Tgju);

        Assert.Equal(CrossValidationStatus.Discrepancy, verdict.Status);
        Assert.Equal(5m, verdict.DifferencePercent);
        Assert.Single(verdict.Reasons);
        Assert.Contains("close differs by", verdict.Reasons[0]);
        Assert.Contains("2025-01-15", verdict.Reasons[0]);
    }

    [Fact]
    public void CompareSnapshot_ReportsProvenanceAndToleranceMetadata()
    {
        var validator = CreateValidator(new Mock<IDataSourceRegistry>());
        var primary = CreateCandle(Day1, 100m, SourceAdapterType.Tsetmc);
        var secondary = DataResult<Candle>.Success(CreateCandle(Day1, 105m, SourceAdapterType.Tgju, volume: 0));

        var verdict = validator.CompareSnapshot(primary, secondary, CreateInstrument(), SourceAdapterType.Tgju);

        Assert.Equal(InstrumentId, verdict.InstrumentId);
        Assert.Equal(SourceAdapterType.Tsetmc, verdict.PrimarySource);
        Assert.Equal(SourceAdapterType.Tgju, verdict.SecondarySource);
        Assert.Equal(Day1, verdict.ObservationDate);
        Assert.Equal(100m, verdict.PrimaryPrice);
        Assert.Equal(105m, verdict.SecondaryPrice);
        Assert.Equal(2.0m, verdict.TolerancePercent);
    }

    // ============================================================
    // CompareSnapshot — unavailable / invalid / incomparable data
    // ============================================================

    [Fact]
    public void CompareSnapshot_SecondarySourceFails_IsIncomplete()
    {
        var validator = CreateValidator(new Mock<IDataSourceRegistry>());
        var primary = CreateCandle(Day1, 100m, SourceAdapterType.Tsetmc);
        var secondary = DataResult<Candle>.Failure(new DataCollectionError2
        {
            Code = "HTTP_ERROR",
            Message = "TSETMC unreachable",
            Retryable = true
        });

        var verdict = validator.CompareSnapshot(primary, secondary, CreateInstrument(), SourceAdapterType.Tgju);

        Assert.Equal(CrossValidationStatus.Incomplete, verdict.Status);
        Assert.Contains(verdict.Reasons, r => r.Contains("HTTP_ERROR"));
        // Primary values still recorded for diagnostics.
        Assert.Equal(100m, verdict.PrimaryPrice);
        Assert.Null(verdict.SecondaryPrice);
    }

    [Fact]
    public void CompareSnapshot_SecondaryMissingRequiredPrice_IsInvalid()
    {
        var validator = CreateValidator(new Mock<IDataSourceRegistry>());
        var primary = CreateCandle(Day1, 100m, SourceAdapterType.Tsetmc);
        // Zero close = unusable observation, never a valid "zero price".
        var secondary = DataResult<Candle>.Success(CreateCandle(Day1, 0m, SourceAdapterType.Tgju, volume: 0));

        var verdict = validator.CompareSnapshot(primary, secondary, CreateInstrument(), SourceAdapterType.Tgju);

        Assert.Equal(CrossValidationStatus.Invalid, verdict.Status);
        Assert.Contains(verdict.Reasons, r => r.Contains("missing required price or date fields"));
    }

    [Fact]
    public void CompareSnapshot_DifferentObservationDates_IsInvalid()
    {
        var validator = CreateValidator(new Mock<IDataSourceRegistry>());
        var primary = CreateCandle(Day1, 100m, SourceAdapterType.Tsetmc);
        var secondary = DataResult<Candle>.Success(CreateCandle(Day2, 100m, SourceAdapterType.Tgju, volume: 0));

        var verdict = validator.CompareSnapshot(primary, secondary, CreateInstrument(), SourceAdapterType.Tgju);

        Assert.Equal(CrossValidationStatus.Invalid, verdict.Status);
        Assert.Contains(verdict.Reasons, r => r.Contains("Observation dates differ"));
    }

    [Fact]
    public void CompareSnapshot_DifferentCanonicalInstruments_IsInvalid()
    {
        var validator = CreateValidator(new Mock<IDataSourceRegistry>());
        var primary = CreateCandle(
            Day1, 100m, SourceAdapterType.Tsetmc,
            extraFields: new Dictionary<string, string> { ["instrumentId"] = "iran-equity-foolad" });
        var secondary = DataResult<Candle>.Success(CreateCandle(
            Day1, 100m, SourceAdapterType.Tgju, volume: 0,
            extraFields: new Dictionary<string, string> { ["instrumentId"] = "iran-equity-khodro" }));

        var verdict = validator.CompareSnapshot(primary, secondary, CreateInstrument(), SourceAdapterType.Tgju);

        Assert.Equal(CrossValidationStatus.Invalid, verdict.Status);
        Assert.Contains(verdict.Reasons, r => r.Contains("different instruments"));
    }

    [Fact]
    public void CompareSnapshot_MatchingCanonicalInstruments_AreComparable()
    {
        // Provider identifiers legitimately differ (TSETMC InsCode vs TGJU slug);
        // comparison operates on the canonical instrument identity only.
        var validator = CreateValidator(new Mock<IDataSourceRegistry>());
        var primary = CreateCandle(
            Day1, 100m, SourceAdapterType.Tsetmc,
            extraFields: new Dictionary<string, string> { ["instrumentId"] = InstrumentId });
        var secondary = DataResult<Candle>.Success(CreateCandle(
            Day1, 100m, SourceAdapterType.Tgju, volume: 0,
            extraFields: new Dictionary<string, string> { ["instrumentId"] = InstrumentId }));

        var verdict = validator.CompareSnapshot(primary, secondary, CreateInstrument(), SourceAdapterType.Tgju);

        Assert.Equal(CrossValidationStatus.Consistent, verdict.Status);
    }

    [Fact]
    public void CompareSnapshot_DifferentRateTypes_IsInvalid()
    {
        // Free-market vs official USD/IRR describe different markets — never comparable.
        var validator = CreateValidator(new Mock<IDataSourceRegistry>());
        var primary = CreateCandle(
            Day1, 1_000_000m, SourceAdapterType.Tsetmc,
            extraFields: new Dictionary<string, string> { ["rateType"] = "free_market" });
        var secondary = DataResult<Candle>.Success(CreateCandle(
            Day1, 1_000_000m, SourceAdapterType.Tgju, volume: 0,
            extraFields: new Dictionary<string, string> { ["rateType"] = "official" }));

        var verdict = validator.CompareSnapshot(primary, secondary, CreateInstrument(), SourceAdapterType.Tgju);

        Assert.Equal(CrossValidationStatus.Invalid, verdict.Status);
        Assert.Contains(verdict.Reasons, r => r.Contains("Rate types differ"));
    }

    // ============================================================
    // CompareSnapshot — optional fields and volume semantics
    // ============================================================

    [Fact]
    public void CompareSnapshot_TgjuZeroVolume_IsNotADiscrepancy()
    {
        // Phase 3 contract: TGJU does not provide volume (Volume == 0 by verified
        // contract) and must never be flagged for it.
        var validator = CreateValidator(new Mock<IDataSourceRegistry>());
        var primary = CreateCandle(Day1, 100m, SourceAdapterType.Tsetmc, volume: 5_000_000);
        var secondary = DataResult<Candle>.Success(CreateCandle(Day1, 100m, SourceAdapterType.Tgju, volume: 0));

        var verdict = validator.CompareSnapshot(primary, secondary, CreateInstrument(), SourceAdapterType.Tgju);

        Assert.Equal(CrossValidationStatus.Consistent, verdict.Status);
        Assert.DoesNotContain(verdict.Reasons, r => r.Contains("volume"));
    }

    [Fact]
    public void CompareSnapshot_BothSourcesReportDivergentVolume_IsDiscrepancy()
    {
        var validator = CreateValidator(new Mock<IDataSourceRegistry>());
        var primary = CreateCandle(Day1, 100m, SourceAdapterType.Tsetmc, volume: 1_000_000);
        var secondary = DataResult<Candle>.Success(CreateCandle(Day1, 100m, SourceAdapterType.Tgju, volume: 500_000));

        var verdict = validator.CompareSnapshot(primary, secondary, CreateInstrument(), SourceAdapterType.Tgju);

        Assert.Equal(CrossValidationStatus.Discrepancy, verdict.Status);
        Assert.Contains(verdict.Reasons, r => r.Contains("volume differs by"));
    }

    [Fact]
    public void CompareSnapshot_OptionalFieldMissingOnOneSide_IsSkipped()
    {
        var validator = CreateValidator(new Mock<IDataSourceRegistry>());
        var primary = CreateCandle(Day1, 100m, SourceAdapterType.Tsetmc, change: 1_000m);
        var secondary = DataResult<Candle>.Success(CreateCandle(Day1, 100m, SourceAdapterType.Tgju, volume: 0, change: null));

        var verdict = validator.CompareSnapshot(primary, secondary, CreateInstrument(), SourceAdapterType.Tgju);

        // change is absent on the secondary side — not comparable, never a discrepancy.
        Assert.Equal(CrossValidationStatus.Consistent, verdict.Status);
        Assert.DoesNotContain(verdict.Reasons, r => r.Contains("change"));
    }

    [Fact]
    public void CompareSnapshot_BothProvideDivergentChange_IsDiscrepancy()
    {
        var validator = CreateValidator(new Mock<IDataSourceRegistry>());
        var primary = CreateCandle(Day1, 100m, SourceAdapterType.Tsetmc, change: 1_000m);
        var secondary = DataResult<Candle>.Success(CreateCandle(Day1, 100m, SourceAdapterType.Tgju, volume: 0, change: 1_500m));

        var verdict = validator.CompareSnapshot(primary, secondary, CreateInstrument(), SourceAdapterType.Tgju);

        Assert.Equal(CrossValidationStatus.Discrepancy, verdict.Status);
        Assert.Contains(verdict.Reasons, r => r.Contains("change differs by"));
    }

    // ============================================================
    // CompareCandleSets — historical comparison
    // ============================================================

    [Fact]
    public void CompareCandleSets_MatchingDatesWithinTolerance_IsConsistent()
    {
        var validator = CreateValidator(new Mock<IDataSourceRegistry>());
        var primary = new[]
        {
            CreateCandle(Day1, 100m, SourceAdapterType.Tsetmc),
            CreateCandle(Day2, 102m, SourceAdapterType.Tsetmc)
        };
        var secondary = DataResult<IReadOnlyList<Candle>>.Success(new[]
        {
            CreateCandle(Day1, 100m, SourceAdapterType.Tgju, volume: 0),
            CreateCandle(Day2, 102m, SourceAdapterType.Tgju, volume: 0)
        });

        var verdict = validator.CompareCandleSets(primary, secondary, CreateInstrument(), SourceAdapterType.Tgju);

        Assert.Equal(CrossValidationStatus.Consistent, verdict.Status);
        Assert.Empty(verdict.Reasons);
        Assert.Equal(Day1, verdict.ObservationDate);
    }

    [Fact]
    public void CompareCandleSets_DiscrepantDate_IsDiscrepancyWithDateInReason()
    {
        var validator = CreateValidator(new Mock<IDataSourceRegistry>());
        var primary = new[]
        {
            CreateCandle(Day1, 100m, SourceAdapterType.Tsetmc),
            CreateCandle(Day2, 100m, SourceAdapterType.Tsetmc)
        };
        var secondary = DataResult<IReadOnlyList<Candle>>.Success(new[]
        {
            CreateCandle(Day1, 100m, SourceAdapterType.Tgju, volume: 0),
            CreateCandle(Day2, 110m, SourceAdapterType.Tgju, volume: 0)
        });

        var verdict = validator.CompareCandleSets(primary, secondary, CreateInstrument(), SourceAdapterType.Tgju);

        Assert.Equal(CrossValidationStatus.Discrepancy, verdict.Status);
        Assert.Contains(verdict.Reasons, r => r.Contains("2025-01-16"));
    }

    [Fact]
    public void CompareCandleSets_PartialOverlap_CountsUnmatchedDatesButComparesIntersection()
    {
        var validator = CreateValidator(new Mock<IDataSourceRegistry>());
        var primary = new[]
        {
            CreateCandle(Day1, 100m, SourceAdapterType.Tsetmc),
            CreateCandle(Day2, 100m, SourceAdapterType.Tsetmc),
            CreateCandle(Day3, 100m, SourceAdapterType.Tsetmc) // no secondary counterpart
        };
        var secondary = DataResult<IReadOnlyList<Candle>>.Success(new[]
        {
            CreateCandle(Day1, 100m, SourceAdapterType.Tgju, volume: 0),
            CreateCandle(Day2, 100m, SourceAdapterType.Tgju, volume: 0),
            CreateCandle(new DateOnly(2025, 2, 1), 100m, SourceAdapterType.Tgju, volume: 0) // no primary counterpart
        });

        var verdict = validator.CompareCandleSets(primary, secondary, CreateInstrument(), SourceAdapterType.Tgju);

        Assert.Equal(CrossValidationStatus.Consistent, verdict.Status);
        Assert.Contains(verdict.Reasons, r => r.Contains("1 primary-only date(s)"));
        Assert.Contains(verdict.Reasons, r => r.Contains("1 secondary-only date(s)"));
    }

    [Fact]
    public void CompareCandleSets_NoOverlappingDates_IsInvalid()
    {
        var validator = CreateValidator(new Mock<IDataSourceRegistry>());
        var primary = new[] { CreateCandle(Day1, 100m, SourceAdapterType.Tsetmc) };
        var secondary = DataResult<IReadOnlyList<Candle>>.Success(
            new[] { CreateCandle(new DateOnly(2025, 2, 1), 100m, SourceAdapterType.Tgju, volume: 0) });

        var verdict = validator.CompareCandleSets(primary, secondary, CreateInstrument(), SourceAdapterType.Tgju);

        Assert.Equal(CrossValidationStatus.Invalid, verdict.Status);
        Assert.Contains(verdict.Reasons, r => r.Contains("No overlapping observation dates"));
    }

    [Fact]
    public void CompareCandleSets_SecondarySourceFails_IsIncomplete()
    {
        var validator = CreateValidator(new Mock<IDataSourceRegistry>());
        var primary = new[] { CreateCandle(Day1, 100m, SourceAdapterType.Tsetmc) };
        var secondary = DataResult<IReadOnlyList<Candle>>.Failure(new DataCollectionError2
        {
            Code = "TIMEOUT",
            Message = "Request timed out",
            Retryable = true
        });

        var verdict = validator.CompareCandleSets(primary, secondary, CreateInstrument(), SourceAdapterType.Tgju);

        Assert.Equal(CrossValidationStatus.Incomplete, verdict.Status);
        Assert.Contains(verdict.Reasons, r => r.Contains("TIMEOUT"));
    }

    [Fact]
    public void CompareCandleSets_MissingPriceOnEitherSideForADate_IsInvalid()
    {
        var validator = CreateValidator(new Mock<IDataSourceRegistry>());
        var primary = new[]
        {
            CreateCandle(Day1, 100m, SourceAdapterType.Tsetmc),
            CreateCandle(Day2, 0m, SourceAdapterType.Tsetmc) // unusable price
        };
        var secondary = DataResult<IReadOnlyList<Candle>>.Success(new[]
        {
            CreateCandle(Day1, 100m, SourceAdapterType.Tgju, volume: 0),
            CreateCandle(Day2, 100m, SourceAdapterType.Tgju, volume: 0)
        });

        var verdict = validator.CompareCandleSets(primary, secondary, CreateInstrument(), SourceAdapterType.Tgju);

        // Comparison happened for Day1 but Day2 is unusable — no confidence manufactured.
        Assert.Equal(CrossValidationStatus.Invalid, verdict.Status);
        Assert.Contains(verdict.Reasons, r => r.Contains("close price missing or invalid"));
    }

    // ============================================================
    // ValidateSnapshotAsync — registry integration and gating
    // ============================================================

    [Fact]
    public async Task ValidateSnapshotAsync_DisabledByConfiguration_IsNotAttemptedWithoutSecondaryCall()
    {
        var registryMock = new Mock<IDataSourceRegistry>();
        var secondaryAdapter = CreateAdapter(
            SourceAdapterType.Tgju,
            latest: DataResult<Candle>.Success(CreateCandle(Day1, 100m, SourceAdapterType.Tgju, volume: 0)));
        SetupRegistry(registryMock, secondaryAdapter);
        var validator = CreateValidator(registryMock, enabled: false);

        var primary = DataResult<Candle>.Success(CreateCandle(Day1, 100m, SourceAdapterType.Tsetmc));
        var snapshot = await validator.ValidateSnapshotAsync(primary, CreateInstrument());

        Assert.Equal(CrossValidationStatus.NotAttempted, snapshot.Validation.Status);
        secondaryAdapter.Verify(
            a => a.GetLatestCandleAsync(It.IsAny<InstrumentMapping>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.Null(snapshot.SecondaryCandle);
    }

    [Fact]
    public async Task ValidateSnapshotAsync_PrimaryFetchFailed_IsNotAttempted()
    {
        var registryMock = new Mock<IDataSourceRegistry>();
        var validator = CreateValidator(registryMock);

        var failedPrimary = DataResult<Candle>.Failure(new DataCollectionError2
        {
            Code = "NETWORK_FAILURE",
            Message = "Primary source unreachable",
            Retryable = true
        });
        var snapshot = await validator.ValidateSnapshotAsync(failedPrimary, CreateInstrument());

        Assert.Equal(CrossValidationStatus.NotAttempted, snapshot.Validation.Status);
        Assert.Same(failedPrimary, snapshot.PrimaryResult);
    }

    [Fact]
    public async Task ValidateSnapshotAsync_NoSecondaryAdapterForInstrument_IsNotAttempted()
    {
        var registryMock = new Mock<IDataSourceRegistry>();
        // Only the primary adapter (Tsetmc) is registered — nothing else can validate it.
        var primaryAdapter = CreateAdapter(
            SourceAdapterType.Tsetmc,
            latest: DataResult<Candle>.Success(CreateCandle(Day1, 100m, SourceAdapterType.Tsetmc)));
        SetupRegistry(registryMock, primaryAdapter);
        var validator = CreateValidator(registryMock);

        var primary = DataResult<Candle>.Success(CreateCandle(Day1, 100m, SourceAdapterType.Tsetmc));
        var snapshot = await validator.ValidateSnapshotAsync(primary, CreateInstrument());

        Assert.Equal(CrossValidationStatus.NotAttempted, snapshot.Validation.Status);
        primaryAdapter.Verify(
            a => a.GetLatestCandleAsync(It.IsAny<InstrumentMapping>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ValidateSnapshotAsync_MissingPrimaryRequiredFields_IsInvalidWithoutSecondaryCall()
    {
        var registryMock = new Mock<IDataSourceRegistry>();
        var secondaryAdapter = CreateAdapter(
            SourceAdapterType.Tgju,
            latest: DataResult<Candle>.Success(CreateCandle(Day1, 100m, SourceAdapterType.Tgju, volume: 0)));
        SetupRegistry(registryMock, secondaryAdapter);
        var validator = CreateValidator(registryMock);

        var primary = DataResult<Candle>.Success(CreateCandle(Day1, 0m, SourceAdapterType.Tsetmc));
        var snapshot = await validator.ValidateSnapshotAsync(primary, CreateInstrument());

        Assert.Equal(CrossValidationStatus.Invalid, snapshot.Validation.Status);
        Assert.Contains(snapshot.Validation.Reasons, r => r.Contains("missing required price or date fields"));
        secondaryAdapter.Verify(
            a => a.GetLatestCandleAsync(It.IsAny<InstrumentMapping>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ValidateSnapshotAsync_Discrepancy_AddsWarningButKeepsPrimaryDataCanonical()
    {
        var registryMock = new Mock<IDataSourceRegistry>();
        var secondaryAdapter = CreateAdapter(
            SourceAdapterType.Tgju,
            latest: DataResult<Candle>.Success(CreateCandle(Day1, 110m, SourceAdapterType.Tgju, volume: 0)));
        SetupRegistry(registryMock, secondaryAdapter);
        var validator = CreateValidator(registryMock);

        var primary = DataResult<Candle>.Success(CreateCandle(Day1, 100m, SourceAdapterType.Tsetmc));
        var snapshot = await validator.ValidateSnapshotAsync(primary, CreateInstrument());

        Assert.Equal(CrossValidationStatus.Discrepancy, snapshot.Validation.Status);
        Assert.Equal(SourceAdapterType.Tsetmc, snapshot.Validation.PrimarySource);
        Assert.Equal(SourceAdapterType.Tgju, snapshot.Validation.SecondarySource);

        // Primary data is returned unchanged and remains canonical — only a warning is attached.
        Assert.Equal(100m, snapshot.PrimaryResult.Data!.Close);
        Assert.Single(snapshot.PrimaryResult.Warnings);
        Assert.Equal("CROSS_VALIDATION_DISCREPANCY", snapshot.PrimaryResult.Warnings[0].Code);
        Assert.Equal(WarningSeverity.Warning, snapshot.PrimaryResult.Warnings[0].Severity);

        // The secondary observation is preserved for diagnostics.
        Assert.NotNull(snapshot.SecondaryCandle);
        Assert.Equal(SourceAdapterType.Tgju, snapshot.SecondaryCandle!.Provenance!.Source);
        Assert.Equal(110m, snapshot.SecondaryCandle.Close);
    }

    [Fact]
    public async Task ValidateSnapshotAsync_Consistent_AddsNoWarning()
    {
        var registryMock = new Mock<IDataSourceRegistry>();
        var secondaryAdapter = CreateAdapter(
            SourceAdapterType.Tgju,
            latest: DataResult<Candle>.Success(CreateCandle(Day1, 100.5m, SourceAdapterType.Tgju, volume: 0)));
        SetupRegistry(registryMock, secondaryAdapter);
        var validator = CreateValidator(registryMock);

        var primary = DataResult<Candle>.Success(CreateCandle(Day1, 100m, SourceAdapterType.Tsetmc));
        var snapshot = await validator.ValidateSnapshotAsync(primary, CreateInstrument());

        Assert.Equal(CrossValidationStatus.Consistent, snapshot.Validation.Status);
        Assert.Empty(snapshot.PrimaryResult.Warnings);
    }

    // ============================================================
    // ValidateCandlesAsync — registry integration and gating
    // ============================================================

    [Fact]
    public async Task ValidateCandlesAsync_DisabledByConfiguration_IsNotAttemptedWithoutSecondaryCall()
    {
        var registryMock = new Mock<IDataSourceRegistry>();
        var secondaryAdapter = CreateAdapter(
            SourceAdapterType.Tgju,
            historical: DataResult<IReadOnlyList<Candle>>.Success([]));
        SetupRegistry(registryMock, secondaryAdapter);
        var validator = CreateValidator(registryMock, enabled: false);

        var primary = DataResult<IReadOnlyList<Candle>>.Success(
            new[] { CreateCandle(Day1, 100m, SourceAdapterType.Tsetmc) });
        var snapshot = await validator.ValidateCandlesAsync(primary, CreateInstrument());

        Assert.Equal(CrossValidationStatus.NotAttempted, snapshot.Validation.Status);
        secondaryAdapter.Verify(
            a => a.GetHistoricalCandlesAsync(
                It.IsAny<InstrumentMapping>(),
                It.IsAny<DateOnly>(),
                It.IsAny<DateOnly>(),
                It.IsAny<CandleResolution?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.Empty(snapshot.SecondaryCandles);
    }

    [Fact]
    public async Task ValidateCandlesAsync_PrimaryFetchFailed_IsNotAttempted()
    {
        var registryMock = new Mock<IDataSourceRegistry>();
        var validator = CreateValidator(registryMock);

        var failedPrimary = DataResult<IReadOnlyList<Candle>>.Failure(new DataCollectionError2
        {
            Code = "HTTP_ERROR",
            Message = "Primary source unreachable",
            Retryable = false
        });
        var snapshot = await validator.ValidateCandlesAsync(failedPrimary, CreateInstrument());

        Assert.Equal(CrossValidationStatus.NotAttempted, snapshot.Validation.Status);
        Assert.Same(failedPrimary, snapshot.PrimaryResult);
    }

    [Fact]
    public async Task ValidateCandlesAsync_FetchesSecondaryOverPrimaryDateRange()
    {
        var registryMock = new Mock<IDataSourceRegistry>();
        var secondaryAdapter = CreateAdapter(
            SourceAdapterType.Tgju,
            historical: DataResult<IReadOnlyList<Candle>>.Success(new[]
            {
                CreateCandle(Day1, 100m, SourceAdapterType.Tgju, volume: 0),
                CreateCandle(Day2, 101m, SourceAdapterType.Tgju, volume: 0)
            }));
        SetupRegistry(registryMock, secondaryAdapter);
        var validator = CreateValidator(registryMock);

        var primary = DataResult<IReadOnlyList<Candle>>.Success(new[]
        {
            CreateCandle(Day1, 100m, SourceAdapterType.Tsetmc),
            CreateCandle(Day2, 101m, SourceAdapterType.Tsetmc)
        });
        var snapshot = await validator.ValidateCandlesAsync(primary, CreateInstrument());

        Assert.Equal(CrossValidationStatus.Consistent, snapshot.Validation.Status);
        Assert.Equal(2, snapshot.SecondaryCandles.Count);
        secondaryAdapter.Verify(
            a => a.GetHistoricalCandlesAsync(
                It.IsAny<InstrumentMapping>(),
                It.Is<DateOnly>(d => d == Day1),
                It.Is<DateOnly>(d => d == Day2),
                It.IsAny<CandleResolution?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ValidateCandlesAsync_SecondaryFailure_IsIncompleteButPrimaryPreserved()
    {
        var registryMock = new Mock<IDataSourceRegistry>();
        var secondaryAdapter = CreateAdapter(
            SourceAdapterType.Tgju,
            historical: DataResult<IReadOnlyList<Candle>>.Failure(new DataCollectionError2
            {
                Code = "TIMEOUT",
                Message = "TGJU request timed out",
                Retryable = true
            }));
        SetupRegistry(registryMock, secondaryAdapter);
        var validator = CreateValidator(registryMock);

        var primary = DataResult<IReadOnlyList<Candle>>.Success(
            new[] { CreateCandle(Day1, 100m, SourceAdapterType.Tsetmc) });
        var snapshot = await validator.ValidateCandlesAsync(primary, CreateInstrument());

        Assert.Equal(CrossValidationStatus.Incomplete, snapshot.Validation.Status);
        Assert.Empty(snapshot.PrimaryResult.Warnings);
        Assert.Equal(100m, snapshot.PrimaryResult.Data![0].Close);
    }

    [Fact]
    public async Task ValidateCandlesAsync_Discrepancy_WarningCarriesValidationReasons()
    {
        var registryMock = new Mock<IDataSourceRegistry>();
        var secondaryAdapter = CreateAdapter(
            SourceAdapterType.Tgju,
            historical: DataResult<IReadOnlyList<Candle>>.Success(
                new[] { CreateCandle(Day1, 120m, SourceAdapterType.Tgju, volume: 0) }));
        SetupRegistry(registryMock, secondaryAdapter);
        var validator = CreateValidator(registryMock);

        var primary = DataResult<IReadOnlyList<Candle>>.Success(
            new[] { CreateCandle(Day1, 100m, SourceAdapterType.Tsetmc) });
        var snapshot = await validator.ValidateCandlesAsync(primary, CreateInstrument());

        Assert.Equal(CrossValidationStatus.Discrepancy, snapshot.Validation.Status);
        var warning = Assert.Single(snapshot.PrimaryResult.Warnings);
        Assert.Equal("CROSS_VALIDATION_DISCREPANCY", warning.Code);
        Assert.Contains("close differs by", warning.Message);
    }

    // ============================================================
    // Configuration gating
    // ============================================================

    [Fact]
    public void IsEnabledFor_DisabledByDefault()
    {
        // DataSourceSettings defaults to CrossValidation.Enabled = false —
        // validation is opt-in and never surprises callers.
        var validator = new CrossSourceValidator(
            new Mock<IDataSourceRegistry>().Object,
            Options.Create(new DataSourceSettings()),
            Mock.Of<ILogger<CrossSourceValidator>>());

        Assert.False(validator.IsEnabledFor(MarketDataType.Snapshot));
        Assert.False(validator.IsEnabledFor(MarketDataType.HistoricalCandles));
    }

    [Fact]
    public void IsEnabledFor_EnabledWithoutTypeFilter_AppliesToAllTypes()
    {
        var validator = new CrossSourceValidator(
            new Mock<IDataSourceRegistry>().Object,
            Options.Create(new DataSourceSettings
            {
                CrossValidation = new CrossValidationSettings { Enabled = true }
            }),
            Mock.Of<ILogger<CrossSourceValidator>>());

        Assert.True(validator.IsEnabledFor(MarketDataType.Snapshot));
        Assert.True(validator.IsEnabledFor(MarketDataType.HistoricalCandles));
        Assert.True(validator.IsEnabledFor(MarketDataType.OrderBook));
    }

    [Fact]
    public void IsEnabledFor_TypeFilterRestrictsEnabledTypes()
    {
        var validator = new CrossSourceValidator(
            new Mock<IDataSourceRegistry>().Object,
            Options.Create(new DataSourceSettings
            {
                CrossValidation = new CrossValidationSettings
                {
                    Enabled = true,
                    EnabledDataTypes = ["Snapshot"]
                }
            }),
            Mock.Of<ILogger<CrossSourceValidator>>());

        Assert.True(validator.IsEnabledFor(MarketDataType.Snapshot));
        Assert.False(validator.IsEnabledFor(MarketDataType.HistoricalCandles));
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StrategyForge.Analysis.Strategy;
using StrategyForge.Api.Contracts;
using StrategyForge.Api.Controllers;
using StrategyForge.Api.Services;
using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Interfaces.Providers;
using StrategyForge.Domain.Models;
using StrategyForge.Infrastructure.Services;
using Xunit;

namespace StrategyForge.Api.Tests.Controllers;

/// <summary>
/// Controller + API-service tests for the Phase 8 deterministic strategy endpoints.
/// Uses the real in-memory enriched store, feature extractor, rules, and
/// evaluation engine — only the instrument resolver is mocked.
/// </summary>
public class StrategyAnalysisControllerTests
{
    private const string InstrumentId = "test-usd-irr";

    private readonly Mock<IInstrumentResolver> _resolverMock = new();
    private readonly InMemoryEnrichedDatasetStore _enrichedStore = new();
    private readonly StrategyController _controller;

    public StrategyAnalysisControllerTests()
    {
        var engine = new StrategyEvaluationEngine(StrategyRuleRegistry.BuiltIn);
        var setupEngine = new StrategySetupEngine(SetupRuleRegistry.BuiltIn);
        var service = new StrategyAnalysisApiService(
            _enrichedStore, _resolverMock.Object, engine, setupEngine);
        _controller = new StrategyController(
            Mock.Of<Domain.Interfaces.Orchestration.IStrategyOrchestrator>(),
            new InstrumentService(_resolverMock.Object),
            service);
    }

    private void SetupResolver() =>
        _resolverMock.Setup(r => r.ResolveAsync("usd-irr", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstrumentMapping
            {
                InstrumentId = InstrumentId,
                Symbol = "USD/IRR",
                DisplayName = "US Dollar / Iranian Rial",
                AssetClass = AssetType.Currency,
                Exchange = "free_market",
                QuoteCurrency = "IRR",
                SourceIdentifiers = new Dictionary<SourceAdapterType, SourceIdentifier>()
            });

    private async Task SeedEnrichedAsync(int count, DateOnly start, SourceAdapterType source)
    {
        var observations = Enumerable.Range(0, count).Select(i => new EnrichedObservation
        {
            InstrumentId = InstrumentId,
            Source = source,
            ObservationDate = start.AddDays(i),
            Open = 100m + i - 1,
            High = 100m + i + 1,
            Low = 100m + i - 2,
            Close = 100m + i,
            Volume = 0,
            Indicators = new Dictionary<string, IReadOnlyDictionary<string, decimal>>
            {
                ["SMA"] = new Dictionary<string, decimal> { ["SMA"] = 90m },
                ["RSI"] = new Dictionary<string, decimal> { ["RSI"] = 55m },
                ["MACD"] = new Dictionary<string, decimal>
                {
                    ["MACD"] = 2m, ["Signal"] = 1m, ["Histogram"] = 1m
                }
            },
            QualityStatus = HistoricalDataQualityStatus.Valid,
            ProcessedBy = "test"
        }).ToList();

        await _enrichedStore.UpsertEnrichedAsync(InstrumentId, source, observations);
    }

    // =====================================================================
    // Regime endpoint
    // =====================================================================

    [Fact]
    public async Task GetRegime_NullInstrument_ReturnsOkWithInvalidRequest()
    {
        var result = await _controller.GetRegime(
            null, SourceAdapterType.Tgju, null, null, 30, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<RegimeResponse>(ok.Value);
        Assert.False(response.Ok);
        Assert.Equal("INVALID_REQUEST", response.ErrorCode);
    }

    [Fact]
    public async Task GetRegime_UnknownInstrument_ReturnsOkWithInstrumentNotFound()
    {
        _resolverMock.Setup(r => r.ResolveAsync("unknown", It.IsAny<CancellationToken>()))
            .ReturnsAsync((InstrumentMapping?)null);

        var result = await _controller.GetRegime(
            "unknown", SourceAdapterType.Tgju, null, null, 30, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<RegimeResponse>(ok.Value);
        Assert.False(response.Ok);
        Assert.Equal("INSTRUMENT_NOT_FOUND", response.ErrorCode);
    }

    [Fact]
    public async Task GetRegime_WithEnrichedData_ReturnsBullishStrongRegime()
    {
        SetupResolver();
        await SeedEnrichedAsync(5, new DateOnly(2026, 1, 1), SourceAdapterType.Tgju);

        var result = await _controller.GetRegime(
            "usd-irr", SourceAdapterType.Tgju,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 5), 30, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<RegimeResponse>(ok.Value);
        Assert.True(response.Ok);
        Assert.Equal(5, response.ObservationsEvaluated);
        Assert.All(response.Regimes, r =>
        {
            Assert.Equal("Bullish", r.Trend);       // close > SMA 90
            Assert.Equal("Unknown", r.Volatility);  // no Bollinger stored
            Assert.Equal("Neutral", r.Momentum);    // RSI 55 ∈ (45, 55]
        });
    }

    [Fact]
    public async Task GetRegime_NoEnrichedData_ReturnsNoData()
    {
        SetupResolver();

        var result = await _controller.GetRegime(
            "usd-irr", SourceAdapterType.Tgju,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 5), 30, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<RegimeResponse>(ok.Value);
        Assert.False(response.Ok);
        Assert.Equal("NO_DATA", response.ErrorCode);
    }

    // =====================================================================
    // Evaluate endpoint
    // =====================================================================

    [Fact]
    public async Task Evaluate_NullInstrument_ReturnsBadRequest()
    {
        var result = await _controller.Evaluate(
            new StrategyEvaluateRequest { Source = SourceAdapterType.Tgju },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Evaluate_MissingSource_ReturnsBadRequest()
    {
        var result = await _controller.Evaluate(
            new StrategyEvaluateRequest { Instrument = "usd-irr" },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Evaluate_WithMatches_ReturnsStatisticsAndEvidence()
    {
        SetupResolver();
        await SeedEnrichedAsync(5, new DateOnly(2026, 1, 1), SourceAdapterType.Tgju);

        var result = await _controller.Evaluate(
            new StrategyEvaluateRequest
            {
                Instrument = "usd-irr",
                Source = SourceAdapterType.Tgju,
                From = new DateOnly(2026, 1, 1),
                To = new DateOnly(2026, 1, 5),
                RuleNames = ["TrendFollowing"],
                ForwardHorizons = [1],
                MaxEvidenceSamples = 2
            },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<StrategyEvaluateResponse>(ok.Value);
        Assert.True(response.Ok);
        Assert.Equal(5, response.ObservationsEvaluated);
        Assert.Equal(5, response.TotalRuleMatches);
        Assert.Equal(0, response.NoMatchCount);

        var stats = response.RuleStatistics.Single(s => s.RuleName == "TrendFollowing");
        Assert.Equal(5, stats.MatchCount);
        var h1 = stats.ForwardReturns.Single(f => f.HorizonObservations == 1);
        // Matches at indices 0..3 have a next observation; the last match's horizon is unavailable.
        Assert.Equal(4, h1.MeasurableCount);
        Assert.Equal(1, h1.UnavailableOutcomeCount);

        // Evidence contains actual values.
        var sample = response.MatchSamples[0];
        Assert.True(sample.Regime.Trend == "Bullish");
        var rsiEvidence = sample.RuleEvaluations[0].Evidence.Single(e => e.Name == "RSI");
        Assert.Equal(55m, rsiEvidence.Value);
    }

    [Fact]
    public async Task Evaluate_SourcesRemainSeparate()
    {
        SetupResolver();
        await SeedEnrichedAsync(5, new DateOnly(2026, 1, 1), SourceAdapterType.Tgju);

        // Nobitex has no data; evaluation must not silently read TGJU data.
        var result = await _controller.Evaluate(
            new StrategyEvaluateRequest
            {
                Instrument = "usd-irr",
                Source = SourceAdapterType.Nobitex,
                From = new DateOnly(2026, 1, 1),
                To = new DateOnly(2026, 1, 5)
            },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<StrategyEvaluateResponse>(ok.Value);
        Assert.False(response.Ok);
        Assert.Equal("NO_DATA", response.ErrorCode);
    }

    [Fact]
    public async Task Evaluate_UnknownRuleName_ReturnsInvalidRequest()
    {
        SetupResolver();
        await SeedEnrichedAsync(3, new DateOnly(2026, 1, 1), SourceAdapterType.Tgju);

        var result = await _controller.Evaluate(
            new StrategyEvaluateRequest
            {
                Instrument = "usd-irr",
                Source = SourceAdapterType.Tgju,
                From = new DateOnly(2026, 1, 1),
                To = new DateOnly(2026, 1, 3),
                RuleNames = ["NoSuchRule"]
            },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<StrategyEvaluateResponse>(ok.Value);
        Assert.False(response.Ok);
        Assert.Equal("INVALID_REQUEST", response.ErrorCode);
    }

    // =====================================================================
    // Splits endpoint
    // =====================================================================

    [Fact]
    public async Task GetSplits_WithEnrichedData_ReturnsChronologicalSegments()
    {
        SetupResolver();
        await SeedEnrichedAsync(100, new DateOnly(2026, 1, 1), SourceAdapterType.Tgju);

        var result = await _controller.GetSplits(
            "usd-irr", SourceAdapterType.Tgju,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 4, 10),
            null, null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<SplitResponse>(ok.Value);
        Assert.True(response.Ok);
        Assert.Equal(100, response.TotalObservations);

        Assert.Equal(3, response.Segments.Count);
        Assert.Equal("Research", response.Segments[0].Segment);
        Assert.Equal("Validation", response.Segments[1].Segment);
        Assert.Equal("Holdout", response.Segments[2].Segment);
        Assert.Equal(60, response.Segments[0].Observations);

        // Chronological ordering: no future leakage between segments.
        Assert.True(response.Segments[0].LastObservation < response.Segments[1].FirstObservation);
        Assert.True(response.Segments[1].LastObservation < response.Segments[2].FirstObservation);
    }

    [Fact]
    public async Task GetSplits_NullInstrument_ReturnsOkWithInvalidRequest()
    {
        var result = await _controller.GetSplits(
            null, SourceAdapterType.Tgju, null, null, null, null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<SplitResponse>(ok.Value);
        Assert.False(response.Ok);
        Assert.Equal("INVALID_REQUEST", response.ErrorCode);
    }

    // =====================================================================
    // Setups endpoint (Phase 8 setup generation)
    // =====================================================================

    [Fact]
    public async Task Setups_NullInstrument_ReturnsBadRequest()
    {
        var result = await _controller.GenerateSetups(
            new StrategySetupsRequest { Source = SourceAdapterType.Tgju },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Setups_MissingSource_ReturnsBadRequest()
    {
        var result = await _controller.GenerateSetups(
            new StrategySetupsRequest { Instrument = "usd-irr" },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Setups_NoEnrichedData_ReturnsNoData()
    {
        SetupResolver();

        var result = await _controller.GenerateSetups(
            new StrategySetupsRequest
            {
                Instrument = "usd-irr",
                Source = SourceAdapterType.Tgju,
                From = new DateOnly(2026, 1, 1),
                To = new DateOnly(2026, 1, 5)
            },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<StrategySetupsResponse>(ok.Value);
        Assert.False(response.Ok);
        Assert.Equal("NO_DATA", response.ErrorCode);
    }

    [Fact]
    public async Task Setups_WithQualifyingData_ReturnsStructuredSetupsWithProvenance()
    {
        SetupResolver();

        // Seed a series with a strictly RISING MACD histogram so every day
        // satisfies TrendContinuation (the flat-histogram series used elsewhere
        // is correctly excluded from day 2 on — momentum must be improving).
        var observations = Enumerable.Range(0, 5).Select(i => new EnrichedObservation
        {
            InstrumentId = InstrumentId,
            Source = SourceAdapterType.Tgju,
            ObservationDate = new DateOnly(2026, 1, 1).AddDays(i),
            Open = 99m + i,
            High = 101m + i,
            Low = 98m + i,
            Close = 100m + i,
            Volume = 0,
            Indicators = new Dictionary<string, IReadOnlyDictionary<string, decimal>>
            {
                ["SMA"] = new Dictionary<string, decimal> { ["SMA"] = 90m },
                ["RSI"] = new Dictionary<string, decimal> { ["RSI"] = 60m },
                ["MACD"] = new Dictionary<string, decimal>
                {
                    ["MACD"] = 2m, ["Signal"] = 1m, ["Histogram"] = 1m + i
                }
            },
            QualityStatus = HistoricalDataQualityStatus.Valid,
            ProcessedBy = "test"
        }).ToList();
        await _enrichedStore.UpsertEnrichedAsync(InstrumentId, SourceAdapterType.Tgju, observations);

        var result = await _controller.GenerateSetups(
            new StrategySetupsRequest
            {
                Instrument = "usd-irr",
                Source = SourceAdapterType.Tgju,
                From = new DateOnly(2026, 1, 1),
                To = new DateOnly(2026, 1, 5)
            },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<StrategySetupsResponse>(ok.Value);
        Assert.True(response.Ok);
        Assert.Equal(5, response.ObservationsEvaluated);
        Assert.Equal(5, response.Setups.Count);

        // Every setup is fully traceable to the dataset identity.
        Assert.All(response.Setups, s =>
        {
            Assert.Equal(InstrumentId, s.InstrumentId);
            Assert.Equal("Tgju", s.Source);
            Assert.Equal("TrendContinuation", s.RuleName);
            Assert.Equal("test", s.ProcessedBy);
            Assert.StartsWith($"{InstrumentId}|Tgju|TrendContinuation|", s.SetupId);
            Assert.NotEmpty(s.EntryCondition);
            Assert.NotEmpty(s.Invalidation.Condition);
            Assert.NotEmpty(s.SupportingEvidence);
        });

        Assert.Equal(5, response.SetupsPerRule.GetValueOrDefault("TrendContinuation"));
    }

    [Fact]
    public async Task Setups_FlatHistogram_ExcludedFromSecondDayOn()
    {
        SetupResolver();
        await SeedEnrichedAsync(5, new DateOnly(2026, 1, 1), SourceAdapterType.Tgju);

        var result = await _controller.GenerateSetups(
            new StrategySetupsRequest
            {
                Instrument = "usd-irr",
                Source = SourceAdapterType.Tgju,
                From = new DateOnly(2026, 1, 1),
                To = new DateOnly(2026, 1, 5)
            },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<StrategySetupsResponse>(ok.Value);
        Assert.True(response.Ok);

        // Day 1 has no predecessor (no falling evidence) → setup emitted.
        // Days 2–5: histogram flat (not rising) → continuation excluded.
        var setup = Assert.Single(response.Setups);
        Assert.Equal(new DateOnly(2026, 1, 1), setup.ObservationDate);
        Assert.Equal("TrendContinuation", setup.RuleName);
    }

    [Fact]
    public async Task Setups_DeterministicAcrossRepeatedCalls()
    {
        SetupResolver();
        await SeedEnrichedAsync(5, new DateOnly(2026, 1, 1), SourceAdapterType.Tgju);

        var request = new StrategySetupsRequest
        {
            Instrument = "usd-irr",
            Source = SourceAdapterType.Tgju,
            From = new DateOnly(2026, 1, 1),
            To = new DateOnly(2026, 1, 5)
        };

        var first = await _controller.GenerateSetups(request, CancellationToken.None);
        var second = await _controller.GenerateSetups(request, CancellationToken.None);

        var firstResponse = Assert.IsType<StrategySetupsResponse>(((OkObjectResult)first).Value);
        var secondResponse = Assert.IsType<StrategySetupsResponse>(((OkObjectResult)second).Value);

        Assert.Equal(
            firstResponse.Setups.Select(s => s.SetupId),
            secondResponse.Setups.Select(s => s.SetupId));
        Assert.Equal(
            firstResponse.Setups.Select(s => (s.ObservationDate, s.Direction, s.EntryCondition)),
            secondResponse.Setups.Select(s => (s.ObservationDate, s.Direction, s.EntryCondition)));
    }

    [Fact]
    public async Task Setups_UnknownRuleName_ReturnsInvalidRequest()
    {
        SetupResolver();
        await SeedEnrichedAsync(3, new DateOnly(2026, 1, 1), SourceAdapterType.Tgju);

        var result = await _controller.GenerateSetups(
            new StrategySetupsRequest
            {
                Instrument = "usd-irr",
                Source = SourceAdapterType.Tgju,
                From = new DateOnly(2026, 1, 1),
                To = new DateOnly(2026, 1, 3),
                RuleNames = ["NoSuchSetupRule"]
            },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<StrategySetupsResponse>(ok.Value);
        Assert.False(response.Ok);
        Assert.Equal("INVALID_REQUEST", response.ErrorCode);
    }

    [Fact]
    public async Task Setups_SourcesRemainSeparate()
    {
        SetupResolver();
        await SeedEnrichedAsync(5, new DateOnly(2026, 1, 1), SourceAdapterType.Tgju);

        var result = await _controller.GenerateSetups(
            new StrategySetupsRequest
            {
                Instrument = "usd-irr",
                Source = SourceAdapterType.Nobitex,
                From = new DateOnly(2026, 1, 1),
                To = new DateOnly(2026, 1, 5)
            },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<StrategySetupsResponse>(ok.Value);
        Assert.False(response.Ok);
        Assert.Equal("NO_DATA", response.ErrorCode);
    }
}

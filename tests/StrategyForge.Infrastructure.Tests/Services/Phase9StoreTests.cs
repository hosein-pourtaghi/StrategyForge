using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Models;
using StrategyForge.Infrastructure.Services;
using Xunit;

namespace StrategyForge.Infrastructure.Tests.Services;

// ============================================================
// InMemoryEvidenceStore Tests
// ============================================================

public class InMemoryEvidenceStoreTests
{
    private static Asset CreateTestAsset(string symbol = "TEST") => new()
    {
        Symbol = symbol,
        Name = $"Test Asset {symbol}",
        Market = "TSE",
        AssetType = AssetType.Stock
    };

    private static AnalysisEvidence CreateTestEvidence(Asset asset) => new()
    {
        Asset = asset,
        AssembledAt = DateTimeOffset.UtcNow,
        CurrentPrice = 15000m,
        IndicatorValues = new Dictionary<string, IndicatorResult>
        {
            ["RSI"] = new() { IndicatorName = "RSI", Date = DateOnly.FromDateTime(DateTime.Today), Value = 55m, Period = 14 }
        }
    };

    private static PersistedEvidence CreateTestPersistedEvidence(
        string symbol = "TEST",
        DateTimeOffset? assembledAt = null) => new()
    {
        Asset = CreateTestAsset(symbol),
        AssembledAt = assembledAt ?? DateTimeOffset.UtcNow,
        Evidence = CreateTestEvidence(CreateTestAsset(symbol)),
        DataSources = ["TSETMC"],
        IndicatorCount = 1,
        NewsItemCount = 0
    };

    [Fact]
    public async Task StoreAsync_PersistsEvidence()
    {
        var store = new InMemoryEvidenceStore();
        var evidence = CreateTestPersistedEvidence();

        var stored = await store.StoreAsync(evidence);

        Assert.Equal(evidence.Id, stored.Id);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsStoredEvidence()
    {
        var store = new InMemoryEvidenceStore();
        var evidence = CreateTestPersistedEvidence();

        await store.StoreAsync(evidence);
        var retrieved = await store.GetByIdAsync(evidence.Id);

        Assert.NotNull(retrieved);
        Assert.Equal("TEST", retrieved!.Asset.Symbol);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullForMissing()
    {
        var store = new InMemoryEvidenceStore();

        var result = await store.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetLatestByAssetAsync_ReturnsMostRecent()
    {
        var store = new InMemoryEvidenceStore();
        var older = CreateTestPersistedEvidence(assembledAt: DateTimeOffset.UtcNow.AddHours(-2));
        var newer = CreateTestPersistedEvidence(assembledAt: DateTimeOffset.UtcNow);

        await store.StoreAsync(older);
        await store.StoreAsync(newer);

        var latest = await store.GetLatestByAssetAsync("TEST");

        Assert.NotNull(latest);
        Assert.Equal(newer.Id, latest!.Id);
    }

    [Fact]
    public async Task GetLatestByAssetAsync_ReturnsNullForUnknownAsset()
    {
        var store = new InMemoryEvidenceStore();
        await store.StoreAsync(CreateTestPersistedEvidence("TEST"));

        var result = await store.GetLatestByAssetAsync("UNKNOWN");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByAssetAndDateRangeAsync_FiltersCorrectly()
    {
        var store = new InMemoryEvidenceStore();
        var now = DateTimeOffset.UtcNow;

        await store.StoreAsync(CreateTestPersistedEvidence(assembledAt: now.AddDays(-10)));
        await store.StoreAsync(CreateTestPersistedEvidence(assembledAt: now.AddDays(-5)));
        await store.StoreAsync(CreateTestPersistedEvidence(assembledAt: now.AddDays(-1)));

        var results = await store.GetByAssetAndDateRangeAsync(
            "TEST", now.AddDays(-6), now.AddDays(-2));

        Assert.Single(results);
    }

    [Fact]
    public async Task GetRecentAsync_ReturnsAllEvidenceOrderedByDate()
    {
        var store = new InMemoryEvidenceStore();
        var now = DateTimeOffset.UtcNow;

        await store.StoreAsync(CreateTestPersistedEvidence(assembledAt: now.AddDays(-2)));
        await store.StoreAsync(CreateTestPersistedEvidence(assembledAt: now.AddDays(-1)));
        await store.StoreAsync(CreateTestPersistedEvidence(assembledAt: now));

        var results = await store.GetRecentAsync();

        Assert.Equal(3, results.Count);
        Assert.True(results[0].AssembledAt >= results[1].AssembledAt);
    }

    [Fact]
    public async Task GetRecentAsync_RespectsMaxResults()
    {
        var store = new InMemoryEvidenceStore();

        for (int i = 0; i < 10; i++)
            await store.StoreAsync(CreateTestPersistedEvidence());

        var results = await store.GetRecentAsync(maxResults: 3);

        Assert.Equal(3, results.Count);
    }

    [Fact]
    public async Task CountByAssetAsync_CountsCorrectly()
    {
        var store = new InMemoryEvidenceStore();

        await store.StoreAsync(CreateTestPersistedEvidence("TEST"));
        await store.StoreAsync(CreateTestPersistedEvidence("TEST"));
        await store.StoreAsync(CreateTestPersistedEvidence("OTHER"));

        var count = await store.CountByAssetAsync("TEST");

        Assert.Equal(2, count);
    }
}

// ============================================================
// InMemoryStrategyHistoryStore Tests
// ============================================================

public class InMemoryStrategyHistoryStoreTests
{
    private static Asset CreateTestAsset(string symbol = "TEST") => new()
    {
        Symbol = symbol,
        Name = $"Test Asset {symbol}",
        Market = "TSE",
        AssetType = AssetType.Stock
    };

    private static StrategyReport CreateTestReport(Asset asset) => new()
    {
        Asset = asset,
        GeneratedAt = DateTimeOffset.UtcNow,
        DataAsOf = DateTimeOffset.UtcNow,
        ExecutiveSummary = new ExecutiveSummary
        {
            OverallSentiment = Sentiment.Bullish,
            Summary = "Test strategy"
        },
        MarketContext = new MarketContext
        {
            Regime = MarketRegime.Uptrend,
            Description = "Strong uptrend"
        }
    };

    private static PersistedStrategy CreateTestPersistedStrategy(
        string symbol = "TEST",
        DateTimeOffset? generatedAt = null,
        PipelineState state = PipelineState.Completed) => new()
    {
        Asset = CreateTestAsset(symbol),
        GeneratedAt = generatedAt ?? DateTimeOffset.UtcNow,
        Report = CreateTestReport(CreateTestAsset(symbol)),
        OverallSentiment = Sentiment.Bullish,
        PipelineState = state
    };

    [Fact]
    public async Task StoreAsync_PersistsStrategy()
    {
        var store = new InMemoryStrategyHistoryStore();
        var strategy = CreateTestPersistedStrategy();

        var stored = await store.StoreAsync(strategy);

        Assert.Equal(strategy.Id, stored.Id);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsStoredStrategy()
    {
        var store = new InMemoryStrategyHistoryStore();
        var strategy = CreateTestPersistedStrategy();

        await store.StoreAsync(strategy);
        var retrieved = await store.GetByIdAsync(strategy.Id);

        Assert.NotNull(retrieved);
        Assert.Equal("TEST", retrieved!.Asset.Symbol);
        Assert.Equal(Sentiment.Bullish, retrieved.OverallSentiment);
    }

    [Fact]
    public async Task GetLatestByAssetAsync_ReturnsMostRecent()
    {
        var store = new InMemoryStrategyHistoryStore();
        var older = CreateTestPersistedStrategy(generatedAt: DateTimeOffset.UtcNow.AddHours(-2));
        var newer = CreateTestPersistedStrategy(generatedAt: DateTimeOffset.UtcNow);

        await store.StoreAsync(older);
        await store.StoreAsync(newer);

        var latest = await store.GetLatestByAssetAsync("TEST");

        Assert.NotNull(latest);
        Assert.Equal(newer.Id, latest!.Id);
    }

    [Fact]
    public async Task GetByAssetAndDateRangeAsync_FiltersCorrectly()
    {
        var store = new InMemoryStrategyHistoryStore();
        var now = DateTimeOffset.UtcNow;

        await store.StoreAsync(CreateTestPersistedStrategy(generatedAt: now.AddDays(-10)));
        await store.StoreAsync(CreateTestPersistedStrategy(generatedAt: now.AddDays(-5)));
        await store.StoreAsync(CreateTestPersistedStrategy(generatedAt: now.AddDays(-1)));

        var results = await store.GetByAssetAndDateRangeAsync(
            "TEST", now.AddDays(-6), now.AddDays(-2));

        Assert.Single(results);
    }

    [Fact]
    public async Task GetByStateAsync_FiltersCorrectly()
    {
        var store = new InMemoryStrategyHistoryStore();

        await store.StoreAsync(CreateTestPersistedStrategy(state: PipelineState.Completed));
        await store.StoreAsync(CreateTestPersistedStrategy(state: PipelineState.Failed));
        await store.StoreAsync(CreateTestPersistedStrategy(state: PipelineState.Completed));

        var completed = await store.GetByStateAsync(PipelineState.Completed);
        var failed = await store.GetByStateAsync(PipelineState.Failed);

        Assert.Equal(2, completed.Count);
        Assert.Single(failed);
    }

    [Fact]
    public async Task GetRecentAsync_ReturnsAllOrderedByDate()
    {
        var store = new InMemoryStrategyHistoryStore();
        var now = DateTimeOffset.UtcNow;

        await store.StoreAsync(CreateTestPersistedStrategy(generatedAt: now.AddDays(-2)));
        await store.StoreAsync(CreateTestPersistedStrategy(generatedAt: now.AddDays(-1)));
        await store.StoreAsync(CreateTestPersistedStrategy(generatedAt: now));

        var results = await store.GetRecentAsync();

        Assert.Equal(3, results.Count);
        Assert.True(results[0].GeneratedAt >= results[1].GeneratedAt);
    }

    [Fact]
    public async Task CountByAssetAsync_CountsCorrectly()
    {
        var store = new InMemoryStrategyHistoryStore();

        await store.StoreAsync(CreateTestPersistedStrategy("TEST"));
        await store.StoreAsync(CreateTestPersistedStrategy("TEST"));
        await store.StoreAsync(CreateTestPersistedStrategy("OTHER"));

        var count = await store.CountByAssetAsync("TEST");

        Assert.Equal(2, count);
    }
}

// ============================================================
// InMemoryIntelligenceRunStore Tests
// ============================================================

public class InMemoryIntelligenceRunStoreTests
{
    [Fact]
    public async Task StoreAsync_PersistsRun()
    {
        var store = new InMemoryIntelligenceRunStore();
        var run = new IntelligenceRun
        {
            ScheduledAt = DateTimeOffset.UtcNow,
            TargetAssets = ["TEST"]
        };

        var stored = await store.StoreAsync(run);

        Assert.Equal(run.Id, stored.Id);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsStoredRun()
    {
        var store = new InMemoryIntelligenceRunStore();
        var run = new IntelligenceRun
        {
            ScheduledAt = DateTimeOffset.UtcNow,
            TargetAssets = ["TEST"]
        };

        await store.StoreAsync(run);
        var retrieved = await store.GetByIdAsync(run.Id);

        Assert.NotNull(retrieved);
        Assert.Single(retrieved!.TargetAssets);
    }

    [Fact]
    public async Task UpdateAsync_ModifiesStoredRun()
    {
        var store = new InMemoryIntelligenceRunStore();
        var run = new IntelligenceRun
        {
            ScheduledAt = DateTimeOffset.UtcNow,
            State = IntelligenceRunState.Running
        };

        await store.StoreAsync(run);

        var updated = run with
        {
            State = IntelligenceRunState.Completed,
            CompletedAt = DateTimeOffset.UtcNow,
            SuccessfulAssets = 1
        };
        await store.UpdateAsync(updated);

        var retrieved = await store.GetByIdAsync(run.Id);

        Assert.NotNull(retrieved);
        Assert.Equal(IntelligenceRunState.Completed, retrieved!.State);
        Assert.Equal(1, retrieved.SuccessfulAssets);
    }

    [Fact]
    public async Task GetRecentAsync_ReturnsOrderedByDate()
    {
        var store = new InMemoryIntelligenceRunStore();

        await store.StoreAsync(new IntelligenceRun
        {
            ScheduledAt = DateTimeOffset.UtcNow.AddHours(-2)
        });
        await store.StoreAsync(new IntelligenceRun
        {
            ScheduledAt = DateTimeOffset.UtcNow
        });

        var results = await store.GetRecentAsync();

        Assert.Equal(2, results.Count);
        Assert.True(results[0].ScheduledAt >= results[1].ScheduledAt);
    }
}

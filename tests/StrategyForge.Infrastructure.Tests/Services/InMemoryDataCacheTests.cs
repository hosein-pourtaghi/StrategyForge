using StrategyForge.Infrastructure.Services;
using Xunit;

namespace StrategyForge.Infrastructure.Tests.Services;

public class InMemoryDataCacheTests
{
    private readonly InMemoryDataCache _cache = new();

    // --- Basic Operations ---

    [Fact]
    public void TryGet_ReturnsFalse_OnEmptyCache()
    {
        var found = _cache.TryGet<string>("missing", out var value);

        Assert.False(found);
        Assert.Null(value);
    }

    [Fact]
    public void Set_ThenTryGet_ReturnsValue()
    {
        _cache.Set("key1", "hello", TimeSpan.FromMinutes(5));

        var found = _cache.TryGet<string>("key1", out var value);

        Assert.True(found);
        Assert.Equal("hello", value);
    }

    [Fact]
    public void Set_ReplacesExistingValue()
    {
        _cache.Set("key1", "first", TimeSpan.FromMinutes(5));
        _cache.Set("key1", "second", TimeSpan.FromMinutes(5));

        _cache.TryGet<string>("key1", out var value);

        Assert.Equal("second", value);
    }

    [Fact]
    public void DifferentKeys_AreIndependent()
    {
        _cache.Set("a", "alpha", TimeSpan.FromMinutes(5));
        _cache.Set("b", "beta", TimeSpan.FromMinutes(5));

        _cache.TryGet<string>("a", out var a);
        _cache.TryGet<string>("b", out var b);

        Assert.Equal("alpha", a);
        Assert.Equal("beta", b);
    }

    // --- TTL Expiration ---

    [Fact]
    public void TryGet_ReturnsFalse_AfterExpiration()
    {
        // Set with very short TTL
        _cache.Set("expire-me", "value", TimeSpan.FromMilliseconds(1));

        // Wait for expiration
        Thread.Sleep(50);

        var found = _cache.TryGet<string>("expire-me", out var value);

        Assert.False(found);
        Assert.Null(value);
    }

    [Fact]
    public void TryGet_ReturnsTrue_BeforeExpiration()
    {
        _cache.Set("not-yet", "value", TimeSpan.FromSeconds(30));

        var found = _cache.TryGet<string>("not-yet", out var value);

        Assert.True(found);
        Assert.Equal("value", value);
    }

    // --- Type Safety ---

    [Fact]
    public void TryGet_WrongType_ReturnsFalse()
    {
        _cache.Set("int-key", 42, TimeSpan.FromMinutes(5));

        var found = _cache.TryGet<string>("int-key", out var value);

        Assert.False(found);
        Assert.Null(value);
    }

    [Fact]
    public void TryGet_CorrectType_ReturnsTrue()
    {
        _cache.Set("int-key", 42, TimeSpan.FromMinutes(5));

        var found = _cache.TryGet<int>("int-key", out var value);

        Assert.True(found);
        Assert.Equal(42, value);
    }

    // --- Remove ---

    [Fact]
    public void Remove_ExistingKey_ReturnsTrue()
    {
        _cache.Set("remove-me", "value", TimeSpan.FromMinutes(5));

        var removed = _cache.Remove("remove-me");

        Assert.True(removed);
        Assert.False(_cache.TryGet<string>("remove-me", out _));
    }

    [Fact]
    public void Remove_NonexistentKey_ReturnsFalse()
    {
        var removed = _cache.Remove("nope");

        Assert.False(removed);
    }

    // --- Clear ---

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        _cache.Set("a", 1, TimeSpan.FromMinutes(5));
        _cache.Set("b", 2, TimeSpan.FromMinutes(5));
        _cache.Set("c", 3, TimeSpan.FromMinutes(5));

        _cache.Clear();

        Assert.Equal(0, _cache.Count);
    }

    // --- Count ---

    [Fact]
    public void Count_ReturnsNumberOfEntries()
    {
        Assert.Equal(0, _cache.Count);

        _cache.Set("a", 1, TimeSpan.FromMinutes(5));
        Assert.Equal(1, _cache.Count);

        _cache.Set("b", 2, TimeSpan.FromMinutes(5));
        Assert.Equal(2, _cache.Count);
    }

    // --- Key Generation ---

    [Fact]
    public void MarketDataKey_GeneratesConsistentKey()
    {
        var key1 = InMemoryDataCache.MarketDataKey("ins-1", "Tsetmc", new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31));
        var key2 = InMemoryDataCache.MarketDataKey("ins-1", "Tsetmc", new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31));

        Assert.Equal(key1, key2);
    }

    [Fact]
    public void MarketDataKey_DifferentInstruments_GenerateDifferentKeys()
    {
        var key1 = InMemoryDataCache.MarketDataKey("ins-1", "Tsetmc", new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31));
        var key2 = InMemoryDataCache.MarketDataKey("ins-2", "Tsetmc", new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31));

        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void LatestCandleKey_GeneratesConsistentKey()
    {
        var key1 = InMemoryDataCache.LatestCandleKey("ins-1", "Tsetmc");
        var key2 = InMemoryDataCache.LatestCandleKey("ins-1", "Tsetmc");

        Assert.Equal(key1, key2);
    }

    [Fact]
    public void LatestCandleKey_DifferentInstruments_GenerateDifferentKeys()
    {
        var key1 = InMemoryDataCache.LatestCandleKey("ins-1", "Tsetmc");
        var key2 = InMemoryDataCache.LatestCandleKey("ins-2", "Tsetmc");

        Assert.NotEqual(key1, key2);
    }
}

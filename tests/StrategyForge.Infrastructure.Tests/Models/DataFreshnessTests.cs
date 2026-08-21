using StrategyForge.Domain.Models;
using Xunit;

namespace StrategyForge.Infrastructure.Tests.Models;

public class DataFreshnessTests
{
    // --- Freshness Creation ---

    [Fact]
    public void Fresh_CreatesNonCachedFreshness()
    {
        var before = DateTimeOffset.UtcNow;
        var freshness = DataFreshness.Fresh();
        var after = DateTimeOffset.UtcNow;

        Assert.False(freshness.IsCached);
        Assert.True(freshness.FetchedAtUtc >= before && freshness.FetchedAtUtc <= after);
        Assert.Equal(86400000, freshness.MaxAllowedAgeMs); // default 24h
    }

    [Fact]
    public void Fresh_WithLongOverload_SetsCorrectMaxAge()
    {
        var freshness = DataFreshness.Fresh(5000L);

        Assert.Equal(5000, freshness.MaxAllowedAgeMs);
        Assert.False(freshness.IsCached);
    }

    [Fact]
    public void Fresh_WithTimeSpanOverload_SetsCorrectMaxAge()
    {
        var freshness = DataFreshness.Fresh(TimeSpan.FromMinutes(15));

        Assert.Equal(900000, freshness.MaxAllowedAgeMs);
        Assert.False(freshness.IsCached);
    }

    [Fact]
    public void Cached_CreatesCachedFreshness()
    {
        var fetchedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var freshness = DataFreshness.Cached(fetchedAt);

        Assert.True(freshness.IsCached);
        Assert.Equal(fetchedAt, freshness.FetchedAtUtc);
        Assert.Equal(86400000, freshness.MaxAllowedAgeMs);
    }

    [Fact]
    public void Cached_WithCustomMaxAge()
    {
        var fetchedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        var freshness = DataFreshness.Cached(fetchedAt, 30000);

        Assert.True(freshness.IsCached);
        Assert.Equal(30000, freshness.MaxAllowedAgeMs);
    }

    // --- Freshness / Staleness ---

    [Fact]
    public void IsFresh_TrueWhenJustCreated()
    {
        var freshness = DataFreshness.Fresh(10000);

        Assert.True(freshness.IsFresh);
    }

    [Fact]
    public void IsFresh_FalseWhenExpired()
    {
        // Create with a past fetch time that exceeds max age
        var freshness = new DataFreshness
        {
            FetchedAtUtc = DateTimeOffset.UtcNow.AddHours(-2),
            MaxAllowedAgeMs = 3600000, // 1 hour
            IsCached = false
        };

        Assert.False(freshness.IsFresh);
    }

    [Fact]
    public void IsFresh_TrueAtBoundary()
    {
        // Just barely within the allowed age
        var freshness = new DataFreshness
        {
            FetchedAtUtc = DateTimeOffset.UtcNow,
            MaxAllowedAgeMs = 60000, // 1 minute
            IsCached = false
        };

        Assert.True(freshness.IsFresh);
    }

    // --- Age Calculation ---

    [Fact]
    public void AgeMs_ReturnsZeroForJustCreated()
    {
        var freshness = DataFreshness.Fresh();

        // Allow tiny tolerance for execution time
        Assert.True(freshness.AgeMs < 100);
    }

    [Fact]
    public void AgeMs_ReturnsPositiveForPastData()
    {
        var freshness = new DataFreshness
        {
            FetchedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-10),
            MaxAllowedAgeMs = 60000,
            IsCached = false
        };

        Assert.True(freshness.AgeMs >= 9000); // ~10 seconds, allow some tolerance
    }

    [Fact]
    public void AgeMs_ReturnsZeroForFutureDate()
    {
        // Should clamp to 0, never negative
        var freshness = new DataFreshness
        {
            FetchedAtUtc = DateTimeOffset.UtcNow.AddSeconds(30),
            MaxAllowedAgeMs = 60000,
            IsCached = false
        };

        Assert.Equal(0, freshness.AgeMs);
    }

    // --- Source Timestamp ---

    [Fact]
    public void SourceTimestampUtc_CanBeNull()
    {
        var freshness = DataFreshness.Fresh();

        Assert.Null(freshness.SourceTimestampUtc);
    }

    [Fact]
    public void SourceTimestampUtc_CanBeSet()
    {
        var sourceTime = DateTimeOffset.UtcNow.AddMinutes(-2);
        var freshness = new DataFreshness
        {
            FetchedAtUtc = DateTimeOffset.UtcNow,
            SourceTimestampUtc = sourceTime,
            MaxAllowedAgeMs = 60000,
            IsCached = false
        };

        Assert.Equal(sourceTime, freshness.SourceTimestampUtc);
    }

    // --- Record Equality ---

    [Fact]
    public void EqualInstances_AreEqual()
    {
        var time = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var a = new DataFreshness { FetchedAtUtc = time, MaxAllowedAgeMs = 1000, IsCached = false };
        var b = new DataFreshness { FetchedAtUtc = time, MaxAllowedAgeMs = 1000, IsCached = false };

        Assert.Equal(a, b);
    }

    [Fact]
    public void DifferentInstances_AreNotEqual()
    {
        var a = DataFreshness.Fresh(1000);
        var b = DataFreshness.Fresh(2000);

        Assert.NotEqual(a, b);
    }
}

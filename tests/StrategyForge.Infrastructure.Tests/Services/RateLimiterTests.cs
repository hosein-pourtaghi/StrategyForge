using StrategyForge.Domain.Enums;
using Microsoft.Extensions.Options;
using StrategyForge.Domain.Configuration;
using StrategyForge.Infrastructure.Services;
using Xunit;

namespace StrategyForge.Infrastructure.Tests.Services;

public class RateLimiterTests : IDisposable
{
    private readonly RateLimiter _limiter;

    public RateLimiterTests()
    {
        // Configure with fast rate for tests: 100 requests per second
        var settings = Options.Create(new DataSourceSettings
        {
            DefaultRateLimit = new RateLimitSettings
            {
                MaxRequests = 100,
                Window = TimeSpan.FromSeconds(1)
            }
        });
        _limiter = new RateLimiter(settings);
    }

    public void Dispose()
    {
        _limiter.Dispose();
    }

    // --- Effective Rate Limit Resolution ---

    [Fact]
    public void GetEffectiveRateLimit_DefaultsToConfiguredGlobalRate()
    {
        var settings = Options.Create(new DataSourceSettings
        {
            DefaultRateLimit = new RateLimitSettings { MaxRequests = 50, Window = TimeSpan.FromSeconds(10) }
        });
        using var limiter = new RateLimiter(settings);

        var effective = limiter.GetEffectiveRateLimit("unknown-source");
        Assert.Equal(50, effective.MaxRequests);
        Assert.Equal(TimeSpan.FromSeconds(10), effective.Window);
    }

    [Fact]
    public void GetEffectiveRateLimit_SourceSpecificOverrideWins()
    {
        var settings = Options.Create(new DataSourceSettings
        {
            DefaultRateLimit = new RateLimitSettings { MaxRequests = 10, Window = TimeSpan.FromMinutes(1) },
            Sources = new Dictionary<string, SourceAdapterConfig>
            {
                ["tsetmc"] = new()
                {
                    Name = "TSETMC",
                    SourceType = SourceAdapterType.Tsetmc,
                    BaseUrl = "https://cdn.tsetmc.com",
                    RateLimit = new RateLimitSettings { MaxRequests = 5, Window = TimeSpan.FromSeconds(30) }
                }
            }
        });
        using var limiter = new RateLimiter(settings);

        var effective = limiter.GetEffectiveRateLimit("tsetmc");
        Assert.Equal(5, effective.MaxRequests);
        Assert.Equal(TimeSpan.FromSeconds(30), effective.Window);
    }

    [Fact]
    public void GetEffectiveRateLimit_FallsBackToSafeDefault_WhenNoConfig()
    {
        var settings = Options.Create(new DataSourceSettings
        {
            DefaultRateLimit = new RateLimitSettings { MaxRequests = 0, Window = TimeSpan.Zero },
            Sources = new Dictionary<string, SourceAdapterConfig>()
        });
        using var limiter = new RateLimiter(settings);

        // Invalid config should fall back to built-in default (10/min)
        var effective = limiter.GetEffectiveRateLimit("unknown");
        Assert.Equal(10, effective.MaxRequests);
        Assert.Equal(TimeSpan.FromMinutes(1), effective.Window);
    }

    [Fact]
    public void GetEffectiveRateLimit_SourceWithNullRateLimit_FallsBackToGlobal()
    {
        var settings = Options.Create(new DataSourceSettings
        {
            DefaultRateLimit = new RateLimitSettings { MaxRequests = 20, Window = TimeSpan.FromSeconds(60) },
            Sources = new Dictionary<string, SourceAdapterConfig>
            {
                ["cbi"] = new()
                {
                    Name = "CBI",
                    SourceType = SourceAdapterType.Cbi,
                    BaseUrl = "https://cbi.ir",
                    RateLimit = null // No source-specific override
                }
            }
        });
        using var limiter = new RateLimiter(settings);

        var effective = limiter.GetEffectiveRateLimit("cbi");
        Assert.Equal(20, effective.MaxRequests);
    }

    // --- Basic Rate Limiting ---

    [Fact]
    public async Task WaitForSlotAsync_AllowsImmediateFirstRequest()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await _limiter.WaitForSlotAsync("test.com");
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 100);
    }

    [Fact]
    public async Task WaitForSlotAsync_DifferentSourcesAreIndependent()
    {
        // Exhaust source A (100 tokens, so this won't exhaust)
        for (int i = 0; i < 5; i++)
        {
            await _limiter.WaitForSlotAsync("source-a");
        }

        // Source B should still be available
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await _limiter.WaitForSlotAsync("source-b");
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 100);
    }

    [Fact]
    public async Task WaitForSlotAsync_WaitsWhenWindowExhausted()
    {
        // Use a very small rate: 3 requests per 1 second
        var settings = Options.Create(new DataSourceSettings
        {
            DefaultRateLimit = new RateLimitSettings { MaxRequests = 3, Window = TimeSpan.FromSeconds(1) }
        });
        using var limiter = new RateLimiter(settings);

        // Exhaust the window
        await limiter.WaitForSlotAsync("exhaust");
        await limiter.WaitForSlotAsync("exhaust");
        await limiter.WaitForSlotAsync("exhaust");

        // 4th request should block briefly
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await limiter.WaitForSlotAsync("exhaust");
        sw.Stop();

        // Should have waited for some portion of the 1s window
        Assert.True(sw.ElapsedMilliseconds > 100);
    }

    [Fact]
    public async Task WaitForSlotAsync_CancellationToken_Stops()
    {
        // Tiny rate to trigger waiting
        var settings = Options.Create(new DataSourceSettings
        {
            DefaultRateLimit = new RateLimitSettings { MaxRequests = 1, Window = TimeSpan.FromSeconds(60) }
        });
        using var limiter = new RateLimiter(settings);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Exhaust the single slot
        await limiter.WaitForSlotAsync("cancel");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await limiter.WaitForSlotAsync("cancel", cts.Token);
        });
    }

    [Fact]
    public async Task WaitForSlotAsync_ConcurrentAccess_IsSafe()
    {
        var tasks = new List<Task>();
        var completedCount = 0;

        for (int i = 0; i < 20; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                await _limiter.WaitForSlotAsync("concurrent");
                Interlocked.Increment(ref completedCount);
            }));
        }

        await Task.WhenAll(tasks);

        Assert.Equal(20, completedCount);
    }

    // --- RateLimitSettings Validation ---

    [Fact]
    public void RateLimitSettings_IsValid_ReturnsTrueForValidSettings()
    {
        var valid = new RateLimitSettings { MaxRequests = 10, Window = TimeSpan.FromMinutes(1) };
        Assert.True(valid.IsValid);
    }

    [Fact]
    public void RateLimitSettings_IsValid_ReturnsFalseForZeroMax()
    {
        var invalid = new RateLimitSettings { MaxRequests = 0, Window = TimeSpan.FromMinutes(1) };
        Assert.False(invalid.IsValid);
    }

    [Fact]
    public void RateLimitSettings_IsValid_ReturnsFalseForZeroWindow()
    {
        var invalid = new RateLimitSettings { MaxRequests = 10, Window = TimeSpan.Zero };
        Assert.False(invalid.IsValid);
    }

    [Fact]
    public void RateLimitSettings_Default_Is10PerMinute()
    {
        var def = RateLimitSettings.Default;
        Assert.Equal(10, def.MaxRequests);
        Assert.Equal(TimeSpan.FromMinutes(1), def.Window);
    }
}

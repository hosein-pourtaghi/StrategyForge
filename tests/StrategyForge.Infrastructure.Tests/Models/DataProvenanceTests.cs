using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Models;
using Xunit;

namespace StrategyForge.Infrastructure.Tests.Models;

public class DataProvenanceTests
{
    [Fact]
    public void RequiredFields_AreSet()
    {
        var p = new DataProvenance
        {
            Source = SourceAdapterType.Tsetmc,
            FetchedAtUtc = DateTimeOffset.UtcNow,
            IsCached = false
        };

        Assert.Equal(SourceAdapterType.Tsetmc, p.Source);
        Assert.False(p.IsCached);
    }

    [Fact]
    public void SourceSymbol_CanBeNull()
    {
        var p = new DataProvenance
        {
            Source = SourceAdapterType.Cbi,
            FetchedAtUtc = DateTimeOffset.UtcNow,
            IsCached = false
        };

        Assert.Null(p.SourceSymbol);
    }

    [Fact]
    public void SourceInstrumentId_CanBeNull()
    {
        var p = new DataProvenance
        {
            Source = SourceAdapterType.Tgju,
            FetchedAtUtc = DateTimeOffset.UtcNow,
            IsCached = false
        };

        Assert.Null(p.SourceInstrumentId);
    }

    [Fact]
    public void SourceSymbol_CanBeSet()
    {
        var p = new DataProvenance
        {
            Source = SourceAdapterType.Tsetmc,
            SourceSymbol = "فولاد",
            SourceInstrumentId = "4439113430858354",
            FetchedAtUtc = DateTimeOffset.UtcNow,
            IsCached = false
        };

        Assert.Equal("فولاد", p.SourceSymbol);
        Assert.Equal("4439113430858354", p.SourceInstrumentId);
    }

    [Fact]
    public void SourceTimestampUtc_CanBeNull()
    {
        var p = new DataProvenance
        {
            Source = SourceAdapterType.Tsetmc,
            FetchedAtUtc = DateTimeOffset.UtcNow,
            IsCached = false
        };

        Assert.Null(p.SourceTimestampUtc);
    }

    [Fact]
    public void SourceTimestampUtc_CanBeSet()
    {
        var ts = DateTimeOffset.UtcNow.AddMinutes(-5);
        var p = new DataProvenance
        {
            Source = SourceAdapterType.Tgju,
            FetchedAtUtc = DateTimeOffset.UtcNow,
            SourceTimestampUtc = ts,
            IsCached = false
        };

        Assert.Equal(ts, p.SourceTimestampUtc);
    }

    [Fact]
    public void IsDerived_DefaultsFalse()
    {
        var p = new DataProvenance
        {
            Source = SourceAdapterType.Tsetmc,
            FetchedAtUtc = DateTimeOffset.UtcNow,
            IsCached = false
        };

        Assert.False(p.IsDerived);
    }

    [Fact]
    public void IsDerived_CanBeSet()
    {
        var p = new DataProvenance
        {
            Source = SourceAdapterType.Tsetmc,
            FetchedAtUtc = DateTimeOffset.UtcNow,
            IsCached = false,
            IsDerived = true,
            InputSources = [SourceAdapterType.Tsetmc, SourceAdapterType.Tgju]
        };

        Assert.True(p.IsDerived);
        Assert.Equal(2, p.InputSources.Count);
        Assert.Contains(SourceAdapterType.Tsetmc, p.InputSources);
        Assert.Contains(SourceAdapterType.Tgju, p.InputSources);
    }

    [Fact]
    public void InputSources_DefaultsEmpty()
    {
        var p = new DataProvenance
        {
            Source = SourceAdapterType.Tsetmc,
            FetchedAtUtc = DateTimeOffset.UtcNow,
            IsCached = false
        };

        Assert.Empty(p.InputSources);
    }

    [Fact]
    public void Endpoint_CanBeNull()
    {
        var p = new DataProvenance
        {
            Source = SourceAdapterType.Tsetmc,
            FetchedAtUtc = DateTimeOffset.UtcNow,
            IsCached = false
        };

        Assert.Null(p.Endpoint);
    }

    [Fact]
    public void Endpoint_CanBeSet()
    {
        var p = new DataProvenance
        {
            Source = SourceAdapterType.Tsetmc,
            FetchedAtUtc = DateTimeOffset.UtcNow,
            IsCached = false,
            Endpoint = "closingPriceHistory"
        };

        Assert.Equal("closingPriceHistory", p.Endpoint);
    }

    [Fact]
    public void ExtraProperties_CanBeNull()
    {
        var p = new DataProvenance
        {
            Source = SourceAdapterType.Tsetmc,
            FetchedAtUtc = DateTimeOffset.UtcNow,
            IsCached = false
        };

        Assert.Null(p.ExtraProperties);
    }

    [Fact]
    public void ExtraProperties_CanBeSet()
    {
        var props = new Dictionary<string, string> { ["insCode"] = "12345" };
        var p = new DataProvenance
        {
            Source = SourceAdapterType.Tsetmc,
            FetchedAtUtc = DateTimeOffset.UtcNow,
            IsCached = false,
            ExtraProperties = props
        };

        Assert.NotNull(p.ExtraProperties);
        Assert.Equal("12345", p.ExtraProperties!["insCode"]);
    }

    [Fact]
    public void AllSourceAdapterTypes_CanBeUsed()
    {
        foreach (var source in Enum.GetValues<SourceAdapterType>())
        {
            var p = new DataProvenance
            {
                Source = source,
                FetchedAtUtc = DateTimeOffset.UtcNow,
                IsCached = false
            };

            Assert.Equal(source, p.Source);
        }
    }

    [Fact]
    public void RecordEquality_SameValuesAreEqual()
    {
        var time = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var a = new DataProvenance { Source = SourceAdapterType.Tsetmc, FetchedAtUtc = time, IsCached = false };
        var b = new DataProvenance { Source = SourceAdapterType.Tsetmc, FetchedAtUtc = time, IsCached = false };

        Assert.Equal(a, b);
    }
}

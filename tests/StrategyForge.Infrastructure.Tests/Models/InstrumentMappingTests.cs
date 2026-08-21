using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Models;
using Xunit;

namespace StrategyForge.Infrastructure.Tests.Models;

public class InstrumentMappingTests
{
    private static InstrumentMapping CreateFoolad() => new()
    {
        InstrumentId = "iran-equity-foolad-4439113430858354",
        Symbol = "فولاد",
        LatinSymbol = "Foolad",
        DisplayName = "Foolad Mobarakeh",
        AssetClass = AssetType.Stock,
        Exchange = "TSE",
        QuoteCurrency = "IRR",
        SourceIdentifiers = new Dictionary<SourceAdapterType, SourceIdentifier>
        {
            [SourceAdapterType.Tsetmc] = new() { Id = "4439113430858354", SourceSymbol = "فولاد" },
            [SourceAdapterType.Rahavard365] = new() { Id = "foolad", SourceSymbol = "فولاد" }
        }
    };

    [Fact]
    public void RequiredFields_AreSet()
    {
        var m = CreateFoolad();

        Assert.Equal("iran-equity-foolad-4439113430858354", m.InstrumentId);
        Assert.Equal("فولاد", m.Symbol);
        Assert.Equal("Foolad Mobarakeh", m.DisplayName);
        Assert.Equal(AssetType.Stock, m.AssetClass);
        Assert.Equal("TSE", m.Exchange);
        Assert.Equal("IRR", m.QuoteCurrency);
    }

    [Fact]
    public void LatinSymbol_CanBeNull()
    {
        var m = new InstrumentMapping
        {
            InstrumentId = "test",
            Symbol = "test",
            DisplayName = "Test",
            AssetClass = AssetType.Stock,
            Exchange = "TSE",
            QuoteCurrency = "IRR"
        };

        Assert.Null(m.LatinSymbol);
    }

    [Fact]
    public void LatinSymbol_CanBeSet()
    {
        var m = CreateFoolad();

        Assert.Equal("Foolad", m.LatinSymbol);
    }

    [Fact]
    public void SourceIdentifiers_DefaultsEmpty()
    {
        var m = new InstrumentMapping
        {
            InstrumentId = "test",
            Symbol = "test",
            DisplayName = "Test",
            AssetClass = AssetType.Stock,
            Exchange = "TSE",
            QuoteCurrency = "IRR"
        };

        Assert.Empty(m.SourceIdentifiers);
    }

    [Fact]
    public void SourceIdentifiers_MultipleSources()
    {
        var m = CreateFoolad();

        Assert.Equal(2, m.SourceIdentifiers.Count);
        Assert.True(m.SourceIdentifiers.ContainsKey(SourceAdapterType.Tsetmc));
        Assert.True(m.SourceIdentifiers.ContainsKey(SourceAdapterType.Rahavard365));
    }

    [Fact]
    public void SourceIdentifier_PreservesIdAndSymbol()
    {
        var m = CreateFoolad();
        var tsetmc = m.SourceIdentifiers[SourceAdapterType.Tsetmc];

        Assert.Equal("4439113430858354", tsetmc.Id);
        Assert.Equal("فولاد", tsetmc.SourceSymbol);
    }

    [Fact]
    public void SourceIdentifier_LastVerified_CanBeNull()
    {
        var id = new SourceIdentifier { Id = "123" };

        Assert.Null(id.LastVerified);
    }

    [Fact]
    public void SourceIdentifier_LastVerified_CanBeSet()
    {
        var time = DateTimeOffset.UtcNow;
        var id = new SourceIdentifier { Id = "123", LastVerified = time };

        Assert.Equal(time, id.LastVerified);
    }

    [Fact]
    public void IsActive_DefaultsTrue()
    {
        var m = new InstrumentMapping
        {
            InstrumentId = "test",
            Symbol = "test",
            DisplayName = "Test",
            AssetClass = AssetType.Stock,
            Exchange = "TSE",
            QuoteCurrency = "IRR"
        };

        Assert.True(m.IsActive);
    }

    [Fact]
    public void IsActive_CanBeFalse()
    {
        var m = new InstrumentMapping
        {
            InstrumentId = "test",
            Symbol = "test",
            DisplayName = "Test",
            AssetClass = AssetType.Stock,
            Exchange = "TSE",
            QuoteCurrency = "IRR",
            IsActive = false
        };

        Assert.False(m.IsActive);
    }

    [Fact]
    public void ExtraProperties_CanBeNull()
    {
        var m = new InstrumentMapping
        {
            InstrumentId = "test",
            Symbol = "test",
            DisplayName = "Test",
            AssetClass = AssetType.Stock,
            Exchange = "TSE",
            QuoteCurrency = "IRR"
        };

        Assert.Null(m.ExtraProperties);
    }

    [Fact]
    public void ExtraProperties_CanBeSet()
    {
        var props = new Dictionary<string, string> { ["sector"] = " metals" };
        var m = new InstrumentMapping
        {
            InstrumentId = "test",
            Symbol = "test",
            DisplayName = "Test",
            AssetClass = AssetType.Stock,
            Exchange = "TSE",
            QuoteCurrency = "IRR",
            ExtraProperties = props
        };

        Assert.NotNull(m.ExtraProperties);
        Assert.Equal(" metals", m.ExtraProperties!["sector"]);
    }

    [Fact]
    public void InstrumentId_IsNotSourceSpecific()
    {
        // The canonical ID should not be a TSETMC InsCode
        var m = CreateFoolad();

        Assert.StartsWith("iran-equity-", m.InstrumentId);
        Assert.NotEqual("4439113430858354", m.InstrumentId);
    }

    [Fact]
    public void CanLookupSourceIdentifier_ByType()
    {
        var m = CreateFoolad();

        var tsetmc = m.SourceIdentifiers.GetValueOrDefault(SourceAdapterType.Tsetmc);
        Assert.NotNull(tsetmc);
        Assert.Equal("4439113430858354", tsetmc!.Id);

        var missing = m.SourceIdentifiers.GetValueOrDefault(SourceAdapterType.Cbi);
        Assert.Null(missing);
    }

    [Fact]
    public void SupportsAllAssetClasses()
    {
        foreach (var assetType in Enum.GetValues<AssetType>())
        {
            var m = new InstrumentMapping
            {
                InstrumentId = $"test-{assetType}",
                Symbol = "SYM",
                DisplayName = "Test",
                AssetClass = assetType,
                Exchange = "TEST",
                QuoteCurrency = "IRR"
            };

            Assert.Equal(assetType, m.AssetClass);
        }
    }

    // NOTE: InstrumentMapping contains IReadOnlyDictionary which uses reference equality,
    // so record value-equality does not work across independent instances.
    // Instead we verify field-by-field equivalence for the common fields.

    [Fact]
    public void FieldEquality_SameValues_HaveEqualFields()
    {
        var a = CreateFoolad();
        var b = CreateFoolad();

        Assert.Equal(a.InstrumentId, b.InstrumentId);
        Assert.Equal(a.Symbol, b.Symbol);
        Assert.Equal(a.LatinSymbol, b.LatinSymbol);
        Assert.Equal(a.DisplayName, b.DisplayName);
        Assert.Equal(a.AssetClass, b.AssetClass);
        Assert.Equal(a.Exchange, b.Exchange);
        Assert.Equal(a.QuoteCurrency, b.QuoteCurrency);
        Assert.Equal(a.IsActive, b.IsActive);
    }

    [Fact]
    public void FieldEquality_DifferentIds_HaveDifferentInstrumentId()
    {
        var a = CreateFoolad();
        var b = a with { InstrumentId = "different-id" };

        Assert.NotEqual(a.InstrumentId, b.InstrumentId);
        Assert.Equal(a.Symbol, b.Symbol); // other fields unchanged
    }
}

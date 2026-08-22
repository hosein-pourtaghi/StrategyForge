using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Models;
using Xunit;

namespace StrategyForge.Infrastructure.Tests;

/// <summary>
/// Semantic safety tests ensuring that distinct financial instruments
/// are never silently merged or substituted.
/// </summary>
public class SemanticSafetyTests
{
    private static readonly InstrumentMapping CbiUsdIrr = new()
    {
        InstrumentId = "usd-irr-official",
        Symbol = "USD/IRR",
        LatinSymbol = "USD/IRR",
        DisplayName = "USD/IRR Official",
        AssetClass = AssetType.Currency,
        Exchange = "CBI",
        QuoteCurrency = "IRR",
        SourceIdentifiers = new Dictionary<SourceAdapterType, SourceIdentifier>
        {
            [SourceAdapterType.Cbi] = new SourceIdentifier { Id = "USD-IRR" }
        }
    };

    private static readonly InstrumentMapping TgjuFreeMarketUsdIrr = new()
    {
        InstrumentId = "usd-irr-free-market",
        Symbol = "USD/IRR",
        LatinSymbol = "USD/IRR (Free Market)",
        DisplayName = "USD/IRR Free Market",
        AssetClass = AssetType.Currency,
        Exchange = "TGJU Free Market",
        QuoteCurrency = "IRR",
        SourceIdentifiers = new Dictionary<SourceAdapterType, SourceIdentifier>
        {
            [SourceAdapterType.Tgju] = new SourceIdentifier { Id = "price_dollar_rl" }
        }
    };

    private static readonly InstrumentMapping NobitexUsdtIrr = new()
    {
        InstrumentId = "usdt-irr-crypto",
        Symbol = "USDT/IRR",
        LatinSymbol = "USDT/IRR",
        DisplayName = "USDT/IRR Crypto",
        AssetClass = AssetType.Crypto,
        Exchange = "Nobitex",
        QuoteCurrency = "IRR",
        SourceIdentifiers = new Dictionary<SourceAdapterType, SourceIdentifier>
        {
            [SourceAdapterType.Nobitex] = new SourceIdentifier { Id = "usdt-irr" }
        }
    };

    [Fact]
    public void CbiUsdIrr_HasCbiIdentifierOnly()
    {
        Assert.True(CbiUsdIrr.SourceIdentifiers.ContainsKey(SourceAdapterType.Cbi));
        Assert.False(CbiUsdIrr.SourceIdentifiers.ContainsKey(SourceAdapterType.Tgju));
        Assert.False(CbiUsdIrr.SourceIdentifiers.ContainsKey(SourceAdapterType.Nobitex));
    }

    [Fact]
    public void TgjuFreeMarketUsdIrr_HasTgjuIdentifierOnly()
    {
        Assert.True(TgjuFreeMarketUsdIrr.SourceIdentifiers.ContainsKey(SourceAdapterType.Tgju));
        Assert.False(TgjuFreeMarketUsdIrr.SourceIdentifiers.ContainsKey(SourceAdapterType.Cbi));
    }

    [Fact]
    public void NobitexUsdtIrr_IsCrypto()
    {
        Assert.Equal(AssetType.Crypto, NobitexUsdtIrr.AssetClass);
    }

    [Fact]
    public void UsdIrr_And_UsdtIrr_AreDistinctInstruments()
    {
        Assert.NotEqual(CbiUsdIrr.InstrumentId, NobitexUsdtIrr.InstrumentId);
        Assert.Equal("USD/IRR", CbiUsdIrr.Symbol);
        Assert.Equal("USDT/IRR", NobitexUsdtIrr.Symbol);
    }

    [Fact]
    public void CbiOfficialFx_And_TgjuFreeMarketFx_AreDistinct()
    {
        Assert.NotEqual(CbiUsdIrr.InstrumentId, TgjuFreeMarketUsdIrr.InstrumentId);
        Assert.Equal("CBI", CbiUsdIrr.Exchange);
        Assert.Equal("TGJU Free Market", TgjuFreeMarketUsdIrr.Exchange);
    }

    [Fact]
    public void NobitexInstrument_NoCbiOrTgjuIdentifiers()
    {
        Assert.False(NobitexUsdtIrr.SourceIdentifiers.ContainsKey(SourceAdapterType.Cbi));
        Assert.False(NobitexUsdtIrr.SourceIdentifiers.ContainsKey(SourceAdapterType.Tgju));
    }

    [Fact]
    public void CbiInstrument_NoNobitexIdentifier()
    {
        Assert.False(CbiUsdIrr.SourceIdentifiers.ContainsKey(SourceAdapterType.Nobitex));
    }

    [Fact]
    public void SourceSelectionMode_HasAllThreeValues()
    {
        Assert.Equal(0, (int)SourceSelectionMode.BestAvailable);
        Assert.Equal(1, (int)SourceSelectionMode.PreferredOnly);
        Assert.Equal(2, (int)SourceSelectionMode.PreferredThenFallback);
    }

    [Fact]
    public void MarketDataType_HasAllRequiredTypes()
    {
        Assert.Equal(0, (int)MarketDataType.HistoricalCandles);
        Assert.Equal(1, (int)MarketDataType.Snapshot);
        Assert.Equal(2, (int)MarketDataType.OrderBook);
        Assert.Equal(3, (int)MarketDataType.OfficialFxRate);
        Assert.Equal(4, (int)MarketDataType.FreeMarketFxRate);
        Assert.Equal(5, (int)MarketDataType.MarketStatistics);
        Assert.Equal(6, (int)MarketDataType.InstrumentMetadata);
    }
}

using Microsoft.Extensions.Logging;
using Moq;
using StrategyForge.Domain.Enums;
using StrategyForge.Infrastructure.InstrumentResolution;
using Xunit;

namespace StrategyForge.Infrastructure.Tests.InstrumentResolution;

public class InMemoryInstrumentResolverTests
{
    private readonly InMemoryInstrumentResolver _resolver;

    public InMemoryInstrumentResolverTests()
    {
        var logger = new Mock<ILogger<InMemoryInstrumentResolver>>();
        _resolver = new InMemoryInstrumentResolver(logger.Object);
    }

    // --- Persian Symbol Lookup ---

    [Fact]
    public async Task Resolve_PersianSymbol_ReturnsCorrectInstrument()
    {
        var result = await _resolver.ResolveAsync("فولاد");

        Assert.NotNull(result);
        Assert.Equal("فولاد", result!.Symbol);
        Assert.Equal("Foolad Mobarakeh Steel", result.DisplayName);
        Assert.Equal(AssetType.Stock, result.AssetClass);
    }

    [Fact]
    public async Task Resolve_PersianSymbol_Dollar()
    {
        var result = await _resolver.ResolveAsync("دلار");

        Assert.NotNull(result);
        Assert.Equal("دلار", result!.Symbol);
        Assert.Equal(AssetType.Currency, result.AssetClass);
    }

    // --- Latin Symbol Lookup ---

    [Fact]
    public async Task Resolve_LatinSymbol_ReturnsCorrectInstrument()
    {
        var result = await _resolver.ResolveAsync("Foolad");

        Assert.NotNull(result);
        Assert.Equal("فولاد", result!.Symbol);
    }

    [Fact]
    public async Task Resolve_LatinSymbol_CaseInsensitive()
    {
        var result = await _resolver.ResolveAsync("foolad");

        Assert.NotNull(result);
        Assert.Equal("فولاد", result!.Symbol);
    }

    [Fact]
    public async Task Resolve_LatinSymbol_Khodro()
    {
        var result = await _resolver.ResolveAsync("Khodro");

        Assert.NotNull(result);
        Assert.Equal("خودرو", result!.Symbol);
    }

    // --- TSETMC InsCode Lookup ---

    [Fact]
    public async Task Resolve_TsetmcInsCode_ReturnsCorrectInstrument()
    {
        var result = await _resolver.ResolveAsync("4439113430858354");

        Assert.NotNull(result);
        Assert.Equal("فولاد", result!.Symbol);
    }

    // --- Canonical InstrumentId Lookup ---

    [Fact]
    public async Task Resolve_CanonicalId_ReturnsCorrectInstrument()
    {
        var result = await _resolver.ResolveAsync("iran-equity-foolad-4439113430858354");

        Assert.NotNull(result);
        Assert.Equal("فولاد", result!.Symbol);
    }

    // --- Not Found ---

    [Fact]
    public async Task Resolve_UnknownSymbol_ReturnsNull()
    {
        var result = await _resolver.ResolveAsync("نمادناموجود");

        Assert.Null(result);
    }

    [Fact]
    public async Task Resolve_EmptyString_ReturnsNull()
    {
        var result = await _resolver.ResolveAsync("");

        Assert.Null(result);
    }

    [Fact]
    public async Task Resolve_Null_ReturnsNull()
    {
        var result = await _resolver.ResolveAsync(null!);

        Assert.Null(result);
    }

    [Fact]
    public async Task Resolve_Whitespace_ReturnsNull()
    {
        var result = await _resolver.ResolveAsync("   ");

        Assert.Null(result);
    }

    // --- Search ---

    [Fact]
    public async Task Search_PersianPartialMatch_ReturnsResults()
    {
        var results = await _resolver.SearchAsync("فولاد");

        Assert.NotEmpty(results);
        Assert.Contains(results, i => i.Symbol == "فولاد");
    }

    [Fact]
    public async Task Search_LatinPartialMatch_ReturnsResults()
    {
        var results = await _resolver.SearchAsync("Fool");

        Assert.NotEmpty(results);
        Assert.Contains(results, i => i.LatinSymbol == "Foolad");
    }

    [Fact]
    public async Task Search_DisplayNameMatch_ReturnsResults()
    {
        var results = await _resolver.SearchAsync("Petrochemical");

        Assert.NotEmpty(results);
    }

    [Fact]
    public async Task Search_MaxResults_LimitsOutput()
    {
        var results = await _resolver.SearchAsync("a", maxResults: 2);

        Assert.True(results.Count <= 2);
    }

    [Fact]
    public async Task Search_EmptyQuery_ReturnsEmpty()
    {
        var results = await _resolver.SearchAsync("");

        Assert.Empty(results);
    }

    // --- Source Identifier Lookup ---

    [Fact]
    public void GetSourceIdentifier_Tsetmc_ReturnsId()
    {
        var mapping = _resolver.ResolveAsync("فولاد").GetAwaiter().GetResult()!;
        var sourceId = _resolver.GetSourceIdentifier(mapping, SourceAdapterType.Tsetmc);

        Assert.NotNull(sourceId);
        Assert.Equal("4439113430858354", sourceId!.Id);
    }

    [Fact]
    public void GetSourceIdentifier_UnmappedSource_ReturnsNull()
    {
        var mapping = _resolver.ResolveAsync("فولاد").GetAwaiter().GetResult()!;
        var sourceId = _resolver.GetSourceIdentifier(mapping, SourceAdapterType.Cbi);

        Assert.Null(sourceId);
    }

    // --- Batch Resolution ---

    [Fact]
    public async Task ResolveBatch_MultipleIdentifiers_ResolvesAll()
    {
        var ids = new[] { "فولاد", "Foolad", "4439113430858354" };
        var results = await _resolver.ResolveBatchAsync(ids);

        Assert.Equal(3, results.Count);
    }

    [Fact]
    public async Task ResolveBatch_MixedValidAndInvalid_OnlyReturnsValid()
    {
        var ids = new[] { "فولاد", "ناموجود", "Foolad" };
        var results = await _resolver.ResolveBatchAsync(ids);

        Assert.Equal(2, results.Count);
    }

    // --- All Asset Classes Present ---

    [Fact]
    public async Task Search_StockIndexCurrencyCommodityCrypto_AllPresent()
    {
        var stock = await _resolver.ResolveAsync("فولاد");
        var index = await _resolver.ResolveAsync("TEDPIX");
        var currency = await _resolver.ResolveAsync("USD/IRR");
        var commodity = await _resolver.ResolveAsync("Gold18K");
        var crypto = await _resolver.ResolveAsync("USDT/IRR");

        Assert.NotNull(stock);
        Assert.NotNull(index);
        Assert.NotNull(currency);
        Assert.NotNull(commodity);
        Assert.NotNull(crypto);

        Assert.Equal(AssetType.Stock, stock!.AssetClass);
        Assert.Equal(AssetType.Index, index!.AssetClass);
        Assert.Equal(AssetType.Currency, currency!.AssetClass);
        Assert.Equal(AssetType.Commodity, commodity!.AssetClass);
        Assert.Equal(AssetType.Crypto, crypto!.AssetClass);
    }
}

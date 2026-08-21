using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Models;
using Xunit;

namespace StrategyForge.Domain.Tests.Models;

public class AssetTests
{
    [Fact]
    public void Asset_Creation_SetsRequiredProperties()
    {
        // Arrange & Act
        var asset = new Asset
        {
            Symbol = "فولاد",
            Name = "Foolad Mobarakeh",
            Market = "TSE",
            AssetType = AssetType.Stock
        };

        // Assert
        Assert.Equal("فولاد", asset.Symbol);
        Assert.Equal("Foolad Mobarakeh", asset.Name);
        Assert.Equal("TSE", asset.Market);
        Assert.Equal(AssetType.Stock, asset.AssetType);
        Assert.NotEqual(Guid.Empty, asset.Id);
    }

    [Fact]
    public void Asset_Creation_GeneratesUniqueId()
    {
        // Arrange & Act
        var asset1 = new Asset
        {
            Symbol = "SYM1",
            Name = "Asset 1",
            Market = "TSE",
            AssetType = AssetType.Stock
        };
        var asset2 = new Asset
        {
            Symbol = "SYM2",
            Name = "Asset 2",
            Market = "TSE",
            AssetType = AssetType.Stock
        };

        // Assert
        Assert.NotEqual(asset1.Id, asset2.Id);
    }

    [Fact]
    public void Asset_OptionalProperties_CanBeNull()
    {
        // Arrange & Act
        var asset = new Asset
        {
            Symbol = "SYM",
            Name = "Asset",
            Market = "TSE",
            AssetType = AssetType.Stock
        };

        // Assert
        Assert.Null(asset.Sector);
        Assert.Null(asset.Isin);
        Assert.Null(asset.Metadata);
    }

    [Fact]
    public void Asset_WithMetadata_PreservesMetadata()
    {
        // Arrange
        var metadata = new Dictionary<string, string> { { "provider", "TSETMC" } };

        // Act
        var asset = new Asset
        {
            Symbol = "SYM",
            Name = "Asset",
            Market = "TSE",
            AssetType = AssetType.Stock,
            Metadata = metadata
        };

        // Assert
        Assert.NotNull(asset.Metadata);
        Assert.Equal("TSETMC", asset.Metadata["provider"]);
    }

    [Theory]
    [InlineData(AssetType.Stock)]
    [InlineData(AssetType.Index)]
    [InlineData(AssetType.Currency)]
    [InlineData(AssetType.Commodity)]
    [InlineData(AssetType.Crypto)]
    [InlineData(AssetType.ETF)]
    [InlineData(AssetType.Bond)]
    [InlineData(AssetType.Other)]
    public void Asset_AssetType_SupportsAllValues(AssetType assetType)
    {
        // Arrange & Act
        var asset = new Asset
        {
            Symbol = "SYM",
            Name = "Asset",
            Market = "TSE",
            AssetType = assetType
        };

        // Assert
        Assert.Equal(assetType, asset.AssetType);
    }
}

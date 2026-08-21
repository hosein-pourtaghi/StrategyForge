using StrategyForge.Domain.Models;
using Xunit;

namespace StrategyForge.Domain.Tests.Models;

public class CandleTests
{
    [Fact]
    public void Candle_Creation_SetsAllRequiredProperties()
    {
        // Arrange & Act
        var candle = new Candle
        {
            Date = new DateOnly(2024, 1, 15),
            Open = 100m,
            High = 110m,
            Low = 95m,
            Close = 105m,
            Volume = 1000000
        };

        // Assert
        Assert.Equal(new DateOnly(2024, 1, 15), candle.Date);
        Assert.Equal(100m, candle.Open);
        Assert.Equal(110m, candle.High);
        Assert.Equal(95m, candle.Low);
        Assert.Equal(105m, candle.Close);
        Assert.Equal(1000000, candle.Volume);
    }

    [Fact]
    public void Candle_IsValid_WithValidOhlc()
    {
        // Arrange & Act
        var candle = new Candle
        {
            Date = new DateOnly(2024, 1, 15),
            Open = 100m,
            High = 110m,
            Low = 95m,
            Close = 105m,
            Volume = 1000000
        };

        // Assert
        Assert.True(candle.IsValid);
    }

    [Fact]
    public void Candle_IsValid_WhenHighEqualsOpenAndClose()
    {
        // Arrange & Act
        var candle = new Candle
        {
            Date = new DateOnly(2024, 1, 15),
            Open = 100m,
            High = 100m,
            Low = 95m,
            Close = 100m,
            Volume = 1000000
        };

        // Assert
        Assert.True(candle.IsValid);
    }

    [Fact]
    public void Candle_IsValid_WhenLowEqualsOpenAndClose()
    {
        // Arrange & Act
        var candle = new Candle
        {
            Date = new DateOnly(2024, 1, 15),
            Open = 100m,
            High = 110m,
            Low = 100m,
            Close = 100m,
            Volume = 1000000
        };

        // Assert
        Assert.True(candle.IsValid);
    }

    [Fact]
    public void Candle_IsInvalid_WhenHighLessThanOpen()
    {
        // Arrange & Act
        var candle = new Candle
        {
            Date = new DateOnly(2024, 1, 15),
            Open = 100m,
            High = 90m, // Invalid: High < Open
            Low = 85m,
            Close = 95m,
            Volume = 1000000
        };

        // Assert
        Assert.False(candle.IsValid);
    }

    [Fact]
    public void Candle_IsInvalid_WhenLowGreaterThanClose()
    {
        // Arrange & Act
        var candle = new Candle
        {
            Date = new DateOnly(2024, 1, 15),
            Open = 100m,
            High = 110m,
            Low = 105m, // Invalid: Low > Close
            Close = 100m,
            Volume = 1000000
        };

        // Assert
        Assert.False(candle.IsValid);
    }

    [Fact]
    public void Candle_IsInvalid_WhenOpenIsZero()
    {
        // Arrange & Act
        var candle = new Candle
        {
            Date = new DateOnly(2024, 1, 15),
            Open = 0m,
            High = 110m,
            Low = 95m,
            Close = 105m,
            Volume = 1000000
        };

        // Assert
        Assert.False(candle.IsValid);
    }

    [Fact]
    public void Candle_OptionalProperties_CanBeNull()
    {
        // Arrange & Act
        var candle = new Candle
        {
            Date = new DateOnly(2024, 1, 15),
            Open = 100m,
            High = 110m,
            Low = 95m,
            Close = 105m,
            Volume = 1000000
        };

        // Assert
        Assert.Null(candle.TradeCount);
        Assert.Null(candle.Metadata);
    }

    [Fact]
    public void Candle_WithMetadata_PreservesMetadata()
    {
        // Arrange
        var metadata = new DataMetadata
        {
            Source = "TSETMC",
            RetrievedAt = DateTimeOffset.UtcNow,
            DataType = Enums.DataSourceType.MarketData
        };

        // Act
        var candle = new Candle
        {
            Date = new DateOnly(2024, 1, 15),
            Open = 100m,
            High = 110m,
            Low = 95m,
            Close = 105m,
            Volume = 1000000,
            Metadata = metadata
        };

        // Assert
        Assert.NotNull(candle.Metadata);
        Assert.Equal("TSETMC", candle.Metadata.Source);
    }
}

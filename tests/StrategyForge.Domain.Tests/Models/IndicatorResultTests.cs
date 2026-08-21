using StrategyForge.Domain.Models;
using Xunit;

namespace StrategyForge.Domain.Tests.Models;

public class IndicatorResultTests
{
    [Fact]
    public void IndicatorResult_Creation_SetsRequiredProperties()
    {
        // Arrange & Act
        var result = new IndicatorResult
        {
            IndicatorName = "RSI",
            Date = new DateOnly(2024, 1, 15),
            Value = 65.5m
        };

        // Assert
        Assert.Equal("RSI", result.IndicatorName);
        Assert.Equal(new DateOnly(2024, 1, 15), result.Date);
        Assert.Equal(65.5m, result.Value);
    }

    [Fact]
    public void IndicatorResult_WithSignal_PreservesSignal()
    {
        // Arrange & Act
        var result = new IndicatorResult
        {
            IndicatorName = "RSI",
            Date = new DateOnly(2024, 1, 15),
            Value = 75m,
            Signal = "Overbought"
        };

        // Assert
        Assert.Equal("Overbought", result.Signal);
    }

    [Fact]
    public void IndicatorResult_WithAdditionalValues_PreservesValues()
    {
        // Arrange
        var additionalValues = new Dictionary<string, decimal>
        {
            ["MACD"] = 1.23m,
            ["Signal"] = 0.98m,
            ["Histogram"] = 0.25m
        };

        // Act
        var result = new IndicatorResult
        {
            IndicatorName = "MACD",
            Date = new DateOnly(2024, 1, 15),
            Value = 1.23m,
            AdditionalValues = additionalValues
        };

        // Assert
        Assert.NotNull(result.AdditionalValues);
        Assert.Equal(3, result.AdditionalValues.Count);
        Assert.Equal(1.23m, result.AdditionalValues["MACD"]);
        Assert.Equal(0.98m, result.AdditionalValues["Signal"]);
        Assert.Equal(0.25m, result.AdditionalValues["Histogram"]);
    }

    [Fact]
    public void IndicatorResult_OptionalProperties_CanBeNull()
    {
        // Arrange & Act
        var result = new IndicatorResult
        {
            IndicatorName = "RSI",
            Date = new DateOnly(2024, 1, 15),
            Value = 50m
        };

        // Assert
        Assert.Null(result.Signal);
        Assert.Null(result.AdditionalValues);
        Assert.Null(result.Period);
        Assert.Null(result.Parameters);
    }

    [Fact]
    public void IndicatorResult_WithParameters_PreservesParameters()
    {
        // Arrange & Act
        var result = new IndicatorResult
        {
            IndicatorName = "RSI",
            Date = new DateOnly(2024, 1, 15),
            Value = 50m,
            Period = 14,
            Parameters = IndicatorParameters.DefaultRsi
        };

        // Assert
        Assert.Equal(14, result.Period);
        Assert.NotNull(result.Parameters);
        Assert.Equal(14, result.Parameters.Period);
    }
}

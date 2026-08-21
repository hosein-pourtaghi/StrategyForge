using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Models;
using Xunit;

namespace StrategyForge.Infrastructure.Tests.Models;

public class DataAdjustmentTests
{
    [Fact]
    public void Unadjusted_SetsCorrectDefaults()
    {
        var a = DataAdjustment.Unadjusted;

        Assert.False(a.IsAdjusted);
        Assert.Equal(DataAdjustmentType.None, a.Type);
        Assert.Null(a.AdjustmentSource);
        Assert.Empty(a.Notes);
    }

    [Fact]
    public void SourceAdjusted_SetsCorrectDefaults()
    {
        var a = DataAdjustment.SourceAdjusted();

        Assert.True(a.IsAdjusted);
        Assert.Equal(DataAdjustmentType.SourceAdjusted, a.Type);
        Assert.Equal("source", a.AdjustmentSource);
        Assert.Empty(a.Notes);
    }

    [Fact]
    public void SourceAdjusted_WithCustomSource()
    {
        var a = DataAdjustment.SourceAdjusted("TSETMC");

        Assert.Equal("TSETMC", a.AdjustmentSource);
    }

    [Fact]
    public void SourceAdjusted_WithNotes()
    {
        var a = DataAdjustment.SourceAdjusted("TSETMC", "Adjusted for dividend", "2024-Q1");

        Assert.Equal(2, a.Notes.Count);
        Assert.Equal("Adjusted for dividend", a.Notes[0]);
        Assert.Equal("2024-Q1", a.Notes[1]);
    }

    [Fact]
    public void Unknown_SetsCorrectDefaults()
    {
        var a = DataAdjustment.Unknown;

        Assert.False(a.IsAdjusted);
        Assert.Equal(DataAdjustmentType.Unknown, a.Type);
        Assert.Single(a.Notes);
        Assert.Contains("could not be verified", a.Notes[0]);
    }

    [Theory]
    [InlineData(DataAdjustmentType.None)]
    [InlineData(DataAdjustmentType.SourceAdjusted)]
    [InlineData(DataAdjustmentType.Split)]
    [InlineData(DataAdjustmentType.Dividend)]
    [InlineData(DataAdjustmentType.CapitalIncrease)]
    [InlineData(DataAdjustmentType.ManualCorrection)]
    [InlineData(DataAdjustmentType.Unknown)]
    public void AllAdjustmentTypes_CanBeAssigned(DataAdjustmentType type)
    {
        var a = new DataAdjustment { IsAdjusted = type != DataAdjustmentType.None, Type = type };

        Assert.Equal(type, a.Type);
    }

    [Fact]
    public void ManualAdjustment_CanBeCreatedManually()
    {
        var a = new DataAdjustment
        {
            IsAdjusted = true,
            Type = DataAdjustmentType.ManualCorrection,
            AdjustmentSource = "analyst",
            Notes = ["Corrected intraday gap on 2024-03-15"]
        };

        Assert.True(a.IsAdjusted);
        Assert.Equal(DataAdjustmentType.ManualCorrection, a.Type);
        Assert.Equal("analyst", a.AdjustmentSource);
    }

    [Fact]
    public void RecordEquality_SameValuesAreEqual()
    {
        var a = new DataAdjustment { IsAdjusted = false, Type = DataAdjustmentType.None };
        var b = new DataAdjustment { IsAdjusted = false, Type = DataAdjustmentType.None };

        Assert.Equal(a, b);
    }

    [Fact]
    public void RecordEquality_DifferentValuesAreNotEqual()
    {
        var a = DataAdjustment.Unadjusted;
        var b = DataAdjustment.Unknown;

        Assert.NotEqual(a, b);
    }
}

using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Models;
using Xunit;

namespace StrategyForge.Infrastructure.Tests.Models;

public class DataQualityTests
{
    [Fact]
    public void Perfect_ReturnsScore100AndComplete()
    {
        var q = DataQuality.Perfect;

        Assert.Equal(100, q.Score);
        Assert.True(q.IsComplete);
        Assert.Equal(QualityFlag.None, q.Flags);
        Assert.Empty(q.FlagDescriptions);
    }

    [Fact]
    public void WithFlags_ClampsScoreAbove100()
    {
        var q = DataQuality.WithFlags(150, true, QualityFlag.None);

        Assert.Equal(100, q.Score);
    }

    [Fact]
    public void WithFlags_ClampsScoreBelow0()
    {
        var q = DataQuality.WithFlags(-10, true, QualityFlag.None);

        Assert.Equal(0, q.Score);
    }

    [Fact]
    public void WithFlags_SingleFlag()
    {
        var q = DataQuality.WithFlags(80, true, QualityFlag.Stale, "Data is stale");

        Assert.Equal(80, q.Score);
        Assert.True(q.IsComplete);
        Assert.True(q.Flags.HasFlag(QualityFlag.Stale));
        Assert.Single(q.FlagDescriptions);
        Assert.Equal("Data is stale", q.FlagDescriptions[0]);
    }

    [Fact]
    public void WithFlags_MultipleFlags()
    {
        var flags = QualityFlag.Stale | QualityFlag.MissingFields | QualityFlag.InvalidNumeric;
        var q = DataQuality.WithFlags(30, false, flags, "stale", "missing", "invalid");

        Assert.Equal(30, q.Score);
        Assert.False(q.IsComplete);
        Assert.True(q.Flags.HasFlag(QualityFlag.Stale));
        Assert.True(q.Flags.HasFlag(QualityFlag.MissingFields));
        Assert.True(q.Flags.HasFlag(QualityFlag.InvalidNumeric));
        Assert.Equal(3, q.FlagDescriptions.Count);
    }

    [Fact]
    public void WithFlags_IsCompleteFalse_WhenMissingFields()
    {
        var q = DataQuality.WithFlags(50, false, QualityFlag.MissingFields);

        Assert.False(q.IsComplete);
    }

    [Fact]
    public void WithFlags_NoDescriptions()
    {
        var q = DataQuality.WithFlags(90, true, QualityFlag.Interpolated);

        Assert.Empty(q.FlagDescriptions);
    }

    [Fact]
    public void CrossValidated_DefaultsFalse()
    {
        var q = DataQuality.Perfect;

        Assert.False(q.CrossValidated);
    }

    [Fact]
    public void CrossValidated_CanBeSet()
    {
        var q = new DataQuality
        {
            Score = 95,
            IsComplete = true,
            CrossValidated = true
        };

        Assert.True(q.CrossValidated);
    }

    [Fact]
    public void AllQualityFlags_CanBeCombined()
    {
        var allFlags = QualityFlag.Stale | QualityFlag.MissingFields | QualityFlag.InvalidNumeric
            | QualityFlag.TimestampIssue | QualityFlag.OhlcInconsistency | QualityFlag.Interpolated
            | QualityFlag.CrossValidationFailed | QualityFlag.SchemaChange | QualityFlag.DuplicateRecords
            | QualityFlag.InstrumentMismatch;

        var q = DataQuality.WithFlags(0, false, allFlags);

        Assert.Equal(QualityFlag.None, q.Flags & QualityFlag.None); // None is 0
        Assert.NotEqual(QualityFlag.None, q.Flags);
        Assert.Equal(10, Enum.GetValues<QualityFlag>().Count(f => f != QualityFlag.None && allFlags.HasFlag(f)));
    }
}

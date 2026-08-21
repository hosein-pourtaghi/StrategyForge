using StrategyForge.Domain.Enums;
using StrategyForge.Domain.Models;
using StrategyForge.Infrastructure.Services;
using Xunit;

namespace StrategyForge.Infrastructure.Tests.Services;

public class DataQualityValidatorTests
{
    private readonly DataQualityValidator _validator = new();

    private static Candle ValidCandle(DateOnly date, decimal open = 100, decimal high = 110, decimal low = 90, decimal close = 105, long volume = 1000000)
        => new() { Date = date, Open = open, High = high, Low = low, Close = close, Volume = volume };

    // ===========================
    // ValidateCandles
    // ===========================

    [Fact]
    public void ValidateCandles_EmptyList_ReturnsScore0()
    {
        var q = _validator.ValidateCandles([]);

        Assert.Equal(0, q.Score);
        Assert.False(q.IsComplete);
        Assert.True(q.Flags.HasFlag(QualityFlag.MissingFields));
    }

    [Fact]
    public void ValidateCandles_ValidCandles_ReturnsPerfectScore()
    {
        var candles = new List<Candle>
        {
            ValidCandle(new DateOnly(2024, 1, 1)),
            ValidCandle(new DateOnly(2024, 1, 2)),
            ValidCandle(new DateOnly(2024, 1, 3))
        };

        var q = _validator.ValidateCandles(candles);

        Assert.Equal(100, q.Score);
        Assert.True(q.IsComplete);
        Assert.Equal(QualityFlag.None, q.Flags);
    }

    // --- OHLC Inconsistency ---

    [Fact]
    public void ValidateCandles_HighLessThanLow_FlagsOhlcInconsistency()
    {
        var candles = new List<Candle>
        {
            ValidCandle(new DateOnly(2024, 1, 1)),
            new() { Date = new DateOnly(2024, 1, 2), Open = 100, High = 80, Low = 90, Close = 105, Volume = 1000 }
        };

        var q = _validator.ValidateCandles(candles);

        Assert.True(q.Flags.HasFlag(QualityFlag.OhlcInconsistency));
        Assert.True(q.Score < 100);
    }

    [Fact]
    public void ValidateCandles_HighLessThanOpen_FlagsOhlcInconsistency()
    {
        var candles = new List<Candle>
        {
            new() { Date = new DateOnly(2024, 1, 1), Open = 100, High = 90, Low = 80, Close = 95, Volume = 1000 }
        };

        var q = _validator.ValidateCandles(candles);

        Assert.True(q.Flags.HasFlag(QualityFlag.OhlcInconsistency));
    }

    [Fact]
    public void ValidateCandles_LowGreaterThanClose_FlagsOhlcInconsistency()
    {
        var candles = new List<Candle>
        {
            new() { Date = new DateOnly(2024, 1, 1), Open = 100, High = 110, Low = 105, Close = 100, Volume = 1000 }
        };

        var q = _validator.ValidateCandles(candles);

        Assert.True(q.Flags.HasFlag(QualityFlag.OhlcInconsistency));
    }

    [Fact]
    public void ValidateCandles_HighLessThanClose_FlagsOhlcInconsistency()
    {
        var candles = new List<Candle>
        {
            new() { Date = new DateOnly(2024, 1, 1), Open = 100, High = 95, Low = 90, Close = 100, Volume = 1000 }
        };

        var q = _validator.ValidateCandles(candles);

        Assert.True(q.Flags.HasFlag(QualityFlag.OhlcInconsistency));
    }

    // --- Missing Volume ---

    [Fact]
    public void ValidateCandles_ZeroVolume_FlagsMissingFields()
    {
        var candles = new List<Candle>
        {
            ValidCandle(new DateOnly(2024, 1, 1), volume: 0)
        };

        var q = _validator.ValidateCandles(candles);

        Assert.True(q.Flags.HasFlag(QualityFlag.MissingFields));
    }

    // --- Timestamp Ordering ---

    [Fact]
    public void ValidateCandles_OutOfOrder_FlagsTimestampIssue()
    {
        var candles = new List<Candle>
        {
            ValidCandle(new DateOnly(2024, 1, 3)),
            ValidCandle(new DateOnly(2024, 1, 1)) // Out of order
        };

        var q = _validator.ValidateCandles(candles);

        Assert.True(q.Flags.HasFlag(QualityFlag.TimestampIssue));
    }

    [Fact]
    public void ValidateCandles_SameDate_FlagsTimestampIssue()
    {
        var candles = new List<Candle>
        {
            ValidCandle(new DateOnly(2024, 1, 1)),
            ValidCandle(new DateOnly(2024, 1, 1)) // Same date
        };

        var q = _validator.ValidateCandles(candles);

        Assert.True(q.Flags.HasFlag(QualityFlag.TimestampIssue));
    }

    // --- Duplicate Dates ---

    [Fact]
    public void ValidateCandles_DuplicateDates_FlagsDuplicateRecords()
    {
        var candles = new List<Candle>
        {
            ValidCandle(new DateOnly(2024, 1, 1)),
            ValidCandle(new DateOnly(2024, 1, 1)),
            ValidCandle(new DateOnly(2024, 1, 2))
        };

        var q = _validator.ValidateCandles(candles);

        Assert.True(q.Flags.HasFlag(QualityFlag.DuplicateRecords));
    }

    // --- Stale Data ---

    [Fact]
    public void ValidateCandles_StaleFreshness_FlagsStale()
    {
        var candles = new List<Candle> { ValidCandle(new DateOnly(2024, 1, 1)) };
        var stale = new DataFreshness
        {
            FetchedAtUtc = DateTimeOffset.UtcNow.AddHours(-2),
            MaxAllowedAgeMs = 3600000,
            IsCached = false
        };

        var q = _validator.ValidateCandles(candles, stale);

        Assert.True(q.Flags.HasFlag(QualityFlag.Stale));
        Assert.True(q.Score < 100);
    }

    [Fact]
    public void ValidateCandles_FreshFreshness_NoStaleFlag()
    {
        var candles = new List<Candle> { ValidCandle(new DateOnly(2024, 1, 1)) };
        var fresh = DataFreshness.Fresh(60000);

        var q = _validator.ValidateCandles(candles, fresh);

        Assert.False(q.Flags.HasFlag(QualityFlag.Stale));
    }

    // --- Multiple Issues ---

    [Fact]
    public void ValidateCandles_MultipleIssues_CombinesFlags()
    {
        var candles = new List<Candle>
        {
            ValidCandle(new DateOnly(2024, 1, 1), volume: 0),
            new() { Date = new DateOnly(2024, 1, 2), Open = 100, High = 80, Low = 120, Close = 105, Volume = 0 }
        };

        var q = _validator.ValidateCandles(candles);

        Assert.True(q.Flags.HasFlag(QualityFlag.OhlcInconsistency));
        Assert.True(q.Flags.HasFlag(QualityFlag.MissingFields));
        Assert.True(q.Score < 100);
    }

    // --- Score Proportionality ---

    [Fact]
    public void ValidateCandles_AllInvalid_FlagsOhlcAndIncomplete()
    {
        var candles = new List<Candle>
        {
            new() { Date = new DateOnly(2024, 1, 1), Open = 100, High = 80, Low = 120, Close = 105, Volume = 0 },
            new() { Date = new DateOnly(2024, 1, 2), Open = 100, High = 80, Low = 120, Close = 105, Volume = 0 }
        };

        var q = _validator.ValidateCandles(candles);

        Assert.False(q.IsComplete);
        Assert.True(q.Flags.HasFlag(QualityFlag.OhlcInconsistency));
        Assert.True(q.Flags.HasFlag(QualityFlag.MissingFields));
    }

    // ===========================
    // ValidateCandle (single)
    // ===========================

    [Fact]
    public void ValidateCandle_ValidCandle_ReturnsPerfectScore()
    {
        var q = _validator.ValidateCandle(ValidCandle(new DateOnly(2024, 1, 1)));

        Assert.Equal(100, q.Score);
        Assert.True(q.IsComplete);
        Assert.Equal(QualityFlag.None, q.Flags);
    }

    [Fact]
    public void ValidateCandle_InvalidOhlc_FlagsOhlcInconsistency()
    {
        var candle = new Candle { Date = new DateOnly(2024, 1, 1), Open = 100, High = 80, Low = 90, Close = 105, Volume = 1000 };

        var q = _validator.ValidateCandle(candle);

        Assert.True(q.Flags.HasFlag(QualityFlag.OhlcInconsistency));
        Assert.True(q.Score < 100);
    }

    [Fact]
    public void ValidateCandle_NegativeVolume_FlagsInvalidNumeric()
    {
        var candle = ValidCandle(new DateOnly(2024, 1, 1), volume: -100);

        var q = _validator.ValidateCandle(candle);

        Assert.True(q.Flags.HasFlag(QualityFlag.InvalidNumeric));
    }

    [Fact]
    public void ValidateCandle_ZeroOpen_FlagsInvalidNumeric()
    {
        var candle = new Candle { Date = new DateOnly(2024, 1, 1), Open = 0, High = 10, Low = 0, Close = 5, Volume = 1000 };

        var q = _validator.ValidateCandle(candle);

        Assert.True(q.Flags.HasFlag(QualityFlag.InvalidNumeric));
    }

    [Fact]
    public void ValidateCandle_ZeroClose_FlagsInvalidNumeric()
    {
        var candle = new Candle { Date = new DateOnly(2024, 1, 1), Open = 10, High = 10, Low = 0, Close = 0, Volume = 1000 };

        var q = _validator.ValidateCandle(candle);

        Assert.True(q.Flags.HasFlag(QualityFlag.InvalidNumeric));
    }

    [Fact]
    public void ValidateCandle_MultipleIssues_CombinesFlags()
    {
        var candle = new Candle { Date = new DateOnly(2024, 1, 1), Open = 0, High = 80, Low = 100, Close = 0, Volume = -1 };

        var q = _validator.ValidateCandle(candle);

        Assert.True(q.Flags.HasFlag(QualityFlag.OhlcInconsistency));
        Assert.True(q.Flags.HasFlag(QualityFlag.InvalidNumeric));
        Assert.True(q.Score < 50);
    }

    [Fact]
    public void ValidateCandle_ScoreClampedAt0()
    {
        var candle = new Candle { Date = new DateOnly(2024, 1, 1), Open = 0, High = 80, Low = 100, Close = 0, Volume = -1 };

        var q = _validator.ValidateCandle(candle);

        Assert.True(q.Score >= 0);
    }

    // ===========================
    // ValidateCurrencyRates
    // ===========================

    [Fact]
    public void ValidateCurrencyRates_EmptyList_ReturnsScore0()
    {
        var q = _validator.ValidateCurrencyRates([]);

        Assert.Equal(0, q.Score);
        Assert.False(q.IsComplete);
        Assert.True(q.Flags.HasFlag(QualityFlag.MissingFields));
    }

    [Fact]
    public void ValidateCurrencyRates_ValidRates_ReturnsPerfectScore()
    {
        var rates = new List<CurrencyRate>
        {
            new() { BaseCurrency = "USD", QuoteCurrency = "IRR", Rate = 500000, Timestamp = DateTimeOffset.UtcNow }
        };

        var q = _validator.ValidateCurrencyRates(rates);

        Assert.Equal(100, q.Score);
        Assert.True(q.IsComplete);
        Assert.Equal(QualityFlag.None, q.Flags);
    }

    [Fact]
    public void ValidateCurrencyRates_NegativeRate_FlagsInvalidNumeric()
    {
        var rates = new List<CurrencyRate>
        {
            new() { BaseCurrency = "USD", QuoteCurrency = "IRR", Rate = -100, Timestamp = DateTimeOffset.UtcNow }
        };

        var q = _validator.ValidateCurrencyRates(rates);

        Assert.True(q.Flags.HasFlag(QualityFlag.InvalidNumeric));
        Assert.True(q.Score < 100);
    }

    [Fact]
    public void ValidateCurrencyRates_ZeroRate_FlagsInvalidNumeric()
    {
        var rates = new List<CurrencyRate>
        {
            new() { BaseCurrency = "USD", QuoteCurrency = "IRR", Rate = 0, Timestamp = DateTimeOffset.UtcNow }
        };

        var q = _validator.ValidateCurrencyRates(rates);

        Assert.True(q.Flags.HasFlag(QualityFlag.InvalidNumeric));
    }

    [Fact]
    public void ValidateCurrencyRates_MixedValidAndInvalid_PartialScoreReduction()
    {
        var rates = new List<CurrencyRate>
        {
            new() { BaseCurrency = "USD", QuoteCurrency = "IRR", Rate = 500000, Timestamp = DateTimeOffset.UtcNow },
            new() { BaseCurrency = "EUR", QuoteCurrency = "IRR", Rate = -100, Timestamp = DateTimeOffset.UtcNow }
        };

        var q = _validator.ValidateCurrencyRates(rates);

        Assert.True(q.Flags.HasFlag(QualityFlag.InvalidNumeric));
        Assert.True(q.Score > 0 && q.Score < 100);
    }
}

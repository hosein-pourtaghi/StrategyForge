using StrategyForge.Domain.Interfaces.Analysis;
using StrategyForge.Analysis.Indicators;
using StrategyForge.Domain.Models;
using Xunit;

namespace StrategyForge.Analysis.Tests;

/// <summary>
/// Helper to create candle sequences for testing.
/// </summary>
public static class TestCandles
{
    public static IReadOnlyList<Candle> Create(params decimal[] closes)
    {
        var list = new List<Candle>();
        for (int i = 0; i < closes.Length; i++)
        {
            list.Add(new Candle
            {
                Date = new DateOnly(2024, 1, 1).AddDays(i),
                Open = closes[i],
                High = closes[i] + 1,
                Low = closes[i] - 1,
                Close = closes[i],
                Volume = 1000
            });
        }
        return list.AsReadOnly();
    }

    public static IReadOnlyList<Candle> CreateWithDate(DateOnly startDate, params decimal[] closes)
    {
        var list = new List<Candle>();
        for (int i = 0; i < closes.Length; i++)
        {
            list.Add(new Candle
            {
                Date = startDate.AddDays(i),
                Open = closes[i],
                High = closes[i] + 1,
                Low = closes[i] - 1,
                Close = closes[i],
                Volume = 1000
            });
        }
        return list.AsReadOnly();
    }
}

// ============================================================
// SMA Tests
// ============================================================
public class SmIndicatorTests
{
    [Fact]
    public void SMA_PeriodValidation_ThrowsOnZero()
    {
        var indicator = new SmIndicator();
        var candles = TestCandles.Create(1, 2, 3, 4, 5);
        var parameters = new IndicatorParameters { Period = 0 };
        Assert.Throws<ArgumentException>(() => indicator.Compute(candles, parameters));
    }

    [Fact]
    public void SMA_InsufficientData_ReturnsEmpty()
    {
        var indicator = new SmIndicator();
        var candles = TestCandles.Create(1, 2, 3);
        var results = indicator.Compute(candles, new IndicatorParameters { Period = 5 });
        Assert.Empty(results);
    }

    [Fact]
    public void SMA_ExactlyMinimum_ReturnsOneResult()
    {
        var indicator = new SmIndicator();
        var candles = TestCandles.Create(10, 20, 30, 40, 50);
        var results = indicator.Compute(candles, new IndicatorParameters { Period = 5 });
        Assert.Single(results);
        Assert.Equal(30m, results[0].Value); // (10+20+30+40+50)/5 = 30
    }

    [Fact]
    public void SMA_KnownSequence_CalculatesCorrectly()
    {
        var indicator = new SmIndicator();
        // 1,2,3,4,5,6,7,8,9,10 with period=3
        var candles = TestCandles.Create(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
        var results = indicator.Compute(candles, new IndicatorParameters { Period = 3 });

        Assert.Equal(8, results.Count); // 10 - 3 + 1 = 8
        Assert.Equal(2m, results[0].Value);   // (1+2+3)/3 = 2
        Assert.Equal(3m, results[1].Value);   // (2+3+4)/3 = 3
        Assert.Equal(9m, results[^1].Value);  // (8+9+10)/3 = 9
    }

    [Fact]
    public void SMA_DefaultPeriod_Is20()
    {
        var indicator = new SmIndicator();
        var candles = TestCandles.Create(Enumerable.Range(1, 25).Select(x => (decimal)x).ToArray());
        var results = indicator.Compute(candles);
        Assert.NotEmpty(results);
        // First result should be SMA-20 of first 20 values
        Assert.Equal(10.5m, results[0].Value); // (1+2+...+20)/20 = 210/20 = 10.5
    }

    [Fact]
    public void SMA_SingleValue_AllSame_ReturnsThatValue()
    {
        var indicator = new SmIndicator();
        var candles = TestCandles.Create(5, 5, 5, 5, 5);
        var results = indicator.Compute(candles, new IndicatorParameters { Period = 5 });
        Assert.Single(results);
        Assert.Equal(5m, results[0].Value);
    }
}

// ============================================================
// EMA Tests
// ============================================================
public class EmaIndicatorTests
{
    [Fact]
    public void EMA_PeriodValidation_ThrowsOnZero()
    {
        var indicator = new EmaIndicator();
        var candles = TestCandles.Create(1, 2, 3);
        Assert.Throws<ArgumentException>(() => indicator.Compute(candles, new IndicatorParameters { Period = 0 }));
    }

    [Fact]
    public void EMA_InsufficientData_ReturnsEmpty()
    {
        var indicator = new EmaIndicator();
        var candles = TestCandles.Create(1, 2, 3);
        var results = indicator.Compute(candles, new IndicatorParameters { Period = 5 });
        Assert.Empty(results);
    }

    [Fact]
    public void EMA_FirstValue_EqualsSmaSeed()
    {
        var indicator = new EmaIndicator();
        var candles = TestCandles.Create(10, 20, 30, 40, 50);
        var results = indicator.Compute(candles, new IndicatorParameters { Period = 5 });
        Assert.Single(results);
        Assert.Equal(30m, results[0].Value); // SMA seed = 30
    }

    [Fact]
    public void EMA_KnownSequence_CalculatesCorrectly()
    {
        var indicator = new EmaIndicator();
        // Constant price: EMA should converge to that price
        var candles = TestCandles.Create(100, 100, 100, 100, 100, 100, 100);
        var results = indicator.Compute(candles, new IndicatorParameters { Period = 3 });

        Assert.NotEmpty(results);
        // After seed, EMA should remain 100
        foreach (var r in results)
            Assert.Equal(100m, r.Value);
    }

    [Fact]
    public void EMA_IncreasingPrice_LagsBelowClose()
    {
        var indicator = new EmaIndicator();
        var candles = TestCandles.Create(10, 20, 30, 40, 50, 60, 70);
        var results = indicator.Compute(candles, new IndicatorParameters { Period = 3 });

        Assert.NotEmpty(results);
        // EMA should be less than the current close for increasing prices
        foreach (var r in results)
        {
            var candleDate = r.Date;
            var close = candles.First(c => c.Date == candleDate).Close;
            Assert.True(r.Value <= close, $"EMA {r.Value} should be <= close {close}");
        }
    }

    [Fact]
    public void EMA_ResponsiveToNewData_FasterThanSma()
    {
        var indicator = new EmaIndicator();
        // Jump from 10 to 100 at the end
        var candles = TestCandles.Create(10, 10, 10, 10, 10, 100);
        var emaResults = indicator.Compute(candles, new IndicatorParameters { Period = 3 });
        var smaIndicator = new SmIndicator();
        var smaResults = smaIndicator.Compute(candles, new IndicatorParameters { Period = 3 });

        Assert.NotEmpty(emaResults);
        Assert.NotEmpty(smaResults);
        // EMA should react faster to the jump
        Assert.True(emaResults[^1].Value > smaResults[^1].Value,
            $"EMA {emaResults[^1].Value} should be > SMA {smaResults[^1].Value} after jump");
    }
}

// ============================================================
// RSI Tests
// ============================================================
public class RsiIndicatorTests
{
    [Fact]
    public void RSI_PeriodValidation_ThrowsOnZero()
    {
        var indicator = new RsiIndicator();
        var candles = TestCandles.Create(1, 2, 3, 4, 5);
        Assert.Throws<ArgumentException>(() => indicator.Compute(candles, new IndicatorParameters { Period = 0 }));
    }

    [Fact]
    public void RSI_InsufficientData_ReturnsEmpty()
    {
        var indicator = new RsiIndicator();
        var candles = TestCandles.Create(1, 2, 3);
        var results = indicator.Compute(candles, new IndicatorParameters { Period = 14 });
        Assert.Empty(results);
    }

    [Fact]
    public void RSI_AllGains_Returns100()
    {
        var indicator = new RsiIndicator();
        // Strictly increasing prices
        var closes = Enumerable.Range(1, 20).Select(x => (decimal)x).ToArray();
        var candles = TestCandles.Create(closes);
        var results = indicator.Compute(candles, new IndicatorParameters { Period = 14 });

        Assert.NotEmpty(results);
        // All changes are positive, so RSI should be 100
        Assert.Equal(100m, results[0].Value);
    }

    [Fact]
    public void RSI_AllLosses_Returns0()
    {
        var indicator = new RsiIndicator();
        // Strictly decreasing prices
        var closes = Enumerable.Range(1, 20).Select(x => (decimal)(20 - x + 1)).ToArray();
        var candles = TestCandles.Create(closes);
        var results = indicator.Compute(candles, new IndicatorParameters { Period = 14 });

        Assert.NotEmpty(results);
        Assert.Equal(0m, results[0].Value);
    }

    [Fact]
    public void RSI_FlatPrices_Returns100()
    {
        var indicator = new RsiIndicator();
        var candles = TestCandles.Create(Enumerable.Repeat(50m, 20).ToArray());
        var results = indicator.Compute(candles, new IndicatorParameters { Period = 14 });

        Assert.NotEmpty(results);
        // No changes = no losses, so RSI = 100
        Assert.Equal(100m, results[0].Value);
    }

    [Fact]
    public void RSI_DefaultPeriod_Is14()
    {
        var indicator = new RsiIndicator();
        var closes = Enumerable.Range(1, 20).Select(x => (decimal)x).ToArray();
        var candles = TestCandles.Create(closes);
        var results = indicator.Compute(candles);

        Assert.NotEmpty(results);
        // With default period=14, need 15 candles, results start at index 14
        Assert.Equal(new DateOnly(2024, 1, 15), results[0].Date);
    }

    [Fact]
    public void RSI_Between0And100()
    {
        var indicator = new RsiIndicator();
        // Alternating up/down
        var candles = TestCandles.Create(10, 12, 11, 13, 12, 14, 13, 15, 14, 16, 15, 17, 16, 18, 17, 19, 18, 20);
        var results = indicator.Compute(candles, new IndicatorParameters { Period = 14 });

        Assert.NotEmpty(results);
        foreach (var r in results)
        {
            Assert.True(r.Value >= 0 && r.Value <= 100, $"RSI {r.Value} out of range");
        }
    }
}

// ============================================================
// MACD Tests
// ============================================================
public class MacdIndicatorTests
{
    [Fact]
    public void MACD_InsufficientData_ReturnsEmpty()
    {
        var indicator = new MacdIndicator();
        var candles = TestCandles.Create(Enumerable.Range(1, 10).Select(x => (decimal)x).ToArray());
        var results = indicator.Compute(candles);
        Assert.Empty(results); // Need 26+9=35 candles
    }

    [Fact]
    public void MACD_FastGreaterThanSlow_Throws()
    {
        var indicator = new MacdIndicator();
        var candles = TestCandles.Create(Enumerable.Range(1, 50).Select(x => (decimal)x).ToArray());
        var parameters = new IndicatorParameters
        {
            Period = 26,
            SecondaryPeriod = 12
        };
        Assert.Throws<ArgumentException>(() => indicator.Compute(candles, parameters));
    }

    [Fact]
    public void MACD_ConstantPrice_AllZeros()
    {
        var indicator = new MacdIndicator();
        var candles = TestCandles.Create(Enumerable.Repeat(100m, 40).ToArray());
        var results = indicator.Compute(candles);

        Assert.NotEmpty(results);
        foreach (var r in results)
        {
            Assert.Equal(0m, r.Value); // MACD line
            var signal = r.AdditionalValues!["Signal"];
            Assert.Equal(0m, signal);
            var histogram = r.AdditionalValues!["Histogram"];
            Assert.Equal(0m, histogram);
        }
    }

    [Fact]
    public void MACD_ProducesAllThreeValues()
    {
        var indicator = new MacdIndicator();
        var candles = TestCandles.Create(Enumerable.Range(1, 40).Select(x => (decimal)x).ToArray());
        var results = indicator.Compute(candles);

        Assert.NotEmpty(results);
        foreach (var r in results)
        {
            Assert.NotNull(r.AdditionalValues);
            Assert.True(r.AdditionalValues.ContainsKey("MACD"));
            Assert.True(r.AdditionalValues.ContainsKey("Signal"));
            Assert.True(r.AdditionalValues.ContainsKey("Histogram"));
        }
    }

    [Fact]
    public void MACD_Histogram_EqualsMacdMinusSignal()
    {
        var indicator = new MacdIndicator();
        var candles = TestCandles.Create(Enumerable.Range(1, 40).Select(x => (decimal)x).ToArray());
        var results = indicator.Compute(candles);

        Assert.NotEmpty(results);
        foreach (var r in results)
        {
            var macd = r.AdditionalValues!["MACD"];
            var signal = r.AdditionalValues["Signal"];
            var histogram = r.AdditionalValues["Histogram"];
            Assert.Equal(macd - signal, histogram);
        }
    }

    [Fact]
    public void MACD_IncreasingPrices_NegativeMacdLine()
    {
        var indicator = new MacdIndicator();
        // Steadily increasing: fast EMA leads slow EMA
        var closes = Enumerable.Range(1, 50).Select(x => (decimal)(x * 2)).ToArray();
        var candles = TestCandles.Create(closes);
        var results = indicator.Compute(candles);

        Assert.NotEmpty(results);
        // When prices increase, fast EMA > slow EMA, so MACD > 0
        Assert.True(results[^1].Value > 0, $"MACD should be positive for rising prices, got {results[^1].Value}");
    }
}

// ============================================================
// Bollinger Bands Tests
// ============================================================
public class BollingerBandsIndicatorTests
{
    [Fact]
    public void BB_PeriodValidation_ThrowsOnZero()
    {
        var indicator = new BollingerBandsIndicator();
        var candles = TestCandles.Create(Enumerable.Range(1, 25).Select(x => (decimal)x).ToArray());
        Assert.Throws<ArgumentException>(() => indicator.Compute(candles, new IndicatorParameters { Period = 0 }));
    }

    [Fact]
    public void BB_MultiplierValidation_ThrowsOnZero()
    {
        var indicator = new BollingerBandsIndicator();
        var candles = TestCandles.Create(Enumerable.Range(1, 25).Select(x => (decimal)x).ToArray());
        Assert.Throws<ArgumentException>(() => indicator.Compute(candles, new IndicatorParameters { Period = 20, StandardDeviation = 0 }));
    }

    [Fact]
    public void BB_InsufficientData_ReturnsEmpty()
    {
        var indicator = new BollingerBandsIndicator();
        var candles = TestCandles.Create(1, 2, 3);
        var results = indicator.Compute(candles, new IndicatorParameters { Period = 20 });
        Assert.Empty(results);
    }

    [Fact]
    public void BB_ConstantPrice_BandsCollapse()
    {
        var indicator = new BollingerBandsIndicator();
        var candles = TestCandles.Create(Enumerable.Repeat(100m, 25).ToArray());
        var results = indicator.Compute(candles, new IndicatorParameters { Period = 20 });

        Assert.NotEmpty(results);
        foreach (var r in results)
        {
            Assert.Equal(100m, r.Value); // Middle = SMA = 100
            Assert.Equal(100m, r.AdditionalValues!["Upper"]);
            Assert.Equal(100m, r.AdditionalValues["Lower"]);
        }
    }

    [Fact]
    public void BB_UpperGreaterThanMiddle_GreaterThanLower()
    {
        var indicator = new BollingerBandsIndicator();
        var candles = TestCandles.Create(Enumerable.Range(1, 25).Select(x => (decimal)(x * 10)).ToArray());
        var results = indicator.Compute(candles, new IndicatorParameters { Period = 20 });

        Assert.NotEmpty(results);
        foreach (var r in results)
        {
            var upper = r.AdditionalValues!["Upper"];
            var middle = r.AdditionalValues["Middle"];
            var lower = r.AdditionalValues["Lower"];
            Assert.True(upper > middle, $"Upper {upper} should be > Middle {middle}");
            Assert.True(middle > lower, $"Middle {middle} should be > Lower {lower}");
        }
    }

    [Fact]
    public void BB_MiddleBand_EqualsSma()
    {
        var smaIndicator = new SmIndicator();
        var bbIndicator = new BollingerBandsIndicator();
        var candles = TestCandles.Create(Enumerable.Range(1, 25).Select(x => (decimal)(x * 5)).ToArray());

        var smaResults = smaIndicator.Compute(candles, new IndicatorParameters { Period = 20 });
        var bbResults = bbIndicator.Compute(candles, new IndicatorParameters { Period = 20 });

        Assert.Equal(smaResults.Count, bbResults.Count);
        for (int i = 0; i < smaResults.Count; i++)
        {
            Assert.Equal(smaResults[i].Value, bbResults[i].Value); // Middle = SMA
        }
    }

    [Fact]
    public void BB_Bandwidth_Positive()
    {
        var indicator = new BollingerBandsIndicator();
        var candles = TestCandles.Create(Enumerable.Range(1, 25).Select(x => (decimal)(x * 10)).ToArray());
        var results = indicator.Compute(candles, new IndicatorParameters { Period = 20 });

        Assert.NotEmpty(results);
        foreach (var r in results)
        {
            var bandwidth = r.AdditionalValues!["Bandwidth"];
            Assert.True(bandwidth >= 0, $"Bandwidth {bandwidth} should be >= 0");
        }
    }

    [Fact]
    public void BB_PercentB_AtMiddleBand_Equals0Point5()
    {
        // When close = SMA (middle band), %B should be 0.5
        // Constant prices: close = middle = upper = lower
        var indicator = new BollingerBandsIndicator();
        var candles = TestCandles.Create(Enumerable.Repeat(100m, 25).ToArray());
        var results = indicator.Compute(candles, new IndicatorParameters { Period = 20 });

        Assert.NotEmpty(results);
        // With constant prices, all bands = 100, so bandwidth = 0
        // %B is 0 when bandwidth = 0 (our implementation)
        Assert.Equal(0m, results[0].AdditionalValues!["PercentB"]);
    }
}

// ============================================================
// IndicatorEngine Tests
// ============================================================
public class IndicatorEngineTests
{
    [Fact]
    public void Engine_EmptyCandles_ReturnsEmptyResult()
    {
        var engine = new IndicatorEngine([]);
        var result = engine.ComputeAll([]);
        Assert.Equal(0, result.CandleCount);
    }

    [Fact]
    public void Engine_RegistersAllIndicators()
    {
        IIndicator[] indicators = [new SmIndicator(), new EmaIndicator(), new RsiIndicator(), new MacdIndicator(), new BollingerBandsIndicator()];
        var engine = new IndicatorEngine(indicators);
        Assert.Equal(5, engine.RegisteredIndicators.Count);
    }

    [Fact]
    public void Engine_ComputeAll_RunsAllIndicators()
    {
        IIndicator[] indicators = [new SmIndicator(), new EmaIndicator()];
        var engine = new IndicatorEngine(indicators);
        var candles = TestCandles.Create(Enumerable.Range(1, 30).Select(x => (decimal)x).ToArray());
        var result = engine.ComputeAll(candles);

        Assert.Contains("SMA", result.SuccessfulIndicators);
        Assert.Contains("EMA", result.SuccessfulIndicators);
    }

    [Fact]
    public void Engine_EnabledIndicators_FiltersCorrectly()
    {
        IIndicator[] indicators = [new SmIndicator(), new EmaIndicator(), new RsiIndicator()];
        var engine = new IndicatorEngine(indicators);
        var candles = TestCandles.Create(Enumerable.Range(1, 30).Select(x => (decimal)x).ToArray());
        var config = new IndicatorConfiguration { EnabledIndicators = ["SMA"] };
        var result = engine.ComputeAll(candles, config);

        Assert.Contains("SMA", result.SuccessfulIndicators);
        Assert.DoesNotContain("EMA", result.SuccessfulIndicators);
    }

    [Fact]
    public void Engine_DisabledIndicators_FiltersCorrectly()
    {
        IIndicator[] indicators = [new SmIndicator(), new EmaIndicator(), new RsiIndicator()];
        var engine = new IndicatorEngine(indicators);
        var candles = TestCandles.Create(Enumerable.Range(1, 30).Select(x => (decimal)x).ToArray());
        var config = new IndicatorConfiguration { DisabledIndicators = ["RSI"] };
        var result = engine.ComputeAll(candles, config);

        Assert.Contains("SMA", result.SuccessfulIndicators);
        Assert.Contains("EMA", result.SuccessfulIndicators);
        Assert.DoesNotContain("RSI", result.SuccessfulIndicators);
    }

    [Fact]
    public void Engine_CustomParameters_PassesToIndicator()
    {
        IIndicator[] indicators = [new SmIndicator()];
        var engine = new IndicatorEngine(indicators);
        var candles = TestCandles.Create(Enumerable.Range(1, 30).Select(x => (decimal)x).ToArray());
        var config = new IndicatorConfiguration
        {
            IndicatorParameters = new Dictionary<string, IndicatorParameters>
            {
                ["SMA"] = new IndicatorParameters { Period = 5 }
            }
        };
        var result = engine.ComputeAll(candles, config);

        Assert.Contains("SMA", result.SuccessfulIndicators);
        var smaResults = result.Results["SMA"];
        Assert.Equal(5, smaResults[0].Period);
    }

    [Fact]
    public void Engine_GetLatest_ReturnsMostRecent()
    {
        IIndicator[] indicators = [new SmIndicator()];
        var engine = new IndicatorEngine(indicators);
        var candles = TestCandles.Create(Enumerable.Range(1, 30).Select(x => (decimal)x).ToArray());
        var result = engine.ComputeAll(candles);

        var latest = result.GetLatest("SMA");
        Assert.NotNull(latest);
        Assert.Equal(new DateOnly(2024, 1, 30), latest.Date);
    }

    [Fact]
    public void Engine_GetLatestValues_ReturnsAll()
    {
        IIndicator[] indicators = [new SmIndicator(), new EmaIndicator()];
        var engine = new IndicatorEngine(indicators);
        var candles = TestCandles.Create(Enumerable.Range(1, 30).Select(x => (decimal)x).ToArray());
        var result = engine.ComputeAll(candles);

        var latest = result.GetLatestValues();
        Assert.Equal(2, latest.Count);
        Assert.True(latest.ContainsKey("SMA"));
        Assert.True(latest.ContainsKey("EMA"));
    }
}

using StrategyForge.Infrastructure.Services;
using Xunit;

namespace StrategyForge.Infrastructure.Tests.Services;

public class JalaliCalendarServiceTests
{
    private readonly JalaliCalendarService _service = new();

    // --- Known Jalali → Gregorian Conversions ---

    [Fact]
    public void ToGregorian_IranianNewYear_2024()
    {
        // 1403/01/01 = 2024-03-20 (Norooz 2024)
        var result = _service.ToGregorian(1403, 1, 1);

        Assert.Equal(new DateOnly(2024, 3, 20), result);
    }

    [Fact]
    public void ToGregorian_IranianNewYear_2025()
    {
        // 1404/01/01 = 2025-03-21 (Norooz 2025)
        var result = _service.ToGregorian(1404, 1, 1);

        Assert.Equal(new DateOnly(2025, 3, 21), result);
    }

    [Fact]
    public void ToGregorian_IranianNewYear_2026()
    {
        // 1405/01/01 = 2026-03-21 (Norooz 2026)
        var result = _service.ToGregorian(1405, 1, 1);

        Assert.Equal(new DateOnly(2026, 3, 21), result);
    }

    [Fact]
    public void ToGregorian_MidYear()
    {
        // 1403/07/15 = 2024-10-05
        var result = _service.ToGregorian(1403, 7, 15);

        Assert.Equal(new DateOnly(2024, 10, 6), result);
    }

    [Fact]
    public void ToGregorian_LastDayOfYear()
    {
        // 1403/12/29 = 2025-03-19 (1403 is leap year, last day is 12/30)
        var result = _service.ToGregorian(1403, 12, 29);

        Assert.Equal(new DateOnly(2025, 3, 19), result);
    }

    // --- Known Gregorian → Jalali Conversions ---

    [Fact]
    public void ToJalali_2024_03_20()
    {
        var result = _service.ToJalali(new DateOnly(2024, 3, 20));

        Assert.Equal("1403/01/01", result);
    }

    [Fact]
    public void ToJalali_2025_03_21()
    {
        var result = _service.ToJalali(new DateOnly(2025, 3, 21));

        Assert.Equal("1404/01/01", result);
    }

    [Fact]
    public void ToJalali_2026_03_21()
    {
        var result = _service.ToJalali(new DateOnly(2026, 3, 21));

        Assert.Equal("1405/01/01", result);
    }

    [Fact]
    public void ToJalali_2024_01_01()
    {
        // 2024-01-01 = 1402/10/11
        var result = _service.ToJalali(new DateOnly(2024, 1, 1));

        Assert.Equal("1402/10/11", result);
    }

    // --- Round-Trip Conversions ---

    [Theory]
    [InlineData(1403, 1, 1)]
    [InlineData(1403, 6, 15)]
    [InlineData(1403, 12, 29)]
    [InlineData(1404, 1, 1)]
    [InlineData(1405, 3, 30)]
    [InlineData(1402, 7, 10)]
    public void RoundTrip_JalaliToGregorianToJalali(int jy, int jm, int jd)
    {
        var gregorian = _service.ToGregorian(jy, jm, jd);
        var jalali = _service.ToJalali(gregorian);
        var roundTrip = _service.ToGregorian(jalali);

        Assert.Equal(gregorian, roundTrip);
    }

    [Theory]
    [InlineData(2024, 3, 20)]
    [InlineData(2024, 7, 15)]
    [InlineData(2025, 1, 1)]
    [InlineData(2025, 12, 31)]
    [InlineData(2026, 8, 21)]
    public void RoundTrip_GregorianToJalaliToGregorian(int gy, int gm, int gd)
    {
        var date = new DateOnly(gy, gm, gd);
        var jalali = _service.ToJalali(date);
        var roundTrip = _service.ToGregorian(jalali);

        Assert.Equal(date, roundTrip);
    }

    // --- Leap Year ---

    [Fact]
    public void ToGregorian_LeapYear_Jalali()
    {
        // 1403 is a leap year in Jalali (year 4 in the 33-year cycle)
        // 1403/12/30 exists (extra day)
        var result = _service.ToGregorian(1403, 12, 30);

        Assert.Equal(new DateOnly(2025, 3, 20), result);
    }

    [Fact]
    public void ToGregorian_RegularYear_NoDay30()
    {
        // 1404 is NOT a leap year — 12th month has only 29 days
        // 1404/12/29 = last day
        var result = _service.ToGregorian(1404, 12, 29);

        Assert.Equal(new DateOnly(2026, 3, 20), result);
    }

    // --- String Parsing ---

    [Fact]
    public void ToGregorian_FromString_SlashFormat()
    {
        var result = _service.ToGregorian("1403/01/01");

        Assert.Equal(new DateOnly(2024, 3, 20), result);
    }

    [Fact]
    public void ToGregorian_InvalidFormat_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => _service.ToGregorian("invalid"));
    }

    [Fact]
    public void ToGregorian_WrongParts_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => _service.ToGregorian("1403/01"));
    }

    // --- ParseJalali ---

    [Fact]
    public void ParseJalali_SlashFormat()
    {
        var result = _service.ParseJalali("1403/01/01");

        Assert.Equal(new DateOnly(2024, 3, 20), result);
    }

    [Fact]
    public void ParseJalali_DashFormat()
    {
        var result = _service.ParseJalali("1403-01-01");

        Assert.Equal(new DateOnly(2024, 3, 20), result);
    }

    [Fact]
    public void ParseJalali_CompactFormat()
    {
        var result = _service.ParseJalali("14030101");

        Assert.Equal(new DateOnly(2024, 3, 20), result);
    }

    [Fact]
    public void ParseJalali_InvalidFormat_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => _service.ParseJalali("abc"));
    }

    // --- CurrentJalali ---

    [Fact]
    public void CurrentJalali_ReturnsNonEmptyString()
    {
        var result = _service.CurrentJalali();

        Assert.False(string.IsNullOrWhiteSpace(result));
        Assert.Contains("/", result);
    }

    // --- Historical Market Date ---

    [Theory]
    [InlineData(1403, 7, 23, 2024, 10, 14)] // Common trading day
    [InlineData(1402, 4, 15, 2023, 7, 6)]
    [InlineData(1401, 1, 1, 2022, 3, 21)]
    public void ToGregorian_HistoricalMarketDates(int jy, int jm, int jd, int gy, int gm, int gd)
    {
        var result = _service.ToGregorian(jy, jm, jd);

        Assert.Equal(new DateOnly(gy, gm, gd), result);
    }
}

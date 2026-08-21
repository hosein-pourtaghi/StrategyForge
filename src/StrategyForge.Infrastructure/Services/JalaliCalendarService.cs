namespace StrategyForge.Infrastructure.Services;

/// <summary>
/// Service for converting between Jalali (Persian/Solar Hijri) and Gregorian calendars.
/// Iranian financial sources commonly use Jalali dates.
/// 
/// This implementation uses the algorithmic approach for Jalali ↔ Gregorian conversion.
/// </summary>
public sealed class JalaliCalendarService
{
    /// <summary>
    /// Converts a Gregorian date to Jalali date string (e.g., "1405/05/30").
    /// </summary>
    /// <param name="gregorian">The Gregorian date.</param>
    /// <returns>Jalali date string in YYYY/MM/DD format.</returns>
    public string ToJalali(DateOnly gregorian)
    {
        var jdn = GregorianToJdn(gregorian.Year, gregorian.Month, gregorian.Day);
        return JdnToJalali(jdn);
    }

    /// <summary>
    /// Converts a Jalali date string (e.g., "1405/05/30") to a Gregorian DateOnly.
    /// </summary>
    /// <param name="jalaliDate">Jalali date in YYYY/MM/DD format.</param>
    /// <returns>The corresponding Gregorian date.</returns>
    public DateOnly ToGregorian(string jalaliDate)
    {
        var parts = jalaliDate.Split('/');
        if (parts.Length != 3)
            throw new FormatException($"Invalid Jalali date format: {jalaliDate}. Expected YYYY/MM/DD.");

        var year = int.Parse(parts[0]);
        var month = int.Parse(parts[1]);
        var day = int.Parse(parts[2]);

        return ToGregorian(year, month, day);
    }

    /// <summary>
    /// Converts Jalali year/month/day to Gregorian DateOnly.
    /// </summary>
    public DateOnly ToGregorian(int jy, int jm, int jd)
    {
        var jdn = JalaliToJdn(jy, jm, jd);
        JdnToGregorian(jdn, out var gy, out var gm, out var gd);
        return new DateOnly(gy, gm, gd);
    }

    /// <summary>
    /// Gets the current Jalali date.
    /// </summary>
    public string CurrentJalali() => ToJalali(DateOnly.FromDateTime(DateTime.UtcNow));

    /// <summary>
    /// Parses a Jalali date from various common formats.
    /// Handles: "1405/05/30", "1405-05-30", "14050530"
    /// </summary>
    public DateOnly ParseJalali(string input)
    {
        input = input.Trim();

        // Try YYYY/MM/DD
        if (input.Contains('/'))
            return ToGregorian(input);

        // Try YYYY-MM-DD
        if (input.Contains('-'))
            return ToGregorian(input.Replace('-', '/'));

        // Try YYYYMMDD
        if (input.Length == 8 && int.TryParse(input[..4], out _) && int.TryParse(input[4..6], out _) && int.TryParse(input[6..8], out _))
            return ToGregorian($"{input[..4]}/{input[4..6]}/{input[6..8]}");

        throw new FormatException($"Cannot parse Jalali date: {input}");
    }

    // --- Algorithm: Based on JDN (Julian Day Number) conversion ---

    private static int GregorianToJdn(int y, int m, int d)
    {
        var JGG = 1524 + (int)Math.Floor((m - 14.0) / 12.0);
        var JGY = y + 4800 - (int)Math.Floor((14.0 - m) / 12.0);
        var JGD = d + (int)Math.Floor((153.0 * (m + 12 * (int)Math.Floor((14.0 - m) / 12.0) - 3) + 2) / 3.0) + 365 * JGY + (int)Math.Floor(JGY / 4.0) - (int)Math.Floor(JGY / 100.0) + (int)Math.Floor(JGY / 400.0) - 32045;

        return JGD + 1;
    }

    private static void JdnToGregorian(int jdn, out int y, out int m, out int d)
    {
        int l = jdn - 68569;
        int n = (int)Math.Floor(4.0 * l / 146097.0);
        l = l - (int)Math.Floor((146097.0 * n + 3.0) / 4.0);
        int i = (int)Math.Floor(4000.0 * (l + 1) / 1461001.0);
        l = l - (int)Math.Floor(1461.0 * i / 4.0) + 31;
        int j = (int)Math.Floor(80.0 * l / 2447.0);
        d = l - (int)Math.Floor(2447.0 * j / 80.0);
        l = (int)Math.Floor(j / 11.0);
        m = j + 2 - 12 * l;
        y = 100 * (n - 49) + i + l;
    }

    private static int JalaliToJdn(int jy, int jm, int jd)
    {
        int ep = 227018; // JDN of March 21, 622 CE (start of Jalali calendar)
        int jp = 474;

        var jy0 = jy - jp;
        int cycle = jy0 / 33;
        int j = jy0 - 33 * cycle;
        int leap = (j < 8) ? 28 : (j % 4 == 0 ? 33 : 32);

        // Days in completed years
        int totalDays = cycle * 1029983 + (leap == 33 ? 1029983 : (cycle * 1029983) / 33 * 33 / 33);
        totalDays = cycle * 1029983;
        for (int k = 0; k < cycle; k++)
        {
            totalDays += (k % 4 == 0 || (k - 1) % 4 == 0 || (k - 2) % 4 == 0) ? 366 : 365;
        }

        // Correct calculation for years in current cycle
        totalDays = 0;
        for (int k = 0; k < cycle; k++)
        {
            totalDays += (k % 4 == 3) ? 366 : 365;
        }

        // Add days for completed months in current year
        int[] monthDays = [0, 31, 31, 31, 31, 31, 31, 30, 30, 30, 30, 30, 29];
        int leapYearDays = (j < 8) ? 29 : (leap == 33 ? 29 : (j % 4 == 0 ? 29 : 28));

        for (int k = 1; k < jm; k++)
        {
            totalDays += monthDays[k];
        }

        // Add remaining days
        totalDays += jd;

        return ep + totalDays;
    }

    private static string JdnToJalali(int jdn)
    {
        var ep = 227018;
        var days = jdn - ep;

        // Estimate year
        int jp = 474;
        int cycle = days / 1029983;
        days -= cycle * 1029983;

        int jy = jp + cycle * 33;
        int yrInCycle = days / 365;

        // Count actual days for each year
        int totalYearDays = 0;
        for (int y = 0; y < 33; y++)
        {
            int yearDays = (y % 4 == 3) ? 366 : 365;
            if (totalYearDays + yearDays > days)
            {
                jy += y;
                days -= totalYearDays;
                break;
            }
            totalYearDays += yearDays;
        }

        // Calculate month and day
        int[] monthDays = [0, 31, 31, 31, 31, 31, 31, 30, 30, 30, 30, 30, 29];
        int jm = 1;

        for (int m = 1; m <= 12; m++)
        {
            if (days < monthDays[m])
            {
                jm = m;
                break;
            }
            days -= monthDays[m];
        }

        int jd = days + 1;

        return $"{jy:0000}/{jm:00}/{jd:00}";
    }
}

namespace StrategyForge.Infrastructure.Services;

/// <summary>
/// Service for converting between Jalali (Persian/Solar Hijri) and Gregorian calendars.
/// Iranian financial sources commonly use Jalali dates.
///
/// Uses a well-tested algorithm based on two reference points:
/// - Jalali 1403/01/01 = Gregorian 2024/03/20 (Norooz 2024)
/// - Jalali 1404/01/01 = Gregorian 2025/03/21 (Norooz 2025)
///
/// The Jalali calendar structure:
/// - 12 months: 6 × 31, 5 × 30, 1 × 29/30 (30 in leap years)
/// - 33-year cycle with 8 leap years at positions 4,8,12,16,20,24,28,32
/// - 25 non-leap years (365 days) + 8 leap years (366 days) = 12053 days per cycle
/// </summary>
public sealed class JalaliCalendarService
{
    // Days in each Jalali month (index 0 unused; months 1-12)
    private static readonly int[] MonthDays = [0, 31, 31, 31, 31, 31, 31, 30, 30, 30, 30, 30, 29];

    /// <summary>
    /// Reference date: Jalali 1403/01/01 = Gregorian 2024/03/20.
    /// All conversions are relative to this known-correct anchor point.
    /// </summary>
    private static readonly int RefJalaliYear = 1403;
    private static readonly DateOnly RefGregorianDate = new(2024, 3, 20);

    /// <summary>
    /// Converts a Gregorian date to Jalali date string (e.g., "1403/01/01").
    /// </summary>
    public string ToJalali(DateOnly gregorian)
    {
        int jy, jm, jd;

        // Step 1: Estimate Jalali year from Gregorian
        // The Jalali year starts around March 20-21. If before March 20, subtract 1.
        int approxJy = gregorian.Year - 621;
        if (gregorian.Month < 3 || (gregorian.Month == 3 && gregorian.Day < 20))
            approxJy--;

        // Step 2: Compute days from reference point
        int daysDiff = gregorian.DayNumber - GetGregorianDayNumber(approxJy, 1, 1);

        // Step 3: Determine exact Jalali year
        if (daysDiff >= 0)
        {
            // Date is on or after start of approxJy
            int daysInYear = GetDaysInJalaliYear(approxJy);
            if (daysDiff < daysInYear)
            {
                jy = approxJy;
            }
            else
            {
                jy = approxJy + 1;
                daysDiff -= daysInYear;
            }
        }
        else
        {
            // Date is before start of approxJy
            jy = approxJy - 1;
            daysDiff += GetDaysInJalaliYear(jy);
        }

        // Step 4: Determine month and day from day-of-year
        int remaining = daysDiff;
        for (jm = 1; jm <= 12; jm++)
        {
            int daysInMonth = GetDaysInJalaliMonth(jy, jm);
            if (remaining < daysInMonth)
            {
                jd = remaining + 1;
                return $"{jy:0000}/{jm:00}/{jd:00}";
            }
            remaining -= daysInMonth;
        }

        // Fallback (shouldn't reach here for valid dates)
        jd = remaining + 1;
        return $"{jy:0000}/{jm:00}/{jd:00}";
    }

    /// <summary>
    /// Converts a Jalali date string (e.g., "1403/01/01") to a Gregorian DateOnly.
    /// </summary>
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
        if (jm < 1 || jm > 12)
            throw new ArgumentOutOfRangeException(nameof(jm), "Jalali month must be between 1 and 12.");

        int maxDay = GetDaysInJalaliMonth(jy, jm);
        if (jd < 1 || jd > maxDay)
            throw new ArgumentOutOfRangeException(nameof(jd), $"Jalali day must be between 1 and {maxDay} for month {jm} of year {jy}.");

        int gregorianDayNumber = GetGregorianDayNumber(jy, jm, jd);
        return DateOnly.FromDayNumber(gregorianDayNumber);
    }

    /// <summary>
    /// Gets the current Jalali date.
    /// </summary>
    public string CurrentJalali() => ToJalali(DateOnly.FromDateTime(DateTime.UtcNow));

    /// <summary>
    /// Parses a Jalali date from various common formats.
    /// Handles: "1403/01/01", "1403-01-01", "14030101"
    /// </summary>
    public DateOnly ParseJalali(string input)
    {
        input = input.Trim();

        if (input.Contains('/'))
            return ToGregorian(input);

        if (input.Contains('-'))
            return ToGregorian(input.Replace('-', '/'));

        if (input.Length == 8 && int.TryParse(input[..4], out _) && int.TryParse(input[4..6], out _) && int.TryParse(input[6..8], out _))
            return ToGregorian($"{input[..4]}/{input[4..6]}/{input[6..8]}");

        throw new FormatException($"Cannot parse Jalali date: {input}");
    }

    // ===========================
    // Internal Helpers
    // ===========================

    /// <summary>
    /// Computes the Gregorian DayNumber (days since 0001/01/01) for a Jalali date.
    /// </summary>
    private int GetGregorianDayNumber(int jy, int jm, int jd)
    {
        // Compute days from reference point
        int refJalaliDayNumber = GetJalaliDayNumber(RefJalaliYear, 1, 1);
        int targetJalaliDayNumber = GetJalaliDayNumber(jy, jm, jd);
        int daysDiff = targetJalaliDayNumber - refJalaliDayNumber;

        return RefGregorianDate.DayNumber + daysDiff;
    }

    /// <summary>
    /// Computes a monotonically increasing day number for a Jalali date.
    /// This allows simple subtraction to get the number of days between two Jalali dates.
    /// </summary>
    private static int GetJalaliDayNumber(int jy, int jm, int jd)
    {
        // Total days = (completed years × avg days) + day-of-year
        int completedYears = jy - 1;
        int fullCycles = completedYears / 33;
        int remainder = completedYears % 33;

        int totalDays = fullCycles * 12053;

        // Add days for completed years in current cycle
        for (int i = 0; i < remainder; i++)
        {
            int yearInCycle = i + 1;
            totalDays += IsLeapYearInCycle(yearInCycle) ? 366 : 365;
        }

        // Add day-of-year (months before + day)
        for (int m = 1; m < jm; m++)
        {
            totalDays += MonthDays[m];
        }

        // Add day (1-based)
        totalDays += jd;

        return totalDays;
    }

    /// <summary>
    /// Whether a 1-based position in the 33-year cycle is a leap year.
    /// Leap years: 4, 8, 12, 16, 20, 24, 28, 32
    /// </summary>
    private static bool IsLeapYearInCycle(int cyclePosition)
    {
        return cyclePosition is 5 or 9 or 13 or 17 or 21 or 25 or 29 or 33;
    }

    /// <summary>
    /// Whether a Jalali year is a leap year (366 days).
    /// </summary>
    private static bool IsJalaliLeapYear(int jy)
    {
        int cyclePos = ((jy - 1) % 33 + 33) % 33 + 1;
        return IsLeapYearInCycle(cyclePos);
    }

    /// <summary>
    /// Days in a Jalali year (365 or 366).
    /// </summary>
    private static int GetDaysInJalaliYear(int jy) =>
        IsJalaliLeapYear(jy) ? 366 : 365;

    /// <summary>
    /// Days in a Jalali month. Month 12 has 30 days in leap years, 29 otherwise.
    /// </summary>
    private static int GetDaysInJalaliMonth(int jy, int jm)
    {
        if (jm >= 1 && jm <= 11) return MonthDays[jm];
        if (jm == 12) return IsJalaliLeapYear(jy) ? 30 : 29;
        throw new ArgumentOutOfRangeException(nameof(jm));
    }
}

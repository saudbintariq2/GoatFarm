using System.Globalization;
using System.Text.RegularExpressions;
using GoatFarm.Application.Common;
using GoatFarm.Application.ViewModels.Assistant;

namespace GoatFarm.Application.Assistant;

public static class AssistantDateRange
{
    private static readonly Regex MonthKeyRegex = new(@"^\d{4}-(0[1-9]|1[0-2])$", RegexOptions.Compiled);
    private static readonly string[] DateFormats = ["yyyy-MM-dd"];

    public static DateOnly ResolveToday(string? timeZone)
    {
        if (!string.IsNullOrWhiteSpace(timeZone))
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZone.Trim());
                return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz));
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return DateOnly.FromDateTime(DateTime.Today);
    }

    public static (DateOnly From, DateOnly To, string MonthKey) ThisMonth(DateOnly today)
    {
        var from = new DateOnly(today.Year, today.Month, 1);
        var to = from.AddMonths(1).AddDays(-1);
        return (from, to, today.ToString("yyyy-MM", CultureInfo.InvariantCulture));
    }

    public static (DateOnly From, DateOnly To, string MonthKey) LastMonth(DateOnly today)
    {
        var firstThisMonth = new DateOnly(today.Year, today.Month, 1);
        var from = firstThisMonth.AddMonths(-1);
        var to = firstThisMonth.AddDays(-1);
        return (from, to, from.ToString("yyyy-MM", CultureInfo.InvariantCulture));
    }

    public static bool TryParseMonth(string? month, DateOnly today, out string monthKey, out string? error)
    {
        monthKey = "";
        error = null;
        if (string.IsNullOrWhiteSpace(month))
        {
            monthKey = ThisMonth(today).MonthKey;
            return true;
        }

        var value = month.Trim();
        if (value.Equals("lastmonth", StringComparison.OrdinalIgnoreCase)
            || value.Equals("last month", StringComparison.OrdinalIgnoreCase)
            || value.Equals("previous month", StringComparison.OrdinalIgnoreCase))
        {
            monthKey = LastMonth(today).MonthKey;
            return true;
        }

        if (value.Equals("month", StringComparison.OrdinalIgnoreCase)
            || value.Equals("this month", StringComparison.OrdinalIgnoreCase)
            || value.Equals("current", StringComparison.OrdinalIgnoreCase))
        {
            monthKey = ThisMonth(today).MonthKey;
            return true;
        }

        if (MonthKeyRegex.IsMatch(value))
        {
            var (start, _) = MonthHelper.GetMonthRange(value);
            if (start.Year is < 2000 or > 2100)
            {
                error = "Use a month between 2000-01 and 2100-12.";
                return false;
            }

            monthKey = value;
            return true;
        }

        error = "Use a month as yyyy-MM, 'lastmonth', or 'this month'.";
        return false;
    }

    public static bool TryParseInclusiveRange(
        string? fromText,
        string? toText,
        string? month,
        DateOnly today,
        out DateOnly from,
        out DateOnly to,
        out string label,
        out string? error)
    {
        from = default;
        to = default;
        label = "";
        error = null;

        if (!string.IsNullOrWhiteSpace(fromText) || !string.IsNullOrWhiteSpace(toText))
        {
            if (!TryParseExactDate(fromText, out from, out error) || !TryParseExactDate(toText, out to, out error))
                return false;
        }
        else if (!string.IsNullOrWhiteSpace(month))
        {
            if (!TryParseMonth(month, today, out var monthKey, out error))
                return false;
            var range = MonthHelper.GetMonthRange(monthKey);
            from = range.Start;
            to = range.EndExclusive.AddDays(-1);
        }
        else
        {
            (from, to, _) = ThisMonth(today);
        }

        if (to < from)
        {
            error = "The end date must be on or after the start date.";
            return false;
        }

        if (to.DayNumber - from.DayNumber + 1 > AssistantLimits.MaxDateRangeDays)
        {
            error = $"Date ranges cannot be longer than {AssistantLimits.MaxDateRangeDays} days.";
            return false;
        }

        label = from == to ? from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : $"{from:yyyy-MM-dd} to {to:yyyy-MM-dd}";
        return true;
    }

    public static bool TryParseExactDate(string? text, out DateOnly date, out string? error)
    {
        date = default;
        error = null;
        if (string.IsNullOrWhiteSpace(text)
            || !DateOnly.TryParseExact(text.Trim(), DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            error = "Use from and to dates as yyyy-MM-dd.";
            return false;
        }

        if (date.Year is < 2000 or > 2100)
        {
            error = "Use dates between 2000-01-01 and 2100-12-31.";
            return false;
        }

        return true;
    }
}

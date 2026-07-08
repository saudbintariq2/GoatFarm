namespace GoatFarm.Application.Common;

public static class MonthHelper
{
    public static string CurrentMonthKey() => DateTime.Today.ToString("yyyy-MM");

    public static (DateOnly Start, DateOnly EndExclusive) GetMonthRange(string monthKey)
    {
        if (DateOnly.TryParse($"{monthKey}-01", out var start))
            return (start, start.AddMonths(1));

        var today = DateOnly.FromDateTime(DateTime.Today);
        start = new DateOnly(today.Year, today.Month, 1);
        return (start, start.AddMonths(1));
    }
}

namespace GoatFarm.Application.Common;

/// <summary>V211: direct/bulk feeds may be active only in selected months; empty = all year.</summary>
public static class FeedSeasonHelper
{
    private static readonly string[] MonthNames =
        ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

    public static bool IsActive(string feedType, DateOnly date, IReadOnlyDictionary<string, int[]>? activeMonthsByFeed)
    {
        if (activeMonthsByFeed is null || !activeMonthsByFeed.TryGetValue(feedType, out var months))
            return true;
        if (months.Length == 0) return true;
        return months.Contains(date.Month);
    }

    public static string SeasonLabel(IReadOnlyList<int> months)
    {
        if (months.Count == 0 || months.Count >= 12) return "all year";
        var ms = months.OrderBy(m => m).ToList();
        var st = ms.FirstOrDefault(m => !ms.Contains(m == 1 ? 12 : m - 1));
        if (st == 0) st = ms[0];
        var ord = ms.OrderBy(m => (m - st + 12) % 12).ToList();
        var run = ord.Count > 2 && ord.Zip(ord.Skip(1), (a, b) => b == a % 12 + 1).All(x => x);
        if (run)
            return $"{MonthNames[ord[0] - 1]}–{MonthNames[ord[^1] - 1]}";
        return string.Join(", ", ord.Select(m => MonthNames[m - 1]));
    }

    public static int[] NormalizeMonthsForSave(int[] months)
    {
        var distinct = months.Where(m => m is >= 1 and <= 12).Distinct().OrderBy(m => m).ToArray();
        return distinct.Length == 12 ? [] : distinct;
    }
}

using System.Globalization;

namespace GoatFarm.Application.Common;

public class FodderPoolItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    /// <summary>Total cash cost (Rs) for the payment.</summary>
    public decimal Amount { get; set; }
    public DateOnly? PaidDate { get; set; }
    public int AllocateMonths { get; set; } = 12;
    public int? AccountId { get; set; }
    /// <summary>Legacy mirror of <see cref="Amount"/> for cash; kept in sync on save.</summary>
    public decimal PaidAmount { get; set; }
}

public class FodderPoolDto
{
    public decimal Acres { get; set; }
    public string Mode { get; set; } = "share";
    public List<FodderPoolItemDto> Items { get; set; } = [];
}

public class FeedSettingsDto
{
    public bool AutoUsage { get; set; } = true;
    public string? LastUsageDate { get; set; }
    public Dictionary<string, decimal> OpeningStockByFeed { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int[]> ActiveMonthsByFeed { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public static class FodderPoolHelper
{
    public const int MinAllocateMonths = 1;
    public const int MaxAllocateMonths = 60;
    public const int DefaultAllocateMonths = 12;

    public static FodderPoolDto DefaultPool() => new()
    {
        Items =
        [
            new() { Id = "rent", Label = "Land rent", Amount = 0, AllocateMonths = DefaultAllocateMonths },
            new() { Id = "seeding", Label = "Seeding & ploughing", Amount = 0, AllocateMonths = DefaultAllocateMonths },
            new() { Id = "cutting", Label = "Cutting & labour", Amount = 0, AllocateMonths = DefaultAllocateMonths },
            new() { Id = "water", Label = "Irrigation / water", Amount = 0, AllocateMonths = DefaultAllocateMonths }
        ]
    };

    public static int EffectiveMonths(int allocateMonths) =>
        Math.Clamp(allocateMonths <= 0 ? DefaultAllocateMonths : allocateMonths, MinAllocateMonths, MaxAllocateMonths);

    public static decimal AnnualTotal(FodderPoolDto pool) =>
        pool.Items.Sum(i => i.Amount);

    /// <summary>Whole-rupee monthly slices; remainder on the last month.</summary>
    public static decimal[] SpreadOverMonths(decimal total, int months)
    {
        months = EffectiveMonths(months);
        if (months <= 0) return [total];
        var baseAmt = (long)Math.Floor(total / months);
        var amounts = new decimal[months];
        for (var i = 0; i < months - 1; i++)
            amounts[i] = baseAmt;
        amounts[months - 1] = total - baseAmt * (months - 1);
        return amounts;
    }

    public static decimal AverageMonthlyAmount(FodderPoolItemDto item)
    {
        var months = EffectiveMonths(item.AllocateMonths);
        return months > 0 ? item.Amount / months : 0;
    }

    public static bool IsActiveInMonth(FodderPoolItemDto item, string yearMonth)
    {
        if (item.Amount <= 0 || !item.PaidDate.HasValue) return false;
        var months = EffectiveMonths(item.AllocateMonths);
        var startYm = item.PaidDate.Value.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        var startIdx = MonthIndex(startYm);
        var curIdx = MonthIndex(yearMonth.Length >= 7 ? yearMonth[..7] : yearMonth);
        return curIdx >= startIdx && curIdx < startIdx + months;
    }

    public static decimal AllocatedInMonth(FodderPoolDto pool, string yearMonth)
    {
        var ym = yearMonth.Length >= 7 ? yearMonth[..7] : yearMonth;
        return pool.Items
            .Where(i => i.Amount > 0 && IsActiveInMonth(i, ym))
            .Sum(i =>
            {
                var months = EffectiveMonths(i.AllocateMonths);
                var parts = SpreadOverMonths(i.Amount, months);
                var start = itemMonthIndex(i);
                var cur = MonthIndex(ym);
                var offset = cur - start;
                return offset >= 0 && offset < parts.Length ? parts[offset] : 0;
            });
    }

    public static decimal DailyTotalForMonth(FodderPoolDto pool, string yearMonth)
    {
        var ym = yearMonth.Length >= 7 ? yearMonth[..7] : yearMonth;
        var allocated = AllocatedInMonth(pool, ym);
        if (allocated <= 0) return 0;
        var parts = ym.Split('-');
        if (parts.Length < 2 || !int.TryParse(parts[0], out var y) || !int.TryParse(parts[1], out var m))
            return allocated / 30m;
        var days = DateTime.DaysInMonth(y, m);
        return allocated / days;
    }

    /// <summary>Backward-compatible alias: current calendar month.</summary>
    public static decimal DailyTotal(FodderPoolDto pool) =>
        DailyTotalForMonth(pool, DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM", CultureInfo.InvariantCulture));

    public static string FormatPaidDate(DateOnly date) =>
        date.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);

    public static string FormatMonthRangeLabel(DateOnly paidDate, int months)
    {
        months = EffectiveMonths(months);
        var end = paidDate.AddMonths(months - 1);
        if (months == 1)
            return $"{paidDate:MMM yyyy}";
        if (paidDate.Year == end.Year)
            return $"{paidDate:MMM} – {end:MMM} {end.Year}";
        return $"{paidDate:MMM yyyy} – {end:MMM yyyy}";
    }

    private static int MonthIndex(string ym)
    {
        var parts = ym.Split('-');
        if (parts.Length < 2) return 0;
        return int.Parse(parts[0], CultureInfo.InvariantCulture) * 12 + int.Parse(parts[1], CultureInfo.InvariantCulture) - 1;
    }

    private static int itemMonthIndex(FodderPoolItemDto item) =>
        MonthIndex(item.PaidDate!.Value.ToString("yyyy-MM", CultureInfo.InvariantCulture));
}
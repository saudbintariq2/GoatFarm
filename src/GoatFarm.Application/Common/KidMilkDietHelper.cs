using GoatFarm.Application.ViewModels.Feed;
using GoatFarm.Domain.Enums;

namespace GoatFarm.Application.Common;

public static class KidMilkDietHelper
{
    public static List<KidMilkMonthViewModel> DefaultMonths() =>
    [
        new() { Name = "Month 1", From = 0, To = 30, PowderG = 250, MotherMilkMl = 0, MixG = 25, GreenG = 0, DryG = 0 },
        new() { Name = "Month 2", From = 31, To = 60, PowderG = 350, MotherMilkMl = 0, MixG = 75, GreenG = 100, DryG = 0 },
        new() { Name = "Month 3", From = 61, To = 90, PowderG = 450, MotherMilkMl = 0, MixG = 150, GreenG = 250, DryG = 50 }
    ];

    public static KidMilkDietViewModel Default() => new()
    {
        Months = DefaultMonths(),
        Logs = [],
        Counts = [0, 0, 0]
    };

    public static KidMilkDietViewModel Normalize(KidMilkDietViewModel? raw)
    {
        var defaults = DefaultMonths();
        var months = (raw?.Months ?? []).ToList();
        while (months.Count < 3)
            months.Add(defaults[months.Count]);
        for (var i = 0; i < 3; i++)
        {
            var d = defaults[i];
            var m = months[i];
            months[i] = new KidMilkMonthViewModel
            {
                Name = string.IsNullOrWhiteSpace(m.Name) ? d.Name : m.Name,
                From = m.From > 0 || i == 0 ? m.From : d.From,
                To = m.To > 0 ? m.To : d.To,
                PowderG = Math.Max(0, m.PowderG),
                MotherMilkMl = Math.Max(0, m.MotherMilkMl),
                MixG = Math.Max(0, m.MixG),
                GreenG = Math.Max(0, m.GreenG),
                DryG = Math.Max(0, m.DryG)
            };
        }

        return new KidMilkDietViewModel
        {
            Months = months.Take(3).ToList(),
            Logs = (raw?.Logs ?? []).OrderByDescending(l => l.Date).ToList(),
            AutoStartDate = raw?.AutoStartDate,
            LastAutoDate = raw?.LastAutoDate,
            Counts = raw?.Counts is { Length: 3 } ? raw.Counts : [0, 0, 0]
        };
    }

    /// <summary>The diet counts toward feed use only after Month 1–3 have been saved once.</summary>
    public static bool IsStarted(KidMilkDietViewModel state) =>
        !string.IsNullOrWhiteSpace(state.AutoStartDate);

    public static IReadOnlyList<KidMilkMonthViewModel> ActiveMonths(KidMilkDietViewModel state) =>
        IsStarted(state) ? state.Months : [];

    public static int BandForAge(int ageDays) =>
        ageDays <= 30 ? 0 : ageDays <= 60 ? 1 : 2;

    /// <summary>Dam milk a suckling kid takes when the diet leaves Mother milk at 0 ml.</summary>
    public static decimal DefaultMotherMl(int ageDays) =>
        ageDays <= 30 ? 800m : ageDays <= 60 ? 1000m : 600m;

    /// <summary>
    /// One kid's daily milk by its own milk type: Powder and Foster kids drink the powder ration,
    /// Mother kids suckle (default dam milk when the band says 0 ml), Mixed kids get half of each.
    /// </summary>
    public static (decimal PowderG, decimal MotherMl) KidDailyMilk(KidMilkMonthViewModel month, KidMilkType milkType, int ageDays)
    {
        var motherMl = month.MotherMilkMl > 0 ? month.MotherMilkMl : DefaultMotherMl(ageDays);
        return milkType switch
        {
            KidMilkType.Mother => (0m, motherMl),
            KidMilkType.Mixed => (month.PowderG / 2m, motherMl / 2m),
            KidMilkType.Foster => (month.PowderG, 0m),
            _ => (month.PowderG, 0m)
        };
    }
}

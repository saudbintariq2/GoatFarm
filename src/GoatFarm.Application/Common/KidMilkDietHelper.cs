using GoatFarm.Application.ViewModels.Feed;

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

    public static int BandForAge(int ageDays) =>
        ageDays <= 30 ? 0 : ageDays <= 60 ? 1 : 2;
}

using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;

namespace GoatFarm.Application.Common;

public static class VaccineDueHelper
{
    public static (DateOnly Date, int Days)? NextDue(
        Goat g,
        Vaccine v,
        IReadOnlyList<VaccinationHistory> log,
        Func<DateOnly, int> daysUntil)
    {
        if (v.RuleType == VaccineRuleType.Age)
        {
            if (log.Any(l => l.GoatId == g.Id && l.VaccineId == v.Id))
                return null;

            var born = BreedingRules.BirthDate(g) ?? g.EventDate;
            var dueDate = born.AddDays(v.Days ?? 0);
            return (dueDate, daysUntil(dueDate));
        }

        var last = log.Where(l => l.GoatId == g.Id && l.VaccineId == v.Id)
            .Select(l => l.VaccinationDate)
            .OrderByDescending(d => d)
            .FirstOrDefault();
        if (last == default)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            return (today, 0);
        }

        var nextDate = last.AddDays((v.Months ?? 0) * 30);
        return (nextDate, daysUntil(nextDate));
    }
}
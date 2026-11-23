using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;

namespace GoatFarm.Application.Common;

public static class VaccineDueHelper
{
    public static string RuleLabel(Vaccine v) => v.RuleType switch
    {
        VaccineRuleType.Age => $"At {v.Days} days old",
        VaccineRuleType.Repeat => $"Every {v.Months} months",
        VaccineRuleType.BeforeKidding => $"{v.Days ?? 30} days before kidding",
        _ => string.Empty
    };

    public static (DateOnly Date, int Days)? NextDue(
        Goat g,
        Vaccine v,
        IReadOnlyList<VaccinationHistory> log,
        Func<DateOnly, int> daysUntil)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var ageDays = HerdRules.AgeDays(g, today);

        if (v.MinAgeDays is int minAge && minAge > 0 && (ageDays is null || ageDays < minAge))
            return null;

        return v.RuleType switch
        {
            VaccineRuleType.BeforeKidding => NextBeforeKidding(g, v, log, daysUntil),
            VaccineRuleType.Age => NextAge(g, v, log, daysUntil),
            VaccineRuleType.Repeat => NextRepeat(g, v, log, daysUntil),
            _ => null
        };
    }

    private static (DateOnly Date, int Days)? NextBeforeKidding(
        Goat g,
        Vaccine v,
        IReadOnlyList<VaccinationHistory> log,
        Func<DateOnly, int> daysUntil)
    {
        if (HerdRules.KiddingDueDate(g) is not { } kiddingDue)
            return null;

        var daysBefore = v.Days ?? 30;
        var dueDate = kiddingDue.AddDays(-daysBefore);

        var last = LastVaccinationDate(g.Id, v.Id, log);
        if (g.MatedDate.HasValue && last != default && last >= g.MatedDate.Value)
            return null;

        return (dueDate, daysUntil(dueDate));
    }

    private static (DateOnly Date, int Days)? NextAge(
        Goat g,
        Vaccine v,
        IReadOnlyList<VaccinationHistory> log,
        Func<DateOnly, int> daysUntil)
    {
        if (log.Any(l => l.GoatId == g.Id && l.VaccineId == v.Id))
            return null;

        var born = BreedingRules.BirthDate(g) ?? g.EventDate;
        var dueDate = born.AddDays(v.Days ?? 0);
        return (dueDate, daysUntil(dueDate));
    }

    private static (DateOnly Date, int Days)? NextRepeat(
        Goat g,
        Vaccine v,
        IReadOnlyList<VaccinationHistory> log,
        Func<DateOnly, int> daysUntil)
    {
        var last = LastVaccinationDate(g.Id, v.Id, log);
        DateOnly nextDate;

        if (last != default)
        {
            var intervalDays = (v.Months ?? 12) * 30;
            nextDate = last.AddDays(intervalDays);
        }
        else
        {
            var born = BreedingRules.BirthDate(g) ?? g.EventDate;
            if (v.MinAgeDays is int minAgeFirst && minAgeFirst > 0)
                nextDate = born.AddDays(minAgeFirst);
            else
                return null;
        }

        return (nextDate, daysUntil(nextDate));
    }

    private static DateOnly LastVaccinationDate(int goatId, int vaccineId, IReadOnlyList<VaccinationHistory> log) =>
        log.Where(l => l.GoatId == goatId && l.VaccineId == vaccineId)
            .Select(l => l.VaccinationDate)
            .OrderByDescending(d => d)
            .FirstOrDefault();
}
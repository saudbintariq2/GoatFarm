using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;

namespace GoatFarm.Application.Common;

public static class HerdRules
{
    public const int WeanDaysDefault = 90;
    public const decimal WeanKgDefault = 13m;
    public const int YoungClassDays = 210;
    public const int AgeUpDays = 365;
    public const int HeatCycleDays = 21;
    public const int BuildUpDays = 60;
    public const int DryOffBeforeKiddingDays = 60;
    public const int DryBeforeCrossDays = 14;

    public static DateOnly? BirthDate(Goat g) => BreedingRules.BirthDate(g);

    public static int? AgeDays(Goat g, DateOnly today) => BreedingRules.AgeDays(g, today);

    public static bool NeedsWean(Goat g, decimal? latestWeightKg, DateOnly today)
    {
        if (g.Status != GoatStatus.Kid || g.WeanDate.HasValue)
            return false;
        var age = AgeDays(g, today);
        if (age >= WeanDaysDefault)
            return true;
        return latestWeightKg is >= WeanKgDefault;
    }

    public static bool NeedsAgeUp(Goat g, DateOnly today) =>
        g.Status == GoatStatus.Kid && AgeDays(g, today) is >= AgeUpDays;

    public static bool NeedsYoungClass(Goat g, DateOnly today) =>
        g.Status == GoatStatus.Kid && g.WeanDate.HasValue && g.YoungClass == YoungGoatClass.None &&
        AgeDays(g, today) is >= YoungClassDays;

    public static DateOnly? KiddingDueDate(Goat g) =>
        g.MatedDate.HasValue && g.KidsCount is > 0
            ? BreedingHelper.ExpectedKidding(g.MatedDate.Value)
            : null;

    public static bool NeedsDryOff(Goat g, DateOnly today)
    {
        var due = KiddingDueDate(g);
        if (due is null || g.Status != GoatStatus.Milking)
            return false;
        var dryOffStart = due.Value.AddDays(-DryOffBeforeKiddingDays);
        return today >= dryOffStart && today < due.Value;
    }

    public static bool CrossDueWithinWeek(Goat g, DateOnly today) =>
        g.NextCrossDate.HasValue && !g.MatedDate.HasValue &&
        BreedingHelper.DaysUntil(g.NextCrossDate.Value) <= 7;

    public static bool OnBuildUpFeed(Goat g, DateOnly today)
    {
        if (!g.NextCrossDate.HasValue || g.MatedDate.HasValue)
            return false;
        var buildStart = g.NextCrossDate.Value.AddDays(-BuildUpDays);
        return today >= buildStart && today < g.NextCrossDate.Value;
    }

    public static bool ShouldStopMilk(Goat g, DateOnly today) =>
        g.Status == GoatStatus.Milking && g.NextCrossDate.HasValue && !g.MatedDate.HasValue &&
        today >= g.NextCrossDate.Value.AddDays(-DryBeforeCrossDays);

    public static bool ScanDue(Goat g, DateOnly today) =>
        g.MatedDate.HasValue && !g.KidsCount.HasValue &&
        today.DayNumber - g.MatedDate.Value.DayNumber >= BreedingHelper.ScanAfterDays;

    /// <summary>
    /// Prototype heatdue: next heat (last + 21d) is within 2 days ahead or up to one cycle overdue.
    /// </summary>
    public static bool HeatExpected(DateOnly lastHeat, DateOnly today)
    {
        var daysUntilNext = lastHeat.AddDays(HeatCycleDays).DayNumber - today.DayNumber;
        return daysUntilNext <= 2 && daysUntilNext >= -HeatCycleDays;
    }
}

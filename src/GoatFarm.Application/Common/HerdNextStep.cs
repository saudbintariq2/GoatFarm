using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;

namespace GoatFarm.Application.Common;

public enum HerdNextStepAction
{
    None = 0,
    Wean,
    Classify,
    Graduate,
    Scan,
    RecordBirth,
    DryOff
}

public sealed class HerdKidStageLine
{
    public string Text { get; init; } = string.Empty;
    public string Color { get; init; } = "var(--ink-soft)";
    /// <summary>When set, show a "wean now" button after <see cref="Text"/>.</summary>
    public bool WeanLink { get; init; }
}

public sealed class HerdNextStepInfo
{
    public HerdNextStepAction Action { get; init; }
    public string ButtonLabel { get; init; } = string.Empty;
    /// <summary>Permission tab: herd or breeding.</summary>
    public string PermTab { get; init; } = "herd";
    public string PermAction { get; init; } = "edit";
    public int? ScanKidsCount { get; init; }
}

public static class HerdNextStep
{
    public static HerdNextStepInfo? Compute(Goat g, decimal? latestWeightKg, DateOnly today)
    {
        if (g.IsArchived)
            return null;

        if (g.Status == GoatStatus.Kid && !g.WeanDate.HasValue && HerdRules.NeedsWean(g, latestWeightKg, today))
            return new HerdNextStepInfo { Action = HerdNextStepAction.Wean, ButtonLabel = "Wean now", PermTab = "herd", PermAction = "edit" };

        if (HerdRules.NeedsYoungClass(g, today))
            return new HerdNextStepInfo { Action = HerdNextStepAction.Classify, ButtonLabel = "Classify", PermTab = "herd", PermAction = "edit" };

        if (g.Status == GoatStatus.Kid && g.WeanDate.HasValue &&
            g.YoungClass is YoungGoatClass.Doeling or YoungGoatClass.Buckling)
            return new HerdNextStepInfo { Action = HerdNextStepAction.Graduate, ButtonLabel = "Move to adult herd", PermTab = "herd", PermAction = "edit" };

        if (g.Gender == GoatGender.Female && g.MatedDate.HasValue && !g.KidsCount.HasValue)
        {
            var daysSince = today.DayNumber - g.MatedDate.Value.DayNumber;
            if (daysSince >= BreedingHelper.ScanAfterDays)
                return new HerdNextStepInfo { Action = HerdNextStepAction.Scan, ButtonLabel = "Scan result", PermTab = "breeding", PermAction = "edit" };
        }

        if (g.Gender == GoatGender.Female && g.MatedDate.HasValue && g.KidsCount is > 0 &&
            g.Status is GoatStatus.Pregnant or GoatStatus.Milking)
        {
            var due = BreedingHelper.ExpectedKidding(g.MatedDate.Value);
            var daysUntilDue = due.DayNumber - today.DayNumber;
            if (daysUntilDue <= 7)
            {
                return new HerdNextStepInfo
                {
                    Action = HerdNextStepAction.RecordBirth,
                    ButtonLabel = "Record birth",
                    PermTab = "breeding",
                    PermAction = "add",
                    ScanKidsCount = g.KidsCount
                };
            }
        }

        if (HerdRules.NeedsDryOff(g, today))
            return new HerdNextStepInfo { Action = HerdNextStepAction.DryOff, ButtonLabel = "Dry off", PermTab = "herd", PermAction = "edit" };

        return null;
    }

    public static HerdKidStageLine? KidStageLine(Goat g, decimal? latestWeightKg, DateOnly today)
    {
        if (g.Status == GoatStatus.Meat || (g.Status == GoatStatus.Kid && g.YoungClass == YoungGoatClass.Meat))
        {
            var w = g.WeanDate.HasValue ? $" · weaned {g.WeanDate:yyyy-MM-dd}" : "";
            return new HerdKidStageLine { Text = $"Meat / finishing ration{w}", Color = "var(--red)" };
        }

        if (g.Status != GoatStatus.Kid)
            return null;

        var age = HerdRules.AgeDays(g, today);
        var wDays = HerdRules.WeanDaysDefault;

        if (!g.WeanDate.HasValue)
        {
            if (HerdRules.NeedsWean(g, latestWeightKg, today))
                return new HerdKidStageLine
                {
                    Text = $"Powder milk · {WeanNote(g, latestWeightKg, today)} →",
                    Color = "var(--green-dark)",
                    WeanLink = true
                };
            if (age is { } d)
                return new HerdKidStageLine { Text = $"Powder milk · day {d} of {wDays} · wean in {Math.Max(0, wDays - d)} days", Color = "var(--amber)" };
            return new HerdKidStageLine { Text = "Powder milk · weaning age unknown", Color = "var(--amber)" };
        }

        var sinceWean = today.DayNumber - g.WeanDate.Value.DayNumber;
        if (g.YoungClass == YoungGoatClass.None)
        {
            var left = HerdRules.YoungClassDays - (age ?? 0);
            var classify = left > 0
                ? $"classify in {left} days"
                : g.Gender == GoatGender.Female ? "classify now: doeling or meat" : "classify now: buckling or meat";
            return new HerdKidStageLine { Text = $"Weaned {g.WeanDate:yyyy-MM-dd} · weaned-kid ration · {classify}", Color = "var(--blue)" };
        }

        var cls = g.YoungClass == YoungGoatClass.Doeling ? "Doeling" : "Buckling";
        if (HerdRules.NeedsAgeUp(g, today))
            return new HerdKidStageLine { Text = $"{cls} · over 1 year → move to adult herd", Color = "var(--red)" };
        return new HerdKidStageLine { Text = $"{cls} · weaned {sinceWean} days ago · move to adult herd when ready", Color = "var(--blue)" };
    }

    private static string WeanNote(Goat g, decimal? latestWeightKg, DateOnly today)
    {
        if (latestWeightKg is >= HerdRules.WeanKgDefault)
            return $"{latestWeightKg:F1} kg";
        var age = HerdRules.AgeDays(g, today);
        return age is { } d ? $"{d} days old" : "ready by weight/age";
    }
}

namespace GoatFarm.Application.Common;

public static class BreedingSeasonHelper
{
    public static BreedingSeasonPlan SeasonPlan(BreedingSeasonBatch batch, BreedingSeasonSettings season, DateOnly today)
    {
        var year = today.Year;
        var kidFrom = ParseMonthDay(year, batch.From);
        var kidTo = ParseMonthDay(year, batch.To);
        if (kidFrom.AddDays(-BreedingHelper.GestationDays) < today)
        {
            year++;
            kidFrom = ParseMonthDay(year, batch.From);
            kidTo = ParseMonthDay(year, batch.To);
        }

        var crossFrom = kidFrom.AddDays(-BreedingHelper.GestationDays);
        var crossTo = kidTo.AddDays(-BreedingHelper.GestationDays);
        return new BreedingSeasonPlan
        {
            Batch = batch,
            KidFrom = kidFrom,
            KidTo = kidTo,
            CrossFrom = crossFrom,
            CrossTo = crossTo,
            BuildFrom = crossFrom.AddDays(-season.BuildUp),
            ScanFrom = crossFrom.AddDays(season.ScanAfter),
            ScanTo = crossTo.AddDays(season.ScanAfter),
            DryFrom = kidFrom.AddDays(-season.DryOff),
            DryTo = kidTo.AddDays(-season.DryOff),
            WeanFrom = kidFrom.AddDays(season.WeanDays),
            WeanTo = kidTo.AddDays(season.WeanDays)
        };
    }

    public static DateOnly? BuildUpStart(DateOnly? nextCross, int buildUpDays) =>
        nextCross?.AddDays(-buildUpDays);

    public static DateOnly? StopMilkDate(DateOnly? nextCross, int dryBeforeCross) =>
        nextCross?.AddDays(-dryBeforeCross);

    public static (string Text, string Color) PlanStatus(
        DateOnly? nextCross,
        DateOnly? matedDate,
        int? kidsCount,
        string? birthBatchName,
        BreedingSeasonSettings season,
        DateOnly today)
    {
        var own = season.Batches.FirstOrDefault(b =>
            string.Equals(b.Name, birthBatchName, StringComparison.OrdinalIgnoreCase));
        var plan = SeasonPlan(own ?? season.Batches[0], season, today);

        if (matedDate.HasValue && kidsCount is > 0)
        {
            var due = BreedingHelper.ExpectedKidding(matedDate.Value);
            var window = NearestWindow(due, own, season);
            var early = (window.From.ToDateTime(TimeOnly.MinValue) - due.ToDateTime(TimeOnly.MinValue)).Days;
            var late = (due.ToDateTime(TimeOnly.MinValue) - window.To.ToDateTime(TimeOnly.MinValue)).Days;
            if (early > 0)
                return (early > 45
                        ? $"due {due:yyyy-MM-dd} — {early}d before the {window.From.Year} window"
                        : $"gives birth {early}d early",
                    early > 45 ? "#8a261c" : "var(--amber)");
            if (late > 0)
                return (late > 45
                        ? $"due {due:yyyy-MM-dd} — {late}d after the {window.From.Year} window"
                        : $"gives birth {late}d late",
                    late > 45 ? "#8a261c" : "var(--amber)");
            return ($"on plan — due {due:yyyy-MM-dd} ({window.From.Year} window)", "var(--green-dark)");
        }

        if (matedDate.HasValue)
            return ($"crossed {matedDate:yyyy-MM-dd}, waiting for scan", "var(--blue)");

        var dCross = BreedingHelper.DaysUntil(plan.CrossFrom);
        var dBuild = BreedingHelper.DaysUntil(plan.BuildFrom);
        if (nextCross.HasValue)
        {
            var dOwn = BreedingHelper.DaysUntil(nextCross.Value);
            var bu = BuildUpStart(nextCross, season.BuildUp);
            var dBu = bu.HasValue ? BreedingHelper.DaysUntil(bu.Value) : 999;
            if (dBu <= 0)
                return ($"feeding her up now · cross in {dOwn}d", "var(--amber)");
            return ($"extra feed starts in {dBu}d", "var(--ink-soft)");
        }

        if (dCross < 0)
            return ($"cross window open — {-dCross}d in", "#8a261c");
        if (dBuild <= 0)
            return ($"feeding her up now · cross in {dCross}d", "var(--amber)");
        return ($"extra feed starts in {dBuild}d", "var(--ink-soft)");
    }

    private static (DateOnly From, DateOnly To, string Name) NearestWindow(
        DateOnly date, BreedingSeasonBatch? only, BreedingSeasonSettings season)
    {
        var batches = only is not null ? [only] : season.Batches;
        var candidates = new List<(DateOnly From, DateOnly To, string Name)>();
        foreach (var b in batches)
        {
            foreach (var y in new[] { date.Year - 1, date.Year, date.Year + 1 })
            {
                candidates.Add((ParseMonthDay(y, b.From), ParseMonthDay(y, b.To), b.Name));
            }
        }

        if (candidates.Count == 0)
            return (new DateOnly(date.Year, 9, 1), new DateOnly(date.Year, 9, 15), "Main group");

        static int Dist(DateOnly d, DateOnly from, DateOnly to)
        {
            if (d < from) return (from.ToDateTime(TimeOnly.MinValue) - d.ToDateTime(TimeOnly.MinValue)).Days;
            if (d > to) return (d.ToDateTime(TimeOnly.MinValue) - to.ToDateTime(TimeOnly.MinValue)).Days;
            return 0;
        }

        return candidates.MinBy(c => Dist(date, c.From, c.To));
    }

    private static DateOnly ParseMonthDay(int year, string mmDd)
    {
        var parts = mmDd.Split('-');
        var month = int.Parse(parts[0]);
        var day = int.Parse(parts[1]);
        return new DateOnly(year, month, day);
    }

    public static string InDaysText(DateOnly when, DateOnly today)
    {
        var n = (when.ToDateTime(TimeOnly.MinValue) - today.ToDateTime(TimeOnly.MinValue)).Days;
        if (n < 0) return Math.Abs(n) + (Math.Abs(n) == 1 ? " day" : " days") + " ago";
        if (n == 0) return "today";
        return "in " + n + (n == 1 ? " day" : " days");
    }
}

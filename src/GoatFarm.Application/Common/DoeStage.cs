using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;

namespace GoatFarm.Application.Common;

/// <summary>Unified doe lifecycle stage (Step 0) — one resolver for herd, breeding, dashboard, and reports.</summary>
public enum FarmDoeStage
{
    GettingReady,
    InHeat,
    Crossed,
    WaitingScan,
    RescanDue,
    Pregnant,
    Dry,
    KiddedNursing,
    CullList
}

public sealed record DoeStageResult(
    FarmDoeStage Stage,
  string StatusKey,
    string Label,
    string? NextJob,
    DateOnly? NextJobDate,
    bool IsPregnant,
    DateOnly? DueDate);

public static class DoeStageResolver
{
    public const int ScanAfterCrossDays = 45;
    public const int MinScanAfterCrossDays = 30;
    public const int DryOffBeforeKidding = HerdRules.DryOffBeforeKiddingDays;
    public const int RestAfterKiddingDays = 60;

    public static DoeStageResult Resolve(
        Goat doe,
        BreedingEvent? lastCross,
        KiddingRecord? lastKidding,
        DateOnly today,
        IReadOnlySet<int>? doesWithBuckIn = null,
        DateOnly? rescanDue = null,
        int kiddingCount = -1)
    {
        if (doe.IsArchived || doe.Gender != GoatGender.Female || doe.Status == GoatStatus.Kid)
            return GettingReadyFallback(today);

        if (IsCull(doe, lastKidding))
            return New(FarmDoeStage.CullList, "cull_list", "Cull list", null, null);

        if (PregnancyRules.IsConfirmedPregnant(doe))
        {
            var due = BreedingHelper.ExpectedKidding(doe.MatedDate!.Value);
            var dryOff = due.AddDays(-DryOffBeforeKidding);
            if (doe.Status == GoatStatus.Dry || today >= dryOff)
                return New(FarmDoeStage.Dry, "dry", "Dry · due soon", "Kidding", due, preg: true, due: due);
            return New(FarmDoeStage.Pregnant, "pregnant", "Pregnant", "Dry off", dryOff, preg: true, due: due);
        }

        if (lastKidding is not null)
        {
            var daysSince = today.DayNumber - lastKidding.KiddingDate.DayNumber;
            var crossAfterKid = lastCross is not null && lastCross.MatedDate >= lastKidding.KiddingDate;
            if (daysSince is >= 0 and < RestAfterKiddingDays && !crossAfterKid && !doe.MatedDate.HasValue)
            {
                var readyOn = lastKidding.KiddingDate.AddDays(RestAfterKiddingDays);
                return New(FarmDoeStage.KiddedNursing, "kidded", "Kidded · nursing", "Ready for cross", readyOn);
            }
        }
        else if (kiddingCount > 0 && doe.Status == GoatStatus.Milking && !doe.MatedDate.HasValue &&
                 !doe.PrepCrossDate.HasValue && !doe.NextCrossDate.HasValue)
        {
            return New(FarmDoeStage.KiddedNursing, "kidded", "Kidded · nursing", null, null);
        }

        if (PregnancyRules.IsAwaitingScan(doe) && doe.MatedDate.HasValue)
        {
            var inconclusive = lastCross is not null &&
                lastCross.Outcome == BreedingOutcome.Open &&
                lastCross.Notes?.Contains("inconclusive", StringComparison.OrdinalIgnoreCase) == true;

            if (rescanDue.HasValue || inconclusive)
            {
                var when = rescanDue ?? today;
                return New(FarmDoeStage.RescanDue, "waiting_scan", "Rescan due", "Rescan", when);
            }

            var scanOn = doe.MatedDate.Value.AddDays(ScanAfterCrossDays);
            if (doesWithBuckIn is not null && doesWithBuckIn.Contains(doe.Id))
                return New(FarmDoeStage.Crossed, "crossed", "Crossed", "Scan", scanOn);

            return New(FarmDoeStage.WaitingScan, "waiting_scan", "Waiting scan", "Scan", scanOn);
        }

        if (doe.HeatEvents.Count > 0)
        {
            var last = doe.HeatEvents.Max(h => h.Date);
            var daysAgo = today.DayNumber - last.DayNumber;
            if (daysAgo is >= 0 and <= 2)
                return New(FarmDoeStage.InHeat, "in_heat", "In heat", null, null);
        }

        if (doe.PrepCrossDate.HasValue || doe.NextCrossDate.HasValue ||
            (doe.Status == GoatStatus.Dry && !doe.MatedDate.HasValue))
            return New(FarmDoeStage.GettingReady, "getting_ready", "Getting ready", null, null);

        return New(FarmDoeStage.GettingReady, "getting_ready", "Getting ready", null, null);

        static DoeStageResult GettingReadyFallback(DateOnly _) =>
            New(FarmDoeStage.GettingReady, "getting_ready", "Getting ready", null, null);
    }

    private static bool IsCull(Goat doe, KiddingRecord? lastKidding)
    {
        if (!string.IsNullOrWhiteSpace(doe.Comment) &&
            doe.Comment.Contains("Cull list", StringComparison.OrdinalIgnoreCase))
            return true;
        return lastKidding?.KeepDecision == DamKeepDecision.Cull;
    }

    private static DoeStageResult New(
        FarmDoeStage stage,
        string key,
        string label,
        string? job,
        DateOnly? jobDate,
        bool preg = false,
        DateOnly? due = null)
    {
        var nextJob = job is null ? null : jobDate is null ? job : $"{job} · {jobDate:dd MMM}";
        return new DoeStageResult(stage, key, label, nextJob, jobDate, preg, due);
    }

    /// <summary>Aggregate chips for breeding today / herd (B1).</summary>
    public static BreedingStageCountSummary SummarizeCounts(IReadOnlyDictionary<FarmDoeStage, int> counts) =>
        new(
            GettingReady: counts.GetValueOrDefault(FarmDoeStage.GettingReady) +
                          counts.GetValueOrDefault(FarmDoeStage.InHeat),
            Crossed: counts.GetValueOrDefault(FarmDoeStage.Crossed),
            WaitingScan: counts.GetValueOrDefault(FarmDoeStage.WaitingScan) +
                         counts.GetValueOrDefault(FarmDoeStage.RescanDue),
            Pregnant: counts.GetValueOrDefault(FarmDoeStage.Pregnant) +
                      counts.GetValueOrDefault(FarmDoeStage.Dry),
            Kidded: counts.GetValueOrDefault(FarmDoeStage.KiddedNursing));
}

public sealed record BreedingStageCountSummary(
    int GettingReady,
    int Crossed,
    int WaitingScan,
    int Pregnant,
    int Kidded);
using System.Globalization;
using System.Text.RegularExpressions;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;

namespace GoatFarm.Application.Common;

/// <summary>Unified doe lifecycle stage — one resolver for herd, goat card, breeding, kidding, dashboard and reports.</summary>
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
    DateOnly? DueDate,
    bool CanBePicked = false)
{
    /// <summary>"Waiting scan · Scan 17 Dec" — the label plus the next job, as shown in the herd list.</summary>
    public string Display => NextJob is null || Stage is FarmDoeStage.RescanDue or FarmDoeStage.Pregnant
        ? Label
        : $"{Label} · {NextJob}";
}

public static partial class DoeStageResolver
{
    /// <summary>First ultrasound is due this many days after the cross (17 Nov → 17 Dec).</summary>
    public const int FirstScanAfterCrossDays = BreedingHelper.ScanAfterDays;
    public const int DryOffBeforeKidding = HerdRules.DryOffBeforeKiddingDays;
    public const int RestAfterKiddingDays = 60;
    public const int GettingReadyCheckDays = 7;

    /// <param name="lastCross">Latest non-cancelled breeding event for the doe.</param>
    /// <param name="rescanDue">Due date of her open "Rescan" task — only used when the latest scan was Unsure.</param>
    /// <param name="nextSeasonCross">Start of the next crossing window in the season plan, for the getting-ready job.</param>
    public static DoeStageResult Resolve(
        Goat doe,
        BreedingEvent? lastCross,
        KiddingRecord? lastKidding,
        DateOnly today,
        IReadOnlySet<int>? doesWithBuckIn = null,
        DateOnly? rescanDue = null,
        int kiddingCount = -1,
        DateOnly? nextSeasonCross = null)
    {
        if (doe.IsArchived || doe.Gender != GoatGender.Female || doe.Status == GoatStatus.Kid)
            return New(FarmDoeStage.GettingReady, "getting_ready", "Getting ready", null, null);

        if (IsCull(doe, lastKidding))
            return New(FarmDoeStage.CullList, "cull_list", "Cull list", null, null);

        var crossDate = lastCross?.MatedDate ?? doe.MatedDate;

        // 1. Kidded recently and not crossed since → nursing, cannot join a buck group.
        if (lastKidding is not null && (crossDate is null || lastKidding.KiddingDate >= crossDate.Value))
        {
            var restEnd = lastKidding.KiddingDate.AddDays(RestAfterKiddingDays);
            if (today < restEnd && today >= lastKidding.KiddingDate)
                return New(FarmDoeStage.KiddedNursing, "kidded", "Kidded · nursing", "Ready again", restEnd);
        }

        var crossIsCurrent = crossDate is not null && (lastKidding is null || crossDate.Value > lastKidding.KiddingDate);
        if (crossIsCurrent)
        {
            var mated = crossDate!.Value;

            // 2. Pregnant (positive scan on the current cross) → dry-off, then kidding.
            if (IsPregnantCross(lastCross, doe))
            {
                var due = BreedingHelper.ExpectedKidding(mated);
                var dryOff = due.AddDays(-DryOffBeforeKidding);
                if (doe.Status == GoatStatus.Dry || today >= dryOff)
                    return New(FarmDoeStage.Dry, "dry", $"Dry · due {due:dd MMM yyyy}", "Kidding", due, preg: true, due: due);
                return New(FarmDoeStage.Pregnant, "pregnant", $"Pregnant · due {due:dd MMM yyyy}", "Dry off", dryOff, preg: true, due: due);
            }

            var stillOpen = lastCross is null
                ? PregnancyRules.IsAwaitingScan(doe)
                : lastCross.Outcome == BreedingOutcome.Open;
            if (stillOpen)
            {
                // 3. Only an Unsure scan keeps the cross open with a rescan.
                var noted = UnsureRescanDate(lastCross);
                if (noted is not null || (lastCross is null && rescanDue is not null))
                {
                    var when = noted ?? rescanDue ?? today;
                    return New(FarmDoeStage.RescanDue, "waiting_scan", $"Rescan due · {when:dd MMM}", "Rescan", when);
                }

                var scanOn = mated.AddDays(FirstScanAfterCrossDays);
                if (doesWithBuckIn is not null && doesWithBuckIn.Contains(doe.Id))
                    return New(FarmDoeStage.Crossed, "crossed", "With buck", "Scan", scanOn);

                // 4. Crossed and not scanned yet → waiting for the first scan.
                return New(FarmDoeStage.WaitingScan, "waiting_scan", "Waiting scan", "Scan", scanOn);
            }
        }

        if (doe.HeatEvents.Count > 0)
        {
            var last = doe.HeatEvents.Max(h => h.Date);
            var daysAgo = today.DayNumber - last.DayNumber;
            if (daysAgo is >= 0 and <= 2)
                return New(FarmDoeStage.InHeat, "in_heat", "In heat", "Cross", today, pickable: true);
        }

        // 5. Everything else → getting ready, always with a real next job.
        var planned = new[] { doe.NextCrossDate, doe.PrepCrossDate, nextSeasonCross }
            .Where(d => d.HasValue)
            .Select(d => d!.Value)
            .OrderBy(d => d < today ? 1 : 0)
            .ThenBy(d => d)
            .Cast<DateOnly?>()
            .FirstOrDefault();
        return planned is { } crossOn
            ? New(FarmDoeStage.GettingReady, "getting_ready", "Getting ready", "Cross", crossOn < today ? today : crossOn, pickable: true)
            : New(FarmDoeStage.GettingReady, "getting_ready", "Getting ready", "Weigh / BCS", today.AddDays(GettingReadyCheckDays), pickable: true);
    }

    public static bool IsPregnantCross(BreedingEvent? cross, Goat doe) =>
        cross is null
            ? PregnancyRules.IsConfirmedPregnant(doe)
            : cross.Outcome == BreedingOutcome.ConfirmedPregnant || (cross.Outcome == BreedingOutcome.Open && cross.ScanKids is > 0);

    /// <summary>Rescan date written on the cross by an Unsure scan ("Scan inconclusive …; rescan ~2027-01-01"), or null if none.</summary>
    public static DateOnly? UnsureRescanDate(BreedingEvent? cross)
    {
        if (cross?.Notes is not { } notes || !notes.Contains("inconclusive", StringComparison.OrdinalIgnoreCase))
            return null;
        var matches = RescanNote().Matches(notes);
        if (matches.Count == 0) return cross.MatedDate.AddDays(FirstScanAfterCrossDays + 14);
        return DateOnly.TryParseExact(matches[^1].Groups[1].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d
            : cross.MatedDate.AddDays(FirstScanAfterCrossDays + 14);
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
        DateOnly? due = null,
        bool pickable = false)
    {
        var nextJob = job is null ? null : jobDate is null ? job : $"{job} {jobDate:dd MMM}";
        return new DoeStageResult(stage, key, label, nextJob, jobDate, preg, due, pickable);
    }

    /// <summary>Aggregate chips for breeding today / herd.</summary>
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

    [GeneratedRegex(@"rescan ~(\d{4}-\d{2}-\d{2})", RegexOptions.IgnoreCase)]
    private static partial Regex RescanNote();
}

public sealed record BreedingStageCountSummary(
    int GettingReady,
    int Crossed,
    int WaitingScan,
    int Pregnant,
    int Kidded);

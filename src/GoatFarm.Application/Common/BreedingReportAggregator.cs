using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;

namespace GoatFarm.Application.Common;

public sealed class BreedingReportAgg
{
    public int Crossed { get; set; }
    public int Preg { get; set; }
    public int Empty { get; set; }
    public int Lost { get; set; }
    public int Services { get; set; }
    public int ExpKids { get; set; }
    public int Kidded { get; set; }
    public int Born { get; set; }
    public int Alive { get; set; }
    public int Still { get; set; }
    public int Hard { get; set; }
    public int Weaned { get; set; }
    public int WeanDue { get; set; }
    public double Wkg { get; set; }
    public int WkgN { get; set; }
    public int[] Lit { get; } = [0, 0, 0, 0];
    public Dictionary<string, int> Months { get; } = new(StringComparer.Ordinal);
    public List<int> Intervals { get; } = [];
    public List<int> AgeFirst { get; } = [];
}

/// <summary>V243 <c>aggR</c> — season breeding report metrics.</summary>
public static class BreedingReportAggregator
{
    private static readonly string[] ReportColors = ["#1E4635", "#3d7a5c", "#c4a035", "#8a261c"];

    public static BreedingReportAgg Aggregate(
        IReadOnlyList<BreedingEvent> events,
        IReadOnlyList<KiddingRecord> seasonKiddings,
        IReadOnlyList<KiddingRecord> allKiddings,
        IReadOnlyDictionary<int, Goat> goatsById,
        BreedingSeasonPlan plan,
        BreedingSeasonSettings season,
        DateOnly today)
    {
        var o = new BreedingReportAgg();
        if (events.Count == 0)
            return o;

        var crossedDoes = events.Select(e => e.DoeId).Distinct().ToHashSet();
        o.Crossed = crossedDoes.Count;

        foreach (var m in events)
        {
            var mk = m.MatedDate.ToString("yyyy-MM");
            o.Months[mk] = o.Months.GetValueOrDefault(mk); // ensure cross months appear on chart
            if (m.Outcome != BreedingOutcome.Open || m.ScanKids is > 0)
                o.Services++;

            if (m.Outcome == BreedingOutcome.Empty)
                o.Empty++;
            if (m.Outcome == BreedingOutcome.Aborted)
                o.Lost++;

            var pregnant = m.Outcome is BreedingOutcome.ConfirmedPregnant or BreedingOutcome.Kidded
                || m.ScanKids is > 0;
            if (pregnant)
            {
                o.ExpKids += m.ScanKids is > 0 ? m.ScanKids.Value : 2;
                var due = ExpectedDue(m);
                if (due.HasValue)
                {
                    var key = due.Value.ToString("yyyy-MM");
                    o.Months[key] = o.Months.GetValueOrDefault(key) + 1;
                }
            }
        }

        o.Preg = events
            .Where(e => e.Outcome is BreedingOutcome.ConfirmedPregnant or BreedingOutcome.Kidded || e.ScanKids is > 0)
            .Select(e => e.DoeId)
            .Distinct()
            .Count();

        foreach (var k in seasonKiddings)
        {
            o.Kidded++;
            o.Born += k.LiveKids + k.Stillborn;
            o.Alive += k.LiveKids;
            o.Still += k.Stillborn;
            var n = k.LiveKids + k.Stillborn;
            if (n > 0)
                o.Lit[Math.Min(4, n) - 1]++;

            if (k.Ease is KiddingEase.HardPull or KiddingEase.Vet or KiddingEase.Caesarean or KiddingEase.SlightHelp)
                o.Hard++;

            var mk = k.KiddingDate.ToString("yyyy-MM");
            o.Months[mk] = o.Months.GetValueOrDefault(mk) + k.LiveKids;

            if (!goatsById.TryGetValue(k.DoeId, out var dam))
                continue;
            var allDates = allKiddings.Where(x => x.DoeId == k.DoeId).Select(x => x.KiddingDate).OrderBy(d => d).ToList();
            var i = allDates.IndexOf(k.KiddingDate);
            if (i > 0)
                o.Intervals.Add(allDates[i].DayNumber - allDates[i - 1].DayNumber);
            else if (dam.Source == GoatSource.Born && HerdRules.BirthDate(dam) is { } dob)
                o.AgeFirst.Add((int)Math.Round((k.KiddingDate.DayNumber - dob.DayNumber) / 30.4));

            foreach (var kid in k.Kids)
            {
                var birth = HerdRules.BirthDate(kid);
                if (birth is null)
                    continue;
                var age = today.DayNumber - birth.Value.DayNumber;
                if (age < season.WeanDays && !kid.WeanDate.HasValue)
                    continue;
                o.WeanDue++;
                var died = kid.IsArchived && string.Equals(kid.ArchivedReason, "died", StringComparison.OrdinalIgnoreCase)
                    && (!kid.WeanDate.HasValue || kid.ArchivedDate < kid.WeanDate);
                if (kid.WeanDate.HasValue && !died)
                {
                    o.Weaned++;
                    var w = BuckStatsHelper.Adjusted90DayKg(kid) ?? BuckStatsHelper.WeanKg(kid);
                    if (w is > 0)
                    {
                        o.Wkg += (double)w.Value;
                        o.WkgN++;
                    }
                }
            }
        }

        o.Wkg = o.WkgN > 0 ? o.Wkg / o.WkgN : 0;
        return o;
    }

    public static string BuckTag(BreedingEvent e) =>
        e.Buck?.Tag ?? e.BuckTagText ?? "?";

    public static IReadOnlyList<BreedingEvent> FilterSeasonEvents(
        IReadOnlyList<BreedingEvent> all,
        HashSet<int> batchDoeIds,
        BreedingSeasonPlan plan,
        string? buckFilter)
    {
        var list = all.Where(e =>
                batchDoeIds.Contains(e.DoeId) &&
                e.MatedDate >= plan.CrossFrom && e.MatedDate <= plan.CrossTo)
            .ToList();
        if (string.IsNullOrWhiteSpace(buckFilter) || buckFilter == "all")
            return list;
        return list.Where(e => string.Equals(BuckTag(e), buckFilter, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public static HashSet<int> BatchDoeIds(IReadOnlyList<Goat> live, BreedingSeasonBatch batch) =>
        live.Where(g => g.Gender == GoatGender.Female && g.Status != GoatStatus.Kid &&
                        string.Equals(g.BirthBatchName, batch.Name, StringComparison.OrdinalIgnoreCase))
            .Select(g => g.Id)
            .ToHashSet();

    public static IReadOnlyList<KiddingRecord> SeasonKiddings(
        IReadOnlyList<KiddingRecord> kiddings,
        HashSet<int> batchDoeIds,
        BreedingSeasonPlan plan) =>
        kiddings.Where(k =>
            batchDoeIds.Contains(k.DoeId) &&
            k.KiddingDate >= plan.KidFrom && k.KiddingDate <= plan.WeanTo).ToList();

    public static int Pct(int a, int b) => b > 0 ? (int)Math.Round(a / (double)b * 100) : 0;

    public static string ColorForBuck(IReadOnlyList<string> buckTags, string tag)
    {
        var i = -1;
        for (var n = 0; n < buckTags.Count; n++)
        {
            if (string.Equals(buckTags[n], tag, StringComparison.OrdinalIgnoreCase))
            {
                i = n;
                break;
            }
        }
        return ReportColors[(i < 0 ? 0 : i) % ReportColors.Length];
    }

    private static DateOnly? ExpectedDue(BreedingEvent m)
    {
        if (m.MatedDate == default)
            return null;
        return m.MatedDate.AddDays(BreedingHelper.KiddingWindowStart + 7);
    }

    public static int? AverageDays(IReadOnlyList<int> values) =>
        values.Count > 0 ? (int)Math.Round(values.Average()) : null;
}

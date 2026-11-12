using System.Globalization;
using System.Net;
using GoatFarm.Application.Common;
using GoatFarm.Application.ViewModels.Breeding;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;

namespace GoatFarm.Infrastructure.Services;

internal static class BreedingReportsAssembler
{
    private static readonly string[] LitterLabels = ["Single", "Twins", "Triplets", "4 or more"];
    private static readonly string[] MonthShort = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
    private static readonly string[] FunnelColors = ["#1E4635", "#3d7a5c"];

    public static BreedingReportsViewModel Build(
        BreedingSeasonSettings season,
        string seasonBatchId,
        string? buckFilter,
        IReadOnlyList<Goat> live,
        IReadOnlyList<BreedingEvent> breedingEvents,
        IReadOnlyList<KiddingRecord> allKiddings,
        IReadOnlyList<BreedingEmptyLog> emptyLogs,
        DateOnly today,
        bool canEditTargets)
    {
        var batch = season.Batches.FirstOrDefault(b => b.Id == seasonBatchId) ?? season.Batches.FirstOrDefault();
        if (batch is null)
            return new BreedingReportsViewModel { HasData = false };

        var plan = BreedingSeasonHelper.SeasonPlan(batch, season, today);
        var batchDoes = BreedingReportAggregator.BatchDoeIds(live, batch);
        var goatsById = live.ToDictionary(g => g.Id);

        var seasonEventsAll = BreedingReportAggregator.FilterSeasonEvents(breedingEvents, batchDoes, plan, null);

        var buckTags = seasonEventsAll
            .Select(BreedingReportAggregator.BuckTag)
            .Where(t => t != "?")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var selectedBuck = string.IsNullOrWhiteSpace(buckFilter) || buckFilter == "all"
            ? "all"
            : buckTags.FirstOrDefault(t => string.Equals(t, buckFilter, StringComparison.OrdinalIgnoreCase)) ?? "all";

        var events = BreedingReportAggregator.FilterSeasonEvents(breedingEvents, batchDoes, plan, selectedBuck == "all" ? null : selectedBuck);
        var seasonKiddings = BreedingReportAggregator.SeasonKiddings(allKiddings, batchDoes, plan);
        if (selectedBuck != "all")
        {
            var eventIds = events.Select(e => e.Id).ToHashSet();
            seasonKiddings = seasonKiddings
                .Where(k => k.BreedingEventId.HasValue && eventIds.Contains(k.BreedingEventId.Value)
                            || events.Any(e => e.DoeId == k.DoeId && e.Outcome == BreedingOutcome.Kidded))
                .ToList();
        }

        var o = BreedingReportAggregator.Aggregate(events, seasonKiddings, allKiddings, goatsById, plan, season, today);
        if (o.Crossed == 0)
        {
            o.Crossed = live.Count(g => batchDoes.Contains(g.Id) &&
                                        g.MatedDate is { } md && md >= plan.CrossFrom && md <= plan.CrossTo);
            o.Preg = live.Count(g => batchDoes.Contains(g.Id) && PregnancyRules.IsConfirmedPregnant(g) &&
                                     g.MatedDate is { } md2 && md2 >= plan.CrossFrom);
        }
        var done = today > plan.WeanTo;
        var buckLab = selectedBuck != "all" ? " · " + selectedBuck : "";
        var kidsExpected = o.ExpKids > 0 ? o.ExpKids : (int)Math.Round(o.Preg * 1.7);

        string dz(int n) => n == 1 ? "doe" : "does";
        var pctCrossPreg = BreedingReportAggregator.Pct(o.Preg, o.Crossed);
        var pctWeanBorn = BreedingReportAggregator.Pct(o.Weaned, o.Born);
        var onMilk = o.Alive - o.WeanDue;
        var headline = done
            ? $"<b>{WebUtility.HtmlEncode(batch.Name)}{WebUtility.HtmlEncode(buckLab)}:</b> {o.Crossed} {dz(o.Crossed)} crossed → <b>{o.Preg} pregnant</b> ({pctCrossPreg}%) → {o.Kidded} kidded → <b>{o.Born} kids</b> born → <b>{o.Weaned} weaned</b> ({pctWeanBorn}% of kids{(onMilk > 0 ? ", " + onMilk + " still on milk" : "")})."
            : $"<b>{WebUtility.HtmlEncode(batch.Name)}{WebUtility.HtmlEncode(buckLab)} — in progress:</b> {o.Crossed} {dz(o.Crossed)} crossed → <b>{o.Preg} pregnant</b> so far ({pctCrossPreg}%){(o.Kidded > 0 ? " → " + o.Kidded + " kidded, " + o.Born + " kids born" : "")}. About <b>{kidsExpected} kids</b> expected from the scans.";

        var seasons = season.Batches.Select(b =>
        {
            var p = BreedingSeasonHelper.SeasonPlan(b, season, today);
            return new BreedingReportSeasonOptionViewModel
            {
                BatchId = b.Id,
                Label = b.Name,
                Done = today > p.WeanTo
            };
        }).ToList();

        var targets = season.Targets ?? new BreedingReportTargets();
        var scorecard = BuildScorecard(o, targets, done);
        var (doeFunnel, kidFunnel, showKid) = BuildFunnels(o, done);
        var litter = BuildLitter(o, done, kidsExpected);
        var (buckCompare, buckBars) = BuildBuckCompare(seasonEventsAll, seasonKiddings, allKiddings, goatsById, plan, season, today, buckTags, done);
        var months = BuildMonths(o, done);
        var problems = BuildProblems(o, done, seasonKiddings, emptyLogs, batchDoes, plan);
        var bestMothers = BuildBestMothers(live, allKiddings);
        var cullWatch = BuildCullWatch(live, allKiddings, emptyLogs, batchDoes, events);

        return new BreedingReportsViewModel
        {
            HasData = o.Crossed > 0 || o.Preg > 0 || o.Kidded > 0 || seasonKiddings.Count > 0,
            SeasonDone = done,
            SelectedBatchId = batch.Id,
            SelectedBuck = selectedBuck,
            HeadlineHtml = headline,
            Seasons = seasons,
            BuckOptions = buckTags,
            Scorecard = scorecard,
            DoeFunnel = doeFunnel,
            KidFunnel = kidFunnel,
            ShowKidFunnel = showKid,
            Litter = litter,
            BuckCompare = buckCompare,
            BuckBars = buckBars,
            Months = months,
            Problems = problems,
            BestMothers = bestMothers,
            CullWatch = cullWatch,
            CanEditTargets = canEditTargets
        };
    }

    private static List<BreedingReportScoreTileViewModel> BuildScorecard(
        BreedingReportAgg o, BreedingReportTargets targets, bool done)
    {
        var tiles = new (string key, string name, double? v, string u, bool hi, string say)[]
        {
            ("preg", "Pregnancy rate", o.Crossed > 0 ? BreedingReportAggregator.Pct(o.Preg, o.Crossed) : null, "%", true,
                $"{o.Preg} of {o.Crossed} does got pregnant"),
            ("kpd", "Kids per doe crossed", done && o.Crossed > 0 ? Math.Round(o.Born / (double)o.Crossed, 2) : null, "", true,
                done ? $"{o.Born} kids from {o.Crossed} does" : ""),
            ("surv", "Kids alive to weaning", done && o.WeanDue > 0 ? BreedingReportAggregator.Pct(o.Weaned, o.WeanDue) : null, "%", true,
                done && o.WeanDue > 0 ? $"{o.Weaned} of {o.WeanDue} kids reached weaning" : ""),
            ("kgd", "Kg weaned per doe crossed",
                done && o.WkgN > 0 && o.Crossed > 0
                    ? Math.Round(o.Weaned * o.Wkg / o.Crossed, 1)
                    : null, " kg", true,
                done && o.WkgN > 0 ? $"{(int)Math.Round(o.Weaned * o.Wkg)} kg of kids weaned (90-day weight)" : done ? "add wean weights to see this" : ""),
            ("interval", "Days between kiddings",
                done ? BreedingReportAggregator.AverageDays(o.Intervals) : null, " d", false,
                done && o.Intervals.Count > 0 ? "average gap from one kidding to the next" : done ? "no repeat mothers yet" : ""),
            ("age", "Age at first kidding",
                done ? BreedingReportAggregator.AverageDays(o.AgeFirst) : null, " m", false,
                done && o.AgeFirst.Count > 0 ? "home-bred first-time mothers" : done ? "no home-bred first-timers" : ""),
            ("spc", "Services per conception", o.Preg > 0 ? Math.Round(o.Services / (double)o.Preg, 2) : null, "", false,
                o.Preg > 0 ? $"{o.Services} crosses for {o.Preg} pregnancies" : "")
        };

        var list = new List<BreedingReportScoreTileViewModel>();
        foreach (var t in tiles)
        {
            var tg = (double)(targets.Get(t.key) ?? 0);
            if (tg <= 0) tg = DefaultTarget(t.key);
            var tile = new BreedingReportScoreTileViewModel
            {
                Key = t.key,
                Name = t.name,
                Value = t.v,
                Unit = t.u,
                Blurb = t.say,
                Target = (decimal)tg,
                HigherIsBetter = t.hi
            };
            if (t.v is null)
            {
                tile.Status = "na";
                tile.StatusLabel = done || t.key is "preg" or "spc" ? "No data yet" : "In progress";
                tile.StatusIcon = "⏳";
                tile.BarPercent = 0;
            }
            else
            {
                var val = t.v!.Value;
                var ok = t.hi ? val >= tg : val <= tg;
                var close = t.hi ? val >= tg * 0.95 : val <= tg * 1.05;
                tile.Status = ok ? "ok" : close ? "close" : "bad";
                tile.StatusLabel = ok ? "On target" : close ? "Close" : "Needs work";
                tile.StatusIcon = ok ? "✓" : close ? "⚠" : "✕";
                tile.BarPercent = (int)Math.Min(100,
                    t.hi ? val / (tg * 1.25) * 100 : tg * 0.8 / Math.Max(val, 0.01) * 100);
            }

            list.Add(tile);
        }

        return list;
    }

    private static double DefaultTarget(string key) => key switch
    {
        "preg" => 85,
        "kpd" => 1.5,
        "surv" => 90,
        "kgd" => 18,
        "interval" => 365,
        "age" => 18,
        "spc" => 1.3,
        _ => 0
    };

    private static (List<BreedingReportFunnelRowViewModel> doe, List<BreedingReportFunnelRowViewModel> kid, bool showKid)
        BuildFunnels(BreedingReportAgg o, bool done)
    {
        var mxD = Math.Max(o.Crossed, 1);
        var doe = new List<BreedingReportFunnelRowViewModel>
        {
            FunnelRow("Crossed", o.Crossed, 0, mxD, FunnelColors[0]),
            FunnelRow("Pregnant at scan", o.Preg, o.Crossed - o.Preg, mxD, FunnelColors[0]),
            FunnelRow("Kidded", o.Kidded, done ? Math.Max(0, o.Preg - o.Kidded) : 0, mxD, FunnelColors[0])
        };
        if (o.Born == 0)
            return (doe, [], false);
        var mxK = Math.Max(o.Born, 1);
        var kid = new List<BreedingReportFunnelRowViewModel>
        {
            FunnelRow("Born", o.Born, 0, mxK, FunnelColors[1]),
            FunnelRow("Born alive", o.Alive, o.Born - o.Alive, mxK, FunnelColors[1]),
            FunnelRow("Weaned", o.Weaned, Math.Max(0, o.WeanDue - o.Weaned), mxK, FunnelColors[1])
        };
        return (doe, kid, true);
    }

    private static BreedingReportFunnelRowViewModel FunnelRow(string label, int value, int lost, int mx, string color) =>
        new()
        {
            Label = label,
            Value = value,
            Lost = lost,
            BaseValue = mx,
            BarPercent = mx > 0 ? (int)Math.Round(value / (double)mx * 100) : 0,
            BarColor = color
        };

    private static BreedingReportLitterViewModel BuildLitter(BreedingReportAgg o, bool done, int kidsExpected)
    {
        var lt = o.Lit.Sum();
        if (lt == 0)
        {
            return new BreedingReportLitterViewModel
            {
                HasKiddings = false,
                SummaryHtml = $"No kiddings yet this season. Kids seen at scan: about {kidsExpected}.",
                ExpectedKidsFromScans = kidsExpected
            };
        }

        var top = Array.IndexOf(o.Lit, o.Lit.Max());
        var avg = o.Born / (double)lt;
        var summary =
            $"Most does had <b>{LitterLabels[top].ToLower()}</b> — {o.Lit[top]} of {lt} kiddings. Average <b>{avg:0.00}</b> kids per kidding.";
        var segments = new List<BreedingReportLitterSegmentViewModel>();
        for (var i = 0; i < 4; i++)
        {
            if (o.Lit[i] == 0) continue;
            segments.Add(new BreedingReportLitterSegmentViewModel
            {
                Label = LitterLabels[i],
                Count = o.Lit[i],
                Percent = BreedingReportAggregator.Pct(o.Lit[i], lt),
                Color = FunnelColors[i % 2 == 0 ? 0 : 1]
            });
        }

        return new BreedingReportLitterViewModel { HasKiddings = true, SummaryHtml = summary, Segments = segments };
    }

    private static (List<BreedingReportBuckMetricViewModel> metrics, List<BreedingReportBuckBarViewModel> flatBars)
        BuildBuckCompare(
            IReadOnlyList<BreedingEvent> seasonEventsAll,
            IReadOnlyList<KiddingRecord> seasonKiddings,
            IReadOnlyList<KiddingRecord> allKiddings,
            IReadOnlyDictionary<int, Goat> goatsById,
            BreedingSeasonPlan plan,
            BreedingSeasonSettings season,
            DateOnly today,
            IReadOnlyList<string> buckTags,
            bool done)
    {
        if (buckTags.Count == 0)
            return ([], []);

        var aggs = buckTags.Select(tag =>
        {
            var ev = seasonEventsAll.Where(e => string.Equals(BreedingReportAggregator.BuckTag(e), tag, StringComparison.OrdinalIgnoreCase)).ToList();
            var kid = seasonKiddings.Where(k =>
                ev.Any(e => e.Id == k.BreedingEventId) || ev.Any(e => e.DoeId == k.DoeId)).ToList();
            return (tag, BreedingReportAggregator.Aggregate(ev, kid, allKiddings, goatsById, plan, season, today));
        }).ToList();

        var metricDefs = new List<(string name, Func<BreedingReportAgg, double> fn, string unit)>
        {
            ("Pregnancy rate", x => BreedingReportAggregator.Pct(x.Preg, x.Crossed), "%")
        };
        if (done)
        {
            metricDefs.Add(("Kids per doe crossed", x => x.Crossed > 0 ? Math.Round(x.Born / (double)x.Crossed, 2) : 0, ""));
            metricDefs.Add(("Kids alive to weaning", x => BreedingReportAggregator.Pct(x.Weaned, x.WeanDue), "%"));
            metricDefs.Add(("Average wean weight (90-day)", x => Math.Round(x.Wkg, 1), " kg"));
        }

        var metrics = new List<BreedingReportBuckMetricViewModel>();
        var flat = new List<BreedingReportBuckBarViewModel>();
        foreach (var (name, fn, unit) in metricDefs)
        {
            var vals = aggs.Select(a => (a.tag, fn(a.Item2))).ToList();
            var mx = vals.Max(v => v.Item2);
            if (mx <= 0) mx = 1;
            var best = vals.OrderByDescending(v => v.Item2).FirstOrDefault().tag;
            var bars = vals.Select(v => new BreedingReportBuckBarViewModel
            {
                BuckTag = v.tag,
                Value = v.Item2,
                Display = v.Item2.ToString("0.##", CultureInfo.InvariantCulture) + unit,
                BarPercent = (int)Math.Round(v.Item2 / mx * 100),
                Color = BreedingReportAggregator.ColorForBuck(buckTags, v.tag),
                IsBest = vals.Count > 1 && v.Item2 > 0 && v.tag == best && Math.Abs(v.Item2 - mx) < 0.0001
            }).ToList();
            metrics.Add(new BreedingReportBuckMetricViewModel { Name = name, Unit = unit, Bars = bars });
            flat.AddRange(bars);
        }

        return (metrics, flat);
    }

    private static BreedingReportMonthsViewModel BuildMonths(BreedingReportAgg o, bool done)
    {
        var keys = o.Months.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
        if (keys.Count == 0 && o.Crossed > 0)
            keys = [DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM")];
        if (keys.Count == 0)
            return new BreedingReportMonthsViewModel { HasData = false, SummaryHtml = "No dates yet." };

        var labels = new List<string>();
        var start = keys[0];
        var end = keys[^1];
        var cur = start;
        while (string.Compare(cur, end, StringComparison.Ordinal) <= 0 && labels.Count < 12)
        {
            labels.Add(cur);
            if (!DateOnly.TryParse(cur + "-01", out var d))
                break;
            cur = d.AddMonths(1).ToString("yyyy-MM");
        }

        var vals = labels.Select(k => o.Months.GetValueOrDefault(k)).ToList();
        var mmx = Math.Max(vals.DefaultIfEmpty(0).Max(), 1);
        if (mmx == 0) mmx = 1;
        var busyIdx = vals.IndexOf(vals.Max());
        var busy = labels[busyIdx >= 0 ? busyIdx : 0];
        var busyMonth = int.Parse(busy.AsSpan(5, 2));
        var summary = done
            ? $"Busiest month: <b>{MonthShort[busyMonth - 1]}</b> with {vals.Max()} kids born alive."
            : "Kiddings <b>expected</b> — does due each month (from scan and cross dates).";

        var bars = labels.Select((k, i) =>
        {
            var m = int.Parse(k.AsSpan(5, 2));
            return new BreedingReportMonthBarViewModel
            {
                MonthKey = k,
                ShortLabel = MonthShort[m - 1],
                Value = vals[i],
                BarPercent = (int)Math.Round(vals[i] / (double)mmx * 100),
                ExpectedStyle = !done
            };
        }).ToList();

        return new BreedingReportMonthsViewModel { HasData = true, SummaryHtml = summary, Bars = bars };
    }

    private static List<BreedingReportProblemViewModel> BuildProblems(
        BreedingReportAgg o,
        bool done,
        IReadOnlyList<KiddingRecord> seasonKiddings,
        IReadOnlyList<BreedingEmptyLog> emptyLogs,
        HashSet<int> batchDoes,
        BreedingSeasonPlan plan)
    {
        var emptySeason = emptyLogs.Count(l => batchDoes.Contains(l.GoatId) && l.MatedDate >= plan.CrossFrom);
        var diedBeforeWean = Math.Max(0, o.WeanDue - o.Weaned);
        var items = new (string ic, string label, int? v)[]
        {
            ("⭕", "Empty at scan", o.Empty > 0 ? o.Empty : emptySeason),
            ("💔", "Lost pregnancy", o.Lost),
            ("🩺", "Hard / vet births", o.Kidded > 0 ? o.Hard : null),
            ("🕯", "Stillborn kids", o.Kidded > 0 ? o.Still : null),
            ("📉", "Kids died before weaning", o.WeanDue > 0 ? diedBeforeWean : null)
        };
        var pm = Math.Max(items.Max(x => x.v ?? 0), 1);
        return items.Select(p => new BreedingReportProblemViewModel
        {
            Icon = p.ic,
            Label = p.label,
            Count = p.v,
            BarPercent = p.v is > 0 ? (int)Math.Round(p.v.Value / (double)pm * 100) : 0
        }).ToList();
    }

    private static List<BreedingReportMotherRowViewModel> BuildBestMothers(
        IReadOnlyList<Goat> live, IReadOnlyList<KiddingRecord> allKiddings)
    {
        var does = live.Where(g => g.Gender == GoatGender.Female && g.Status != GoatStatus.Kid).ToList();
        var rows = does.Select(g =>
        {
            var kd = allKiddings.Where(k => k.DoeId == g.Id).OrderByDescending(k => k.KiddingDate).ToList();
            var kiddings = kd.Count;
            var lastBorn = kd.FirstOrDefault()?.LiveKids;
            return new BreedingReportMotherRowViewModel
            {
                DoeId = g.Id,
                Tag = g.Tag,
                Breed = g.Breed ?? "",
                Kiddings = kiddings,
                LastBornText = lastBorn is null ? "" : lastBorn + " kid" + (lastBorn == 1 ? "" : "s") + " last time",
                ScoreText = kiddings >= 3 ? "★ " + Math.Min(5, 3 + kiddings / 2).ToString("0.0") : kiddings > 0 ? "★ 3.5" : "—"
            };
        }).Where(r => r.Kiddings > 0).OrderByDescending(r => r.Kiddings).Take(5).ToList();
        return rows;
    }

    private static List<BreedingReportCullRowViewModel> BuildCullWatch(
        IReadOnlyList<Goat> live,
        IReadOnlyList<KiddingRecord> allKiddings,
        IReadOnlyList<BreedingEmptyLog> emptyLogs,
        HashSet<int> batchDoes,
        IReadOnlyList<BreedingEvent> seasonEvents)
    {
        var emptyByGoat = emptyLogs.GroupBy(l => l.GoatId).ToDictionary(g => g.Key, g => g.Count());
        var does = live.Where(g => g.Gender == GoatGender.Female && g.Status != GoatStatus.Kid).ToList();
        var list = new List<BreedingReportCullRowViewModel>();
        foreach (var g in does)
        {
            var kd = allKiddings.Where(k => k.DoeId == g.Id).ToList();
            var reasons = new List<string>();
            if (emptyByGoat.GetValueOrDefault(g.Id) >= 2)
                reasons.Add("Empty at scan " + emptyByGoat[g.Id] + " times");
            if (kd.Count(k => k.Ease == KiddingEase.Difficult) >= 2)
                reasons.Add("Difficult birth twice");
            if (!string.IsNullOrWhiteSpace(g.Comment) && g.Comment.Contains("Cull list", StringComparison.OrdinalIgnoreCase))
                reasons.Add("On cull list");
            if (seasonEvents.Any(e => e.DoeId == g.Id && e.Outcome == BreedingOutcome.Aborted))
                reasons.Add("Lost pregnancy this season");
            if (reasons.Count == 0) continue;
            list.Add(new BreedingReportCullRowViewModel
            {
                DoeId = g.Id,
                Tag = g.Tag,
                Kiddings = kd.Count,
                InSeason = batchDoes.Contains(g.Id),
                Reasons = reasons
            });
        }

        return list.OrderByDescending(c => c.Reasons.Count).ToList();
    }
}

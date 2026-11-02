using GoatFarm.Application.Common;
using GoatFarm.Application.ViewModels.Breeding;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;

namespace GoatFarm.Infrastructure.Services;

internal static class BreedingPageAssembler
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

    public static IReadOnlyList<BreedingPipelineRowViewModel> BuildPipelineRows(IReadOnlyList<Goat> goats)
    {
        var today = Today;
        var rows = goats
            .Where(g => g.Gender == GoatGender.Female && g.Status != GoatStatus.Kid && StageKey(g) is not null)
            .Select(g => MapPipeline(g, today))
            .ToList();
        var order = new Dictionary<string, int> { ["plan"] = 0, ["prep"] = 1, ["wait"] = 2, ["preg"] = 3 };
        rows.Sort((a, b) => order.GetValueOrDefault(a.StageKey, 9).CompareTo(order.GetValueOrDefault(b.StageKey, 9)));
        return rows;
    }

    private static string? StageKey(Goat g)
    {
        if (g.MatedDate.HasValue)
            return g.KidsCount is > 0 ? "preg" : "wait";
        if (g.PrepCrossDate.HasValue)
            return "prep";
        if (g.NextCrossDate.HasValue)
            return "plan";
        return null;
    }

    private static BreedingPipelineRowViewModel MapPipeline(Goat g, DateOnly today)
    {
        var stage = StageKey(g)!;
        var (label, css) = stage switch
        {
            "plan" => ("Next cross planned", "chip-dry"),
            "prep" => ("Ready for cross", "chip-kid"),
            "wait" => ("Waiting for scan", "chip-buck"),
            _ => ("Pregnant", "chip-preg")
        };

        var row = new BreedingPipelineRowViewModel
        {
            Id = g.Id,
            Tag = g.Tag,
            Name = g.Name,
            Owner = string.IsNullOrWhiteSpace(g.Owner) || g.Owner == "Farm" ? null : g.Owner,
            StageKey = stage,
            StageLabel = label,
            StageCssClass = css,
            MatedDate = g.MatedDate?.ToString("yyyy-MM-dd"),
            BuckTag = g.BuckTag,
            KidsCount = g.KidsCount
        };

        if (stage == "plan")
        {
            var cross = g.NextCrossDate!.Value;
            var bu = BreedingSeasonHelper.BuildUpStart(cross, HerdRules.BuildUpDays);
            var sm = BreedingSeasonHelper.StopMilkDate(cross, HerdRules.DryBeforeCrossDays);
            var d = BreedingHelper.DaysUntil(cross);
            var dbu = bu.HasValue ? BreedingHelper.DaysUntil(bu.Value) : 999;
            var dsm = sm.HasValue ? BreedingHelper.DaysUntil(sm.Value) : 999;
            var sub = dbu > 0
                ? $"extra feed from {bu:yyyy-MM-dd}"
                : dsm > 0
                    ? "<span style=\"color:var(--amber);font-weight:700\">feeding her up now</span> · stop milking " + sm!.Value.ToString("yyyy-MM-dd")
                    : "<span style=\"color:#8a261c;font-weight:700\">stop milking now</span>";
            row.KeyDateHtml = $"<b>cross {cross:yyyy-MM-dd}</b><div class=\"name\">{sub}</div>";
            row.DaysText = d < 0 ? $"overdue {-d}d" : $"in {d}d";
            if (d <= 14) row.DaysStyle = "color:var(--amber);font-weight:700";
        }
        else if (stage == "prep")
        {
            var since = g.PrepCrossDate!.Value;
            var n = today.DayNumber - since.DayNumber;
            if (since > today)
            {
                row.KeyDateHtml = $"<b>target cross {since:yyyy-MM-dd}</b><div class=\"name\">on extra feed</div>";
                row.DaysText = BreedingHelper.DaysUntil(since) + "d";
            }
            else
            {
                var warn = n >= 60
                    ? $"<span style=\"color:var(--amber);font-weight:700\">{n} days — cross her, or check she is cycling</span>"
                    : "on extra feed";
                row.KeyDateHtml = $"<b>ready since {since:yyyy-MM-dd}</b><div class=\"name\">{warn}</div>";
                row.DaysText = n + "d";
                if (n >= 60) row.DaysStyle = "color:var(--amber);font-weight:700";
            }
        }
        else if (stage == "wait")
        {
            var mated = g.MatedDate!.Value;
            var since = today.DayNumber - mated.DayNumber;
            var from = mated.AddDays(BreedingHelper.ScanAfterDays);
            var scanNote = since >= BreedingHelper.ScanAfterDays
                ? "<span style=\"color:var(--amber);font-weight:700\">scan now</span>"
                : $"scan in {BreedingHelper.ScanAfterDays - since}d";
            row.KeyDateHtml = $"<b>{from:yyyy-MM-dd}</b><div class=\"name\">{scanNote}</div>";
            row.DaysText = since + "d since";
        }
        else if (stage == "preg")
        {
            var mated = g.MatedDate!.Value;
            var kd = BreedingHelper.ExpectedKidding(mated);
            var d = BreedingHelper.DaysUntil(kd);
            row.KeyDateHtml = $"<b>{kd:yyyy-MM-dd}</b><div class=\"name\">{mated.AddDays(BreedingHelper.KiddingWindowStart):yyyy-MM-dd} → {mated.AddDays(BreedingHelper.KiddingWindowEnd):yyyy-MM-dd}</div>";
            row.DaysText = d < 0 ? $"overdue {-d}d" : $"in {d}d";
            row.DaysStyle = d < 0 ? "color:#8a261c;font-weight:700"
                : d <= 14 ? "color:var(--amber);font-weight:700"
                : "color:var(--green-dark);font-weight:700";
            row.KidsHtml = KidsCell(g);
        }

        if (stage != "preg")
            row.KidsHtml = g.KidsCount is > 0 ? KidsCell(g) : "<span class=\"breed\" style=\"color:#bbb\">—</span>";

        return row;
    }

    private static string KidsCell(Goat g)
    {
        if (g.KidsCount is not > 0) return "<span class=\"breed\" style=\"color:#bbb\">—</span>";
        var flag = g.KidsCount >= 2
            ? "<div class=\"name\" style=\"color:var(--amber);font-weight:700\">extra feed</div>" : "";
        return $"<b>{BreedingHelper.KidsLabel(g.KidsCount.Value)}</b>{flag}";
    }

    public static IReadOnlyList<BreedingNowJobViewModel> BuildNowJobs(
        IReadOnlyList<Goat> live,
        BreedingSeasonSettings season,
        DateOnly today)
    {
        var jobs = new List<BreedingNowJobViewModel>();
        void Add(string icon, string text, IEnumerable<Goat> does, string tone, string? filter = null) =>
            jobs.Add(new BreedingNowJobViewModel
            {
                Icon = icon,
                Text = text,
                Tone = tone,
                Tags = does.Select(d => d.Tag).ToList(),
                FilterLink = filter
            });

        var scan = live.Where(g => g.MatedDate.HasValue && !g.KidsCount.HasValue &&
            today.DayNumber - g.MatedDate.Value.DayNumber >= season.ScanAfter).ToList();
        if (scan.Count > 0)
            Add("🔍", scan.Count + " doe(s) ready for the ultrasound scan", scan, "var(--amber)", "br:scan");

        var due14 = live.Where(g => g.MatedDate.HasValue && g.KidsCount is > 0 &&
            BreedingHelper.DaysUntil(BreedingHelper.ExpectedKidding(g.MatedDate.Value)) is var d && d <= 14 && d >= 0).ToList();
        if (due14.Count > 0)
            Add("🍼", due14.Count + " doe(s) give birth within two weeks",
                due14, "#8a261c", "br:due30");

        var over = live.Where(g => g.MatedDate.HasValue && g.KidsCount is > 0 &&
            BreedingHelper.DaysUntil(BreedingHelper.ExpectedKidding(g.MatedDate.Value)) < 0).ToList();
        if (over.Count > 0)
            Add("⚠", over.Count + " doe(s) are past their due date", over, "#8a261c");

        var dry = live.Where(g => HerdRules.NeedsDryOff(g, today)).ToList();
        if (dry.Count > 0)
            Add("🌙", dry.Count + " doe(s) should stop being milked now", dry, "#8a261c", "dryoff");

        var heat = live.Where(g => g.Gender == GoatGender.Female && g.Status != GoatStatus.Kid && !g.MatedDate.HasValue &&
            LastHeat(g) is { } lh && HerdRules.HeatExpected(lh, today)).ToList();
        if (heat.Count > 0)
            Add("♡", heat.Count + " doe(s) expected in heat now", heat, "var(--blue)", "heatdue");

        var crossNow = live.Where(g => g.NextCrossDate.HasValue && !g.MatedDate.HasValue &&
            BreedingHelper.DaysUntil(g.NextCrossDate.Value) <= 7).ToList();
        if (crossNow.Count > 0)
            Add("♥", crossNow.Count + " doe(s) due to be crossed within a week", crossNow, "var(--pink)", "crossdue");

        var feedNow = live.Where(g => HerdRules.OnBuildUpFeed(g, today)).ToList();
        if (feedNow.Count > 0)
            Add("🌾", feedNow.Count + " doe(s) should be on extra feed", feedNow, "var(--amber)", "buildup");

        var ret = live.Where(g => g.MatedDate.HasValue && !g.KidsCount.HasValue &&
            today.DayNumber - g.MatedDate.Value.DayNumber is var age && age >= 17 && age <= 24).ToList();
        if (ret.Count > 0)
            Add("👀", ret.Count + " doe(s) to watch for a return to heat", ret, "var(--blue)");

        return jobs;
    }

    public static BreedingSeasonPanelViewModel BuildSeasonPanel(
        BreedingSeasonSettings season,
        string? selectedBatchId,
        IReadOnlyList<Goat> live,
        DateOnly today)
    {
        var batches = season.Batches;
        var cur = batches.FirstOrDefault(b => b.Id == selectedBatchId) ?? batches[0];
        var plan = BreedingSeasonHelper.SeasonPlan(cur, season, today);
        var inBatch = live.Count(g => string.Equals(g.BirthBatchName, cur.Name, StringComparison.OrdinalIgnoreCase));
        var unassigned = live.Count(g => g.Gender == GoatGender.Female && g.Status != GoatStatus.Kid &&
            string.IsNullOrWhiteSpace(g.BirthBatchName));

        var panel = new BreedingSeasonPanelViewModel
        {
            BuildUp = season.BuildUp,
            ScanAfter = season.ScanAfter,
            DryOff = season.DryOff,
            WeanDays = season.WeanDays,
            WeanKg = season.WeanKg,
            DryBeforeCross = season.DryBeforeCross,
            SeasonSub = $"births {plan.KidFrom:yyyy-MM-dd} → {plan.KidTo:yyyy-MM-dd}",
            SelectedBatchId = cur.Id,
            BatchCountText = inBatch + " doe(s) in this group" +
                (unassigned > 0 ? " · " + unassigned + " not assigned" : ""),
            Batches = batches.Select(b => new BreedingSeasonBatchViewModel
            {
                Id = b.Id,
                Name = b.Name,
                From = b.From,
                To = b.To
            }).ToList()
        };

        var dryCrossFrom = plan.CrossFrom.AddDays(-season.DryBeforeCross);
        var dryCrossTo = plan.CrossTo.AddDays(-season.DryBeforeCross);
        var steps = new (string Icon, string Label, string Rule, string Window, DateOnly When, bool Anchor)[]
        {
            ("🌾", "Start extra feed", season.BuildUp + " days <b>before crossing</b>",
                plan.BuildFrom.ToString("yyyy-MM-dd"), plan.BuildFrom, false),
            ("🌙", "Stop milking", season.DryBeforeCross + " days <b>before crossing</b>",
                $"{dryCrossFrom:yyyy-MM-dd} → {dryCrossTo:yyyy-MM-dd}", dryCrossFrom, false),
            ("♥", "Cross the does", $"<span class=\"breed\">{BreedingHelper.GestationDays} days <b>before birth</b> — a goat's pregnancy</span>",
                $"{plan.CrossFrom:yyyy-MM-dd} → {plan.CrossTo:yyyy-MM-dd}", plan.CrossFrom, false),
            ("🔍", "Ultrasound scan", season.ScanAfter + " days <b>after crossing</b>",
                $"{plan.ScanFrom:yyyy-MM-dd} → {plan.ScanTo:yyyy-MM-dd}", plan.ScanFrom, false),
            ("🌙", "Dry off the milkers", season.DryOff + " days <b>before birth</b>",
                $"{plan.DryFrom:yyyy-MM-dd} → {plan.DryTo:yyyy-MM-dd}", plan.DryFrom, false),
            ("🍼", "Birth window", "<b>you choose this</b>",
                $"{plan.KidFrom:yyyy-MM-dd} → {plan.KidTo:yyyy-MM-dd}", plan.KidFrom, true),
            ("🐣", "Wean the kids", season.WeanDays + " days <b>after birth</b>, or once they reach " + season.WeanKg + " kg",
                $"{plan.WeanFrom:yyyy-MM-dd} → {plan.WeanTo:yyyy-MM-dd}", plan.WeanFrom, false)
        };

        panel.Steps = steps.Select(s =>
        {
            var n = BreedingHelper.DaysUntil(s.When);
            var col = n < 0 ? "color:var(--ink-soft)" : n <= 21 ? "color:var(--amber);font-weight:700" : "font-weight:600";
            return new BreedingSeasonStepViewModel
            {
                Icon = s.Icon,
                Label = s.Label,
                RuleHtml = s.Rule,
                WindowText = s.Window.Contains("→", StringComparison.Ordinal)
                    ? FormatRange(s.Window)
                    : s.Window,
                WhenDate = s.When,
                DaysText = BreedingSeasonHelper.InDaysText(s.When, today),
                DaysStyle = col,
                Anchor = s.Anchor
            };
        }).ToList();

        return panel;
    }

    private static string FormatRange(string win)
    {
        var parts = win.Split(" → ");
        if (parts.Length != 2) return win;
        return ShortDate(parts[0]) + " → " + ShortDate(parts[1]);
    }

    private static string ShortDate(string iso)
    {
        if (!DateOnly.TryParse(iso, out var d)) return iso;
        return d.ToString("d MMM", System.Globalization.CultureInfo.GetCultureInfo("en-GB"));
    }

    public static IReadOnlyList<BreedingYearRowViewModel> BuildYearRows(
        BreedingSeasonSettings season,
        string selectedBatchId,
        IReadOnlyList<Goat> live,
        DateOnly today)
    {
        return season.Batches.Select(bb =>
        {
            var q = BreedingSeasonHelper.SeasonPlan(bb, season, today);
            var jobs = new (string Label, DateOnly Date)[]
            {
                ("extra feed", q.BuildFrom),
                ("stop milking", q.CrossFrom.AddDays(-season.DryBeforeCross)),
                ("cross", q.CrossFrom),
                ("scan", q.ScanFrom),
                ("dry off", q.DryFrom),
                ("births", q.KidFrom),
                ("wean", q.WeanFrom)
            }.Select(j => (j.Label, j.Date, N: BreedingHelper.DaysUntil(j.Date)))
                .Where(j => j.N >= 0)
                .OrderBy(j => j.N)
                .ToList();
            var nx = jobs.FirstOrDefault();
            return new BreedingYearRowViewModel
            {
                BatchId = bb.Id,
                Name = bb.Name,
                Highlight = bb.Id == selectedBatchId,
                DoeCount = live.Count(g => string.Equals(g.BirthBatchName, bb.Name, StringComparison.OrdinalIgnoreCase)),
                CrossText = $"<b>{q.CrossFrom:d MMM}</b> <span class=\"breed\">→ {q.CrossTo:d MMM}</span>",
                ScanText = q.ScanFrom.ToString("d MMM"),
                DryText = q.DryFrom.ToString("d MMM"),
                BirthText = $"<b>{q.KidFrom:d MMM}</b> <span class=\"breed\">→ {q.KidTo:d MMM} {q.KidFrom.Year}</span>",
                NextJobText = nx.Label is null ? "—" : nx.Label + " in " + nx.N + "d",
                NextJobStyle = nx.N <= 21 ? "color:var(--amber);font-weight:700" : ""
            };
        }).ToList();
    }

    public static IReadOnlyList<BreedingPlanRowViewModel> BuildPlanRows(
        IReadOnlyList<Goat> does,
        BreedingSeasonSettings season,
        IReadOnlyDictionary<int, int?> latestBcs,
        DateOnly today)
    {
        return does.Select(x =>
        {
            var (txt, col) = BreedingSeasonHelper.PlanStatus(
                x.NextCrossDate, x.MatedDate, x.KidsCount, x.BirthBatchName, season, today);
            var (stText, stCss) = DisplayHelper.GetStatusDisplay(x.Status);
            var batch = season.Batches.FirstOrDefault(b =>
                string.Equals(b.Name, x.BirthBatchName, StringComparison.OrdinalIgnoreCase));
            var batchHtml = batch is not null
                ? $"<span class=\"chip chip-cap\">{System.Net.WebUtility.HtmlEncode(batch.Name)}</span>"
                : "<span class=\"breed\" style=\"color:#bbb\">not set</span>";
            var bcs = latestBcs.GetValueOrDefault(x.Id);
            var bcsHtml = bcs switch
            {
                null => "<span class=\"breed\" style=\"color:#bbb\">—</span>",
                <= 2 => $"<span style=\"font-weight:700;color:#8a261c\">{bcs}/5</span>",
                >= 4 => $"<span style=\"font-weight:700;color:var(--amber)\">{bcs}/5</span>",
                _ => $"<span style=\"font-weight:700;color:var(--green-dark)\">{bcs}/5</span>"
            };
            return new BreedingPlanRowViewModel
            {
                Id = x.Id,
                Tag = x.Tag,
                StatusDisplay = stText,
                StatusCssClass = stCss,
                BatchHtml = batchHtml,
                BcsHtml = bcsHtml,
                StandText = txt,
                StandColor = col
            };
        }).ToList();
    }

    public static IReadOnlyList<BreedingHeatRowViewModel> BuildHeatRows(
        IReadOnlyList<Goat> does,
        IReadOnlyDictionary<int, List<DateOnly>> heatsByGoat,
        DateOnly today)
    {
        return does.Select(x =>
        {
            var heats = heatsByGoat.GetValueOrDefault(x.Id) ?? [];
            var last = heats.Count > 0 ? heats.Max() : (DateOnly?)null;
            var next = last?.AddDays(HerdRules.HeatCycleDays);
            var (stText, stCss) = DisplayHelper.GetStatusDisplay(x.Status);
            var crossed = x.MatedDate.HasValue && !x.KidsCount.HasValue;
            string inTxt = "—", inStyle = "";
            if (next.HasValue)
            {
                var due = BreedingHelper.DaysUntil(next.Value);
                if (due < -HerdRules.HeatCycleDays) { inTxt = "overdue"; inStyle = "color:#8a261c;font-weight:700"; }
                else if (due < 0) { inTxt = (-due) + "d ago"; inStyle = "color:var(--ink-soft)"; }
                else if (due <= 2) { inTxt = "now"; inStyle = "color:var(--amber);font-weight:800"; }
                else inTxt = "in " + due + "d";
            }

            return new BreedingHeatRowViewModel
            {
                Id = x.Id,
                Tag = x.Tag,
                StatusDisplay = stText,
                StatusCssClass = stCss,
                CrossedNote = crossed ? "crossed " + x.MatedDate!.Value.ToString("yyyy-MM-dd") : null,
                LastSeenHtml = last.HasValue
                    ? "<b>" + last.Value.ToString("yyyy-MM-dd") + "</b>"
                    : "<span class=\"breed\" style=\"color:#bbb\">not yet</span>",
                NextExpected = next?.ToString("yyyy-MM-dd"),
                InText = inTxt,
                InStyle = inStyle,
                Times = heats.Count
            };
        }).ToList();
    }

    public static IReadOnlyList<BreedingBuckRowViewModel> BuildBuckRows(
        IReadOnlyList<Goat> bucks,
        IReadOnlyList<Goat> live,
        IReadOnlyList<KiddingRecord> kiddingsYear,
        IReadOnlyDictionary<string, int> emptyByBuckTag,
        DateOnly today)
    {
        return bucks.Select(x =>
        {
            var carrying = live.Count(z => z.MatedDate.HasValue &&
                string.Equals(z.BuckTag, x.Tag, StringComparison.OrdinalIgnoreCase));
            var kidYr = kiddingsYear.Count(k =>
                (k.BreedingEvent?.Buck?.Tag ?? k.BreedingEvent?.BuckTagText ?? "")
                    .Equals(x.Tag, StringComparison.OrdinalIgnoreCase));
            var empty = emptyByBuckTag.GetValueOrDefault(x.Tag.ToLowerInvariant());
            var took = kidYr + live.Count(z =>
                string.Equals(z.BuckTag, x.Tag, StringComparison.OrdinalIgnoreCase) && z.KidsCount is > 0);
            var scanned = took + empty;
            int? conc = scanned > 0 ? (int)Math.Round(took / (double)scanned * 100) : null;
            var dates = live.Where(z => string.Equals(z.BuckTag, x.Tag, StringComparison.OrdinalIgnoreCase) && z.MatedDate.HasValue)
                .Select(z => z.MatedDate!.Value)
                .Concat(kiddingsYear.Where(k =>
                    (k.BreedingEvent?.Buck?.Tag ?? k.BreedingEvent?.BuckTagText ?? "")
                        .Equals(x.Tag, StringComparison.OrdinalIgnoreCase))
                    .Select(k => k.KiddingDate))
                .OrderBy(d => d)
                .ToList();
            var load = carrying >= 25 ? ("Heavy — rest him", "#8a261c")
                : carrying >= 15 ? ("Busy", "var(--amber)")
                : ("Fine", "var(--green-dark)");
            return new BreedingBuckRowViewModel
            {
                Id = x.Id,
                Tag = x.Tag,
                Carrying = carrying,
                CrossesThisYear = kidYr,
                ConceptionText = conc is null ? "—" : conc + "%",
                ConceptionStyle = conc is null ? ""
                    : conc >= 70 ? "font-weight:700;color:var(--green-dark)" : "font-weight:700;color:#8a261c",
                LastUsed = dates.Count > 0 ? dates[^1].ToString("yyyy-MM-dd") : null,
                LoadText = load.Item1,
                LoadColor = load.Item2
            };
        }).ToList();
    }

    private static DateOnly? LastHeat(Goat g) =>
        g.HeatEvents.Count > 0 ? g.HeatEvents.Max(h => h.Date) : null;
}

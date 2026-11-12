using GoatFarm.Application.Common;
using GoatFarm.Application.ViewModels.Breeding;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;

namespace GoatFarm.Infrastructure.Services;

internal static class BreedingPageAssembler
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

    public static IReadOnlyList<BreedingDoeFlowStepViewModel> BuildDoeFlowSteps(IReadOnlyList<Goat> goats, DateOnly today)
    {
        var counts = BreedingDoeFlowHelper.Steps.ToDictionary(s => s.Key, _ => 0);
        foreach (var g in goats)
        {
            var flow = BreedingDoeFlowHelper.FlowStage(g, today);
            if (flow is not null && counts.ContainsKey(flow))
                counts[flow]++;
        }

        return BreedingDoeFlowHelper.Steps
            .Select(s => new BreedingDoeFlowStepViewModel
            {
                Key = s.Key,
                Label = s.Label,
                Count = counts[s.Key],
                ArrowLabel = s.ArrowAfter
            })
            .ToList();
    }

    public static IReadOnlyList<BreedingPipelineRowViewModel> BuildPipelineRows(IReadOnlyList<Goat> goats)
    {
        var today = Today;
        var rows = goats
            .Where(g => g.Gender == GoatGender.Female && g.Status != GoatStatus.Kid && !g.IsArchived)
            .Select(g =>
            {
                var flow = BreedingDoeFlowHelper.FlowStage(g, today);
                if (flow is null) return null;
                return MapPipeline(g, today, flow);
            })
            .Where(r => r is not null)
            .Cast<BreedingPipelineRowViewModel>()
            .ToList();
        var flowOrder = BreedingDoeFlowHelper.Steps.Select((s, i) => (s.Key, i)).ToDictionary(x => x.Key, x => x.i);
        rows.Sort((a, b) =>
        {
            var c = flowOrder.GetValueOrDefault(a.FlowStageKey, 9).CompareTo(flowOrder.GetValueOrDefault(b.FlowStageKey, 9));
            return c != 0 ? c : string.Compare(a.Tag, b.Tag, StringComparison.OrdinalIgnoreCase);
        });
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

    private static BreedingPipelineRowViewModel MapPipeline(Goat g, DateOnly today, string flowStage)
    {
        var stage = StageKey(g) ?? flowStage switch
        {
            "in_heat" or "getting_ready" => "prep",
            "crossed" or "waiting_scan" => "wait",
            "pregnant" or "dry" => "preg",
            "kidded" => "prep",
            _ => "prep"
        };
        var (label, css) = stage switch
        {
            "plan" => ("Next cross planned", "chip-dry"),
            "prep" => ("Ready for cross", "chip-kid"),
            "wait" => ("Waiting for scan", "chip-buck"),
            "preg" => ("Pregnant", "chip-preg"),
            _ => (BreedingDoeFlowHelper.FlowStageLabel(flowStage), BreedingDoeFlowHelper.FlowStageChipClass(flowStage))
        };

        var row = new BreedingPipelineRowViewModel
        {
            Id = g.Id,
            Tag = g.Tag,
            Name = g.Name,
            Owner = string.IsNullOrWhiteSpace(g.Owner) || g.Owner == "Farm" ? null : g.Owner,
            FlowStageKey = flowStage,
            StageKey = stage,
            StageLabel = BreedingDoeFlowHelper.FlowStageLabel(flowStage),
            StageCssClass = BreedingDoeFlowHelper.FlowStageChipClass(flowStage),
            MatedDate = g.MatedDate?.ToString("yyyy-MM-dd"),
            BuckTag = g.BuckTag,
            KidsCount = g.KidsCount
        };

        if (stage == "plan" && g.NextCrossDate.HasValue)
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
        else if (stage == "prep" && g.PrepCrossDate.HasValue)
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
        else if (stage == "wait" && g.MatedDate.HasValue)
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
        else if (stage == "preg" && g.MatedDate.HasValue)
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

        if (string.IsNullOrEmpty(row.KeyDateHtml))
        {
            row.KeyDateHtml = flowStage switch
            {
                "in_heat" => "<b>In heat</b><div class=\"name\">watch for breeding</div>",
                "kidded" => "<b>With kids</b><div class=\"name\">ready for next season step</div>",
                "dry" => "<b>Dry</b><div class=\"name\">dry-off before kidding</div>",
                "crossed" => g.MatedDate.HasValue
                    ? $"<b>Crossed {g.MatedDate:yyyy-MM-dd}</b><div class=\"name\">buck still with does</div>"
                    : "<span class=\"breed\">—</span>",
                _ => "<span class=\"breed\">—</span>"
            };
            row.DaysText = g.MatedDate.HasValue ? (today.DayNumber - g.MatedDate.Value.DayNumber) + "d since" : "—";
        }

        return row;
    }

    private static string KidsCell(Goat g)
    {
        if (g.KidsCount is not > 0) return "<span class=\"breed\" style=\"color:#bbb\">—</span>";
        var flag = g.KidsCount >= 2
            ? "<div class=\"name\" style=\"color:var(--amber);font-weight:700\">extra feed</div>" : "";
        return $"<b>{BreedingHelper.KidsLabel(g.KidsCount.Value)}</b>{flag}";
    }

    public static IReadOnlyList<BreedingSeasonStepViewModel> BuildTodaySeasonNextSteps(
        BreedingSeasonSettings season,
        string batchId,
        DateOnly today)
    {
        var batch = season.Batches.FirstOrDefault(b => b.Id == batchId) ?? season.Batches[0];
        var plan = BreedingSeasonHelper.SeasonPlan(batch, season, today);
        var culture = System.Globalization.CultureInfo.GetCultureInfo("en-GB");
        var dryCrossFrom = plan.CrossFrom.AddDays(-season.DryBeforeCross);
        var dryCrossTo = plan.CrossTo.AddDays(-season.DryBeforeCross);

        var phases = new (DateOnly Start, DateOnly End, string Icon, string Label)[]
        {
            (plan.BuildFrom, plan.CrossFrom, "🌾", "Extra feed"),
            (dryCrossFrom, dryCrossTo, "🌙", "Stop milking"),
            (plan.CrossFrom, plan.CrossTo, "♥", "Crossing"),
            (plan.ScanFrom, plan.ScanTo, "🔍", "Scan"),
            (plan.DryFrom, plan.DryTo, "🌙", "Dry off"),
            (plan.KidFrom, plan.KidTo, "🍼", "Births"),
            (plan.WeanFrom, plan.WeanTo, "🐣", "Weaning")
        };

        static string RangeText(DateOnly a, DateOnly b, System.Globalization.CultureInfo culture) =>
            a.ToString("d MMM", culture) + " → " + b.ToString("d MMM", culture);

        static string AwayText(DateOnly start, DateOnly end, DateOnly today)
        {
            var s = BreedingHelper.DaysUntil(start);
            var e = BreedingHelper.DaysUntil(end);
            if (s > 0) return "in " + s + (s == 1 ? " day" : " days");
            if (e >= 0) return s == 0 ? "today" : "now · " + e + (e == 1 ? " day" : " days") + " left";
            return "done · " + (-e) + (Math.Abs(e) == 1 ? " day" : " days") + " ago";
        }

        return phases
            .Where(p => p.End >= today)
            .Take(3)
            .Select(p => new BreedingSeasonStepViewModel
            {
                Icon = p.Icon,
                Label = p.Label,
                WindowText = RangeText(p.Start, p.End, culture),
                WhenDate = p.Start,
                DaysText = AwayText(p.Start, p.End, today)
            })
            .ToList();
    }

    public static IReadOnlyList<BreedingTodayTimelineLabelViewModel> BuildTodayTimelineLabels(
        BreedingSeasonSettings season,
        string batchId,
        DateOnly today)
    {
        var (todayPct, _) = BuildPlanTimeline(season, batchId, today);
        var batch = season.Batches.FirstOrDefault(b => b.Id == batchId) ?? season.Batches[0];
        var plan = BreedingSeasonHelper.SeasonPlan(batch, season, today);
        var lo = plan.BuildFrom;
        var hi = plan.WeanTo;
        var span = Math.Max(1, hi.DayNumber - lo.DayNumber);
        double Pc(DateOnly d) => Math.Clamp((d.DayNumber - lo.DayNumber) * 100.0 / span, 0, 100);
        var culture = System.Globalization.CultureInfo.GetCultureInfo("en-GB");
        return new[]
        {
            (plan.CrossFrom, "♥", false),
            (plan.KidFrom, "🍼", false),
            (plan.WeanFrom, "🥣", true)
        }.Select(x => new BreedingTodayTimelineLabelViewModel
        {
            Icon = x.Item2,
            DateText = x.Item2 + " " + x.Item1.ToString("d MMM", culture),
            LeftPercent = Pc(x.Item1),
            AlignEnd = x.Item3
        }).ToList();
    }

    public static IReadOnlyList<BreedingNowJobViewModel> BuildNowJobs(
        IReadOnlyList<Goat> live,
        BreedingSeasonSettings season,
        DateOnly today,
        IReadOnlyList<BreedingGroupDto> groups,
        IReadOnlyDictionary<int, int> kiddingCounts,
        IReadOnlyDictionary<int, decimal?> latestWeightKg,
        IReadOnlySet<int> rescanGoatIds,
        IReadOnlyList<Goat> kids)
    {
        var drafts = new List<(DateOnly Sort, BreedingNowJobViewModel Job)>();
        var culture = System.Globalization.CultureInfo.GetCultureInfo("en-GB");

        void Add(
            DateOnly sortDate,
            string icon,
            string title,
            string subtext,
            IEnumerable<Goat> does,
            string tone,
            string? filter = null,
            string? breedView = null,
            string actionLabel = "Open",
            bool openScan = false)
        {
            var days = BreedingHelper.DaysUntil(sortDate);
            var urgency = days < 0 ? "no" : days == 0 ? "no" : days <= 60 ? "wt" : "ok";
            var whenLabel = days <= 0 ? "Today" : sortDate.ToString("d MMM", culture);
            var whenSub = days < 0 ? "overdue" : days == 0 ? "today" : "in " + days + " days";
            drafts.Add((sortDate, new BreedingNowJobViewModel
            {
                Icon = icon,
                Text = title,
                Subtext = subtext,
                Tone = tone,
                Tags = does.Select(d => d.Tag).ToList(),
                FilterLink = filter,
                SortDate = sortDate,
                UrgencyKey = urgency,
                WhenLabel = whenLabel,
                WhenSubtext = whenSub,
                ActionLabel = actionLabel,
                BreedView = breedView,
                OpenScan = openScan
            }));
        }

        foreach (var bb in season.Batches)
        {
            var plan = BreedingSeasonHelper.SeasonPlan(bb, season, today);
            if (plan.CrossTo < today) continue;

            var weighCutoff = plan.CrossFrom.AddDays(-season.BuildUp);
            if (BreedingHelper.DaysUntil(weighCutoff) > 21) continue;

            var young = live.Where(g =>
                g.Gender == GoatGender.Female && g.Status != GoatStatus.Kid && !g.MatedDate.HasValue &&
                (string.IsNullOrWhiteSpace(g.BirthBatchName) ||
                 string.Equals(g.BirthBatchName, bb.Name, StringComparison.OrdinalIgnoreCase)) &&
                kiddingCounts.GetValueOrDefault(g.Id) == 0 &&
                (g.PrepCrossDate.HasValue || g.NextCrossDate.HasValue ||
                 (LastHeat(g) is { } lh && HerdRules.HeatExpected(lh, today))) &&
                HerdRules.AgeDays(g, today) is >= 240).ToList();

            young = young.Where(g =>
            {
                latestWeightKg.TryGetValue(g.Id, out var kg);
                return kg is null or 0 || kg < season.WeanKg;
            }).ToList();

            if (young.Count > 0)
            {
                Add(weighCutoff, "⚖", "Weigh " + young.Count + " young doe(s)",
                    "Under minimum weight — needed before " + bb.Name + " crossing (" + plan.CrossFrom.ToString("d MMM", culture) + ")",
                    young, "var(--amber)", actionLabel: "Weigh");
            }

            var feedStart = plan.BuildFrom;
            if (BreedingHelper.DaysUntil(feedStart) is <= 14 and >= -30)
                Add(feedStart, "🌾", "Start extra feed", bb.Name + " · " + season.BuildUp + " days before crossing",
                    [], "var(--amber)", breedView: "plan", actionLabel: "Plan");

            var stopMilk = plan.CrossFrom.AddDays(-season.DryBeforeCross);
            if (BreedingHelper.DaysUntil(stopMilk) is <= 7 and >= -3)
                Add(stopMilk, "🌙", "Stop milking does for crossing", bb.Name + " · " + season.DryBeforeCross + " days before crossing",
                    [], "#8a261c", breedView: "plan", actionLabel: "Plan");

            if (BreedingHelper.DaysUntil(plan.KidFrom) is <= 10 and >= -7)
                Add(plan.KidFrom.AddDays(-7), "🏠", "Get kidding pens ready",
                    bb.Name + " · births from " + plan.KidFrom.ToString("d MMM", culture),
                    [], "#8a261c", breedView: "kidding");
        }

        var byGoat = live.ToDictionary(g => g.Id);
        foreach (var grp in groups.Where(g => string.Equals(g.Status, "active", StringComparison.OrdinalIgnoreCase)))
        {
            var buckIn = DateOnly.TryParse(grp.Start, out var sIn) ? sIn : (DateOnly?)null;
            var buckOut = DateOnly.TryParse(grp.End, out var sOut) ? sOut : (DateOnly?)null;
            var memberCount = grp.Members.Count;

            if (grp.CrossDeferred && string.IsNullOrWhiteSpace(grp.Start) && buckIn is null)
            {
                var plannedIn = grp.Members
                    .Select(m => byGoat.GetValueOrDefault(m.GoatId))
                    .Where(g => g?.NextCrossDate is not null)
                    .Select(g => g!.NextCrossDate!.Value)
                    .DefaultIfEmpty(today)
                    .Min();
                if (BreedingHelper.DaysUntil(plannedIn) <= 7)
                    Add(plannedIn, "🐐", "Put " + grp.Buck + " in with " + memberCount + " does",
                        grp.Name, [], "var(--pink)", breedView: "bucks");
            }
            else if (!grp.CrossDeferred && buckIn.HasValue && BreedingHelper.DaysUntil(buckIn.Value) <= 45)
            {
                Add(buckIn.Value.AddDays(-14), "🩺", "Buck check for " + grp.Buck,
                    "Needed before he goes in · " + grp.Name, [], "var(--blue)", breedView: "bucks", actionLabel: "Check");
            }

            if (buckIn.HasValue && grp.CrossDeferred && BreedingHelper.DaysUntil(buckIn.Value) <= 7)
                Add(buckIn.Value, "🐐", "Put " + grp.Buck + " in with " + memberCount + " does", grp.Name, [],
                    "var(--pink)", breedView: "bucks");

            if (buckOut.HasValue && !string.IsNullOrWhiteSpace(grp.Start) && BreedingHelper.DaysUntil(buckOut.Value) <= 3)
                Add(buckOut.Value, "🚪", "Take " + grp.Buck + " out", "After " + grp.StayDays + " days with the does", [],
                    "var(--amber)", breedView: "bucks");

            var waiting = grp.Members
                .Select(m => byGoat.GetValueOrDefault(m.GoatId))
                .Where(g => g is not null && PregnancyRules.IsAwaitingScan(g) && !rescanGoatIds.Contains(g!.Id))
                .Cast<Goat>()
                .ToList();
            if (waiting.Count == 0) continue;

            DateOnly scanWhen = today;
            if (buckOut.HasValue)
                scanWhen = buckOut.Value.AddDays(season.ScanAfter);
            else if (waiting.Min(g => g.MatedDate) is { } md)
                scanWhen = md.AddDays(season.ScanAfter);

            if (BreedingHelper.DaysUntil(scanWhen) <= 14)
            {
                Add(scanWhen, "🔍", "Scan " + waiting.Count + " doe(s)",
                    grp.Name + " · " + grp.Buck + " group",
                    waiting, "var(--amber)", breedView: "does", actionLabel: "Open", openScan: true);
            }
        }

        var handMated = live.Where(g => PregnancyRules.IsAwaitingScan(g) && !rescanGoatIds.Contains(g.Id) &&
            !groups.Any(grp => grp.Members.Any(m => m.GoatId == g.Id))).ToList();
        if (handMated.Count > 0 && handMated.Any(g => HerdRules.ScanDue(g, today)))
            Add(today, "🔍", "Scan " + handMated.Count(h => HerdRules.ScanDue(h, today)) + " doe(s)",
                "Hand-mated does · buck out + scan days",
                handMated.Where(g => HerdRules.ScanDue(g, today)).ToList(), "var(--amber)", breedView: "does", actionLabel: "Open", openScan: true);

        var rescans = live.Where(g => rescanGoatIds.Contains(g.Id) && PregnancyRules.IsAwaitingScan(g)).ToList();
        if (rescans.Count > 0)
            Add(today, "🔍", "Scan again — " + rescans.Count + " doe(s)", "Last scan was not sure",
                rescans, "var(--amber)", breedView: "does", actionLabel: "Open", openScan: true);

        var dryDoes = live.Where(g => PregnancyRules.IsConfirmedPregnant(g) && g.Status == GoatStatus.Milking)
            .Select(g => (Goat: g, DryOn: BreedingHelper.ExpectedKidding(g.MatedDate!.Value).AddDays(-season.DryOff)))
            .Where(x => BreedingHelper.DaysUntil(x.DryOn) is <= 14 and >= 0)
            .ToList();
        if (dryDoes.Count > 0)
        {
            var earliest = dryDoes.Min(x => x.DryOn);
            Add(earliest, "🌙", "Dry off " + dryDoes.Count + " pregnant doe(s)", "Earliest dry-off window",
                dryDoes.Where(x => x.DryOn == earliest).Select(x => x.Goat).ToList(), "#8a261c", breedView: "kidding");
        }

        var dry = live.Where(g => HerdRules.NeedsDryOff(g, today)).ToList();
        if (dry.Count > 0)
            Add(today, "🌙", "Dry off " + dry.Count + " milker(s)", "Due now", dry, "#8a261c", "dryoff", "does");

        var due7 = live.Where(g => PregnancyRules.IsConfirmedPregnant(g) &&
            BreedingHelper.DaysUntil(BreedingHelper.ExpectedKidding(g.MatedDate!.Value)) is var d7 && d7 <= 7 && d7 >= 0).ToList();
        if (due7.Count > 0)
        {
            var due = due7.Min(g => BreedingHelper.ExpectedKidding(g.MatedDate!.Value));
            Add(due, "🍼", due7.Count + " doe(s) due to kid", "Within 7 days — pens and colostrum ready",
                due7, "#8a261c", breedView: "kidding");
        }

        var over = live.Where(g => PregnancyRules.IsConfirmedPregnant(g) &&
            BreedingHelper.DaysUntil(BreedingHelper.ExpectedKidding(g.MatedDate!.Value)) < 0).ToList();
        if (over.Count > 0)
            Add(today, "⚠", over.Count + " doe(s) past due date", "Check kidding", over, "#8a261c", breedView: "kidding", actionLabel: "Show");

        var weanSoon = kids.Where(k =>
        {
            var age = HerdRules.AgeDays(k, today);
            if (!age.HasValue) return false;
            var left = season.WeanDays - age.Value;
            return left <= 7 && left >= 0;
        }).ToList();
        if (weanSoon.Count > 0)
        {
            var weanDates = weanSoon
                .Select(k => HerdRules.BirthDate(k)?.AddDays(season.WeanDays))
                .Where(d => d.HasValue)
                .Select(d => d!.Value)
                .ToList();
            if (weanDates.Count > 0)
            {
                Add(weanDates.Min(), "🐣", weanSoon.Count + " kid(s) wean this week",
                    "Add wean weights when they move off milk", weanSoon, "var(--amber)", breedView: "kidding");
            }
        }

        var heat = live.Where(g => g.Gender == GoatGender.Female && g.Status != GoatStatus.Kid && !g.MatedDate.HasValue &&
            LastHeat(g) is { } lh && HerdRules.HeatExpected(lh, today)).ToList();
        if (heat.Count > 0)
            Add(today, "♡", heat.Count + " doe(s) in heat now", "Record heat · ready to cross", heat, "var(--blue)", "heatdue", "does", "Show");

        var crossNow = live.Where(g => g.NextCrossDate.HasValue && !g.MatedDate.HasValue).ToList();
        if (crossNow.Count > 0)
        {
            var crossDate = crossNow.Min(g => g.NextCrossDate!.Value);
            Add(crossDate, "♥", crossNow.Count + " doe(s) due to cross", "Within the plan window",
                crossNow, "var(--pink)", "crossdue", "bucks", "Open");
        }

        var feedNow = live.Where(g => HerdRules.OnBuildUpFeed(g, today)).ToList();
        if (feedNow.Count > 0)
            Add(today, "🌾", feedNow.Count + " doe(s) on extra feed", "Build-up ration", feedNow, "var(--amber)", "buildup", "does", "Show");

        return drafts
            .OrderBy(d => d.Sort)
            .ThenBy(d => d.Job.Text, StringComparer.OrdinalIgnoreCase)
            .Select(d => d.Job)
            .ToList();
    }

    public static (string Name, double TodayPct, IReadOnlyList<BreedingTodayTimelineMarkerViewModel> Markers)
        BuildTodayTimeline(BreedingSeasonSettings season, string batchId, DateOnly today)
    {
        var batch = season.Batches.FirstOrDefault(b => b.Id == batchId) ?? season.Batches.First();
        var plan = BreedingSeasonHelper.SeasonPlan(batch, season, today);
        var start = plan.BuildFrom;
        var end = plan.WeanTo;
        var span = Math.Max(1, end.DayNumber - start.DayNumber);
        double Pct(DateOnly d) => Math.Clamp((d.DayNumber - start.DayNumber) * 100.0 / span, 0, 100);
        var culture = System.Globalization.CultureInfo.GetCultureInfo("en-GB");
        var markers = new[]
        {
            (plan.CrossFrom, "♥"),
            (plan.KidFrom, "🍼"),
            (plan.WeanFrom, "🥣")
        }.Select(m => new BreedingTodayTimelineMarkerViewModel
        {
            Icon = m.Item2,
            DateText = m.Item1.ToString("d MMM", culture),
            LeftPercent = Pct(m.Item1)
        }).ToList();
        return (batch.Name, Pct(today), markers);
    }

    public static (double TodayPct, IReadOnlyList<BreedingPlanTimelineSegmentViewModel> Segments) BuildPlanTimeline(
        BreedingSeasonSettings season,
        string batchId,
        DateOnly today)
    {
        var batch = season.Batches.FirstOrDefault(b => b.Id == batchId) ?? season.Batches.First();
        var plan = BreedingSeasonHelper.SeasonPlan(batch, season, today);
        var lo = plan.BuildFrom;
        var hi = plan.WeanTo;
        var span = Math.Max(1, hi.DayNumber - lo.DayNumber);
        double Pc(DateOnly d) => Math.Clamp((d.DayNumber - lo.DayNumber) * 100.0 / span, 0, 100);
        double W(DateOnly a, DateOnly b) => Math.Max(1, (b.DayNumber - a.DayNumber) * 100.0 / span);

        var dryCrossFrom = plan.CrossFrom.AddDays(-season.DryBeforeCross);
        var dryCrossTo = plan.CrossTo.AddDays(-season.DryBeforeCross);
        var segs = new (DateOnly A, DateOnly B, string Col, string Name)[]
        {
            (plan.BuildFrom, plan.CrossFrom, "#D8C38F", "Extra feed"),
            (dryCrossFrom, dryCrossTo, "#A9BFCB", "Stop milking"),
            (plan.CrossFrom, plan.CrossTo, "#D9826B", "Crossing"),
            (plan.ScanFrom, plan.ScanTo, "#8C9BD0", "Scan"),
            (plan.DryFrom, plan.DryTo, "#A9BFCB", "Dry off"),
            (plan.KidFrom, plan.KidTo, "#6FB07C", "Births"),
            (plan.WeanFrom, plan.WeanTo, "#E5AE5C", "Weaning")
        }.Select(s => new BreedingPlanTimelineSegmentViewModel
        {
            LeftPercent = Pc(s.A),
            WidthPercent = W(s.A, s.B),
            Color = s.Col,
            Title = s.Name + ": " + s.A.ToString("d MMM") + " → " + s.B.ToString("d MMM")
        }).ToList();
        return (Pc(today), segs);
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
            Batches = batches.Select(b =>
            {
                var bp = BreedingSeasonHelper.SeasonPlan(b, season, today);
                var culture = System.Globalization.CultureInfo.GetCultureInfo("en-GB");
                return new BreedingSeasonBatchViewModel
                {
                    Id = b.Id,
                    Name = b.Name,
                    Icon = string.IsNullOrWhiteSpace(b.Icon) ? "⭐" : b.Icon,
                    From = b.From,
                    To = b.To,
                    BirthWindowText = bp.KidFrom.ToString("d MMM", culture) + " → " + bp.KidTo.ToString("d MMM", culture)
                };
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
        DateOnly today,
        IReadOnlyList<BreedingGroupDto> breedingGroups)
    {
        return season.Batches.Select(bb =>
        {
            var q = BreedingSeasonHelper.SeasonPlan(bb, season, today);
            var batchDoes = live.Where(g => string.Equals(g.BirthBatchName, bb.Name, StringComparison.OrdinalIgnoreCase))
                .Select(g => g.Id).ToHashSet();
            var buckGroups = breedingGroups.Count(g =>
            {
                if (!string.Equals(g.Status, "active", StringComparison.OrdinalIgnoreCase)) return false;
                return g.Members.Any(m => batchDoes.Contains(m.GoatId));
            });
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
                Icon = string.IsNullOrWhiteSpace(bb.Icon) ? "⭐" : bb.Icon,
                Highlight = bb.Id == selectedBatchId,
                DoeCount = batchDoes.Count,
                BirthWindowText = $"{q.KidFrom:d MMM} → {q.KidTo:d MMM}",
                CrossText = $"<b>{q.CrossFrom:d MMM}</b> <span class=\"breed\">→ {q.CrossTo:d MMM}</span>",
                ScanText = q.ScanFrom.ToString("d MMM"),
                DryText = q.DryFrom.ToString("d MMM"),
                WeanText = q.WeanFrom.ToString("d MMM"),
                BuckGroupCount = buckGroups,
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
        IReadOnlyList<KiddingRecord> kiddingsAll,
        IReadOnlyList<Goat> bornKids,
        IReadOnlyDictionary<string, int> emptyByBuckTag,
        IReadOnlyList<BuckHealthCheck> buckChecks,
        DateOnly today)
    {
        return bucks.Select(x =>
        {
            var check = BuckCheckHelper.LatestCheck(buckChecks, x.Id);
            var checkOk = BuckCheckHelper.IsOk(check, today);
            var carrying = live.Count(z => z.MatedDate.HasValue &&
                string.Equals(z.BuckTag, x.Tag, StringComparison.OrdinalIgnoreCase));
            var kidYr = kiddingsYear.Count(k =>
                (k.BreedingEvent?.Buck?.Tag ?? k.BreedingEvent?.BuckTagText ?? "")
                    .Equals(x.Tag, StringComparison.OrdinalIgnoreCase));
            var kidsSired = kiddingsYear.Where(k =>
                    (k.BreedingEvent?.Buck?.Tag ?? k.BreedingEvent?.BuckTagText ?? "")
                        .Equals(x.Tag, StringComparison.OrdinalIgnoreCase))
                .Sum(k => k.LiveKids);
            var crossesYr = kidYr + live.Count(z =>
                string.Equals(z.BuckTag, x.Tag, StringComparison.OrdinalIgnoreCase) && z.MatedDate >= new DateOnly(today.Year, 1, 1));
            var kpc = crossesYr > 0 ? (kidsSired / (double)crossesYr).ToString("0.0") : "—";
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

            static bool KiddingByBuck(KiddingRecord k, string tag) =>
                (k.BreedingEvent?.Buck?.Tag ?? k.BreedingEvent?.BuckTagText ?? "")
                    .Equals(tag, StringComparison.OrdinalIgnoreCase);

            var buckKiddings = kiddingsAll.Where(k => KiddingByBuck(k, x.Tag)).ToList();
            var kidsOfBuck = bornKids.Where(k => BuckStatsHelper.KidSiredBy(k, x)).ToList();
            var kpk = buckKiddings.Count > 0 ? buckKiddings.Average(k => k.LiveKids) : (double?)null;
            var avgBirth = BuckStatsHelper.Average(kidsOfBuck.Select(BuckStatsHelper.BirthKg));
            var avg90 = BuckStatsHelper.Average(kidsOfBuck.Select(BuckStatsHelper.Adjusted90DayKg));

            return new BreedingBuckRowViewModel
            {
                Id = x.Id,
                Tag = x.Tag,
                Carrying = carrying,
                CrossesThisYear = kidYr,
                KidsSiredYear = kidsSired,
                KidsSired = kidsOfBuck.Count,
                KidsPerCrossText = kpc,
                KidsPerKiddingText = BuckStatsHelper.FormatAvg(kpk),
                AvgBirthKgText = BuckStatsHelper.FormatAvg(avgBirth),
                Avg90DayKgText = BuckStatsHelper.FormatAvg(avg90),
                ConceptionPercent = conc,
                ConceptionText = conc is null ? "—" : conc + "%",
                ConceptionStyle = conc is null ? ""
                    : conc >= 70 ? "font-weight:700;color:var(--green-dark)" : "font-weight:700;color:#8a261c",
                LastUsed = dates.Count > 0 ? dates[^1].ToString("yyyy-MM-dd") : null,
                LoadText = load.Item1,
                LoadColor = load.Item2,
                BuckCheckOk = checkOk,
                LastCheckDate = check?.CheckDate.ToString("yyyy-MM-dd"),
                BuckCheckLineHtml = checkOk
                    ? "Buck check done " + check!.CheckDate.ToString("d MMM yyyy") +
                      (check.Bcs is not null ? " · BCS " + check.Bcs.Value.ToString("0.#") : "") +
                      (check.WeightKg is not null ? " · " + check.WeightKg.Value.ToString("0.#") + " kg" : "")
                    : check is null
                        ? "Buck check needed"
                        : "Last check " + check.CheckDate.ToString("d MMM yyyy") + " — check again"
            };
        }).ToList();
    }

    private static DateOnly? LastHeat(Goat g) =>
        g.HeatEvents.Count > 0 ? g.HeatEvents.Max(h => h.Date) : null;
}

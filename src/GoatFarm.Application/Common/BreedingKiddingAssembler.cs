using GoatFarm.Application.ViewModels.Breeding;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;

namespace GoatFarm.Application.Common;

public static class BreedingKiddingAssembler
{
    private const int MotherCheckDays = 200;

    public static BreedingKiddingStatsViewModel BuildStats(
        IReadOnlyList<BreedingDueToKidRowViewModel> dueRows,
        IReadOnlyList<BreedingMotherCheckRowViewModel> motherRows,
        IReadOnlyList<BreedingKidGroundRowViewModel> kidRows,
        IReadOnlyList<Goat> does,
        DateOnly today)
    {
        var dueSoon = dueRows.Count(r => r.DaysToDue <= 30);
        var kiddedMothers = does.Count(d =>
            d.Status == GoatStatus.Milking &&
            !PregnancyRules.IsAwaitingScan(d) &&
            !(d.MatedDate.HasValue && d.KidsCount is > 0));
        var watch = motherRows.Count(r => !string.IsNullOrEmpty(r.RowCssClass));

        return new BreedingKiddingStatsViewModel
        {
            DueTotal = dueRows.Count,
            DueSoon30 = dueSoon,
            KiddedMothers = kiddedMothers,
            KidsOnGround = kidRows.Count(k => k.StatusDisplay == "On milk"),
            MothersToWatch = watch
        };
    }

    public static IReadOnlyList<BreedingDueToKidRowViewModel> BuildDueRows(
        IReadOnlyList<Goat> confirmed,
        IReadOnlyDictionary<int, int> kiddingsByDoe,
        DateOnly today)
    {
        return confirmed
            .Select(g =>
            {
                var mated = g.MatedDate!.Value;
                var kd = BreedingHelper.ExpectedKidding(mated);
                var w1 = mated.AddDays(BreedingHelper.KiddingWindowStart);
                var w2 = mated.AddDays(BreedingHelper.KiddingWindowEnd);
                var du = BreedingHelper.DaysUntil(kd);
                var n = kiddingsByDoe.GetValueOrDefault(g.Id);
                var flow = BreedingDoeFlowHelper.FlowStage(g, today) ?? "pregnant";
                return new BreedingDueToKidRowViewModel
                {
                    Id = g.Id,
                    Tag = g.Tag,
                    Name = g.Name,
                    Breed = g.Breed,
                    KiddingsCount = n,
                    NextKiddingOrdinal = Ordinal(n + 1),
                    BuckTag = g.BuckTag,
                    FlowStageLabel = BreedingDoeFlowHelper.FlowStageLabel(flow),
                    FlowStageCssClass = BreedingDoeFlowHelper.FlowStageChipClass(flow),
                    IsDry = g.Status == GoatStatus.Dry,
                    DueWindow = $"{w1:yyyy-MM-dd} – {w2:yyyy-MM-dd}",
                    DaysToDue = du,
                    DaysToDueText = du < 0 ? $"{-du} days overdue" : du == 0 ? "today" : $"in {du} days",
                    DaysToDueStyle = du < 0 ? "color:#8a261c;font-weight:600"
                        : du <= 7 ? "color:var(--amber);font-weight:600" : "color:var(--muted)",
                    KidsCount = g.KidsCount,
                    KidsSeenDisplay = g.KidsCount switch
                    {
                        null or 0 => "—",
                        >= 4 => "4+",
                        _ => g.KidsCount.ToString()!
                    }
                };
            })
            .OrderBy(r => r.DaysToDue)
            .ToList();
    }

    public static IReadOnlyList<BreedingMotherCheckRowViewModel> BuildMotherCheckRows(
        IReadOnlyList<KiddingRecord> allKiddings,
        IReadOnlyDictionary<int, Goat> goatsById,
        IReadOnlyDictionary<int, int> kiddingsByDoe,
        DateOnly today)
    {
        var cutoff = today.AddDays(-MotherCheckDays);
        return allKiddings
            .Where(k => k.KiddingDate >= cutoff)
            .OrderByDescending(k => k.KiddingDate)
            .Select(k =>
            {
                goatsById.TryGetValue(k.DoeId, out var doe);
                var n = kiddingsByDoe.GetValueOrDefault(k.DoeId);
                var born = k.LiveKids + k.Stillborn;
                var days = today.DayNumber - k.KiddingDate.DayNumber;
                var ease = EaseLabel(k.Ease);
                var warn = k.Ease != KiddingEase.Normal || k.Stillborn > 0;
                return new BreedingMotherCheckRowViewModel
                {
                    DoeId = k.DoeId,
                    KiddingRecordId = k.Id,
                    Tag = doe?.Tag ?? $"#{k.DoeId}",
                    Breed = doe?.Breed ?? "",
                    KiddingsCount = n,
                    KiddingOrdinalText = Ordinal(n),
                    KiddedDate = k.KiddingDate.ToString("yyyy-MM-dd"),
                    KiddedAgoText = days == 0 ? "today" : days == 1 ? "yesterday" : $"{days} days ago",
                    Alive = k.LiveKids,
                    Born = born,
                    EaseDisplay = ease,
                    EaseCssClass = k.Ease switch
                    {
                        KiddingEase.Normal => "chip-kid",
                        KiddingEase.Assisted => "chip-dry",
                        _ => "chip-preg"
                    },
                    Notes = k.Notes,
                    RowCssClass = warn ? "breed-mother-warn" : ""
                };
            })
            .ToList();
    }

    public static IReadOnlyList<BreedingKidAutoWeanBarViewModel> BuildAutoWeanBars(
        BreedingSeasonSettings season,
        IReadOnlyList<Goat> onMilkKids)
    {
        var bars = new List<BreedingKidAutoWeanBarViewModel>();
        foreach (var batch in season.Batches)
        {
            var n = onMilkKids.Count(k =>
                string.Equals(k.BirthBatchName, batch.Name, StringComparison.OrdinalIgnoreCase));
            if (n == 0) continue;
            bars.Add(new BreedingKidAutoWeanBarViewModel
            {
                BatchId = batch.Id,
                Name = batch.Name,
                Icon = batch.Icon,
                WeanDays = season.WeanDays,
                AutoWean = season.AutoWean,
                KidCount = n
            });
        }

        var other = onMilkKids.Count(k =>
            string.IsNullOrWhiteSpace(k.BirthBatchName) ||
            season.Batches.All(b => !string.Equals(b.Name, k.BirthBatchName, StringComparison.OrdinalIgnoreCase)));
        if (other > 0 && bars.Count == 0)
        {
            bars.Add(new BreedingKidAutoWeanBarViewModel
            {
                BatchId = season.Batches.FirstOrDefault()?.Id ?? "b1",
                Name = "All kids",
                Icon = "⭐",
                WeanDays = season.WeanDays,
                AutoWean = season.AutoWean,
                KidCount = other
            });
        }

        return bars;
    }

    public static IReadOnlyList<BreedingKidGroundRowViewModel> BuildKidRows(
        IReadOnlyList<Goat> bornKids,
        IReadOnlyDictionary<int, Goat> goatsById,
        int weanDays,
        bool autoWean,
        DateOnly today)
    {
        return bornKids
            .Where(g => !g.IsArchived && g.Status == GoatStatus.Kid)
            .OrderByDescending(g => g.EventDate)
            .Select(g =>
            {
                var age = HerdRules.AgeDays(g, today) ?? 0;
                var birthKg = BuckStatsHelper.BirthKg(g);
                var weanKg = BuckStatsHelper.WeanKg(g);
                var kg90 = BuckStatsHelper.Adjusted90DayKg(g);
                var weaned = g.WeanDate.HasValue;
                var weanTarget = g.EventDate.AddDays(weanDays);
                var daysToWean = weanTarget.DayNumber - today.DayNumber;
                var weak = g.KidHealthTag != KidHealthTag.Good
                    || g.HealthState is GoatHealthState.Sick or GoatHealthState.WeakOffFeed or GoatHealthState.UnderTreatment;

                string? damTag = null;
                if (g.DamId is { } did && goatsById.TryGetValue(did, out var dam))
                    damTag = dam.Tag;
                else if (!string.IsNullOrWhiteSpace(g.DamTagText))
                    damTag = g.DamTagText;

                string sub;
                string subStyle = "color:var(--muted)";
                if (weaned)
                {
                    var how = string.IsNullOrWhiteSpace(g.WeanMethod) ? "manual" : g.WeanMethod!;
                    sub = $"{how} · {g.WeanDate!.Value:dd MMM}";
                }
                else if (autoWean)
                {
                    if (daysToWean <= 0)
                    {
                        sub = "weans today · " + weanTarget.ToString("dd MMM");
                        subStyle = "color:var(--amber);font-weight:600";
                    }
                    else if (daysToWean <= 7)
                    {
                        sub = $"weans in {daysToWean} d · {weanTarget:dd MMM}";
                        subStyle = "color:var(--amber);font-weight:600";
                    }
                    else
                    {
                        sub = $"weans in {daysToWean} d · {weanTarget:dd MMM}";
                    }
                }
                else
                {
                    sub = "auto-wean off";
                }

                return new BreedingKidGroundRowViewModel
                {
                    Id = g.Id,
                    Tag = g.Tag,
                    IsTemporaryTag = g.IsTemporaryTag,
                    DamTag = damTag,
                    SexDisplay = g.Gender == GoatGender.Male ? "♂ Buckling" : "♀ Doeling",
                    AgeDays = age,
                    BirthKg = birthKg,
                    WeanKg = weanKg,
                    Kg90Text = kg90?.ToString("0.1") ?? (age >= 85 && birthKg.HasValue ? "—" : null),
                    MilkType = g.KidMilkType.ToString(),
                    HealthTag = g.KidHealthTag.ToString(),
                    StatusDisplay = weaned ? "Weaned" : "On milk",
                    StatusCssClass = weaned ? "chip-dry" : "chip-kid",
                    WeanDate = g.WeanDate?.ToString("yyyy-MM-dd"),
                    WeanMethod = g.WeanMethod,
                    StatusSubText = sub,
                    StatusSubStyle = subStyle,
                    IsWeak = weak,
                    WeaningSoon = !weaned && daysToWean <= 7 && daysToWean >= 0,
                    NeedsWeanWeight = weaned && !(weanKg is > 0),
                    OnMilk = !weaned
                };
            })
            .ToList();
    }

    private static string Ordinal(int n) => n switch
    {
        1 => "1st",
        2 => "2nd",
        3 => "3rd",
        _ => $"{n}th"
    };

    private static string EaseLabel(KiddingEase ease) => ease switch
    {
        KiddingEase.Normal => "Easy",
        KiddingEase.Assisted => "Slight help",
        KiddingEase.Difficult => "Hard / vet",
        _ => ease.ToString()
    };
}

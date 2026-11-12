using GoatFarm.Application.ViewModels.Breeding;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;

namespace GoatFarm.Application.Common;

public static class BreedingDoeProfileBuilder
{
    public static BreedingDoeProfileViewModel Build(
        Goat goat,
        IReadOnlyList<BreedingEvent> events,
        IReadOnlyList<KiddingRecord> kiddings,
        int kidsAlive,
        Func<int, string> ageLabelFromDays,
        DateOnly today)
    {
        var flowKey = BreedingDoeFlowHelper.FlowStage(goat, today) ?? "getting_ready";
        var kiddingsHere = kiddings.Count;
        var before = 0;
        var kidsBorn = kiddings.Sum(k => k.LiveKids + k.Stillborn);
        var timesPregnant = events.Count(e =>
            e.Outcome is BreedingOutcome.ConfirmedPregnant or BreedingOutcome.Kidded
            || e.ScanKids is > 0);

        string? dueRange = null;
        var dueLabel = "Not crossed now";
        if (goat.MatedDate is { } md)
        {
            if (goat.KidsCount is > 0)
            {
                var lo = md.AddDays(BreedingHelper.KiddingWindowStart);
                var hi = md.AddDays(BreedingHelper.KiddingWindowEnd);
                dueRange = $"{lo:yyyy-MM-dd} – {hi:yyyy-MM-dd}";
                dueLabel = $"Due · {goat.BuckTag ?? "—"}";
            }
            else
            {
                dueRange = "—";
                dueLabel = $"Crossed · {goat.BuckTag ?? "—"}";
            }
        }

        var ageDays = HerdRules.AgeDays(goat, today);
        var ageText = ageDays is { } days
            ? (BreedingRules.AgeIsEstimated(goat) ? "about " : "") + ageLabelFromDays(days)
            : "—";

        var avatar = new string(goat.Tag.Where(char.IsLetterOrDigit).ToArray());
        if (avatar.Length > 3) avatar = avatar[^3..];
        if (string.IsNullOrEmpty(avatar)) avatar = "?";

        var history = events
            .OrderByDescending(e => e.MatedDate)
            .Select(MapHistoryItem)
            .ToList();

        return new BreedingDoeProfileViewModel
        {
            Id = goat.Id,
            Tag = goat.Tag,
            Name = goat.Name,
            Breed = goat.Breed,
            AgeText = ageText,
            BirthBatchLabel = string.IsNullOrWhiteSpace(goat.BirthBatchName) ? "—" : goat.BirthBatchName,
            FlowStageLabel = BreedingDoeFlowHelper.FlowStageLabel(flowKey),
            FlowStageCssClass = BreedingDoeFlowHelper.FlowStageChipClass(flowKey),
            AvatarText = avatar,
            KiddingsHere = kiddingsHere,
            KiddingsBeforePurchase = before,
            KiddingsTotal = kiddingsHere + before,
            KidsBorn = kidsBorn,
            KidsAlive = kidsAlive,
            TimesPregnant = timesPregnant,
            DueRangeText = dueRange,
            DueStatLabel = dueLabel,
            SireTag = goat.Sire?.Tag ?? goat.SireTagText ?? "—",
            SireId = goat.SireId,
            SirePedigree = PedigreeCaption(goat.Sire, goat.SireTagText),
            DamTag = goat.Dam?.Tag ?? goat.DamTagText ?? "—",
            DamId = goat.DamId,
            DamPedigree = PedigreeCaption(goat.Dam, goat.DamTagText),
            MainHerdKiddingText = kiddingsHere + before == 0 ? "first time" : $"{kiddingsHere + before}× kidded",
            History = history
        };
    }

    private static string? PedigreeCaption(Goat? parent, string? tagText)
    {
        if (parent is null)
            return string.IsNullOrWhiteSpace(tagText) ? null : "recorded tag only";
        var bits = new List<string>();
        if (!string.IsNullOrWhiteSpace(parent.Breed)) bits.Add(parent.Breed);
        if (!string.IsNullOrWhiteSpace(parent.Name)) bits.Add(parent.Name);
        return bits.Count > 0 ? string.Join(" · ", bits) : null;
    }

    private static BreedingDoeHistoryItemViewModel MapHistoryItem(BreedingEvent e)
    {
        var buck = e.Buck?.Tag ?? e.BuckTagText ?? "—";
        var head = $"{buck} · {e.MatedDate:yyyy-MM-dd}";

        return e.Outcome switch
        {
            BreedingOutcome.ConfirmedPregnant => new BreedingDoeHistoryItemViewModel
            {
                Headline = head,
                Detail = DescribePregnant(e),
                CssClass = "breed-hist-preg"
            },
            BreedingOutcome.Kidded => new BreedingDoeHistoryItemViewModel
            {
                Headline = head,
                Detail = e.OutcomeDate.HasValue
                    ? $"Kidded {e.OutcomeDate:yyyy-MM-dd}" + DetailNote(e)
                    : "Kidded" + DetailNote(e),
                CssClass = "breed-hist-ok"
            },
            BreedingOutcome.Empty => new BreedingDoeHistoryItemViewModel
            {
                Headline = head,
                Detail = "Scanned empty" + ScanWhen(e) + DetailNote(e),
                CssClass = "breed-hist-empty"
            },
            BreedingOutcome.Aborted => new BreedingDoeHistoryItemViewModel
            {
                Headline = head,
                Detail = "Lost the kids" + OutcomeWhen(e) + DetailNote(e),
                CssClass = "breed-hist-empty"
            },
            BreedingOutcome.Cancelled => new BreedingDoeHistoryItemViewModel
            {
                Headline = head,
                Detail = "Removed — entered by mistake" + DetailNote(e),
                CssClass = "breed-hist-empty"
            },
            BreedingOutcome.Open when e.ScanKids is > 0 => new BreedingDoeHistoryItemViewModel
            {
                Headline = head,
                Detail = DescribePregnant(e),
                CssClass = "breed-hist-preg"
            },
            BreedingOutcome.Open when e.ScanDate.HasValue => new BreedingDoeHistoryItemViewModel
            {
                Headline = head,
                Detail = "Scanned — awaiting outcome" + ScanWhen(e),
                CssClass = "breed-hist-empty"
            },
            _ => new BreedingDoeHistoryItemViewModel
            {
                Headline = head,
                Detail = e.OutcomeDate.HasValue
                    ? $"Crossed · open {e.OutcomeDate:yyyy-MM-dd}"
                    : "Crossed — waiting for scan",
                CssClass = "breed-hist-wait"
            }
        };
    }

    private static string DescribePregnant(BreedingEvent e)
    {
        var kids = e.ScanKids is > 0 ? BreedingHelper.KidsLabel(e.ScanKids.Value) : "Pregnant";
        return kids + ScanWhen(e) + DetailNote(e);
    }

    private static string ScanWhen(BreedingEvent e) =>
        e.ScanDate.HasValue ? $" ({e.ScanDate:yyyy-MM-dd})" : "";

    private static string OutcomeWhen(BreedingEvent e) =>
        e.OutcomeDate.HasValue ? $" {e.OutcomeDate:yyyy-MM-dd}" : "";

    private static string DetailNote(BreedingEvent e) =>
        string.IsNullOrWhiteSpace(e.Notes) ? "" : " · " + e.Notes;
}

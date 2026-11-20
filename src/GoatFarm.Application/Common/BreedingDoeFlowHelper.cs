using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;

namespace GoatFarm.Application.Common;

/// <summary>V243 <c>statusOf</c> — simplified for the “How a doe moves” strip.</summary>
public static class BreedingDoeFlowHelper
{
    public static readonly IReadOnlyList<(string Key, string Label, string? ArrowAfter)> Steps =
    [
        ("getting_ready", "Getting ready", "heat seen"),
        ("in_heat", "In heat", "with a buck"),
        ("crossed", "Crossed", "buck out"),
        ("waiting_scan", "Waiting scan", "scan: pregnant"),
        ("pregnant", "Pregnant", "dry-off date"),
        ("dry", "Dry", "record kidding"),
        ("kidded", "Kidded", null),
        ("cull_list", "Cull list", null)
    ];

    public static string FlowStageLabel(string key) =>
        Steps.FirstOrDefault(s => s.Key == key).Label ?? key;

    public static string? FlowStage(Goat g, DateOnly today, IReadOnlySet<int>? doesWithBuckIn = null, int kiddingCount = -1)
    {
        if (g.IsArchived || g.Gender != GoatGender.Female || g.Status == GoatStatus.Kid)
            return null;

        if (!string.IsNullOrWhiteSpace(g.Comment) &&
            g.Comment.Contains("Cull list", StringComparison.OrdinalIgnoreCase))
            return "cull_list";

        if (PregnancyRules.IsConfirmedPregnant(g))
            return g.Status == GoatStatus.Dry ? "dry" : "pregnant";

        if (PregnancyRules.IsAwaitingScan(g) && g.MatedDate.HasValue)
        {
            if (doesWithBuckIn is not null && doesWithBuckIn.Contains(g.Id))
                return "crossed";
            return "waiting_scan";
        }

        if (g.HeatEvents.Count > 0)
        {
            var last = g.HeatEvents.Max(h => h.Date);
            var daysAgo = today.DayNumber - last.DayNumber;
            if (daysAgo is >= 0 and <= 2)
                return "in_heat";
        }

        if (g.Status == GoatStatus.Milking && !g.MatedDate.HasValue && !g.PrepCrossDate.HasValue && !g.NextCrossDate.HasValue)
        {
            var kiddedBefore = kiddingCount >= 0
                ? kiddingCount + Math.Max(0, g.KiddingsBeforePurchase)
                : Math.Max(0, g.KiddingsBeforePurchase);
            return kiddedBefore > 0 ? "kidded" : "getting_ready";
        }

        if (g.Status == GoatStatus.Dry && !g.MatedDate.HasValue)
            return "getting_ready";

        if (g.PrepCrossDate.HasValue || g.NextCrossDate.HasValue || !g.MatedDate.HasValue)
            return "getting_ready";

        return "getting_ready";
    }

    public static string FlowStageChipClass(string flowKey) => flowKey switch
    {
        "in_heat" => "chip-buck",
        "crossed" => "chip-preg",
        "waiting_scan" => "chip-milk",
        "pregnant" => "chip-preg",
        "dry" => "chip-dry",
        "kidded" => "chip-kid",
        "cull_list" => "chip-sale",
        _ => "chip-dry"
    };
}

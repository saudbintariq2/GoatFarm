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

    public static string? FlowStage(
        Goat g,
        DateOnly today,
        IReadOnlySet<int>? doesWithBuckIn = null,
        int kiddingCount = -1,
        BreedingEvent? lastCross = null,
        KiddingRecord? lastKidding = null,
        DateOnly? rescanDue = null)
    {
        if (g.IsArchived || g.Gender != GoatGender.Female || g.Status == GoatStatus.Kid)
            return null;

        return DoeStageResolver.Resolve(
            g, lastCross, lastKidding, today, doesWithBuckIn, rescanDue, kiddingCount).StatusKey;
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

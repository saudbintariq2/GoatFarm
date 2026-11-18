namespace GoatFarm.Application.Common;

/// <summary>V16 "Change status" targets on the Does tab.</summary>
public static class BreedingDoeStatusCatalog
{
    public static readonly IReadOnlyList<(string Key, string Label, string Hint)> Statuses =
    [
        ("getting_ready", "Getting ready", "Plan her for a birth group"),
        ("in_heat", "In heat", "Heat seen today"),
        ("crossed", "Crossed", "With a buck"),
        ("waiting_scan", "Waiting scan", "Buck taken out"),
        ("pregnant", "Pregnant", "After ultrasound"),
        ("empty", "Empty", "Scan not pregnant"),
        ("dry", "Dry", "Dried off"),
        ("kidded", "Kidded", "Record kidding"),
        ("lost_kids", "Lost kids", "Aborted / lost pregnancy"),
        ("cull_list", "Cull list", "Plan to remove"),
        ("mistake", "Entered by mistake", "Undo last cross")
    ];

    public static string LabelFor(string key) =>
        Statuses.FirstOrDefault(s => s.Key == key).Label ?? key;

    public static bool IsOddMove(string currentFlow, string target) =>
        target switch
        {
            "dry" => currentFlow is not "pregnant" and not "dry",
            "kidded" => currentFlow is not "pregnant" and not "dry",
            _ => false
        };
}
using GoatFarm.Domain.Enums;

namespace GoatFarm.Application.Common;

public static class DisplayHelper
{
    private static readonly Dictionary<GoatStatus, (string Text, string Css)> StatusMap = new()
    {
        [GoatStatus.Kid] = ("Kid", "chip-kid"),
        [GoatStatus.Pregnant] = ("Pregnant", "chip-preg"),
        [GoatStatus.Milking] = ("Milking", "chip-milk"),
        [GoatStatus.Dry] = ("Dry", "chip-dry"),
        [GoatStatus.Buck] = ("Buck", "chip-buck"),
        [GoatStatus.Sale] = ("For sale", "chip-sale")
    };

    private static readonly Dictionary<VaccineScope, string> ScopeMap = new()
    {
        [VaccineScope.All] = "All goats",
        [VaccineScope.Kid] = "Kids",
        [VaccineScope.Milking] = "Milking",
        [VaccineScope.Pregnant] = "Pregnant",
        [VaccineScope.Dry] = "Dry",
        [VaccineScope.Buck] = "Bucks",
        [VaccineScope.Sale] = "For sale",
        [VaccineScope.None] = "Whole farm"
    };

    public static (string Text, string Css) GetStatusDisplay(GoatStatus status) =>
        StatusMap.TryGetValue(status, out var v) ? v : ("Kid", "chip-kid");

    public static string GetScopeLabel(VaccineScope scope) =>
        ScopeMap.TryGetValue(scope, out var v) ? v : scope.ToString();

    public static string FormatRs(decimal amount) =>
        "Rs " + Math.Round(amount).ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

    public static string StatusKey(GoatStatus status) =>
        status.ToString().ToLowerInvariant();

    public static GoatStatus ParseStatusKey(string key) =>
        Enum.TryParse<GoatStatus>(key, true, out var s) ? s : GoatStatus.Kid;

    public static VaccineScope ParseScopeKey(string key) => key switch
    {
        "all" => VaccineScope.All,
        "kid" => VaccineScope.Kid,
        "milking" => VaccineScope.Milking,
        "pregnant" => VaccineScope.Pregnant,
        "dry" => VaccineScope.Dry,
        "buck" => VaccineScope.Buck,
        "sale" => VaccineScope.Sale,
        "none" => VaccineScope.None,
        _ => VaccineScope.All
    };

    public static string ScopeToKey(VaccineScope scope) => scope switch
    {
        VaccineScope.All => "all",
        VaccineScope.Kid => "kid",
        VaccineScope.Milking => "milking",
        VaccineScope.Pregnant => "pregnant",
        VaccineScope.Dry => "dry",
        VaccineScope.Buck => "buck",
        VaccineScope.Sale => "sale",
        VaccineScope.None => "none",
        _ => "all"
    };
}

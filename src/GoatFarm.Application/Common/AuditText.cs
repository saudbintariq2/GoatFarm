namespace GoatFarm.Application.Common;

/// <summary>Human-readable audit and activity lines (Dashboard D3, Admin audit A2).</summary>
public static class AuditText
{
    private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Finance.AddExpense"] = "Expense added",
        ["Finance.AddEmployee"] = "Employee added",
        ["Finance.RelinkUnpostedMoney"] = "Unposted entries linked to an account",
        ["Vaccine.SaveTreatment"] = "Treatment saved",
        ["Vaccine.MarkDone"] = "Vaccination marked done",
        ["Vaccine.MarkVaccineDone"] = "Vaccination marked done",
        ["Vaccine.GiveVaccineToGroup"] = "Vaccination given",
        ["Feed.AddPurchase"] = "Feed purchased",
        ["Feed.AddFeedPurchase"] = "Feed purchased",
        ["Breeding.RecordBirth"] = "Kidding recorded",
        ["Breeding.RecordKidding"] = "Kidding recorded",
        ["Breeding.StageAction"] = "Doe status changed",
        ["Breeding.ChangeDoeStatus"] = "Doe status changed",
        ["Breeding.SaveScanBatch"] = "Scan results saved",
        ["Breeding.RecordCross"] = "Cross recorded",
        ["Breeding.RecordUltrasound"] = "Scan recorded",
        ["Goat.Create"] = "Goat added",
        ["Milk.AddProduction"] = "Milking recorded",
        ["Milk.AddSale"] = "Milk sale recorded",
        ["Finance.AddIncome"] = "Income recorded",
        ["Goat.RecordSale"] = "Goat sale recorded",
        ["Goat.RecordDeath"] = "Death recorded",
    };

    public static string ToText(string action, string? detail = null)
    {
        var key = action.Trim();
        var text = Map.TryGetValue(key, out var t) ? t
            : key.Contains('.', StringComparison.Ordinal)
                ? key[(key.LastIndexOf('.') + 1)..]
                : key;
        return string.IsNullOrWhiteSpace(detail) ? text : $"{text} – {detail}";
    }

    public static string FormatWhenUtc(DateTime utc, TimeZoneInfo? zone = null)
    {
        zone ??= TimeZoneInfo.Local;
        var local = TimeZoneInfo.ConvertTimeFromUtc(
            utc.Kind == DateTimeKind.Utc ? utc : DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone);
        return local.ToString("yyyy-MM-dd HH:mm");
    }
}
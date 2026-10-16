using System.Text.Json;

namespace GoatFarm.Infrastructure.Services;

/// <summary>
/// Backup file format. Version 2 adds breeding events, kidding records, tasks and a credential-free security section,
/// and requires stable ids so every relationship can be re-linked. Files without <c>formatVersion</c> are legacy
/// (V19 HTML app or pre-V240 MVC exports) and are imported best-effort.
/// </summary>
public static class BackupFormat
{
    public const int CurrentVersion = 3;
    public const string AppMarker = "GoatFarmPro";

    public static readonly string[] RequiredV2Sections = ["goats", "breedingEvents", "kiddingRecords", "farmTasks"];
}

/// <summary>Checks a backup before anything is deleted, so a bad file can never wipe the farm.</summary>
internal static class BackupValidator
{
    private const int MaxErrors = 50;

    public static int? ReadVersion(JsonElement root) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty("formatVersion", out var v) &&
        v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) ? n : null;

    public static IReadOnlyList<string> Validate(JsonElement root)
    {
        var errors = new List<string>();
        void Error(string message)
        {
            if (errors.Count < MaxErrors) errors.Add(message);
        }

        if (root.ValueKind != JsonValueKind.Object)
            return ["The file is not a Goat Records backup."];

        if (root.TryGetProperty("formatVersion", out var versionEl))
        {
            if (versionEl.ValueKind != JsonValueKind.Number || !versionEl.TryGetInt32(out var version) || version < 1)
                return ["formatVersion must be a positive whole number."];
            if (version > BackupFormat.CurrentVersion)
                return [$"This backup uses format version {version}, but this app only supports up to version {BackupFormat.CurrentVersion}. Update the app before restoring it."];
            if (version >= 2)
                return ValidateV2(root, errors, Error);
        }

        if (!root.TryGetProperty("goats", out var legacyGoats) || legacyGoats.ValueKind != JsonValueKind.Array)
            return ["The file has no goats section, so it is not a Goat Records backup."];

        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var g in legacyGoats.EnumerateArray())
        {
            index++;
            var tag = g.ValueKind == JsonValueKind.Object ? (Str(g, "tag") ?? Str(g, "Tag"))?.Trim() : null;
            if (string.IsNullOrEmpty(tag)) Error($"Goat #{index} has no tag.");
            else if (!tags.Add(tag)) Error($"Tag '{tag}' appears more than once.");
        }
        return errors;
    }

    private static IReadOnlyList<string> ValidateV2(JsonElement root, List<string> errors, Action<string> error)
    {
        if (Str(root, "app") != BackupFormat.AppMarker)
            error($"The file is not marked as a {BackupFormat.AppMarker} backup.");
        foreach (var section in BackupFormat.RequiredV2Sections)
        {
            if (!root.TryGetProperty(section, out var s) || s.ValueKind != JsonValueKind.Array)
                error($"Required section '{section}' is missing.");
        }
        if (errors.Count > 0) return errors;

        var goatIds = CollectIds(root, "goats", "Goat", error);
        var eventIds = CollectIds(root, "breedingEvents", "Breeding event", error);
        var kiddingIds = CollectIds(root, "kiddingRecords", "Kidding record", error);
        CollectIds(root, "farmTasks", "Task", error);

        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var g in root.GetProperty("goats").EnumerateArray())
        {
            var id = Int(g, "id");
            var tag = Str(g, "tag")?.Trim();
            if (string.IsNullOrEmpty(tag)) error($"Goat {id} has no tag.");
            else if (!tags.Add(tag)) error($"Tag '{tag}' appears more than once.");
            if (!IsDate(g, "eventDate")) error($"Goat '{tag}' has no valid date.");
            if (g.TryGetProperty("dateOfBirth", out var dob) && dob.ValueKind != JsonValueKind.Null)
            {
                if (!DateOnly.TryParseExact(Str(g, "dateOfBirth"), "yyyy-MM-dd", out var born))
                    error($"Goat '{tag}' has an invalid date of birth.");
                else if (born > DateOnly.FromDateTime(DateTime.Today))
                    error($"Goat '{tag}' has a date of birth in the future.");
            }
            RequireRef(g, "sireId", goatIds, $"Goat '{tag}' sire", error);
            RequireRef(g, "damId", goatIds, $"Goat '{tag}' dam", error);
            RequireRef(g, "kiddingRecordId", kiddingIds, $"Goat '{tag}' kidding record", error);
            if (id is not null && (Int(g, "sireId") == id || Int(g, "damId") == id))
                error($"Goat '{tag}' is listed as its own parent.");
        }

        foreach (var e in root.GetProperty("breedingEvents").EnumerateArray())
        {
            var id = Int(e, "id");
            if (Int(e, "doeId") is null) error($"Breeding event {id} has no doe.");
            RequireRef(e, "doeId", goatIds, $"Breeding event {id} doe", error);
            RequireRef(e, "buckId", goatIds, $"Breeding event {id} buck", error);
            if (!IsDate(e, "matedDate")) error($"Breeding event {id} has no valid mating date.");
        }

        var requestIds = new HashSet<Guid>();
        foreach (var k in root.GetProperty("kiddingRecords").EnumerateArray())
        {
            var id = Int(k, "id");
            if (Int(k, "doeId") is null) error($"Kidding record {id} has no doe.");
            RequireRef(k, "doeId", goatIds, $"Kidding record {id} doe", error);
            RequireRef(k, "breedingEventId", eventIds, $"Kidding record {id} breeding event", error);
            if (!IsDate(k, "kiddingDate")) error($"Kidding record {id} has no valid date.");
            if (!Guid.TryParse(Str(k, "clientRequestId"), out var requestId) || requestId == Guid.Empty)
                error($"Kidding record {id} has no valid request id.");
            else if (!requestIds.Add(requestId))
                error($"Kidding request id {requestId} appears more than once.");
        }

        foreach (var t in root.GetProperty("farmTasks").EnumerateArray())
        {
            var id = Int(t, "id");
            if (string.IsNullOrWhiteSpace(Str(t, "title"))) error($"Task {id} has no title.");
            RequireRef(t, "goatId", goatIds, $"Task {id} goat", error);
        }

        foreach (var section in new[] { "goatWeights", "breedingEmptyLogs", "vaccLog" })
        {
            if (!root.TryGetProperty(section, out var arr) || arr.ValueKind != JsonValueKind.Array) continue;
            foreach (var row in arr.EnumerateArray())
                RequireRef(row, "goatId", goatIds, $"{section} row {Int(row, "id")} goat", error);
        }

        return errors;
    }

    private static HashSet<int> CollectIds(JsonElement root, string section, string label, Action<string> error)
    {
        var ids = new HashSet<int>();
        var index = 0;
        foreach (var row in root.GetProperty(section).EnumerateArray())
        {
            index++;
            if (row.ValueKind != JsonValueKind.Object) { error($"{label} #{index} is not an object."); continue; }
            var id = Int(row, "id");
            if (id is null) error($"{label} #{index} has no id.");
            else if (!ids.Add(id.Value)) error($"{label} id {id} appears more than once.");
        }
        return ids;
    }

    private static void RequireRef(JsonElement row, string property, HashSet<int> targets, string label, Action<string> error)
    {
        if (row.ValueKind != JsonValueKind.Object || !row.TryGetProperty(property, out var el) || el.ValueKind == JsonValueKind.Null)
            return;
        if (el.ValueKind != JsonValueKind.Number || !el.TryGetInt32(out var id) || !targets.Contains(id))
            error($"{label} points to a record that is not in the backup.");
    }

    private static string? Str(JsonElement el, string name) =>
        el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int? Int(JsonElement el, string name) =>
        el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) ? n : null;

    private static bool IsDate(JsonElement el, string name) =>
        DateOnly.TryParseExact(Str(el, name), "yyyy-MM-dd", out _);
}

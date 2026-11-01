using System.Globalization;
using System.Text;
using GoatFarm.Domain.Enums;

namespace GoatFarm.Application.Common;

public sealed class HerdCsvImportRow
{
    public string Tag { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string Breed { get; set; } = "Beetal";
    public GoatGender Gender { get; set; }
    public GoatStatus Status { get; set; }
    public DateOnly EventDate { get; set; }
    public GoatSource Source { get; set; }
    public decimal PurchasePrice { get; set; }
    public string? Owner { get; set; }
    public string? Comment { get; set; }
    public string? DamTag { get; set; }
    public string? SireTag { get; set; }
}

public sealed class HerdCsvImportPreview
{
    public IReadOnlyList<HerdCsvImportRow> Rows { get; init; } = [];
    public IReadOnlyList<string> Errors { get; init; } = [];
}

public static class HerdCsvImporter
{
    private static readonly Dictionary<string, GoatStatus> StatusMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["kid"] = GoatStatus.Kid,
        ["milking"] = GoatStatus.Milking,
        ["milk"] = GoatStatus.Milking,
        ["pregnant"] = GoatStatus.Pregnant,
        ["dry"] = GoatStatus.Dry,
        ["buck"] = GoatStatus.Buck,
        ["male"] = GoatStatus.Buck,
        ["sale"] = GoatStatus.Sale,
        ["for sale"] = GoatStatus.Sale,
        ["meat"] = GoatStatus.Meat,
        ["doe"] = GoatStatus.Dry,
        ["female"] = GoatStatus.Dry,
    };

    public static HerdCsvImportPreview Parse(string csvText, string defaultBreed)
    {
        var rows = ParseCsv(csvText);
        if (rows.Count < 2)
            return new HerdCsvImportPreview { Errors = ["That file has no data rows."] };

        var head = rows[0].Select(h => h.Trim().ToLowerInvariant()).ToList();
        int Col(params string[] names)
        {
            foreach (var n in names)
            {
                var i = head.IndexOf(n);
                if (i >= 0) return i;
            }
            return -1;
        }

        var cTag = Col("tag", "tag / id", "tag id", "id", "rfid");
        if (cTag < 0)
            return new HerdCsvImportPreview { Errors = ["Header row must include a \"tag\" column."] };

        var cName = Col("name");
        var cBreed = Col("breed");
        var cSex = Col("sex", "gender");
        var cStatus = Col("status");
        var cDate = Col("date", "date_in", "dob", "born", "bought");
        var cPrice = Col("price", "cost");
        var cOwner = Col("owner");
        var cDam = Col("dam", "dam tag", "mother");
        var cSire = Col("sire", "sire tag", "buck", "father");
        var cNote = Col("note", "comment");

        var outRows = new List<HerdCsvImportRow>();
        var errs = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 1; i < rows.Count; i++)
        {
            var r = rows[i];
            var line = i + 1;
            string Cell(int c) => c >= 0 && c < r.Count ? r[c].Trim() : string.Empty;

            var tag = Cell(cTag);
            if (string.IsNullOrEmpty(tag))
            {
                errs.Add($"Line {line}: no tag.");
                continue;
            }

            if (!seen.Add(tag))
            {
                errs.Add($"Line {line}: duplicate tag \"{tag}\" in file.");
                continue;
            }

            var sexRaw = Cell(cSex).ToLowerInvariant();
            var gender = sexRaw.StartsWith('m') || sexRaw == "buck" ? GoatGender.Male : GoatGender.Female;
            var stRaw = Cell(cStatus).ToLowerInvariant();
            var status = StatusMap.TryGetValue(stRaw, out var st)
                ? st
                : gender == GoatGender.Male ? GoatStatus.Buck : GoatStatus.Dry;

            var dateRaw = Cell(cDate);
            if (!TryParseDate(dateRaw, out var eventDate))
            {
                errs.Add($"Line {line}: date \"{dateRaw}\" is not a valid date (use YYYY-MM-DD or DD/MM/YYYY).");
                continue;
            }

            var price = ParseDecimal(Cell(cPrice));
            outRows.Add(new HerdCsvImportRow
            {
                Tag = tag,
                Name = NullIfEmpty(Cell(cName)),
                Breed = NullIfEmpty(Cell(cBreed)) ?? defaultBreed,
                Gender = gender,
                Status = status,
                EventDate = eventDate,
                Source = price > 0 ? GoatSource.Bought : GoatSource.Born,
                PurchasePrice = price > 0 ? price : 0,
                Owner = NullIfEmpty(Cell(cOwner)),
                Comment = NullIfEmpty(Cell(cNote)),
                DamTag = NullIfEmpty(Cell(cDam)),
                SireTag = NullIfEmpty(Cell(cSire))
            });
        }

        return new HerdCsvImportPreview { Rows = outRows, Errors = errs };
    }

    private static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static decimal ParseDecimal(string s)
    {
        var cleaned = new string(s.Where(c => char.IsDigit(c) || c == '.').ToArray());
        return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    private static bool TryParseDate(string raw, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(raw))
            return false;
        raw = raw.Trim();
        if (DateOnly.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            return true;
        var m = System.Text.RegularExpressions.Regex.Match(raw, @"^(\d{2})[/\-](\d{2})[/\-](\d{4})$");
        if (!m.Success)
            return false;
        if (!int.TryParse(m.Groups[3].Value, out var y) ||
            !int.TryParse(m.Groups[2].Value, out var mo) ||
            !int.TryParse(m.Groups[1].Value, out var d))
            return false;
        try
        {
            date = new DateOnly(y, mo, d);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static List<List<string>> ParseCsv(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"')
                {
                    cell.Append('"');
                    i++;
                }
                else if (c == '"')
                    inQuotes = false;
                else
                    cell.Append(c);
            }
            else if (c == '"')
                inQuotes = true;
            else if (c == ',')
            {
                row.Add(cell.ToString());
                cell.Clear();
            }
            else if (c == '\n' || c == '\r')
            {
                if (cell.Length > 0 || row.Count > 0)
                {
                    row.Add(cell.ToString());
                    rows.Add(row);
                    row = new List<string>();
                    cell.Clear();
                }
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    i++;
            }
            else
                cell.Append(c);
        }

        if (cell.Length > 0 || row.Count > 0)
        {
            row.Add(cell.ToString());
            rows.Add(row);
        }

        return rows.Where(r => r.Any(x => !string.IsNullOrWhiteSpace(x))).ToList();
    }
}

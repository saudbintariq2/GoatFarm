using System.Globalization;
using System.Text.RegularExpressions;

namespace GoatFarm.Infrastructure.Services;

public static partial class BackupFileNames
{
    public const string Prefix = "goat-records-";

    public static string Build(string label, DateTime savedUtc) =>
        $"{Prefix}{label}-{savedUtc:yyyyMMdd-HHmmss}.json";

    /// <summary>The save time written into the name; file-system dates change when a file is copied or the disk is restored.</summary>
    public static DateTime? TryParseSavedUtc(string fileName)
    {
        var m = StampPattern().Match(fileName);
        return m.Success && DateTime.TryParseExact(m.Groups[1].Value, "yyyyMMdd-HHmmss", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var utc)
            ? utc
            : null;
    }

    public static string LabelOf(string fileName) =>
        fileName.Contains("pre-restore", StringComparison.OrdinalIgnoreCase) ? "pre-restore"
        : fileName.Contains("pre-wipe", StringComparison.OrdinalIgnoreCase) ? "pre-wipe"
        : fileName.Contains("-auto-", StringComparison.OrdinalIgnoreCase) ? "automatic"
        : "stored";

    [GeneratedRegex(@"(\d{8}-\d{6})\.json$", RegexOptions.IgnoreCase)]
    private static partial Regex StampPattern();
}

using System.Text;

namespace GoatFarm.Application.Common;

public static class CsvHelper
{
    public static string Cell(string? value)
    {
        var s = value ?? "";
        if (s.Length > 0 && "=+-@\t\r".Contains(s[0]))
            s = "'" + s;
        if (s.Contains('"') || s.Contains(',') || s.Contains('\n') || s.Contains('\r'))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }

    public static byte[] ToFile(string content) =>
        Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(content)).ToArray();
}

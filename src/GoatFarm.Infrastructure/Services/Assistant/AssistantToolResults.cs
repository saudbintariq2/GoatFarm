using System.Net;
using System.Text.RegularExpressions;
using GoatFarm.Application.ViewModels.Assistant;

namespace GoatFarm.Infrastructure.Services.Assistant;

internal static class AssistantToolResults
{
    private static readonly Regex HtmlTag = new(@"<[^>]+>", RegexOptions.Compiled);

    public static string Plain(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";
        var plain = WebUtility.HtmlDecode(HtmlTag.Replace(value, " "));
        return Regex.Replace(plain, @"\s+", " ").Trim();
    }
    public static IReadOnlyList<AssistantBlock> Denied(string module) =>
    [
        new AssistantBlock
        {
            Kind = AssistantBlockKind.Error,
            Title = "Permission needed",
            Text = $"You do not have permission to view {module}. Ask an admin if you need access.",
            ErrorCode = AssistantErrorCodes.PermissionDenied
        }
    ];

    public static IReadOnlyList<AssistantBlock> InvalidArgs(string text) =>
    [
        new AssistantBlock
        {
            Kind = AssistantBlockKind.Clarify,
            Title = "Need a bit more detail",
            Text = text,
            Clarification = new AssistantClarification { Questions = [text] },
            ErrorCode = AssistantErrorCodes.InvalidArguments
        }
    ];

    public static IReadOnlyList<AssistantBlock> Error(string text, string code) =>
    [
        new AssistantBlock
        {
            Kind = AssistantBlockKind.Error,
            Title = "Could not complete that",
            Text = text,
            ErrorCode = code
        }
    ];

    public static AssistantBlock Text(string title, string text) => new()
    {
        Kind = AssistantBlockKind.Text,
        Title = Plain(title),
        Text = Plain(text)
    };

    public static AssistantBlock Summary(string title, IReadOnlyList<AssistantKpi> items, string? text = null) => new()
    {
        Kind = AssistantBlockKind.Summary,
        Title = Plain(title),
        Text = Plain(text),
        Summary = new AssistantKpiList
        {
            Items = items.Select(i => new AssistantKpi
            {
                Label = Plain(i.Label),
                Value = Plain(i.Value),
                Note = string.IsNullOrWhiteSpace(i.Note) ? null : Plain(i.Note)
            }).ToList()
        }
    };

    public static AssistantBlock Table(string title, IReadOnlyList<string> columns, IReadOnlyList<IReadOnlyList<string>> rows) => new()
    {
        Kind = AssistantBlockKind.Table,
        Title = Plain(title),
        Table = new AssistantTable
        {
            Columns = columns.Select(Plain).ToList(),
            Rows = rows.Take(AssistantLimits.MaxTableRows)
                .Select(row => (IReadOnlyList<string>)row.Select(Plain).ToList())
                .ToList()
        }
    };

    public static AssistantBlock Chart(string title, string chartType, IReadOnlyList<AssistantChartPoint> points) => new()
    {
        Kind = AssistantBlockKind.Chart,
        Title = Plain(title),
        Chart = new AssistantChartData
        {
            ChartType = chartType,
            Points = points.Select(p => new AssistantChartPoint { Label = Plain(p.Label), Value = p.Value }).ToList()
        }
    };

    public static AssistantBlock Navigate(AssistantNavigation navigation) => new()
    {
        Kind = AssistantBlockKind.Navigate,
        Title = navigation.Label,
        Navigation = navigation
    };
}

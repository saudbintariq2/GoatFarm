using System.Text.Json;
using GoatFarm.Application.Assistant;
using GoatFarm.Application.Common;
using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Assistant;
using GoatFarm.Application.ViewModels.Reports;
using GoatFarm.Domain.Constants;

namespace GoatFarm.Infrastructure.Services.Assistant.Tools;

public sealed class GetReportsTool : IAssistantTool
{
    private static readonly HashSet<string> Periods = new(StringComparer.OrdinalIgnoreCase)
    {
        "today", "week", "month", "lastmonth", "3m", "6m", "year", "all", "custom"
    };

    private readonly IReportsService _reportsService;

    public GetReportsTool(IReportsService reportsService) => _reportsService = reportsService;

    public string Name => "get_reports";
    public string Description =>
        "Get the farm reports page for a period (today, week, month, lastmonth, 3m, 6m, year, all, custom). Financial figures are omitted unless the user has Finance view.";
    public string ParametersJsonSchema => """
        {
          "type": "object",
          "properties": {
            "period": { "type": "string", "description": "today, week, month, lastmonth, 3m, 6m, year, all, or custom." },
            "from": { "type": "string", "description": "Custom start yyyy-MM-dd when period is custom." },
            "to": { "type": "string", "description": "Custom end yyyy-MM-dd when period is custom." },
            "groupBy": { "type": "string", "description": "day, week, or month." }
          }
        }
        """;

    public async Task<IReadOnlyList<AssistantBlock>> ExecuteAsync(
        JsonElement arguments,
        AssistantToolContext context,
        CancellationToken cancellationToken)
    {
        if (!await context.Permissions.CanAsync(FarmTabs.Reports, FarmActions.View, cancellationToken))
            return AssistantToolResults.Denied("Reports");

        AssistantJson.TryGetString(arguments, "period", out var period);
        AssistantJson.TryGetString(arguments, "from", out var from);
        AssistantJson.TryGetString(arguments, "to", out var to);
        AssistantJson.TryGetString(arguments, "groupBy", out var groupBy);

        period ??= "month";
        if (!Periods.Contains(period))
            return AssistantToolResults.InvalidArgs("Period must be today, week, month, lastmonth, 3m, 6m, year, all, or custom.");

        if (period.Equals("custom", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
                return AssistantToolResults.InvalidArgs("Enter custom from and to dates as yyyy-MM-dd.");
            if (!AssistantDateRange.TryParseInclusiveRange(from, to, null, context.Today,
                    out _, out _, out _, out var rangeError))
                return AssistantToolResults.InvalidArgs(rangeError ?? "Enter custom from and to dates as yyyy-MM-dd.");
        }

        var page = await _reportsService.GetReportsPageAsync(period, from, to, groupBy, cancellationToken);
        if (!await context.Permissions.CanAsync(FarmTabs.Finance, FarmActions.View, cancellationToken))
            page = FinanceVisibility.WithoutFinance(page);

        var kpis = page.Dashboard.Kpis
            .Concat(page.Dashboard.Ratios)
            .Select(ToKpi)
            .ToList();

        var blocks = new List<AssistantBlock>
        {
            AssistantToolResults.Summary(
                page.FinanceHidden ? $"Reports for {page.RangeLabel} (finance hidden)" : $"Reports for {page.RangeLabel}",
                kpis,
                page.FinanceHidden ? FinanceVisibility.HiddenNote : "")
        };

        if (!page.FinanceHidden && page.Finance.ExpenseCategories.Count > 0)
        {
            blocks.Add(AssistantToolResults.Table("Expense categories", ["Category", "Amount"],
                page.Finance.ExpenseCategories
                    .Take(AssistantLimits.MaxTableRows)
                    .Select(c => (IReadOnlyList<string>)[c.Name, DisplayHelper.FormatRs(c.Amount)])
                    .ToList()));
        }

        if (!page.FinanceHidden && page.Dashboard.TrendRows.Count > 0)
        {
            blocks.Add(AssistantToolResults.Chart("Income vs expense", "bar",
                page.Dashboard.TrendRows
                    .Select(r => new AssistantChartPoint { Label = r.Key, Value = r.Net })
                    .Take(12)
                    .ToList()));
        }

        if (AssistantNavigationCatalog.TryBuild("reports", null, period, from, to, groupBy, out var nav, out _))
            blocks.Add(AssistantToolResults.Navigate(nav));

        return blocks;
    }

    private static AssistantKpi ToKpi(ReportKpiViewModel k) => new()
    {
        Label = AssistantToolResults.Plain(k.Label),
        Value = AssistantToolResults.Plain(k.Value),
        Note = string.IsNullOrWhiteSpace(k.Sub) ? null : AssistantToolResults.Plain(k.Sub)
    };
}

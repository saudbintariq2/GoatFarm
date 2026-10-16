using System.Text.Json;
using GoatFarm.Application.Assistant;
using GoatFarm.Application.Common;
using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Assistant;
using GoatFarm.Domain.Constants;

namespace GoatFarm.Infrastructure.Services.Assistant.Tools;

public sealed class GetDashboardTool : IAssistantTool
{
    private readonly IStatisticsService _statisticsService;

    public GetDashboardTool(IStatisticsService statisticsService) => _statisticsService = statisticsService;

    public string Name => "get_dashboard";
    public string Description =>
        "Get the farm dashboard snapshot for a calendar month. Financial totals are omitted unless the user has Finance view.";
    public string ParametersJsonSchema => """
        {
          "type": "object",
          "properties": {
            "month": {
              "type": "string",
              "description": "Calendar month as yyyy-MM, lastmonth, or this month."
            }
          }
        }
        """;

    public async Task<IReadOnlyList<AssistantBlock>> ExecuteAsync(
        JsonElement arguments,
        AssistantToolContext context,
        CancellationToken cancellationToken)
    {
        if (!await context.Permissions.CanAsync(FarmTabs.Dashboard, FarmActions.View, cancellationToken))
            return AssistantToolResults.Denied("Dashboard");

        AssistantJson.TryGetString(arguments, "month", out var monthArg);
        if (!AssistantDateRange.TryParseMonth(monthArg, context.Today, out var monthKey, out var error))
            return AssistantToolResults.InvalidArgs(error ?? "Enter a valid month.");

        var dashboard = await _statisticsService.GetDashboardAsync(monthKey, cancellationToken);
        if (!await context.Permissions.CanAsync(FarmTabs.Finance, FarmActions.View, cancellationToken))
            dashboard = FinanceVisibility.WithoutFinance(dashboard);

        var items = new List<AssistantKpi>
        {
            new() { Label = "Herd", Value = dashboard.HerdStats.Total.ToString() },
            new() { Label = "Milk produced", Value = $"{dashboard.LitersProduced:0.##} L" },
            new() { Label = "Milk sold", Value = $"{dashboard.LitersSold:0.##} L" },
            new() { Label = "Vaccines due", Value = dashboard.DueNowCount.ToString() }
        };

        if (!dashboard.FinanceHidden)
        {
            items.Add(new AssistantKpi { Label = "Income", Value = DisplayHelper.FormatRs(dashboard.TotalIncome) });
            items.Add(new AssistantKpi { Label = "Expenses", Value = DisplayHelper.FormatRs(dashboard.TotalExpense) });
            items.Add(new AssistantKpi
            {
                Label = dashboard.IsLoss ? "Loss" : "Profit",
                Value = DisplayHelper.FormatRs(Math.Abs(dashboard.Profit))
            });
        }

        var blocks = new List<AssistantBlock>
        {
            AssistantToolResults.Summary(
                dashboard.FinanceHidden ? $"Dashboard {dashboard.Month} (finance hidden)" : $"Dashboard {dashboard.Month}",
                items,
                dashboard.FinanceHidden ? FinanceVisibility.HiddenNote : "")
        };

        if (dashboard.MilkLast7Days.Count > 0)
        {
            blocks.Add(AssistantToolResults.Chart("Milk last 7 days", "bar",
                dashboard.MilkLast7Days
                    .Select(d => new AssistantChartPoint { Label = d.Label, Value = d.Liters })
                    .ToList()));
        }

        if (AssistantNavigationCatalog.TryBuild("dashboard", null, null, null, null, null, out var nav, out _))
            blocks.Add(AssistantToolResults.Navigate(nav));

        return blocks;
    }
}

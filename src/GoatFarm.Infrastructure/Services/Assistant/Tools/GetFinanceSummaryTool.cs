using System.Text.Json;
using GoatFarm.Application.Assistant;
using GoatFarm.Application.Common;
using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Assistant;
using GoatFarm.Domain.Constants;

namespace GoatFarm.Infrastructure.Services.Assistant.Tools;

public sealed class GetFinanceSummaryTool : IAssistantTool
{
    private readonly IFinanceService _financeService;

    public GetFinanceSummaryTool(IFinanceService financeService) => _financeService = financeService;

    public string Name => "get_finance_summary";
    public string Description =>
        "Get the monthly profit and loss summary: income, expenses, profit, and how the expense total is composed. Requires Finance view. Month as yyyy-MM or lastmonth.";
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
        if (!await context.Permissions.CanAsync(FarmTabs.Finance, FarmActions.View, cancellationToken))
            return AssistantToolResults.Denied("Finance");

        AssistantJson.TryGetString(arguments, "month", out var monthArg);
        if (!AssistantDateRange.TryParseMonth(monthArg, context.Today, out var monthKey, out var error))
            return AssistantToolResults.InvalidArgs(error ?? "Enter a valid month.");

        var page = await _financeService.GetFinancePageAsync(monthKey, cancellationToken);
        var composition =
            "Total expenses include manual expense entries plus amounts the app already counts automatically: "
            + $"feed bought ({DisplayHelper.FormatRs(page.FeedBoughtMonthly)}), "
            + $"medicine ({DisplayHelper.FormatRs(page.MedicineMonthly)}), "
            + $"staff salary ({DisplayHelper.FormatRs(page.StaffSalaryMonthly)}), "
            + $"recurring costs ({DisplayHelper.FormatRs(page.RecurringMonthly)}), "
            + $"and vaccine purchases ({DisplayHelper.FormatRs(page.VaccineBoughtMonthly)}). "
            + $"Manual expenses are {DisplayHelper.FormatRs(page.ManualExpenseMonthly)}. "
            + "Income includes milk sales plus other income entries.";

        AssistantNavigationCatalog.TryBuild("finance", monthKey, null, null, null, null, out var nav, out _);
        return
        [
            AssistantToolResults.Summary($"Profit and loss for {page.Month}",
            [
                new AssistantKpi { Label = "Income", Value = DisplayHelper.FormatRs(page.TotalIncome) },
                new AssistantKpi { Label = "Expenses", Value = DisplayHelper.FormatRs(page.TotalExpense) },
                new AssistantKpi
                {
                    Label = page.IsLoss ? "Loss" : "Profit",
                    Value = DisplayHelper.FormatRs(Math.Abs(page.Profit)),
                    Note = page.ProfitNote
                },
                new AssistantKpi { Label = "Capital", Value = DisplayHelper.FormatRs(page.Capital) }
            ], composition),
            AssistantToolResults.Chart("Expense composition", "bar",
            [
                new AssistantChartPoint { Label = "Manual", Value = page.ManualExpenseMonthly },
                new AssistantChartPoint { Label = "Feed bought", Value = page.FeedBoughtMonthly },
                new AssistantChartPoint { Label = "Medicine", Value = page.MedicineMonthly },
                new AssistantChartPoint { Label = "Staff", Value = page.StaffSalaryMonthly },
                new AssistantChartPoint { Label = "Recurring", Value = page.RecurringMonthly },
                new AssistantChartPoint { Label = "Vaccines", Value = page.VaccineBoughtMonthly }
            ]),
            AssistantToolResults.Navigate(nav)
        ];
    }
}

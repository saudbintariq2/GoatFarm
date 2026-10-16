using System.Text.Json;
using GoatFarm.Application.Assistant;
using GoatFarm.Application.Common;
using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Assistant;
using GoatFarm.Domain.Constants;

namespace GoatFarm.Infrastructure.Services.Assistant.Tools;

public sealed class GetExpensesTool : IAssistantTool
{
    private readonly IFinanceService _financeService;

    public GetExpensesTool(IFinanceService financeService) => _financeService = financeService;

    public string Name => "get_expenses";
    public string Description =>
        "List manual expense entries for a calendar month and the automatically included expense categories. Requires Finance view.";
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
        var rows = page.Expenses
            .Take(AssistantLimits.MaxTableRows)
            .Select(e => (IReadOnlyList<string>)[e.DateDisplay, e.Type, DisplayHelper.FormatRs(e.Amount), e.Comment ?? ""])
            .ToList();

        var autoRows = new List<IReadOnlyList<string>>
        {
            new[] { "Auto", "Feed bought", DisplayHelper.FormatRs(page.FeedBoughtMonthly), "From feed purchases" },
            new[] { "Auto", "Medicine", DisplayHelper.FormatRs(page.MedicineMonthly), "From the feed plan" },
            new[] { "Auto", "Staff salary", DisplayHelper.FormatRs(page.StaffSalaryMonthly), "From the employees list" },
            new[] { "Auto", "Recurring costs", DisplayHelper.FormatRs(page.RecurringMonthly), "Monthly equivalent" },
            new[] { "Auto", "Vaccine purchases", DisplayHelper.FormatRs(page.VaccineBoughtMonthly), "From health purchases" }
        };

        AssistantNavigationCatalog.TryBuild("finance", monthKey, null, null, null, null, out var nav, out _);
        var blocks = new List<AssistantBlock>
        {
            AssistantToolResults.Summary($"Expenses for {page.Month}",
            [
                new AssistantKpi { Label = "Total expenses", Value = DisplayHelper.FormatRs(page.TotalExpense) },
                new AssistantKpi { Label = "Manual entries", Value = DisplayHelper.FormatRs(page.ManualExpenseMonthly) }
            ], "Total expenses include the manual rows plus the automatic categories listed below.")
        };

        if (rows.Count > 0)
            blocks.Add(AssistantToolResults.Table("Manual expenses", ["Date", "Type", "Amount", "Comment"], rows));
        else
            blocks.Add(AssistantToolResults.Text("No manual expenses", $"There are no manual expense entries in {page.Month}."));

        blocks.Add(AssistantToolResults.Table("Automatically included", ["Source", "Category", "Amount", "Note"], autoRows));
        blocks.Add(AssistantToolResults.Navigate(nav));
        return blocks;
    }
}

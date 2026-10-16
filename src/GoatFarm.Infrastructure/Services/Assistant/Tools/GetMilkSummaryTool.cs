using System.Text.Json;
using GoatFarm.Application.Assistant;
using GoatFarm.Application.Common;
using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Assistant;
using GoatFarm.Domain.Constants;

namespace GoatFarm.Infrastructure.Services.Assistant.Tools;

public sealed class GetMilkSummaryTool : IAssistantTool
{
    private readonly IMilkService _milkService;

    public GetMilkSummaryTool(IMilkService milkService) => _milkService = milkService;

    public string Name => "get_milk_summary";
    public string Description =>
        "Get milk production, sales, waste and milk income totals for a calendar month. Use month as yyyy-MM, lastmonth, or this month.";
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
        if (!await context.Permissions.CanAsync(FarmTabs.Milk, FarmActions.View, cancellationToken))
            return AssistantToolResults.Denied("Milk");

        AssistantJson.TryGetString(arguments, "month", out var monthArg);
        if (!AssistantDateRange.TryParseMonth(monthArg, context.Today, out var monthKey, out var error))
            return AssistantToolResults.InvalidArgs(error ?? "Enter a valid month.");

        if (!AssistantDateRange.TryParseInclusiveRange(null, null, monthKey, context.Today,
                out var from, out var to, out _, out var rangeError))
            return AssistantToolResults.InvalidArgs(rangeError ?? "Enter a valid month.");

        var range = await _milkService.QueryRangeAsync(from, to, 1, cancellationToken);
        var empty = range.ProductionCount == 0 && range.LitersSold == 0 && range.LitersWasted == 0;
        AssistantNavigationCatalog.TryBuild("milk", null, null, null, null, null, out var nav, out _);
        return
        [
            AssistantToolResults.Summary($"Milk summary for {monthKey}",
            [
                new AssistantKpi { Label = "Produced", Value = $"{range.LitersProduced:0.##} L" },
                new AssistantKpi { Label = "Sold", Value = $"{range.LitersSold:0.##} L" },
                new AssistantKpi { Label = "Wasted", Value = $"{range.LitersWasted:0.##} L" },
                new AssistantKpi { Label = "Left", Value = $"{range.LitersProduced - range.LitersSold - range.LitersWasted:0.##} L" },
                new AssistantKpi { Label = "Milk income", Value = DisplayHelper.FormatRs(range.MilkIncome) }
            ], empty ? $"No milk records were found for {monthKey}." : ""),
            AssistantToolResults.Navigate(nav)
        ];
    }
}

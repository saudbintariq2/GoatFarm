using System.Text.Json;
using GoatFarm.Application.Assistant;
using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Assistant;
using GoatFarm.Domain.Constants;

namespace GoatFarm.Infrastructure.Services.Assistant.Tools;

public sealed class GetMilkRecordsTool : IAssistantTool
{
    private readonly IMilkService _milkService;

    public GetMilkRecordsTool(IMilkService milkService) => _milkService = milkService;

    public string Name => "get_milk_records";
    public string Description =>
        "List milk production records and totals for an inclusive date range. Prefer from/to as yyyy-MM-dd, or month as yyyy-MM or lastmonth.";
    public string ParametersJsonSchema => """
        {
          "type": "object",
          "properties": {
            "from": { "type": "string", "description": "Start date yyyy-MM-dd, inclusive." },
            "to": { "type": "string", "description": "End date yyyy-MM-dd, inclusive." },
            "month": { "type": "string", "description": "yyyy-MM, lastmonth, or this month when from/to are omitted." }
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

        AssistantJson.TryGetString(arguments, "from", out var fromText);
        AssistantJson.TryGetString(arguments, "to", out var toText);
        AssistantJson.TryGetString(arguments, "month", out var month);
        if (!AssistantDateRange.TryParseInclusiveRange(fromText, toText, month, context.Today,
                out var from, out var to, out var label, out var error))
            return AssistantToolResults.InvalidArgs(error ?? "Enter a valid date range.");

        var range = await _milkService.QueryRangeAsync(from, to, AssistantLimits.MaxMilkRows, cancellationToken);
        var rows = range.Productions
            .Select(p => (IReadOnlyList<string>)[p.DateDisplay, p.Breed, p.Liters.ToString("0.##"), p.Comment ?? ""])
            .ToList();

        var blocks = new List<AssistantBlock>
        {
            AssistantToolResults.Summary($"Milk production {label}",
            [
                new AssistantKpi { Label = "Produced", Value = $"{range.LitersProduced:0.##} L" },
                new AssistantKpi { Label = "Sold", Value = $"{range.LitersSold:0.##} L" },
                new AssistantKpi { Label = "Wasted", Value = $"{range.LitersWasted:0.##} L" },
                new AssistantKpi { Label = "Records", Value = range.ProductionCount.ToString() }
            ], range.ProductionCount > range.Productions.Count
                ? $"Showing the {range.Productions.Count} most recent of {range.ProductionCount} production records."
                : "")
        };

        if (rows.Count > 0)
            blocks.Add(AssistantToolResults.Table("Production records", ["Date", "Breed", "Litres", "Comment"], rows));
        else
            blocks.Add(AssistantToolResults.Text("No production records", $"There are no milk production records for {label}."));

        if (AssistantNavigationCatalog.TryBuild("milk", null, null, null, null, null, out var nav, out _))
            blocks.Add(AssistantToolResults.Navigate(nav));

        return blocks;
    }
}

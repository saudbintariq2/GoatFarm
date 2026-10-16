using System.Text.Json;
using GoatFarm.Application.Assistant;
using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Assistant;
using GoatFarm.Domain.Constants;

namespace GoatFarm.Infrastructure.Services.Assistant.Tools;

public sealed class ResolveNavigationTool : IAssistantTool
{
    public string Name => "resolve_navigation";
    public string Description =>
        "Build a validated in-app path for dashboard, milk, finance, reports, herd, breeding, feed, health, tasks, or search. Never pass a URL.";
    public string ParametersJsonSchema => """
        {
          "type": "object",
          "properties": {
            "destination": {
              "type": "string",
              "description": "Page name: dashboard, milk, finance, reports, herd, breeding, feed, health, tasks, or search."
            },
            "month": { "type": "string", "description": "yyyy-MM for the Finance page." },
            "period": { "type": "string", "description": "Reports period." },
            "from": { "type": "string", "description": "Reports custom from yyyy-MM-dd." },
            "to": { "type": "string", "description": "Reports custom to yyyy-MM-dd." },
            "groupBy": { "type": "string", "description": "Reports groupBy: day, week, or month." }
          },
          "required": ["destination"]
        }
        """;

    public async Task<IReadOnlyList<AssistantBlock>> ExecuteAsync(
        JsonElement arguments,
        AssistantToolContext context,
        CancellationToken cancellationToken)
    {
        if (AssistantJson.HasProperty(arguments, "url")
            || AssistantJson.HasProperty(arguments, "href")
            || AssistantJson.HasProperty(arguments, "path"))
            return AssistantToolResults.Error(
                "Arbitrary URLs are not allowed. Use a page name such as milk or finance.",
                AssistantErrorCodes.InvalidArguments);

        AssistantJson.TryGetString(arguments, "destination", out var destination);
        AssistantJson.TryGetString(arguments, "month", out var month);
        AssistantJson.TryGetString(arguments, "period", out var period);
        AssistantJson.TryGetString(arguments, "from", out var from);
        AssistantJson.TryGetString(arguments, "to", out var to);
        AssistantJson.TryGetString(arguments, "groupBy", out var groupBy);

        if (!string.IsNullOrWhiteSpace(month)
            && AssistantDateRange.TryParseMonth(month, context.Today, out var monthKey, out _))
            month = monthKey;

        if (!AssistantNavigationCatalog.TryBuild(destination, month, period, from, to, groupBy, out var nav, out var error))
            return AssistantToolResults.InvalidArgs(error ?? "That page is not available.");

        var destKey = destination!.Trim();
        if (!AssistantNavigationCatalog.Destinations.TryGetValue(destKey, out var tab)
            || !await context.Permissions.CanAsync(tab, FarmActions.View, cancellationToken))
            return AssistantToolResults.Denied(destKey);

        return [AssistantToolResults.Navigate(nav)];
    }
}

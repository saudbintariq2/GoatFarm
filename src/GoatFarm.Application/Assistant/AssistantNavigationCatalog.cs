using System.Text.RegularExpressions;
using GoatFarm.Application.ViewModels.Assistant;
using GoatFarm.Domain.Constants;

namespace GoatFarm.Application.Assistant;

public static class AssistantNavigationCatalog
{
    private static readonly Regex MonthKeyRegex = new(@"^\d{4}-(0[1-9]|1[0-2])$", RegexOptions.Compiled);
    private static readonly Regex DateRegex = new(@"^\d{4}-\d{2}-\d{2}$", RegexOptions.Compiled);

    private static readonly HashSet<string> Periods = new(StringComparer.OrdinalIgnoreCase)
    {
        "today", "week", "month", "lastmonth", "3m", "6m", "year", "all", "custom"
    };

    private static readonly HashSet<string> GroupBys = new(StringComparer.OrdinalIgnoreCase)
    {
        "day", "week", "month"
    };

    public static readonly IReadOnlyDictionary<string, string> Destinations =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["dashboard"] = FarmTabs.Dashboard,
            ["milk"] = FarmTabs.Products,
            ["products"] = FarmTabs.Products,
            ["finance"] = FarmTabs.Finance,
            ["reports"] = FarmTabs.Reports,
            ["herd"] = FarmTabs.Herd,
            ["breeding"] = FarmTabs.Breeding,
            ["feed"] = FarmTabs.Feed,
            ["health"] = FarmTabs.Vaccines,
            ["vaccines"] = FarmTabs.Vaccines,
            ["tasks"] = FarmTabs.Tasks,
            ["search"] = FarmTabs.Search
        };

    public static bool TryBuild(
        string? destination,
        string? month,
        string? period,
        string? from,
        string? to,
        string? groupBy,
        out AssistantNavigation navigation,
        out string? error)
    {
        navigation = new AssistantNavigation();
        error = null;

        if (string.IsNullOrWhiteSpace(destination))
        {
            error = "Choose a page: dashboard, milk, products, finance, or reports.";
            return false;
        }

        var key = destination.Trim();
        if (key.Contains('/') || key.Contains('?') || key.Contains('\\')
            || key.Contains(':') || key.Contains('#') || key.Contains(' '))
        {
            error = "Use a page name such as milk or finance. Arbitrary URLs are not allowed.";
            return false;
        }

        if (!Destinations.TryGetValue(key, out _))
        {
            error = "That page is not available to the assistant.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(month) && !MonthKeyRegex.IsMatch(month.Trim()))
        {
            error = "Month must be yyyy-MM.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(period) && !Periods.Contains(period.Trim()))
        {
            error = "Period must be today, week, month, lastmonth, 3m, 6m, year, all, or custom.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(from) && !DateRegex.IsMatch(from.Trim()))
        {
            error = "from must be yyyy-MM-dd.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(to) && !DateRegex.IsMatch(to.Trim()))
        {
            error = "to must be yyyy-MM-dd.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(groupBy) && !GroupBys.Contains(groupBy.Trim()))
        {
            error = "groupBy must be day, week, or month.";
            return false;
        }

        var path = key.ToLowerInvariant() switch
        {
            "dashboard" => "/",
            "milk" => "/Milk",
            "products" => BuildQuery("/Products", ("month", month)),
            "finance" => BuildQuery("/Finance", ("month", month)),
            "reports" => BuildQuery("/Reports", ("period", period), ("from", from), ("to", to), ("groupBy", groupBy)),
            "herd" => "/Goat",
            "breeding" => "/Breeding",
            "feed" => "/Feed",
            "health" or "vaccines" => "/Vaccine",
            "tasks" => "/Tasks",
            "search" => "/Search",
            _ => ""
        };

        if (string.IsNullOrEmpty(path) || !path.StartsWith('/'))
        {
            error = "That page is not available to the assistant.";
            return false;
        }

        navigation = new AssistantNavigation
        {
            Label = key.ToLowerInvariant() switch
            {
                "dashboard" => "Open Dashboard",
                "milk" => "Open Milk ledger",
                "products" => "Open Products",
                "finance" => "Open Accounting",
                "reports" => "Open Reports",
                "herd" => "Open Herd",
                "breeding" => "Open Breeding",
                "feed" => "Open Feed",
                "health" or "vaccines" => "Open Health",
                "tasks" => "Open Tasks",
                "search" => "Open Search",
                _ => "Open page"
            },
            Path = path
        };
        return true;
    }

    private static string BuildQuery(string path, params (string Key, string? Value)[] pairs)
    {
        var parts = pairs
            .Where(p => !string.IsNullOrWhiteSpace(p.Value))
            .Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value!.Trim())}")
            .ToList();
        return parts.Count == 0 ? path : path + "?" + string.Join("&", parts);
    }
}

using System.Reflection;
using GoatFarm.Domain.Constants;
using GoatFarm.Web.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoatFarm.Tests;

public class PermissionMapTests
{
    private static IEnumerable<(string Controller, string Action, bool Anonymous)> AllActions()
    {
        var controllers = typeof(FarmPermissionMap).Assembly.GetTypes()
            .Where(t => typeof(Controller).IsAssignableFrom(t) && !t.IsAbstract);
        foreach (var type in controllers)
        {
            var controller = type.Name[..^"Controller".Length];
            var anonymousController = type.IsDefined(typeof(AllowAnonymousAttribute), true);
            var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName && !m.IsDefined(typeof(NonActionAttribute), true));
            foreach (var m in methods)
            {
                var name = m.GetCustomAttribute<ActionNameAttribute>()?.Name ?? m.Name;
                yield return (controller, name, anonymousController || m.IsDefined(typeof(AllowAnonymousAttribute), true));
            }
        }
    }

    [Fact]
    public void EveryAuthenticatedActionHasAnExplicitPermission()
    {
        Assert.Contains(AllActions(), a => a.Controller == "Tasks" && a.Action == "SetStatus");
        Assert.True(AllActions().Count() > 50);
        var unmapped = AllActions()
            .Where(a => !a.Anonymous)
            .Where(a => !(a.Action == "Index" && FarmPermissionMap.IndexTabs.ContainsKey(a.Controller)))
            .Where(a => !FarmPermissionMap.ActionPermissions.ContainsKey($"{a.Controller}.{a.Action}"))
            .Where(a => !FarmPermissionMap.OpenControllers.Contains(a.Controller))
            .Select(a => $"{a.Controller}.{a.Action}")
            .Distinct()
            .ToList();

        Assert.True(unmapped.Count == 0, "Unmapped actions (denied at runtime): " + string.Join(", ", unmapped));
    }

    [Fact]
    public void PermissionMapHasNoStaleEntries()
    {
        var actions = AllActions().Select(a => $"{a.Controller}.{a.Action}").ToHashSet(StringComparer.OrdinalIgnoreCase);
        var stale = FarmPermissionMap.ActionPermissions.Keys.Where(k => !actions.Contains(k)).ToList();
        Assert.True(stale.Count == 0, "Mapped but no such action: " + string.Join(", ", stale));
    }

    [Fact]
    public void BackupImportAndExportRequireSettingsPermission()
    {
        Assert.Equal((FarmTabs.Settings, FarmActions.Edit), FarmPermissionMap.ActionPermissions["Dashboard.Import"]);
        Assert.Equal((FarmTabs.Settings, FarmActions.View), FarmPermissionMap.ActionPermissions["Dashboard.Export"]);
    }
}

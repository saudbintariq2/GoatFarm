using GoatFarm.Application.ViewModels.Settings;
using GoatFarm.Domain.Constants;

namespace GoatFarm.Web.Authorization;

public sealed record AdminActor(string UserId, string? Role, IReadOnlyDictionary<string, TabPermissionViewModel> Permissions)
{
    public bool IsAdmin => Role == FarmRoles.Admin;
}

/// <summary>
/// Settings-tab permission lets a user reach the user-admin endpoints; these rules stop a non-Admin holder of
/// that permission from turning it into more access than they already have. Each method returns null when allowed,
/// otherwise the message to send back with a 403.
/// </summary>
public static class UserAdminGuard
{
    public static string? CanManageUser(AdminActor actor, string targetUserId, string? targetRole)
    {
        if (actor.IsAdmin) return null;
        if (string.Equals(actor.UserId, targetUserId, StringComparison.Ordinal))
            return "You cannot change your own account, role or permissions. Ask an administrator.";
        if (targetRole == FarmRoles.Admin)
            return "Only an administrator can change an administrator account.";
        return null;
    }

    public static string? CanAssignRole(AdminActor actor, string role, IReadOnlyDictionary<string, TabPermissionViewModel>? rolePermissions)
    {
        if (actor.IsAdmin) return null;
        if (role == FarmRoles.Admin)
            return "Only an administrator can give someone the Admin role.";
        return CanGrant(actor, rolePermissions ?? new Dictionary<string, TabPermissionViewModel>());
    }

    public static string? CanGrant(AdminActor actor, IReadOnlyDictionary<string, TabPermissionViewModel> requested)
    {
        if (actor.IsAdmin) return null;
        var missing = new List<string>();
        foreach (var (tab, perm) in requested)
        {
            if (perm is null) continue;
            actor.Permissions.TryGetValue(tab, out var own);
            foreach (var (action, wanted) in Flags(perm))
                if (wanted && own?.Allows(action) != true)
                    missing.Add($"{tab} {action}");
        }
        return missing.Count == 0
            ? null
            : "You cannot give permissions you do not have yourself: " + string.Join(", ", missing.Take(10)) + ".";
    }

    public static string? CanChangeFarmWideSecurity(AdminActor actor) =>
        actor.IsAdmin ? null : "Only an administrator can change role permissions or the password policy.";

    private static IEnumerable<(string Action, bool Wanted)> Flags(TabPermissionViewModel p)
    {
        yield return (FarmActions.View, p.View);
        yield return (FarmActions.Add, p.Add);
        yield return (FarmActions.Edit, p.Edit);
        yield return (FarmActions.Delete, p.Delete);
    }
}

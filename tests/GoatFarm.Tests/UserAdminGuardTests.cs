using GoatFarm.Application.ViewModels.Settings;
using GoatFarm.Domain.Constants;
using GoatFarm.Web.Authorization;

namespace GoatFarm.Tests;

public class UserAdminGuardTests
{
    private static readonly TabPermissionViewModel ViewOnly = new() { View = true };

    private static AdminActor Manager(params (string Tab, TabPermissionViewModel Perm)[] perms) =>
        new("mgr-1", FarmRoles.Manager, perms.ToDictionary(p => p.Tab, p => p.Perm));

    private static readonly AdminActor Admin = new("admin-1", FarmRoles.Admin, new Dictionary<string, TabPermissionViewModel>());

    private static readonly AdminActor ManagerWithSettings = Manager(
        (FarmTabs.Settings, TabPermissionViewModel.FullAccess),
        (FarmTabs.Herd, TabPermissionViewModel.FullAccess),
        (FarmTabs.Reports, ViewOnly));

    [Fact]
    public void Admin_IsNeverBlocked()
    {
        Assert.Null(UserAdminGuard.CanManageUser(Admin, "admin-1", FarmRoles.Admin));
        Assert.Null(UserAdminGuard.CanManageUser(Admin, "other", FarmRoles.Admin));
        Assert.Null(UserAdminGuard.CanAssignRole(Admin, FarmRoles.Admin, null));
        Assert.Null(UserAdminGuard.CanGrant(Admin, new Dictionary<string, TabPermissionViewModel> { [FarmTabs.Finance] = TabPermissionViewModel.FullAccess }));
        Assert.Null(UserAdminGuard.CanChangeFarmWideSecurity(Admin));
    }

    [Fact]
    public void NonAdmin_CannotChangeTheirOwnAccount()
    {
        Assert.NotNull(UserAdminGuard.CanManageUser(ManagerWithSettings, "mgr-1", FarmRoles.Manager));
    }

    [Fact]
    public void NonAdmin_CannotTouchAnAdministrator()
    {
        Assert.NotNull(UserAdminGuard.CanManageUser(ManagerWithSettings, "admin-1", FarmRoles.Admin));
    }

    [Fact]
    public void NonAdmin_CanManageOtherNonAdminUsers()
    {
        Assert.Null(UserAdminGuard.CanManageUser(ManagerWithSettings, "staff-1", FarmRoles.Staff));
        Assert.Null(UserAdminGuard.CanManageUser(ManagerWithSettings, "missing", null));
    }

    [Fact]
    public void NonAdmin_CannotAssignAdminRole()
    {
        var msg = UserAdminGuard.CanAssignRole(ManagerWithSettings, FarmRoles.Admin, null);
        Assert.NotNull(msg);
        Assert.Contains("Admin role", msg);
    }

    [Fact]
    public void NonAdmin_CannotAssignRoleThatCarriesMoreThanTheyHold()
    {
        var staffRoleWithFinance = new Dictionary<string, TabPermissionViewModel>
        {
            [FarmTabs.Herd] = ViewOnly,
            [FarmTabs.Finance] = ViewOnly
        };
        var msg = UserAdminGuard.CanAssignRole(ManagerWithSettings, FarmRoles.Staff, staffRoleWithFinance);
        Assert.NotNull(msg);
        Assert.Contains($"{FarmTabs.Finance} view", msg);

        var staffRoleWithinManager = new Dictionary<string, TabPermissionViewModel> { [FarmTabs.Herd] = TabPermissionViewModel.FullAccess };
        Assert.Null(UserAdminGuard.CanAssignRole(ManagerWithSettings, FarmRoles.Staff, staffRoleWithinManager));
    }

    [Fact]
    public void NonAdmin_CannotGrantPermissionsTheyDoNotHold()
    {
        var reportsExport = new Dictionary<string, TabPermissionViewModel>
        {
            [FarmTabs.Reports] = new() { View = true, Edit = true }
        };
        var msg = UserAdminGuard.CanGrant(ManagerWithSettings, reportsExport);
        Assert.NotNull(msg);
        Assert.Contains($"{FarmTabs.Reports} edit", msg);
        Assert.DoesNotContain($"{FarmTabs.Reports} view", msg);
    }

    [Fact]
    public void NonAdmin_CanGrantSubsetOfTheirOwnPermissions()
    {
        var subset = new Dictionary<string, TabPermissionViewModel>
        {
            [FarmTabs.Herd] = new() { View = true, Add = true },
            [FarmTabs.Reports] = ViewOnly,
            [FarmTabs.Finance] = TabPermissionViewModel.NoAccess
        };
        Assert.Null(UserAdminGuard.CanGrant(ManagerWithSettings, subset));
    }

    [Fact]
    public void NonAdmin_FlagsWithoutViewStillCountAsGrants()
    {
        var sneaky = new Dictionary<string, TabPermissionViewModel> { [FarmTabs.Finance] = new() { Delete = true } };
        Assert.NotNull(UserAdminGuard.CanGrant(ManagerWithSettings, sneaky));
    }

    [Fact]
    public void NonAdmin_CannotChangeRolePermissionsOrPasswordPolicy()
    {
        Assert.NotNull(UserAdminGuard.CanChangeFarmWideSecurity(ManagerWithSettings));
        Assert.NotNull(UserAdminGuard.CanChangeFarmWideSecurity(new AdminActor("x", null, new Dictionary<string, TabPermissionViewModel>())));
    }
}

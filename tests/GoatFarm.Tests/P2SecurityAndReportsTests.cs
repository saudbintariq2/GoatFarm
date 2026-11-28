using GoatFarm.Application.ViewModels.Settings;
using GoatFarm.Domain.Constants;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Tests;

public class P2SecurityAndReportsTests
{
    [Fact]
    public void StaffDefaults_HaveNoDelete_AndNoFinanceTrading()
    {
        var perms = UserSettingsService.DefaultRolePermissions()[FarmRoles.Staff];
        Assert.All(perms.Values, p => Assert.False(p.Delete));
        Assert.False(perms[FarmTabs.Finance].View);
        Assert.False(perms[FarmTabs.Trading].View);
        Assert.False(perms[FarmTabs.Projects].View);
        Assert.False(perms[FarmTabs.Settings].View);
        Assert.False(perms[FarmTabs.Backup].View);
        Assert.False(perms[FarmTabs.Admin].View);
        Assert.True(perms[FarmTabs.Herd].View);
        Assert.True(perms[FarmTabs.Herd].Add);
        Assert.True(perms[FarmTabs.Herd].Edit);
    }

    [Fact]
    public void PasswordPolicy_MatchesA4()
    {
        var p = UserSettingsService.DefaultPasswordPolicy();
        Assert.Equal(10, p.RequiredLength);
        Assert.True(p.RequireDigit);
        Assert.True(p.RequireLowercase);
        Assert.True(p.RequireUppercase);
        Assert.True(p.RequireNonAlphanumeric);
    }

    [Fact]
    public void ViewAddEdit_NeverAllowsDelete()
    {
        Assert.False(TabPermissionViewModel.ViewAddEdit.Allows(FarmActions.Delete));
        Assert.True(TabPermissionViewModel.ViewAddEdit.Allows(FarmActions.Edit));
    }
}

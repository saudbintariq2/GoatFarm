using GoatFarm.Domain.Constants;
using GoatFarm.Infrastructure.Services;
using GoatFarm.Web.Authorization;

namespace GoatFarm.Tests.Assistant;

public class AssistantPermissionTests
{
    [Fact]
    public void DefaultRoles_StaffGetsAssistantView_ButNotFinance()
    {
        var roles = UserSettingsService.DefaultRolePermissions();
        Assert.True(roles[FarmRoles.Admin][FarmTabs.Assistant].View);
        Assert.True(roles[FarmRoles.Manager][FarmTabs.Assistant].View);
        Assert.True(roles[FarmRoles.Staff][FarmTabs.Assistant].View);
        Assert.False(roles[FarmRoles.Staff][FarmTabs.Assistant].Add);
        Assert.False(roles[FarmRoles.Staff][FarmTabs.Finance].View);
        Assert.True(roles[FarmRoles.Staff][FarmTabs.Products].View);
        Assert.Equal(FarmTabs.Products, FarmPermissionMap.IndexTabs["Milk"]);
        Assert.Equal(FarmTabs.Products, FarmPermissionMap.ActionPermissions["Milk.GetData"].Tab);
    }

    [Fact]
    public void AssistantChat_IsMappedToAssistantView()
    {
        Assert.Equal((FarmTabs.Assistant, FarmActions.View), FarmPermissionMap.ActionPermissions["Assistant.Chat"]);
    }
}

using System.Text.Json;
using GoatFarm.Domain.Constants;
using GoatFarm.Domain.Entities;
using GoatFarm.Infrastructure.Persistence;
using GoatFarm.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Infrastructure.Persistence;

public static class SettingsSeedHelper
{
    public static async Task SeedDefaultsAsync(GoatFarmDbContext context, CancellationToken cancellationToken = default)
    {
        if (!await context.AppSettings.AnyAsync(s => s.Key == "PasswordPolicy", cancellationToken))
        {
            context.AppSettings.Add(new AppSetting
            {
                Key = "PasswordPolicy",
                Value = JsonSerializer.Serialize(UserSettingsService.DefaultPasswordPolicy(), UserSettingsService.SerializerOptions)
            });
        }

        var rolePermissionsSetting = await context.AppSettings
            .FirstOrDefaultAsync(s => s.Key == "RolePermissions", cancellationToken);

        if (rolePermissionsSetting is null)
        {
            context.AppSettings.Add(new AppSetting
            {
                Key = "RolePermissions",
                Value = JsonSerializer.Serialize(UserSettingsService.DefaultRolePermissions(), UserSettingsService.SerializerOptions)
            });
        }
        else if (!HasValidRolePermissions(rolePermissionsSetting.Value))
        {
            rolePermissionsSetting.Value = JsonSerializer.Serialize(
                UserSettingsService.DefaultRolePermissions(),
                UserSettingsService.SerializerOptions);
            rolePermissionsSetting.UpdatedDate = DateTime.UtcNow;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static bool HasValidRolePermissions(string json)
    {
        var parsed = UserSettingsService.ParseRolePermissions(json);
        if (!parsed.Permissions.TryGetValue(FarmRoles.Admin, out var adminTabs))
            return false;

        return adminTabs.TryGetValue(FarmTabs.Dashboard, out var dashboard) && dashboard.View;
    }
}

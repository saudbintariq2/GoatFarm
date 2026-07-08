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
                Value = System.Text.Json.JsonSerializer.Serialize(UserSettingsService.DefaultPasswordPolicy())
            });
        }

        if (!await context.AppSettings.AnyAsync(s => s.Key == "RolePermissions", cancellationToken))
        {
            context.AppSettings.Add(new AppSetting
            {
                Key = "RolePermissions",
                Value = System.Text.Json.JsonSerializer.Serialize(UserSettingsService.DefaultRolePermissions())
            });
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}

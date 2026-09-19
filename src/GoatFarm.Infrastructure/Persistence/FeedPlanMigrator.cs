using System.Text.Json;
using GoatFarm.Application.Common;
using GoatFarm.Domain.Constants;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Infrastructure.Persistence;

public static class FeedPlanMigrator
{
    public static async Task MigrateAsync(GoatFarmDbContext context, CancellationToken cancellationToken = default)
    {
        if (!await context.AppSettings.AnyAsync(s => s.Key == AppSettingKeys.MixRecipe, cancellationToken))
        {
            context.AppSettings.Add(new AppSetting
            {
                Key = AppSettingKeys.MixRecipe,
                Value = JsonSerializer.Serialize(MixRecipeHelper.DefaultRecipe)
            });
        }

        if (!await context.AppSettings.AnyAsync(s => s.Key == AppSettingKeys.MixRecipes, cancellationToken))
        {
            var perStatus = MixRecipeHelper.DefaultRecipesPerStatus.ToDictionary(
                p => DisplayHelper.StatusKey(p.Key),
                p => p.Value);
            context.AppSettings.Add(new AppSetting
            {
                Key = AppSettingKeys.MixRecipes,
                Value = JsonSerializer.Serialize(perStatus)
            });
        }

        if (!await context.AppSettings.AnyAsync(s => s.Key == AppSettingKeys.FodderPool, cancellationToken))
        {
            context.AppSettings.Add(new AppSetting
            {
                Key = AppSettingKeys.FodderPool,
                Value = JsonSerializer.Serialize(FodderPoolHelper.DefaultPool())
            });
        }

        if (!await context.AppSettings.AnyAsync(s => s.Key == AppSettingKeys.FeedSettings, cancellationToken))
        {
            context.AppSettings.Add(new AppSetting
            {
                Key = AppSettingKeys.FeedSettings,
                Value = JsonSerializer.Serialize(new FeedSettingsDto())
            });
        }

        var plans = await context.FeedPlans.Include(p => p.Items).ToListAsync(cancellationToken);
        foreach (var plan in plans)
        {
            if (plan.MixKgPerDay > 0 || plan.FodderKgPerDay > 0 || plan.FodderDryKgPerDay > 0) continue;

            if (MixRecipeHelper.DefaultPlans.TryGetValue(plan.StatusKey, out var defaults))
            {
                plan.MixKgPerDay = defaults.Mix;
                plan.FodderKgPerDay = defaults.Fodder;
                plan.FodderDryKgPerDay = defaults.FodderDry;
                if (plan.MedicineCostPerGoatPerMonth == 0)
                    plan.MedicineCostPerGoatPerMonth = defaults.Med;
            }
            else
            {
                var fodderGrams = plan.Items.FirstOrDefault(i =>
                    string.Equals(i.FeedType, FeedTypes.Fodder, StringComparison.OrdinalIgnoreCase))?.GramsPerDay ?? 0;
                var mixGrams = plan.Items
                    .Where(i => !string.Equals(i.FeedType, FeedTypes.Fodder, StringComparison.OrdinalIgnoreCase))
                    .Sum(i => i.GramsPerDay);
                plan.FodderKgPerDay = fodderGrams / 1000m;
                plan.MixKgPerDay = mixGrams / 1000m;
            }
            plan.UpdatedDate = DateTime.UtcNow;
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}

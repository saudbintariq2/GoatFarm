using GoatFarm.Application.Common;
using GoatFarm.Domain.Constants;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Infrastructure.Persistence;

public static class DbSeeder
{
    public static async Task SeedAsync(GoatFarmDbContext context, CancellationToken cancellationToken = default)
    {
        if (await context.FeedPrices.AnyAsync(cancellationToken))
            return;

        foreach (var (key, name) in FeedTypes.All)
        {
            context.FeedPrices.Add(new FeedPrice
            {
                FeedType = key,
                DisplayName = name,
                PricePerKg = 0,
                StockKg = 0
            });
        }

        var planData = new Dictionary<GoatStatus, (int med, Dictionary<string, int> rations)>
        {
            [GoatStatus.Kid] = (50, new() { ["wanda"] = 150, ["fodder"] = 500 }),
            [GoatStatus.Milking] = (80, new() { ["wanda"] = 500, ["binola"] = 200, ["bran"] = 200, ["maize"] = 100, ["sheera"] = 50, ["fodder"] = 2000 }),
            [GoatStatus.Pregnant] = (150, new() { ["wanda"] = 400, ["binola"] = 150, ["bran"] = 150, ["maize"] = 100, ["sheera"] = 50, ["fodder"] = 1500 }),
            [GoatStatus.Dry] = (40, new() { ["wanda"] = 200, ["bran"] = 100, ["fodder"] = 1500 }),
            [GoatStatus.Buck] = (60, new() { ["wanda"] = 400, ["binola"] = 100, ["bran"] = 100, ["maize"] = 100, ["fodder"] = 1500 }),
            [GoatStatus.Sale] = (30, new() { ["wanda"] = 250, ["bran"] = 100, ["fodder"] = 1500 })
        };

        foreach (var (status, data) in planData)
        {
            var defaults = MixRecipeHelper.DefaultPlans[status];
            var plan = new FeedPlan
            {
                StatusKey = status,
                MixKgPerDay = defaults.Mix,
                FodderKgPerDay = defaults.Fodder,
                FodderDryKgPerDay = defaults.FodderDry,
                MedicineCostPerGoatPerMonth = data.med
            };
            foreach (var f in FeedTypes.All)
            {
                plan.Items.Add(new FeedPlanItem
                {
                    FeedType = f.Key,
                    GramsPerDay = data.rations.GetValueOrDefault(f.Key, 0)
                });
            }
            context.FeedPlans.Add(plan);
        }

        if (!await context.AppSettings.AnyAsync(s => s.Key == AppSettingKeys.RemindDays, cancellationToken))
        {
            context.AppSettings.Add(new AppSetting { Key = AppSettingKeys.RemindDays, Value = "30" });
        }

        if (!await context.AppSettings.AnyAsync(s => s.Key == AppSettingKeys.MixRecipe, cancellationToken))
        {
            context.AppSettings.Add(new AppSetting
            {
                Key = AppSettingKeys.MixRecipe,
                Value = System.Text.Json.JsonSerializer.Serialize(MixRecipeHelper.DefaultRecipe)
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
                Value = System.Text.Json.JsonSerializer.Serialize(perStatus)
            });
        }

        if (!await context.AppSettings.AnyAsync(s => s.Key == AppSettingKeys.FodderPool, cancellationToken))
        {
            context.AppSettings.Add(new AppSetting
            {
                Key = AppSettingKeys.FodderPool,
                Value = System.Text.Json.JsonSerializer.Serialize(FodderPoolHelper.DefaultPool())
            });
        }

        if (!await context.AppSettings.AnyAsync(s => s.Key == AppSettingKeys.FeedSettings, cancellationToken))
        {
            context.AppSettings.Add(new AppSetting
            {
                Key = AppSettingKeys.FeedSettings,
                Value = System.Text.Json.JsonSerializer.Serialize(new FeedSettingsDto())
            });
        }

        context.Vaccines.AddRange(
            new Vaccine { Name = "Enterotoxaemia (ET)", Scope = VaccineScope.Kid, RuleType = VaccineRuleType.Age, Days = 30 },
            new Vaccine { Name = "PPR", Scope = VaccineScope.All, RuleType = VaccineRuleType.Repeat, Months = 12 },
            new Vaccine { Name = "FMD", Scope = VaccineScope.All, RuleType = VaccineRuleType.Repeat, Months = 6 },
            new Vaccine { Name = "CCPP", Scope = VaccineScope.All, RuleType = VaccineRuleType.Repeat, Months = 12 },
            new Vaccine { Name = "ET booster (pre-kidding)", Scope = VaccineScope.Pregnant, RuleType = VaccineRuleType.Repeat, Months = 6 },
            new Vaccine { Name = "Deworming", Scope = VaccineScope.All, RuleType = VaccineRuleType.Repeat, Months = 3 }
        );

        await context.SaveChangesAsync(cancellationToken);
    }
}

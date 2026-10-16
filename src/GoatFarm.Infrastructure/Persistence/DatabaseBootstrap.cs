using GoatFarm.Application.Interfaces;
using GoatFarm.Domain.Constants;
using GoatFarm.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GoatFarm.Infrastructure.Persistence;

public static class DatabaseBootstrap
{
    /// <param name="isDevelopment">Destructive seed options are honoured only in Development.</param>
    public static async Task InitializeAsync(IServiceProvider services, bool isDevelopment = false, CancellationToken cancellationToken = default)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseBootstrap");

        try
        {
            using var scope = services.CreateScope();
            var provider = scope.ServiceProvider;
            var context = provider.GetRequiredService<GoatFarmDbContext>();

            logger.LogInformation("Applying database migrations...");
            await context.Database.MigrateAsync(cancellationToken);

            var configuration = provider.GetRequiredService<IConfiguration>();
            var clearFarmData = string.Equals(configuration["Seed:ClearFarmData"], "true", StringComparison.OrdinalIgnoreCase);
            if (clearFarmData && !isDevelopment)
            {
                logger.LogError("Seed:ClearFarmData=true was ignored: wiping farm data is only allowed in the Development environment. Remove the setting.");
            }
            else if (clearFarmData)
            {
                logger.LogWarning("Clearing all farm data (Seed:ClearFarmData=true)...");
                await DbDataCleaner.ClearAllFarmDataAsync(context, cancellationToken);
            }

            logger.LogInformation("Seeding default configuration (if not already present)...");
            await DbSeeder.SeedAsync(context, cancellationToken);
            await FeedPlanMigrator.MigrateAsync(context, cancellationToken);

            var roleManager = provider.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (var role in FarmRoles.All)
            {
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));
            }

            var resetAdminPassword = string.Equals(configuration["Seed:ResetAdminPassword"], "true", StringComparison.OrdinalIgnoreCase);
            if (resetAdminPassword)
                logger.LogWarning("Seed:ResetAdminPassword=true — the seeded admin password will be reset. Turn this off after recovery.");

            var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
            await IdentitySeedHelper.SeedAdminUserAsync(
                userManager,
                resetAdminPassword: resetAdminPassword,
                logger: logger,
                cancellationToken: cancellationToken);

            if (isDevelopment &&
                string.Equals(configuration["Seed:EnsureDevUser"], "true", StringComparison.OrdinalIgnoreCase))
            {
                await IdentitySeedHelper.SeedDevUserAsync(userManager, resetPassword: true, logger: logger, cancellationToken: cancellationToken);
            }

            await SettingsSeedHelper.SeedDefaultsAsync(context, cancellationToken);
            await provider.GetRequiredService<ILookupService>().EnsureDefaultsAsync(cancellationToken);

            if (isDevelopment &&
                string.Equals(configuration["Seed:DemoData"], "true", StringComparison.OrdinalIgnoreCase))
            {
                var reloadDemo = string.Equals(configuration["Seed:ReloadDemoData"], "true", StringComparison.OrdinalIgnoreCase);
                if (reloadDemo)
                    logger.LogWarning("Reloading demo/sample farm data (Seed:ReloadDemoData=true)...");
                else
                    logger.LogInformation("Seeding demo data for empty modules (Seed:DemoData=true)...");
                await DemoDataSeedHelper.SeedAsync(context, reloadDemo, cancellationToken);
            }

            logger.LogInformation("Database initialization completed.");
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Database initialization failed during application startup.");
            throw;
        }
    }
}

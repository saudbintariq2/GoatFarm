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
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseBootstrap");

        try
        {
            using var scope = services.CreateScope();
            var provider = scope.ServiceProvider;
            var context = provider.GetRequiredService<GoatFarmDbContext>();

            logger.LogInformation("Applying database migrations...");
            await context.Database.MigrateAsync(cancellationToken);

            logger.LogInformation("Seeding demo data (if database is empty)...");
            await DbSeeder.SeedAsync(context, cancellationToken);

            var roleManager = provider.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (var role in FarmRoles.All)
            {
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));
            }

            var configuration = provider.GetRequiredService<IConfiguration>();
            var resetAdminPassword = !string.Equals(configuration["Seed:ResetAdminPassword"], "false", StringComparison.OrdinalIgnoreCase);

            await IdentitySeedHelper.SeedAdminUserAsync(
                provider.GetRequiredService<UserManager<ApplicationUser>>(),
                resetAdminPassword: resetAdminPassword,
                logger: logger,
                cancellationToken: cancellationToken);
            await SettingsSeedHelper.SeedDefaultsAsync(context, cancellationToken);

            logger.LogInformation("Database initialization completed.");
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Database initialization failed during application startup.");
            throw;
        }
    }
}

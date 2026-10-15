using GoatFarm.Domain.Constants;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace GoatFarm.Infrastructure.Identity;

public static class IdentitySeedHelper
{
    public const string DefaultAdminEmail = "admin@goatfarm.local";
    public const string DefaultAdminPassword = "Admin123!";

    /// <summary>Secondary local-dev administrator (Development seed only).</summary>
    public const string DevUserEmail = "dev@goatfarm.local";
    public const string DevUserPassword = "DevFarm123!";

    public static async Task SeedAdminUserAsync(
        UserManager<ApplicationUser> userManager,
        bool resetAdminPassword = false,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(DefaultAdminEmail);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = DefaultAdminEmail,
                Email = DefaultAdminEmail,
                FullName = "Administrator",
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(user, DefaultAdminPassword);
            if (!result.Succeeded)
            {
                logger?.LogError(
                    "Failed to create admin user {Email}: {Errors}",
                    DefaultAdminEmail,
                    string.Join("; ", result.Errors.Select(e => e.Description)));
                return;
            }

            logger?.LogInformation("Created admin user {Email}.", DefaultAdminEmail);
        }

        if (!await userManager.IsInRoleAsync(user, FarmRoles.Admin))
        {
            await userManager.AddToRoleAsync(user, FarmRoles.Admin);
            logger?.LogInformation("Assigned Admin role to {Email}.", DefaultAdminEmail);
        }

        var passwordValid = await userManager.CheckPasswordAsync(user, DefaultAdminPassword);
        if (!passwordValid && resetAdminPassword)
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var reset = await userManager.ResetPasswordAsync(user, token, DefaultAdminPassword);
            if (reset.Succeeded)
                logger?.LogInformation("Reset admin password for {Email} to the default seed password.", DefaultAdminEmail);
            else
                logger?.LogError(
                    "Failed to reset admin password: {Errors}",
                    string.Join("; ", reset.Errors.Select(e => e.Description)));
        }
    }

    public static async Task SeedDevUserAsync(
        UserManager<ApplicationUser> userManager,
        bool resetPassword = true,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(DevUserEmail);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = DevUserEmail,
                Email = DevUserEmail,
                FullName = "Dev Administrator",
                EmailConfirmed = true,
                UsesRolePermissions = true
            };

            var result = await userManager.CreateAsync(user, DevUserPassword);
            if (!result.Succeeded)
            {
                logger?.LogError(
                    "Failed to create dev user {Email}: {Errors}",
                    DevUserEmail,
                    string.Join("; ", result.Errors.Select(e => e.Description)));
                return;
            }

            logger?.LogInformation("Created dev user {Email}.", DevUserEmail);
        }

        if (!await userManager.IsInRoleAsync(user, FarmRoles.Admin))
        {
            await userManager.AddToRoleAsync(user, FarmRoles.Admin);
            logger?.LogInformation("Assigned Admin role to {Email}.", DevUserEmail);
        }

        user.UsesRolePermissions = true;
        user.PermissionsJson = null;
        await userManager.UpdateAsync(user);

        if (resetPassword)
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var reset = await userManager.ResetPasswordAsync(user, token, DevUserPassword);
            if (reset.Succeeded)
                logger?.LogInformation("Set dev user password for {Email}.", DevUserEmail);
        }
    }
}

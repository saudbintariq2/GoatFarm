using GoatFarm.Domain.Constants;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GoatFarm.Infrastructure.Identity;

public static class IdentitySeedHelper
{
    /// <summary>Local-dev administrator. Created only in the Development environment, never on a live server.</summary>
    public const string DevAdminEmail = "admin@goatfarm.local";
    public const string DevAdminPassword = "Admin123!";

    /// <summary>Secondary local-dev administrator (Development seed only).</summary>
    public const string DevUserEmail = "dev@goatfarm.local";
    public const string DevUserPassword = "DevFarm123!";

    public const string AdminEmailKey = "ADMIN_EMAIL";
    public const string AdminPasswordKey = "ADMIN_PASSWORD";

    /// <summary>
    /// Live servers: the owner admin comes only from ADMIN_EMAIL / ADMIN_PASSWORD. With no users at all and no
    /// variables set, startup stops with a clear message instead of creating a guessable account. A leftover
    /// placeholder admin@goatfarm.local is locked out as soon as another admin exists.
    /// Development: the local placeholder admin is created as before.
    /// </summary>
    public static async Task SeedAdminAsync(
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        bool isDevelopment,
        bool resetAdminPassword = false,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        var email = configuration[AdminEmailKey]?.Trim();
        var password = configuration[AdminPasswordKey];
        var configured = !string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(password);

        if (configured)
            await EnsureAdminAsync(userManager, email!, password!, "Administrator", resetAdminPassword, logger);
        else if (isDevelopment)
            await EnsureAdminAsync(userManager, DevAdminEmail, DevAdminPassword, "Administrator", resetAdminPassword, logger);
        else if (!await userManager.Users.AnyAsync(cancellationToken))
            throw new InvalidOperationException(
                $"No users exist. Set {AdminEmailKey} and {AdminPasswordKey} in the server (Railway) variables to create the owner admin.");

        if (isDevelopment) return;
        if (configured)
            await LockPlaceholderAdminAsync(userManager, email!, logger);
        else if (await userManager.FindByEmailAsync(DevAdminEmail) is not null)
            logger?.LogCritical(
                "The placeholder admin {Email} exists on this server. Set {EmailKey} and {PasswordKey} and restart: the owner admin is created and the placeholder is locked.",
                DevAdminEmail, AdminEmailKey, AdminPasswordKey);
    }

    private static async Task EnsureAdminAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string password,
        string fullName,
        bool resetPassword,
        ILogger? logger)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = fullName,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
                throw new InvalidOperationException(
                    $"Could not create admin user {email}: {string.Join("; ", result.Errors.Select(e => e.Description))}");

            logger?.LogInformation("Created admin user {Email}.", email);
        }

        if (!await userManager.IsInRoleAsync(user, FarmRoles.Admin))
        {
            await userManager.AddToRoleAsync(user, FarmRoles.Admin);
            logger?.LogInformation("Assigned Admin role to {Email}.", email);
        }

        if (resetPassword && !await userManager.CheckPasswordAsync(user, password))
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var reset = await userManager.ResetPasswordAsync(user, token, password);
            if (reset.Succeeded)
                logger?.LogInformation("Reset admin password for {Email}.", email);
            else
                logger?.LogError(
                    "Failed to reset admin password: {Errors}",
                    string.Join("; ", reset.Errors.Select(e => e.Description)));
        }
    }

    private static async Task LockPlaceholderAdminAsync(UserManager<ApplicationUser> userManager, string ownerEmail, ILogger? logger)
    {
        if (string.Equals(ownerEmail, DevAdminEmail, StringComparison.OrdinalIgnoreCase)) return;
        var placeholder = await userManager.FindByEmailAsync(DevAdminEmail);
        if (placeholder is null) return;

        if (placeholder.LockoutEnd is { } end && end >= DateTimeOffset.MaxValue.AddYears(-1)) return;
        await userManager.SetLockoutEnabledAsync(placeholder, true);
        await userManager.SetLockoutEndDateAsync(placeholder, DateTimeOffset.MaxValue);
        await userManager.UpdateSecurityStampAsync(placeholder);
        logger?.LogWarning("Locked the placeholder admin {Email}; sign in with the {EmailKey} account.", DevAdminEmail, AdminEmailKey);
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

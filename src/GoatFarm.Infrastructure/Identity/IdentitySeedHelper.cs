using GoatFarm.Domain.Constants;
using GoatFarm.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace GoatFarm.Infrastructure.Identity;

public static class IdentitySeedHelper
{
    public const string DefaultAdminEmail = "admin@goatfarm.local";
    public const string DefaultAdminPassword = "Admin123!";

    public static async Task SeedAdminUserAsync(UserManager<ApplicationUser> userManager)
    {
        if (await userManager.FindByEmailAsync(DefaultAdminEmail) is not null)
            return;

        var user = new ApplicationUser
        {
            UserName = DefaultAdminEmail,
            Email = DefaultAdminEmail,
            FullName = "Administrator",
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user, DefaultAdminPassword);
        if (result.Succeeded)
            await userManager.AddToRoleAsync(user, FarmRoles.Admin);
    }
}

using Microsoft.AspNetCore.Identity;

namespace GoatFarm.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public string? FullName { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>When true, permissions come from the user's role. When false, <see cref="PermissionsJson"/> is used.</summary>
    public bool UsesRolePermissions { get; set; } = true;

    /// <summary>JSON map of tab key → { view, add, edit, delete } for this user.</summary>
    public string? PermissionsJson { get; set; }
}

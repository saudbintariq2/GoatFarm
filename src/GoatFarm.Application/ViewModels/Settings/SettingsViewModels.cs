using System.ComponentModel.DataAnnotations;
using GoatFarm.Domain.Constants;

namespace GoatFarm.Application.ViewModels.Settings;

public class UserViewModel
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsLocked { get; set; }
    public bool UsesRolePermissions { get; set; } = true;
}

public class CreateUserViewModel
{
    [Required]
    [StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(6)]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = string.Empty;
}

public class UpdateUserViewModel
{
    [Required]
    [StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = string.Empty;

    public bool IsLocked { get; set; }
}

public class ResetPasswordViewModel
{
    [Required]
    [MinLength(6)]
    public string NewPassword { get; set; } = string.Empty;
}

public class PasswordPolicyViewModel
{
    public int RequiredLength { get; set; } = 8;
    public bool RequireDigit { get; set; } = true;
    public bool RequireLowercase { get; set; } = true;
    public bool RequireUppercase { get; set; }
    public bool RequireNonAlphanumeric { get; set; }
}

public class TabPermissionViewModel
{
    public bool View { get; set; }
    public bool Add { get; set; }
    public bool Edit { get; set; }
    public bool Delete { get; set; }

    public bool Allows(string action) => action switch
    {
        FarmActions.View => View,
        FarmActions.Add => View && Add,
        FarmActions.Edit => View && Edit,
        FarmActions.Delete => View && Delete,
        _ => false
    };

    public static TabPermissionViewModel FullAccess => new() { View = true, Add = true, Edit = true, Delete = true };
    public static TabPermissionViewModel NoAccess => new();
}

public class RolePermissionsViewModel
{
    public Dictionary<string, Dictionary<string, TabPermissionViewModel>> Permissions { get; set; } = new();
}

public class UserPermissionsViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool UsesRolePermissions { get; set; } = true;
    public Dictionary<string, TabPermissionViewModel> Permissions { get; set; } = new();
}

public class SaveUserPermissionsViewModel
{
    public bool UsesRolePermissions { get; set; } = true;
    public Dictionary<string, TabPermissionViewModel> Permissions { get; set; } = new();
}

public class SettingsPageViewModel
{
    public IReadOnlyList<UserViewModel> Users { get; set; } = [];
    public IReadOnlyList<string> Roles { get; set; } = [];
    public PasswordPolicyViewModel PasswordPolicy { get; set; } = new();
    public RolePermissionsViewModel RolePermissions { get; set; } = new();
    public IReadOnlyList<(string Key, string Label)> Tabs { get; set; } = [];
}

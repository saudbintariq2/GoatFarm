using System.Text.Json;
using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Settings;
using GoatFarm.Domain.Constants;
using GoatFarm.Domain.Entities;
using GoatFarm.Infrastructure.Identity;
using GoatFarm.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Infrastructure.Services;

public class UserSettingsService : IUserSettingsService
{
    private const string PasswordPolicyKey = "PasswordPolicy";
    private const string RolePermissionsKey = "RolePermissions";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly GoatFarmDbContext _context;

    public UserSettingsService(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        GoatFarmDbContext context)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
    }

    public async Task<SettingsPageViewModel> GetSettingsPageAsync(CancellationToken cancellationToken = default) =>
        new()
        {
            Users = await GetUsersAsync(cancellationToken),
            Roles = FarmRoles.All.ToList(),
            PasswordPolicy = await GetPasswordPolicyAsync(cancellationToken),
            RolePermissions = await GetRolePermissionsAsync(cancellationToken),
            Tabs = FarmTabs.All.ToList()
        };

    public async Task<IReadOnlyList<UserViewModel>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = await _userManager.Users.OrderBy(u => u.Email).ToListAsync(cancellationToken);
        var result = new List<UserViewModel>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            result.Add(MapUser(user, roles.FirstOrDefault() ?? FarmRoles.Staff));
        }
        return result;
    }

    public async Task<UserViewModel> CreateUserAsync(CreateUserViewModel model, CancellationToken cancellationToken = default)
    {
        if (!FarmRoles.All.Contains(model.Role))
            throw new InvalidOperationException("Invalid role selected.");

        var policy = await GetPasswordPolicyAsync(cancellationToken);
        var errors = ValidatePassword(model.Password, policy).ToList();
        if (errors.Count > 0)
            throw new InvalidOperationException(string.Join(" ", errors));

        var email = model.Email.Trim().ToLowerInvariant();
        if (await _userManager.FindByEmailAsync(email) is not null)
            throw new InvalidOperationException("A user with this email already exists.");

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = model.FullName.Trim(),
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(e => e.Description)));

        await _userManager.AddToRoleAsync(user, model.Role);
        return MapUser(user, model.Role);
    }

    public async Task<UserViewModel?> UpdateUserAsync(string id, UpdateUserViewModel model, CancellationToken cancellationToken = default)
    {
        if (!FarmRoles.All.Contains(model.Role))
            throw new InvalidOperationException("Invalid role selected.");

        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return null;

        user.FullName = model.FullName.Trim();

        if (model.IsLocked)
            await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        else
            await _userManager.SetLockoutEndDateAsync(user, null);

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
            throw new InvalidOperationException(string.Join(" ", updateResult.Errors.Select(e => e.Description)));

        var currentRoles = await _userManager.GetRolesAsync(user);
        if (currentRoles.Count > 0)
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
        await _userManager.AddToRoleAsync(user, model.Role);

        return MapUser(user, model.Role);
    }

    public async Task<bool> DeleteUserAsync(string id, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return false;

        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Contains(FarmRoles.Admin))
        {
            var adminCount = 0;
            foreach (var admin in await _userManager.GetUsersInRoleAsync(FarmRoles.Admin))
                adminCount++;
            if (adminCount <= 1)
                throw new InvalidOperationException("Cannot delete the last administrator.");
        }

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(e => e.Description)));
        return true;
    }

    public async Task ResetPasswordAsync(string id, ResetPasswordViewModel model, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null) throw new InvalidOperationException("User not found.");

        var policy = await GetPasswordPolicyAsync(cancellationToken);
        var errors = ValidatePassword(model.NewPassword, policy).ToList();
        if (errors.Count > 0)
            throw new InvalidOperationException(string.Join(" ", errors));

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(e => e.Description)));
    }

    public async Task<PasswordPolicyViewModel> GetPasswordPolicyAsync(CancellationToken cancellationToken = default)
    {
        var setting = await _context.AppSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == PasswordPolicyKey, cancellationToken);
        if (setting is null || string.IsNullOrWhiteSpace(setting.Value))
            return DefaultPasswordPolicy();

        return JsonSerializer.Deserialize<PasswordPolicyViewModel>(setting.Value, JsonOptions) ?? DefaultPasswordPolicy();
    }

    public async Task SavePasswordPolicyAsync(PasswordPolicyViewModel model, CancellationToken cancellationToken = default)
    {
        model.RequiredLength = Math.Clamp(model.RequiredLength, 6, 128);
        await SaveSettingAsync(PasswordPolicyKey, JsonSerializer.Serialize(model, JsonOptions), cancellationToken);
    }

    public async Task<RolePermissionsViewModel> GetRolePermissionsAsync(CancellationToken cancellationToken = default)
    {
        var setting = await _context.AppSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == RolePermissionsKey, cancellationToken);
        if (setting is null || string.IsNullOrWhiteSpace(setting.Value))
            return new RolePermissionsViewModel { Permissions = DefaultRolePermissions() };

        return ParseRolePermissions(setting.Value);
    }

    public async Task SaveRolePermissionsAsync(RolePermissionsViewModel model, CancellationToken cancellationToken = default)
    {
        var cleaned = DefaultRolePermissions();
        foreach (var role in FarmRoles.All)
        {
            if (!model.Permissions.TryGetValue(role, out var tabs))
                continue;

            foreach (var tab in FarmTabs.All.Select(t => t.Key))
            {
                if (tabs.TryGetValue(tab, out var perm))
                    cleaned[role][tab] = NormalizePermission(perm);
            }
        }

        EnsureAdminSettingsAccess(cleaned);
        await SaveSettingAsync(RolePermissionsKey, JsonSerializer.Serialize(cleaned, JsonOptions), cancellationToken);
    }

    public async Task<UserPermissionsViewModel?> GetUserPermissionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return null;

        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault(r => FarmRoles.All.Contains(r)) ?? FarmRoles.Staff;
        var rolePermissions = await GetPermissionsForRoleAsync(role, cancellationToken);

        if (user.UsesRolePermissions || string.IsNullOrWhiteSpace(user.PermissionsJson))
        {
            return new UserPermissionsViewModel
            {
                UserId = user.Id,
                Role = role,
                UsesRolePermissions = true,
                Permissions = rolePermissions.ToDictionary(p => p.Key, p => p.Value)
            };
        }

        var custom = DeserializeUserPermissions(user.PermissionsJson);
        return new UserPermissionsViewModel
        {
            UserId = user.Id,
            Role = role,
            UsesRolePermissions = false,
            Permissions = MergeWithRoleDefaults(custom, rolePermissions)
        };
    }

    public async Task SaveUserPermissionsAsync(string userId, SaveUserPermissionsViewModel model, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) throw new InvalidOperationException("User not found.");

        user.UsesRolePermissions = model.UsesRolePermissions;
        if (model.UsesRolePermissions)
        {
            user.PermissionsJson = null;
        }
        else
        {
            var cleaned = new Dictionary<string, TabPermissionViewModel>();
            foreach (var tab in FarmTabs.All.Select(t => t.Key))
            {
                if (model.Permissions.TryGetValue(tab, out var perm))
                    cleaned[tab] = NormalizePermission(perm);
                else
                    cleaned[tab] = TabPermissionViewModel.NoAccess;
            }

            if (await GetUserRoleAsync(user) == FarmRoles.Admin)
                cleaned[FarmTabs.Settings] = TabPermissionViewModel.FullAccess;

            user.PermissionsJson = JsonSerializer.Serialize(cleaned, JsonOptions);
        }

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(e => e.Description)));
    }

    public async Task<IReadOnlyDictionary<string, TabPermissionViewModel>> GetEffectivePermissionsAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return new Dictionary<string, TabPermissionViewModel>();

        var role = await GetUserRoleAsync(user);
        var rolePermissions = await GetPermissionsForRoleAsync(role, cancellationToken);

        if (user.UsesRolePermissions || string.IsNullOrWhiteSpace(user.PermissionsJson))
            return EnsureAdminUserSettings(role, rolePermissions);

        var effective = MergeWithRoleDefaults(DeserializeUserPermissions(user.PermissionsJson), rolePermissions);
        return EnsureAdminUserSettings(role, effective);
    }

    internal static RolePermissionsViewModel ParseRolePermissions(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
            return new RolePermissionsViewModel { Permissions = DefaultRolePermissions() };

        var firstRole = doc.RootElement.EnumerateObject().FirstOrDefault();
        if (firstRole.Value.ValueKind == JsonValueKind.Array)
        {
            var legacy = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json, JsonOptions);
            return new RolePermissionsViewModel { Permissions = MigrateLegacyPermissions(legacy) };
        }

        var parsed = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, TabPermissionViewModel>>>(json, JsonOptions);
        var permissions = parsed ?? DefaultRolePermissions();
        EnsureAdminSettingsAccess(permissions);
        EnsureStaffVaccineAccess(permissions);
        return new RolePermissionsViewModel { Permissions = permissions };
    }

    private static TabPermissionViewModel NormalizePermission(TabPermissionViewModel perm)
    {
        if (!perm.View)
            return TabPermissionViewModel.NoAccess;

        return new TabPermissionViewModel
        {
            View = true,
            Add = perm.Add,
            Edit = perm.Edit,
            Delete = perm.Delete
        };
    }

    private static void EnsureAdminSettingsAccess(Dictionary<string, Dictionary<string, TabPermissionViewModel>> permissions)
    {
        if (!permissions.TryGetValue(FarmRoles.Admin, out var adminTabs))
        {
            adminTabs = new Dictionary<string, TabPermissionViewModel>();
            permissions[FarmRoles.Admin] = adminTabs;
        }

        adminTabs[FarmTabs.Settings] = TabPermissionViewModel.FullAccess;
    }

    private static Dictionary<string, Dictionary<string, TabPermissionViewModel>> MigrateLegacyPermissions(
        Dictionary<string, List<string>>? legacy)
    {
        var result = DefaultRolePermissions();
        if (legacy is null)
            return result;

        foreach (var role in FarmRoles.All)
        {
            if (!legacy.TryGetValue(role, out var tabList))
                continue;

            foreach (var tab in FarmTabs.All.Select(t => t.Key))
            {
                result[role][tab] = tabList.Contains(tab)
                    ? TabPermissionViewModel.FullAccess
                    : TabPermissionViewModel.NoAccess;
            }
        }

        EnsureAdminSettingsAccess(result);
        EnsureStaffVaccineAccess(result);
        return result;
    }

    public IReadOnlyList<string> ValidatePassword(string password, PasswordPolicyViewModel? policy = null)
    {
        policy ??= DefaultPasswordPolicy();
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(password))
        {
            errors.Add("Password is required.");
            return errors;
        }
        if (password.Length < policy.RequiredLength)
            errors.Add($"Password must be at least {policy.RequiredLength} characters.");
        if (policy.RequireDigit && !password.Any(char.IsDigit))
            errors.Add("Password must include a digit.");
        if (policy.RequireLowercase && !password.Any(char.IsLower))
            errors.Add("Password must include a lowercase letter.");
        if (policy.RequireUppercase && !password.Any(char.IsUpper))
            errors.Add("Password must include an uppercase letter.");
        if (policy.RequireNonAlphanumeric && password.All(char.IsLetterOrDigit))
            errors.Add("Password must include a special character.");
        return errors;
    }

    private static UserViewModel MapUser(ApplicationUser user, string role) => new()
    {
        Id = user.Id,
        FullName = user.FullName ?? user.Email ?? user.UserName ?? "",
        Email = user.Email ?? "",
        Role = role,
        IsLocked = user.LockoutEnd is not null && user.LockoutEnd > DateTimeOffset.UtcNow,
        UsesRolePermissions = user.UsesRolePermissions
    };

    private async Task<string> GetUserRoleAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        return roles.FirstOrDefault(r => FarmRoles.All.Contains(r)) ?? FarmRoles.Staff;
    }

    private async Task<IReadOnlyDictionary<string, TabPermissionViewModel>> GetPermissionsForRoleAsync(
        string role, CancellationToken cancellationToken)
    {
        var all = await GetRolePermissionsAsync(cancellationToken);
        return all.Permissions.TryGetValue(role, out var rolePerms)
            ? rolePerms
            : DefaultRolePermissions()[role];
    }

    private static Dictionary<string, TabPermissionViewModel> DeserializeUserPermissions(string json)
    {
        return JsonSerializer.Deserialize<Dictionary<string, TabPermissionViewModel>>(json, JsonOptions)
               ?? new Dictionary<string, TabPermissionViewModel>();
    }

    private static IReadOnlyDictionary<string, TabPermissionViewModel> EnsureAdminUserSettings(
        string role,
        IReadOnlyDictionary<string, TabPermissionViewModel> permissions)
    {
        if (role != FarmRoles.Admin)
            return permissions;

        var copy = permissions.ToDictionary(p => p.Key, p => p.Value);
        copy[FarmTabs.Settings] = TabPermissionViewModel.FullAccess;
        return copy;
    }

    private static Dictionary<string, TabPermissionViewModel> MergeWithRoleDefaults(
        Dictionary<string, TabPermissionViewModel> custom,
        IReadOnlyDictionary<string, TabPermissionViewModel> roleDefaults)
    {
        var merged = roleDefaults.ToDictionary(p => p.Key, p => p.Value);
        foreach (var tab in FarmTabs.All.Select(t => t.Key))
        {
            if (custom.TryGetValue(tab, out var perm))
                merged[tab] = perm;
        }
        return merged;
    }

    private async Task SaveSettingAsync(string key, string value, CancellationToken cancellationToken)
    {
        var setting = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == key, cancellationToken);
        if (setting is null)
            _context.AppSettings.Add(new AppSetting { Key = key, Value = value });
        else
        {
            setting.Value = value;
            setting.UpdatedDate = DateTime.UtcNow;
        }
        await _context.SaveChangesAsync(cancellationToken);
    }

    public static PasswordPolicyViewModel DefaultPasswordPolicy() => new()
    {
        RequiredLength = 8,
        RequireDigit = true,
        RequireLowercase = true,
        RequireUppercase = false,
        RequireNonAlphanumeric = false
    };

    public static Dictionary<string, Dictionary<string, TabPermissionViewModel>> DefaultRolePermissions()
    {
        var result = new Dictionary<string, Dictionary<string, TabPermissionViewModel>>();
        foreach (var role in FarmRoles.All)
        {
            var tabs = new Dictionary<string, TabPermissionViewModel>();
            foreach (var tab in FarmTabs.All.Select(t => t.Key))
            {
                var hasTab = role switch
                {
                    FarmRoles.Admin => true,
                    FarmRoles.Manager => tab != FarmTabs.Settings,
                    FarmRoles.Staff => tab is FarmTabs.Dashboard or FarmTabs.Herd or FarmTabs.Feed or FarmTabs.Milk or FarmTabs.Vaccines,
                    _ => false
                };
                tabs[tab] = hasTab ? TabPermissionViewModel.FullAccess : TabPermissionViewModel.NoAccess;
            }
            result[role] = tabs;
        }

        EnsureAdminSettingsAccess(result);
        EnsureStaffVaccineAccess(result);
        return result;
    }

    private static void EnsureStaffVaccineAccess(Dictionary<string, Dictionary<string, TabPermissionViewModel>> permissions)
    {
        if (!permissions.TryGetValue(FarmRoles.Staff, out var staffTabs))
            return;

        if (!staffTabs.TryGetValue(FarmTabs.Vaccines, out var vaccines) || !vaccines.View)
        {
            staffTabs[FarmTabs.Vaccines] = TabPermissionViewModel.FullAccess;
            return;
        }

        if (vaccines.View && !vaccines.Add && !vaccines.Edit && !vaccines.Delete)
            staffTabs[FarmTabs.Vaccines] = TabPermissionViewModel.FullAccess;
    }
}

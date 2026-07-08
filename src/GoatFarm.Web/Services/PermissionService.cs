using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Settings;
using GoatFarm.Domain.Constants;
using GoatFarm.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace GoatFarm.Web.Services;

public class PermissionService : IPermissionService
{
    private const string PermissionsCacheKey = "__FarmPermissions";
    private const string RoleCacheKey = "__FarmUserRole";

    private readonly IUserSettingsService _userSettingsService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PermissionService(
        IUserSettingsService userSettingsService,
        UserManager<ApplicationUser> userManager,
        IHttpContextAccessor httpContextAccessor)
    {
        _userSettingsService = userSettingsService;
        _userManager = userManager;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<string?> GetCurrentUserRoleAsync(CancellationToken cancellationToken = default)
    {
        var context = _httpContextAccessor.HttpContext;
        if (context?.Items.TryGetValue(RoleCacheKey, out var cachedRole) == true)
            return cachedRole as string;

        var user = await GetCurrentUserAsync();
        if (user is null)
            return null;

        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault(r => FarmRoles.All.Contains(r));
        context!.Items[RoleCacheKey] = role;
        return role;
    }

    public async Task<IReadOnlyDictionary<string, TabPermissionViewModel>> GetCurrentUserPermissionsAsync(
        CancellationToken cancellationToken = default)
    {
        var context = _httpContextAccessor.HttpContext;
        if (context?.Items.TryGetValue(PermissionsCacheKey, out var cached) == true &&
            cached is IReadOnlyDictionary<string, TabPermissionViewModel> cachedPerms)
            return cachedPerms;

        var user = await GetCurrentUserAsync();
        if (user is null)
        {
            var empty = new Dictionary<string, TabPermissionViewModel>();
            if (context is not null)
                context.Items[PermissionsCacheKey] = empty;
            return empty;
        }

        var perms = await _userSettingsService.GetEffectivePermissionsAsync(user.Id, cancellationToken);
        context!.Items[PermissionsCacheKey] = perms;
        return perms;
    }

    public async Task<bool> CanAsync(string tab, string action, CancellationToken cancellationToken = default)
    {
        var perms = await GetCurrentUserPermissionsAsync(cancellationToken);
        return perms.TryGetValue(tab, out var perm) && perm.Allows(action);
    }

    private async Task<ApplicationUser?> GetCurrentUserAsync()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context?.User.Identity?.IsAuthenticated != true)
            return null;

        return await _userManager.GetUserAsync(context.User);
    }
}

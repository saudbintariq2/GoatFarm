using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Settings;
using GoatFarm.Domain.Constants;
using GoatFarm.Infrastructure.Identity;
using GoatFarm.Web.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GoatFarm.Web.Controllers;

public class SettingsController : Controller
{
    private readonly IUserSettingsService _userSettingsService;
    private readonly IPermissionService _permissionService;
    private readonly UserManager<ApplicationUser> _userManager;

    public SettingsController(
        IUserSettingsService userSettingsService,
        IPermissionService permissionService,
        UserManager<ApplicationUser> userManager)
    {
        _userSettingsService = userSettingsService;
        _permissionService = permissionService;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewData["ActiveTab"] = "settings";
        return View(await _userSettingsService.GetSettingsPageAsync(cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var actor = await GetActorAsync(cancellationToken);
        if (UserAdminGuard.CanAssignRole(actor, model.Role, await RolePermissionsAsync(model.Role, cancellationToken)) is { } denied)
            return Denied(denied);
        try
        {
            return Json(await _userSettingsService.CreateUserAsync(model, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut]
    public async Task<IActionResult> UpdateUser(string id, [FromBody] UpdateUserViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var actor = await GetActorAsync(cancellationToken);
        var targetRole = await GetUserRoleAsync(id);
        if (UserAdminGuard.CanManageUser(actor, id, targetRole) is { } denied)
            return Denied(denied);
        if (model.Role != targetRole &&
            UserAdminGuard.CanAssignRole(actor, model.Role, await RolePermissionsAsync(model.Role, cancellationToken)) is { } roleDenied)
            return Denied(roleDenied);
        try
        {
            var result = await _userSettingsService.UpdateUserAsync(id, model, cancellationToken);
            return result is null ? NotFound() : Json(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteUser(string id, CancellationToken cancellationToken)
    {
        if (UserAdminGuard.CanManageUser(await GetActorAsync(cancellationToken), id, await GetUserRoleAsync(id)) is { } denied)
            return Denied(denied);
        try
        {
            var ok = await _userSettingsService.DeleteUserAsync(id, cancellationToken);
            return ok ? Ok(new { success = true }) : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> ResetPassword(string id, [FromBody] ResetPasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (UserAdminGuard.CanManageUser(await GetActorAsync(cancellationToken), id, await GetUserRoleAsync(id)) is { } denied)
            return Denied(denied);
        try
        {
            await _userSettingsService.ResetPasswordAsync(id, model, cancellationToken);
            return Ok(new { success = true });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut]
    public async Task<IActionResult> SavePasswordPolicy([FromBody] PasswordPolicyViewModel model, CancellationToken cancellationToken)
    {
        if (UserAdminGuard.CanChangeFarmWideSecurity(await GetActorAsync(cancellationToken)) is { } denied)
            return Denied(denied);
        await _userSettingsService.SavePasswordPolicyAsync(model, cancellationToken);
        return Ok(new { success = true });
    }

    [HttpPut]
    public async Task<IActionResult> SaveRolePermissions([FromBody] RolePermissionsViewModel model, CancellationToken cancellationToken)
    {
        if (UserAdminGuard.CanChangeFarmWideSecurity(await GetActorAsync(cancellationToken)) is { } denied)
            return Denied(denied);
        await _userSettingsService.SaveRolePermissionsAsync(model, cancellationToken);
        return Ok(new { success = true });
    }

    [HttpGet]
    public async Task<IActionResult> GetUserPermissions(string id, CancellationToken cancellationToken)
    {
        var result = await _userSettingsService.GetUserPermissionsAsync(id, cancellationToken);
        return result is null ? NotFound() : Json(result);
    }

    [HttpPut]
    public async Task<IActionResult> SaveUserPermissions(string id, [FromBody] SaveUserPermissionsViewModel model, CancellationToken cancellationToken)
    {
        var actor = await GetActorAsync(cancellationToken);
        var targetRole = await GetUserRoleAsync(id);
        if (UserAdminGuard.CanManageUser(actor, id, targetRole) is { } denied)
            return Denied(denied);
        var granted = model.UsesRolePermissions
            ? await RolePermissionsAsync(targetRole ?? FarmRoles.Staff, cancellationToken)
            : model.Permissions;
        if (UserAdminGuard.CanGrant(actor, granted ?? new Dictionary<string, TabPermissionViewModel>()) is { } grantDenied)
            return Denied(grantDenied);
        try
        {
            await _userSettingsService.SaveUserPermissionsAsync(id, model, cancellationToken);
            return Ok(new { success = true });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private async Task<AdminActor> GetActorAsync(CancellationToken cancellationToken) => new(
        _userManager.GetUserId(User) ?? string.Empty,
        await _permissionService.GetCurrentUserRoleAsync(cancellationToken),
        await _permissionService.GetCurrentUserPermissionsAsync(cancellationToken));

    private async Task<string?> GetUserRoleAsync(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return null;
        return (await _userManager.GetRolesAsync(user)).FirstOrDefault(r => FarmRoles.All.Contains(r));
    }

    private async Task<IReadOnlyDictionary<string, TabPermissionViewModel>?> RolePermissionsAsync(string role, CancellationToken cancellationToken) =>
        (await _userSettingsService.GetRolePermissionsAsync(cancellationToken)).Permissions.GetValueOrDefault(role);

    private ObjectResult Denied(string message) => StatusCode(StatusCodes.Status403Forbidden, new { error = message });
}

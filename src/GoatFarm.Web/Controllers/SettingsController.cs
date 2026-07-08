using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Settings;
using Microsoft.AspNetCore.Mvc;

namespace GoatFarm.Web.Controllers;

[IgnoreAntiforgeryToken]
public class SettingsController : Controller
{
    private readonly IUserSettingsService _userSettingsService;

    public SettingsController(IUserSettingsService userSettingsService) => _userSettingsService = userSettingsService;

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
        await _userSettingsService.SavePasswordPolicyAsync(model, cancellationToken);
        return Ok(new { success = true });
    }

    [HttpPut]
    public async Task<IActionResult> SaveRolePermissions([FromBody] RolePermissionsViewModel model, CancellationToken cancellationToken)
    {
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
}

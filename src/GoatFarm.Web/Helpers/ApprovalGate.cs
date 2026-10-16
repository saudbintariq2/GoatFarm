using System.Security.Claims;
using System.Text.Json;
using GoatFarm.Application.Interfaces;
using GoatFarm.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace GoatFarm.Web.Helpers;

public static class ApprovalGate
{
    public static string UserId(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

    public static string UserDisplay(ClaimsPrincipal user) =>
        user.FindFirst("FullName")?.Value ?? user.Identity?.Name ?? "";

    public static async Task<IActionResult?> GuardDeleteAsync(
        Controller controller,
        ClaimsPrincipal user,
        IApprovalService approval,
        IPermissionService permissions,
        string module,
        string entity,
        int id,
        CancellationToken cancellationToken)
    {
        var role = await permissions.GetCurrentUserRoleAsync(cancellationToken) ?? FarmRoles.Staff;
        if (!await approval.NeedsApprovalAsync(UserId(user), role, FarmActions.Delete, cancellationToken))
            return null;

        var payload = JsonSerializer.Serialize(new { entity, action = "Delete", id });
        await approval.SubmitAsync(module, "Delete", payload, UserId(user), UserDisplay(user), cancellationToken);
        return controller.StatusCode(StatusCodes.Status202Accepted, new
        {
            pendingApproval = true,
            message = "Delete submitted for manager approval."
        });
    }

    public static async Task<IActionResult?> GuardDeleteAsync(
        Controller controller,
        ClaimsPrincipal user,
        IApprovalService approval,
        IPermissionService permissions,
        string module,
        string entity,
        object payloadExtra,
        CancellationToken cancellationToken)
    {
        var role = await permissions.GetCurrentUserRoleAsync(cancellationToken) ?? FarmRoles.Staff;
        if (!await approval.NeedsApprovalAsync(UserId(user), role, FarmActions.Delete, cancellationToken))
            return null;

        var payload = JsonSerializer.Serialize(payloadExtra);
        await approval.SubmitAsync(module, "Delete", payload, UserId(user), UserDisplay(user), cancellationToken);
        return controller.StatusCode(StatusCodes.Status202Accepted, new
        {
            pendingApproval = true,
            message = "Delete submitted for manager approval."
        });
    }

    public static async Task<IActionResult?> GuardEditAsync<T>(
        Controller controller,
        ClaimsPrincipal user,
        IApprovalService approval,
        IPermissionService permissions,
        string module,
        string entity,
        int id,
        T data,
        CancellationToken cancellationToken)
    {
        var role = await permissions.GetCurrentUserRoleAsync(cancellationToken) ?? FarmRoles.Staff;
        if (!await approval.NeedsApprovalAsync(UserId(user), role, FarmActions.Edit, cancellationToken))
            return null;

        var payload = JsonSerializer.Serialize(new { entity, action = "Update", id, data });
        await approval.SubmitAsync(module, "Update", payload, UserId(user), UserDisplay(user), cancellationToken);
        return controller.StatusCode(StatusCodes.Status202Accepted, new
        {
            pendingApproval = true,
            message = "Change submitted for manager approval."
        });
    }
}

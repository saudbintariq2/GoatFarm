using System.Text.Json;
using GoatFarm.Application.Interfaces;
using GoatFarm.Domain.Constants;
using GoatFarm.Web.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GoatFarm.Web.Filters;

public sealed class FarmPermissionFilter : IAsyncActionFilter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.ActionDescriptor is not ControllerActionDescriptor descriptor)
        {
            await next();
            return;
        }

        if (HasAllowAnonymous(descriptor))
        {
            await next();
            return;
        }

        var permissionService = context.HttpContext.RequestServices.GetRequiredService<IPermissionService>();
        var controllerName = descriptor.ControllerName;
        var actionName = descriptor.ActionName;
        var mapKey = $"{controllerName}.{actionName}";

        if (context.Controller is Controller controller)
        {
            var perms = await permissionService.GetCurrentUserPermissionsAsync(context.HttpContext.RequestAborted);
            var role = await permissionService.GetCurrentUserRoleAsync(context.HttpContext.RequestAborted) ?? "";
            controller.ViewData["FarmPermissionsJson"] = JsonSerializer.Serialize(perms, JsonOptions);
            controller.ViewData["FarmUserRole"] = role;
            controller.ViewData["VisibleTabKeys"] = perms
                .Where(p => p.Value.View)
                .Select(p => p.Key)
                .ToList();
        }

        if (actionName == "Index" &&
            FarmPermissionMap.IndexTabs.TryGetValue(controllerName, out var indexTab))
        {
            if (!await permissionService.CanAsync(indexTab, FarmActions.View, context.HttpContext.RequestAborted))
            {
                context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
                return;
            }
        }
        else if (FarmPermissionMap.ActionPermissions.TryGetValue(mapKey, out var required))
        {
            if (!await permissionService.CanAsync(required.Tab, required.Action, context.HttpContext.RequestAborted))
            {
                context.Result = WantsJson(context)
                    ? new JsonResult(new { error = "You do not have permission to perform this action." })
                    {
                        StatusCode = StatusCodes.Status403Forbidden
                    }
                    : new RedirectToActionResult("AccessDenied", "Account", null);
                return;
            }
        }

        await next();
    }

    private static bool HasAllowAnonymous(ControllerActionDescriptor descriptor)
    {
        return descriptor.MethodInfo.IsDefined(typeof(AllowAnonymousAttribute), true) ||
               descriptor.ControllerTypeInfo.IsDefined(typeof(AllowAnonymousAttribute), true);
    }

    private static bool WantsJson(ActionExecutingContext context)
    {
        var request = context.HttpContext.Request;
        if (request.Headers.Accept.Any(v => v?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true))
            return true;

        return request.Headers.XRequestedWith == "XMLHttpRequest" ||
               request.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true ||
               request.Method is "POST" or "PUT" or "DELETE" or "PATCH";
    }
}

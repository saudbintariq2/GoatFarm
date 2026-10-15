using System.Text.Json;
using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Settings;
using GoatFarm.Domain.Constants;
using GoatFarm.Infrastructure.Identity;
using GoatFarm.Web.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;

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
        var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var bypassChecks = configuration.GetValue<bool>("LocalDev:DisablePermissionChecks");
        var controllerName = descriptor.ControllerName;
        var actionName = descriptor.ActionName;
        var mapKey = $"{controllerName}.{actionName}";

        if (context.Controller is Controller controller)
        {
            IReadOnlyDictionary<string, TabPermissionViewModel> perms;
            var role = await permissionService.GetCurrentUserRoleAsync(context.HttpContext.RequestAborted) ?? "";
            if (bypassChecks && context.HttpContext.User.Identity?.IsAuthenticated == true)
                perms = FullAccessPermissions();
            else
                perms = await permissionService.GetCurrentUserPermissionsAsync(context.HttpContext.RequestAborted);

            if (context.HttpContext.User.Identity?.IsAuthenticated == true)
            {
                var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
                var user = await userManager.GetUserAsync(context.HttpContext.User);
                if (user is not null)
                    controller.ViewData["FarmUserFullName"] = user.FullName?.Trim() ?? "";
            }

            controller.ViewData["FarmPermissionsJson"] = JsonSerializer.Serialize(perms, JsonOptions);
            controller.ViewData["FarmUserRole"] = role;
            controller.ViewData["VisibleTabKeys"] = perms
                .Where(p => p.Value.View)
                .Select(p => p.Key)
                .ToList();
        }

        if (bypassChecks)
        {
            await next();
            return;
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
        else if (!FarmPermissionMap.OpenControllers.Contains(controllerName))
        {
            context.Result = WantsJson(context)
                ? new JsonResult(new { error = "This action is not available." }) { StatusCode = StatusCodes.Status403Forbidden }
                : new RedirectToActionResult("AccessDenied", "Account", null);
            return;
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

    private static IReadOnlyDictionary<string, TabPermissionViewModel> FullAccessPermissions()
    {
        var d = new Dictionary<string, TabPermissionViewModel>();
        foreach (var tab in FarmTabs.All.Select(t => t.Key))
            d[tab] = TabPermissionViewModel.FullAccess;
        return d;
    }
}

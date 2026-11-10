using GoatFarm.Application.Interfaces;
using GoatFarm.Domain.Constants;
using GoatFarm.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using System.Text.Json;

namespace GoatFarm.Web.Filters;

/// <summary>Writes an audit row after successful POST/PUT/PATCH/DELETE (controllers with manual audit are skipped).</summary>
public sealed class FarmAuditActionFilter : IAsyncActionFilter
{
    private static readonly HashSet<string> SkipControllers = new(StringComparer.OrdinalIgnoreCase)
    {
        "Account", "Admin", "Assistant", "Dashboard", "Home", "Reports", "Search",
        "Trading", "Products", "Projects", "BackupCenter"
    };

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();
        if (executed.Exception is not null && !executed.ExceptionHandled)
            return;

        var http = context.HttpContext.Request.Method;
        if (http is not ("POST" or "PUT" or "PATCH" or "DELETE"))
            return;

        if (IsFailedResult(executed.Result))
            return;

        if (context.ActionDescriptor is not ControllerActionDescriptor descriptor)
            return;

        if (SkipControllers.Contains(descriptor.ControllerName))
            return;
        if (HasAllowAnonymous(descriptor))
            return;

        var module = FarmAuditMap.ModuleFor(descriptor.ControllerName);
        if (module is null)
            return;

        var action = MapAuditAction(http, descriptor.ActionName);
        var userId = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userDisplay = context.HttpContext.User.FindFirst("FullName")?.Value
            ?? context.HttpContext.User.Identity?.Name
            ?? "User";

        var detail = JsonSerializer.Serialize(new
        {
            controller = descriptor.ControllerName,
            action = descriptor.ActionName,
            method = http,
            path = context.HttpContext.Request.Path.Value,
            userAgent = Truncate(context.HttpContext.Request.Headers.UserAgent.ToString(), 200)
        }, JsonOptions);

        var summary = $"{descriptor.ControllerName}.{descriptor.ActionName}";
        var audit = context.HttpContext.RequestServices.GetRequiredService<IAuditService>();
        await audit.LogAsync(module, action, summary, userId, userDisplay, detailJson: detail,
            showInNotices: action is not (AuditAction.Delete or AuditAction.Import),
            cancellationToken: context.HttpContext.RequestAborted);
    }

    private static AuditAction MapAuditAction(string method, string actionName)
    {
        if (method == "DELETE" || actionName.StartsWith("Delete", StringComparison.OrdinalIgnoreCase)
            || actionName.StartsWith("Remove", StringComparison.OrdinalIgnoreCase))
            return AuditAction.Delete;
        if (method == "PUT" || actionName.StartsWith("Update", StringComparison.OrdinalIgnoreCase))
            return AuditAction.Update;
        if (actionName.Contains("Import", StringComparison.OrdinalIgnoreCase))
            return AuditAction.Import;
        if (actionName.Contains("Export", StringComparison.OrdinalIgnoreCase))
            return AuditAction.Export;
        return AuditAction.Create;
    }

    private static bool HasAllowAnonymous(ControllerActionDescriptor descriptor) =>
        descriptor.EndpointMetadata.Any(m => m is Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute)
        || descriptor.ControllerTypeInfo.IsDefined(typeof(Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute), true);

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..max];

    private static bool IsFailedResult(IActionResult? result)
    {
        if (result is IStatusCodeActionResult { StatusCode: int code } && code >= 400)
            return true;
        return result is BadRequestObjectResult or UnauthorizedResult or ForbidResult
            or NotFoundResult or NotFoundObjectResult;
    }
}

internal static class FarmAuditMap
{
    public static string? ModuleFor(string controller) => controller switch
    {
        "Goat" => FarmTabs.Herd,
        "Breeding" => FarmTabs.Breeding,
        "Feed" => FarmTabs.Feed,
        "Finance" => FarmTabs.Finance,
        "Vaccine" => FarmTabs.Vaccines,
        "Tasks" => FarmTabs.Tasks,
        "Milk" => FarmTabs.Products,
        "Reminder" => FarmTabs.Vaccines,
        "Treatment" => FarmTabs.Vaccines,
        "Settings" => FarmTabs.Settings,
        "Ledger" => FarmTabs.Finance,
        _ => null
    };
}

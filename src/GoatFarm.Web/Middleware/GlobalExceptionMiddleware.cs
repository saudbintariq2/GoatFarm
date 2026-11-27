using System.Net;

namespace GoatFarm.Web.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client went away; nothing to report.
        }
        catch (Exception ex)
        {
            var refId = Random.Shared.Next(1000, 10000).ToString();
            _logger.LogError(ex, "Unhandled error ref {Ref} on {Method} {Path}", refId, context.Request.Method, context.Request.Path);
            if (context.Response.HasStarted) throw;
            await HandleExceptionAsync(context, refId);
        }
    }

    public static string FriendlyMessage(string refId) => $"Could not save – please try again. (ref {refId})";

    private static Task HandleExceptionAsync(HttpContext context, string refId)
    {
        context.Response.Clear();
        if (WantsJson(context.Request))
        {
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";
            return context.Response.WriteAsJsonAsync(new { error = FriendlyMessage(refId), reference = refId });
        }

        context.Response.Redirect("/Home/Error?ref=" + refId);
        return Task.CompletedTask;
    }

    /// <summary>Browser page navigations ask for text/html; fetch() calls (Accept */* or json) get JSON.</summary>
    private static bool WantsJson(HttpRequest request)
    {
        var accept = request.Headers.Accept.ToString();
        return request.Path.StartsWithSegments("/api")
            || accept.Contains("application/json", StringComparison.OrdinalIgnoreCase)
            || string.Equals(request.Headers.XRequestedWith, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase)
            || (request.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) ?? false)
            || !accept.Contains("text/html", StringComparison.OrdinalIgnoreCase);
    }
}

public static class GlobalExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionMiddleware(this IApplicationBuilder app) =>
        app.UseMiddleware<GlobalExceptionMiddleware>();
}

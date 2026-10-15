using System.Diagnostics;
using System.Globalization;
using GoatFarm.Infrastructure.Diagnostics;

namespace GoatFarm.Web.Middleware;

/// <summary>
/// Opt-in (<c>Diagnostics:ServerTiming=true</c>): measures database commands, database time and total time per request,
/// sends them as a <c>Server-Timing</c> header and logs them. Off by default because timings help attackers profile the server.
/// </summary>
public class ServerTimingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ServerTimingMiddleware> _logger;

    public ServerTimingMiddleware(RequestDelegate next, ILogger<ServerTimingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/css") || context.Request.Path.StartsWithSegments("/js") ||
            context.Request.Path.StartsWithSegments("/lib") || Path.HasExtension(context.Request.Path.Value))
        {
            await _next(context);
            return;
        }

        var metrics = new QueryMetrics();
        var clock = Stopwatch.StartNew();
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["Server-Timing"] = string.Create(CultureInfo.InvariantCulture,
                $"db;dur={metrics.DatabaseTime.TotalMilliseconds:F1};desc=\"{metrics.Commands} queries\", app;dur={clock.Elapsed.TotalMilliseconds:F1}");
            return Task.CompletedTask;
        });

        using (QueryMetrics.Begin(metrics))
            await _next(context);

        _logger.LogInformation("Timing {Method} {Path}: {Status}, {Queries} queries, {DbMs:F0} ms database, {TotalMs:F0} ms total",
            context.Request.Method, context.Request.Path.Value, context.Response.StatusCode,
            metrics.Commands, metrics.DatabaseTime.TotalMilliseconds, clock.Elapsed.TotalMilliseconds);
    }
}

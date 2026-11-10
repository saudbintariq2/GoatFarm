using GoatFarm.Application.Interfaces;
using GoatFarm.Infrastructure;
using GoatFarm.Infrastructure.Persistence;
using GoatFarm.Web.Filters;
using GoatFarm.Web.Middleware;
using GoatFarm.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using GoatFarm.Infrastructure.Identity;

var builder = WebApplication.CreateBuilder(args);

var home = Environment.GetEnvironmentVariable("HOME");
var keysPath = !string.IsNullOrEmpty(home)
    ? Path.Combine(home, "ASP.NET", "DataProtection-Keys")
    : Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GoatFarm",
        "DataProtection-Keys");
Directory.CreateDirectory(keysPath);
builder.Services.AddDataProtection()
    .SetApplicationName("GoatFarm")
    .PersistKeysToFileSystem(new DirectoryInfo(keysPath));

var cookieSecurePolicy = builder.Environment.IsDevelopment()
    ? CookieSecurePolicy.SameAsRequest
    : CookieSecurePolicy.Always;

builder.Services.AddHttpContextAccessor();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
        | ForwardedHeaders.XForwardedProto
        | ForwardedHeaders.XForwardedHost;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "__GoatFarm.Antiforgery";
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = cookieSecurePolicy;
    options.HeaderName = "X-CSRF-TOKEN";
});

var mvcBuilder = builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<FarmPermissionFilter>();
    options.Filters.Add<FarmAuditActionFilter>();
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
})
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

#if DEBUG
if (builder.Environment.IsDevelopment())
    mvcBuilder.AddRazorRuntimeCompilation();
#endif

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddScoped<IPermissionService, PermissionService>();

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 8;
    options.User.RequireUniqueEmail = true;
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
})
.AddEntityFrameworkStores<GoatFarmDbContext>()
.AddDefaultTokenProviders();

builder.Services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, FarmUserClaimsPrincipalFactory>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = cookieSecurePolicy;
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
});

var app = builder.Build();

await DatabaseBootstrap.InitializeAsync(app.Services, app.Environment.IsDevelopment());

if (args.Contains("--initialize-only", StringComparer.OrdinalIgnoreCase))
    return;

app.UseForwardedHeaders();

app.UseGlobalExceptionMiddleware();
if (app.Configuration.GetValue<bool>("Diagnostics:ServerTiming"))
    app.UseMiddleware<ServerTimingMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseStaticFiles();
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        var ct = context.Response.ContentType;
        if (ct != null
            && ct.StartsWith("text/html", StringComparison.OrdinalIgnoreCase)
            && !ct.Contains("charset", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.ContentType = ct + "; charset=utf-8";
        }
        return Task.CompletedTask;
    });
    await next();
});
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.Run();

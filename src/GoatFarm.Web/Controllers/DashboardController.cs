using System.Security.Claims;
using System.Text.Json;
using GoatFarm.Application.Common;
using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Dashboard;
using GoatFarm.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoatFarm.Web.Controllers;

public class DashboardController : Controller
{
    private readonly IStatisticsService _statisticsService;
    private readonly IBackupService _backupService;
    private readonly ITaskService _taskService;
    private readonly IPermissionService _permissionService;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(
        IStatisticsService statisticsService,
        IBackupService backupService,
        ITaskService taskService,
        IPermissionService permissionService,
        ILogger<DashboardController> logger)
    {
        _statisticsService = statisticsService;
        _backupService = backupService;
        _taskService = taskService;
        _permissionService = permissionService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewData["ActiveTab"] = "dashboard";
        if (await _permissionService.CanAsync(FarmTabs.Tasks, FarmActions.View, cancellationToken))
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            ViewData["MyTasks"] = await _taskService.GetOpenForUserAsync(userId, 6, cancellationToken);
        }
        return View(await GetDashboardForUserAsync(null, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Stats(string? month, CancellationToken cancellationToken) =>
        Json(await GetDashboardForUserAsync(month, cancellationToken));

    private async Task<DashboardViewModel> GetDashboardForUserAsync(string? month, CancellationToken cancellationToken)
    {
        var dashboard = await _statisticsService.GetDashboardAsync(month, cancellationToken);
        return await _permissionService.CanAsync(FarmTabs.Finance, FarmActions.View, cancellationToken)
            ? dashboard
            : FinanceVisibility.WithoutFinance(dashboard);
    }

    [HttpGet]
    public async Task<IActionResult> Export(CancellationToken cancellationToken)
    {
        var data = await _backupService.ExportAsync(cancellationToken);
        var json = JsonSerializer.Serialize(data, GoatFarm.Infrastructure.Services.BackupService.SerializerOptions);
        return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json",
            $"goat-backup-{DateTime.Today:yyyy-MM-dd}.json");
    }

    [HttpPost]
    public async Task<IActionResult> Import(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No backup file selected." });

        await using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream);
        var json = await reader.ReadToEndAsync(cancellationToken);

        try
        {
            await _backupService.ImportAsync(json, cancellationToken);
            return Json(new { success = true });
        }
        catch (JsonException)
        {
            return BadRequest(new { error = "Could not read this file — make sure it is a Goat Records backup (.json)." });
        }
        catch (GoatFarm.Application.Common.BackupValidationException ex)
        {
            return BadRequest(new { error = "This backup was not restored: " + string.Join(" ", ex.Errors.Take(5)), errors = ex.Errors });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Backup restore failed; the transaction was rolled back");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "The backup could not be restored. Your existing data was not changed." });
        }
    }
}

public class HomeController : Controller
{
    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View("Error");
}

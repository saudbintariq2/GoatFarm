using System.Text.Json;
using GoatFarm.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoatFarm.Web.Controllers;

[IgnoreAntiforgeryToken]
public class DashboardController : Controller
{
    private readonly IStatisticsService _statisticsService;
    private readonly IBackupService _backupService;

    public DashboardController(IStatisticsService statisticsService, IBackupService backupService)
    {
        _statisticsService = statisticsService;
        _backupService = backupService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewData["ActiveTab"] = "dashboard";
        return View(await _statisticsService.GetDashboardAsync(null, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Stats(string? month, CancellationToken cancellationToken) =>
        Json(await _statisticsService.GetDashboardAsync(month, cancellationToken));

    [HttpGet]
    public async Task<IActionResult> Export(CancellationToken cancellationToken)
    {
        var data = await _backupService.ExportAsync(cancellationToken);
        var json = System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
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
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

public class HomeController : Controller
{
    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View("Error");
}

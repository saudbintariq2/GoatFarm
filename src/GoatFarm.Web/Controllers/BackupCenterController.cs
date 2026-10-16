using GoatFarm.Application.Interfaces;
using GoatFarm.Domain.Constants;
using GoatFarm.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;

namespace GoatFarm.Web.Controllers;

public class BackupCenterController : Controller
{
    private readonly IBackupService _backup;
    private readonly IAuditService _audit;

    public BackupCenterController(IBackupService backup, IAuditService audit)
    {
        _backup = backup;
        _audit = audit;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
    private string UserDisplay => User.FindFirst("FullName")?.Value ?? User.Identity?.Name ?? "";

    [HttpGet]
    public IActionResult Index()
    {
        ViewData["ActiveTab"] = "backup";
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Export(CancellationToken cancellationToken)
    {
        var payload = await _backup.ExportAsync(cancellationToken);
        await _audit.LogAsync(FarmTabs.Backup, AuditAction.Export, "Full farm backup downloaded", UserId, UserDisplay, cancellationToken: cancellationToken);
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", $"goat-records-backup-{DateTime.UtcNow:yyyyMMdd-HHmm}.json");
    }

    [HttpPost]
    public async Task<IActionResult> Import(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "Choose a backup JSON file." });
        await using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream);
        var json = await reader.ReadToEndAsync(cancellationToken);
        await _backup.ImportAsync(json, cancellationToken);
        await _audit.LogAsync(FarmTabs.Backup, AuditAction.Import, "Farm restored from backup", UserId, UserDisplay, cancellationToken: cancellationToken);
        return Ok(new { success = true });
    }
}

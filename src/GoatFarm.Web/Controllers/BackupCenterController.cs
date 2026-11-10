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
    private readonly IPermissionService _permissions;
    private readonly IFarmCsvExportService _csvExport;

    public BackupCenterController(
        IBackupService backup,
        IAuditService audit,
        IPermissionService permissions,
        IFarmCsvExportService csvExport)
    {
        _backup = backup;
        _audit = audit;
        _permissions = permissions;
        _csvExport = csvExport;
    }



    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

    private string UserDisplay => User.FindFirst("FullName")?.Value ?? User.Identity?.Name ?? "";



    [HttpGet]

    public async Task<IActionResult> Index(CancellationToken cancellationToken)

    {

        ViewData["ActiveTab"] = "backup";

        ViewBag.StoredBackups = await _backup.ListStoredBackupsAsync(cancellationToken);

        ViewBag.CanRestore = string.Equals(

            await _permissions.GetCurrentUserRoleAsync(cancellationToken),

            FarmRoles.Admin,

            StringComparison.OrdinalIgnoreCase);

        return View();

    }



    [HttpGet]

    public async Task<IActionResult> Export(CancellationToken cancellationToken)

    {

        var payload = await _backup.ExportAsync(cancellationToken);

        await _audit.LogAsync(FarmTabs.Backup, AuditAction.Export, "Full farm backup downloaded", UserId, UserDisplay, cancellationToken: cancellationToken);

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });

        return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", $"farm-backup-{DateTime.UtcNow:yyyyMMdd-HHmm}.json");

    }

    [HttpGet]
    public async Task<IActionResult> ExportCsv(string module, string? month, CancellationToken cancellationToken)
    {
        module = (module ?? "").Trim().ToLowerInvariant();
        byte[] bytes;
        string fileStem;
        switch (module)
        {
            case "herd":
                bytes = await _csvExport.ExportHerdAsync(cancellationToken);
                fileStem = "herd";
                break;
            case "finance":
                bytes = await _csvExport.ExportFinanceAsync(month, cancellationToken);
                fileStem = "finance";
                break;
            case "feed":
                bytes = await _csvExport.ExportFeedAsync(cancellationToken);
                fileStem = "feed";
                break;
            case "health":
                bytes = await _csvExport.ExportHealthAsync(cancellationToken);
                fileStem = "health";
                break;
            case "breeding":
                bytes = await _csvExport.ExportBreedingAsync(cancellationToken);
                fileStem = "breeding";
                break;
            case "tasks":
                bytes = await _csvExport.ExportTasksAsync(cancellationToken);
                fileStem = "tasks";
                break;
            default:
                return BadRequest(new { error = "Unknown module. Use herd, finance, feed, health, breeding, or tasks." });
        }

        await _audit.LogAsync(FarmTabs.Backup, AuditAction.Export, $"CSV export: {module}", UserId, UserDisplay, cancellationToken: cancellationToken);
        return File(bytes, "text/csv", $"{fileStem}-{DateTime.UtcNow:yyyyMMdd}.csv");
    }



    [HttpGet]

    public async Task<IActionResult> DownloadStored(string name, CancellationToken cancellationToken)

    {

        if (string.IsNullOrWhiteSpace(name) || name.Contains("..", StringComparison.Ordinal) || name.Contains('/', StringComparison.Ordinal) || name.Contains('\\', StringComparison.Ordinal))

            return BadRequest(new { error = "Invalid file name." });



        var bytes = await _backup.ReadStoredBackupAsync(name, cancellationToken);
        if (bytes is null)
            return NotFound();
        return File(bytes, "application/json", name);

    }



    [HttpPost]

    public async Task<IActionResult> Import(IFormFile file, CancellationToken cancellationToken)

    {

        var role = await _permissions.GetCurrentUserRoleAsync(cancellationToken) ?? "";

        if (!string.Equals(role, FarmRoles.Admin, StringComparison.OrdinalIgnoreCase))

            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Only an administrator can restore from backup." });



        if (file is null || file.Length == 0)

            return BadRequest(new { error = "Choose a backup JSON file." });



        var snapshot = await _backup.ExportAsync(cancellationToken);

        await _backup.SaveStoredBackupAsync(snapshot, "pre-restore", cancellationToken);



        await using var stream = file.OpenReadStream();

        using var reader = new StreamReader(stream);

        var json = await reader.ReadToEndAsync(cancellationToken);

        await _backup.ImportAsync(json, cancellationToken);

        await _audit.LogAsync(FarmTabs.Backup, AuditAction.Import, "Farm restored from backup", UserId, UserDisplay, cancellationToken: cancellationToken);

        return Ok(new { success = true });

    }

    [HttpPost]
    public async Task<IActionResult> ClearFarmData([FromBody] ClearFarmDataRequest request, CancellationToken cancellationToken)
    {
        var role = await _permissions.GetCurrentUserRoleAsync(cancellationToken) ?? "";
        if (!string.Equals(role, FarmRoles.Admin, StringComparison.OrdinalIgnoreCase))
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Only an administrator can clear all farm data." });

        if (!string.Equals(request?.Confirm?.Trim(), "WIPE ALL FARM DATA", StringComparison.Ordinal))
            return BadRequest(new { error = "Type WIPE ALL FARM DATA exactly to confirm." });

        var snapshot = await _backup.ExportAsync(cancellationToken);
        await _backup.SaveStoredBackupAsync(snapshot, "pre-wipe", cancellationToken);
        await _backup.ClearAllFarmDataAsync(cancellationToken);
        await _audit.LogAsync(FarmTabs.Backup, AuditAction.Delete, "All farm data cleared (empty slate)", UserId, UserDisplay, cancellationToken: cancellationToken);
        return Ok(new { success = true });
    }

    public sealed class ClearFarmDataRequest
    {
        public string? Confirm { get; set; }
    }

}


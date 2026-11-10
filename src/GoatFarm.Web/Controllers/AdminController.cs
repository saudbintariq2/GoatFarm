using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Admin;
using GoatFarm.Application.ViewModels.Approval;
using GoatFarm.Domain.Constants;
using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Security.Claims;
using System.Text;

namespace GoatFarm.Web.Controllers;

public class AdminController : Controller
{
    private readonly IAuditService _audit;
    private readonly IApprovalService _approval;
    private readonly ITaskService _tasks;
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminController(IAuditService audit, IApprovalService approval, ITaskService tasks, UserManager<ApplicationUser> userManager)
    {
        _audit = audit;
        _approval = approval;
        _tasks = tasks;
        _userManager = userManager;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
    private string UserDisplay => User.FindFirst("FullName")?.Value ?? User.Identity?.Name ?? "";

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewData["ActiveTab"] = "admin";
        ViewBag.Approvals = await _approval.GetPageAsync(cancellationToken);
        return View(await _audit.GetAdminPageAsync(100, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> AuditLog(string? module, string? user, DateOnly? from, DateOnly? to, int take = 200, CancellationToken cancellationToken = default) =>
        Json(await _audit.QueryAuditLogAsync(new AuditLogQueryViewModel
        {
            Module = module,
            UserSearch = user,
            From = from,
            To = to,
            Take = take
        }, cancellationToken));

    [HttpGet]
    public async Task<IActionResult> ExportAuditCsv(string? module, string? user, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default)
    {
        var rows = await _audit.QueryAuditLogAsync(new AuditLogQueryViewModel
        {
            Module = module,
            UserSearch = user,
            From = from,
            To = to,
            Take = 2000
        }, cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("When,Module,Action,User,Summary");
        foreach (var r in rows)
        {
            sb.Append(CsvCell(r.WhenDisplay)).Append(',')
                .Append(CsvCell(r.Module)).Append(',')
                .Append(CsvCell(r.Action)).Append(',')
                .Append(CsvCell(r.UserDisplay)).Append(',')
                .AppendLine(CsvCell(r.Summary));
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", $"audit-log-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    [HttpGet]
    public async Task<IActionResult> Notices(CancellationToken cancellationToken) =>
        Json(await _audit.GetRecentNoticesAsync(15, cancellationToken));

    [HttpGet]
    public async Task<IActionResult> PendingApprovalCount(CancellationToken cancellationToken) =>
        Json(new { count = await _approval.CountPendingAsync(cancellationToken) });

    [HttpGet]
    public async Task<IActionResult> NotificationSummary(CancellationToken cancellationToken)
    {
        var approvals = await _approval.CountPendingAsync(cancellationToken);
        var tasks = string.IsNullOrEmpty(UserId) ? 0 : await _tasks.CountMyOpenTasksAsync(UserId, cancellationToken);
        return Json(new { approvals, tasks, total = approvals + tasks });
    }

    [HttpGet]
    public async Task<IActionResult> Approvals(CancellationToken cancellationToken) =>
        Json(await _approval.GetPageAsync(cancellationToken));

    [HttpPost]
    public async Task<IActionResult> ReviewApproval(int id, [FromBody] ReviewApprovalViewModel model, CancellationToken cancellationToken)
    {
        var ok = await _approval.ReviewAsync(id, model.Approve, UserId, UserDisplay, model.Note, cancellationToken);
        if (ok)
            await _audit.LogAsync(FarmTabs.Admin, model.Approve ? AuditAction.Approve : AuditAction.Reject,
                $"Approval #{id}", UserId, UserDisplay, cancellationToken: cancellationToken);
        return ok ? Ok(new { success = true }) : BadRequest(new { error = "Could not approve — action failed or was already reviewed." });
    }

    [HttpPost]
    public async Task<IActionResult> ChangePassword([FromBody] ChangeOwnPasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();
        var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
            return BadRequest(new { error = string.Join(" ", result.Errors.Select(e => e.Description)) });
        await _audit.LogAsync(FarmTabs.Admin, AuditAction.Update, "Password changed", UserId, UserDisplay, showInNotices: false, cancellationToken: cancellationToken);
        return Ok(new { success = true });
    }

    private static string CsvCell(string? value)
    {
        var v = value ?? "";
        if (v.Contains('"', StringComparison.Ordinal)) v = v.Replace("\"", "\"\"", StringComparison.Ordinal);
        if (v.Contains(',') || v.Contains('\n') || v.Contains('\r'))
            return $"\"{v}\"";
        return v;
    }
}

public class ReviewApprovalViewModel
{
    public bool Approve { get; set; }
    public string? Note { get; set; }
}

public class ChangeOwnPasswordViewModel
{
    public string CurrentPassword { get; set; } = "";
    public string NewPassword { get; set; } = "";
}

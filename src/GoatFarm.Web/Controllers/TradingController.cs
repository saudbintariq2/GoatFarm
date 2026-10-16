using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Trading;
using GoatFarm.Domain.Constants;
using GoatFarm.Domain.Enums;
using GoatFarm.Web.Helpers;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GoatFarm.Web.Controllers;

public class TradingController : Controller
{
    private readonly ITradingService _trading;
    private readonly IAuditService _audit;
    private readonly IApprovalService _approvalService;
    private readonly IPermissionService _permissionService;

    public TradingController(ITradingService trading, IAuditService audit, IApprovalService approvalService, IPermissionService permissionService)
    {
        _trading = trading;
        _audit = audit;
        _approvalService = approvalService;
        _permissionService = permissionService;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
    private string UserDisplay => User.FindFirst("FullName")?.Value ?? User.Identity?.Name ?? "";

    [HttpGet]
    public async Task<IActionResult> Index(string? month, CancellationToken cancellationToken)
    {
        ViewData["ActiveTab"] = "trading";
        return View(await _trading.GetPageAsync(month, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> GetData(string? month, CancellationToken cancellationToken) =>
        Json(await _trading.GetPageAsync(month, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> SaveMaster([FromBody] SaveTradeMasterViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var result = await _trading.SaveMasterAsync(model, cancellationToken);
        await _audit.LogAsync(FarmTabs.Trading, AuditAction.Update, $"Trading master {model.Kind}: {model.Name}", UserId, UserDisplay, cancellationToken: cancellationToken);
        return Json(result);
    }

    [HttpPost]
    public async Task<IActionResult> SaveEntry([FromBody] SaveTradeEntryViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var result = await _trading.SaveEntryAsync(model, cancellationToken);
            await _audit.LogAsync(FarmTabs.Trading, AuditAction.Create, $"Trading {model.Type} entry", UserId, UserDisplay, cancellationToken: cancellationToken);
            return Json(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteEntry(int id, CancellationToken cancellationToken)
    {
        var pending = await ApprovalGate.GuardDeleteAsync(this, User, _approvalService, _permissionService,
            FarmTabs.Trading, "TradeEntry", id, cancellationToken);
        if (pending is not null) return pending;
        var ok = await _trading.DeleteEntryAsync(id, cancellationToken);
        if (ok)
            await _audit.LogAsync(FarmTabs.Trading, AuditAction.Delete, $"Cancelled trade entry #{id}", UserId, UserDisplay, cancellationToken: cancellationToken);
        return ok ? Ok(new { success = true }) : NotFound();
    }
}

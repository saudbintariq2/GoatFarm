using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Projects;
using GoatFarm.Domain.Constants;
using GoatFarm.Domain.Enums;
using GoatFarm.Web.Helpers;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GoatFarm.Web.Controllers;

public class ProjectsController : Controller
{
    private readonly IProjectsService _projects;
    private readonly IAuditService _audit;
    private readonly IApprovalService _approvalService;
    private readonly IPermissionService _permissionService;

    public ProjectsController(IProjectsService projects, IAuditService audit, IApprovalService approvalService, IPermissionService permissionService)
    {
        _projects = projects;
        _audit = audit;
        _approvalService = approvalService;
        _permissionService = permissionService;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
    private string UserDisplay => User.FindFirst("FullName")?.Value ?? User.Identity?.Name ?? "";

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewData["ActiveTab"] = "projects";
        return View(await _projects.GetPageAsync(cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> GetData(CancellationToken cancellationToken) =>
        Json(await _projects.GetPageAsync(cancellationToken));

    [HttpPost]
    public async Task<IActionResult> SaveProject([FromBody] SaveProjectViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var result = await _projects.SaveProjectAsync(model, cancellationToken);
        await _audit.LogAsync(FarmTabs.Projects, AuditAction.Update, $"Project {model.Name}", UserId, UserDisplay, cancellationToken: cancellationToken);
        return Json(result);
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteProject(int id, CancellationToken cancellationToken)
    {
        var pending = await ApprovalGate.GuardDeleteAsync(this, User, _approvalService, _permissionService,
            FarmTabs.Projects, "FarmProject", id, cancellationToken);
        if (pending is not null) return pending;
        var ok = await _projects.DeleteProjectAsync(id, cancellationToken);
        return ok ? Ok(new { success = true }) : NotFound();
    }

    [HttpPost]
    public async Task<IActionResult> SaveExpenditure([FromBody] SaveProjectExpenditureViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Json(await _projects.SaveExpenditureAsync(model, cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> SaveFixedAsset([FromBody] SaveFixedAssetViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Json(await _projects.SaveFixedAssetAsync(model, cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> SaveSolarLog([FromBody] SaveSolarLogViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Json(await _projects.SaveSolarLogAsync(model, cancellationToken));
    }
}

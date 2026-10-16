using System.Security.Claims;
using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Tasks;
using GoatFarm.Domain.Constants;
using GoatFarm.Web.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace GoatFarm.Web.Controllers;

public class TasksController : Controller
{
    private readonly ITaskService _taskService;
    private readonly IPermissionService _permissionService;
    private readonly IApprovalService _approvalService;

    public TasksController(ITaskService taskService, IPermissionService permissionService, IApprovalService approvalService)
    {
        _taskService = taskService;
        _permissionService = permissionService;
        _approvalService = approvalService;
    }

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    private Task<bool> CanEditAsync(CancellationToken cancellationToken) =>
        _permissionService.CanAsync(FarmTabs.Tasks, FarmActions.Edit, cancellationToken);

    [HttpGet]
    public async Task<IActionResult> Index(string? filter, CancellationToken cancellationToken)
    {
        ViewData["ActiveTab"] = "tasks";
        return View(await _taskService.GetPageAsync(CurrentUserId, await CanEditAsync(cancellationToken), filter, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> GetData(string? filter, CancellationToken cancellationToken) =>
        Json(await _taskService.GetPageAsync(CurrentUserId, await CanEditAsync(cancellationToken), filter, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveTaskViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            return Json(await _taskService.CreateAsync(model, CurrentUserId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut]
    public async Task<IActionResult> Update(int id, [FromBody] SaveTaskViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var result = await _taskService.UpdateAsync(id, model, CurrentUserId, cancellationToken);
            return result is null ? NotFound(new { error = "Task not found." }) : Json(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> SetStatus(int id, [FromBody] SetTaskStatusViewModel model, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _taskService.SetStatusAsync(id, model.Status, CurrentUserId, await CanEditAsync(cancellationToken), cancellationToken);
            return result is null ? NotFound(new { error = "Task not found." }) : Json(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var pending = await ApprovalGate.GuardDeleteAsync(this, User, _approvalService, _permissionService,
            FarmTabs.Tasks, "FarmTask", id, cancellationToken);
        if (pending is not null) return pending;
        var ok = await _taskService.DeleteAsync(id, cancellationToken);
        return ok ? Ok(new { success = true }) : NotFound(new { error = "Task not found." });
    }
}

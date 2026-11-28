using System.Security.Claims;
using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Breeding;
using GoatFarm.Application.ViewModels.Tasks;
using GoatFarm.Domain.Constants;
using GoatFarm.Domain.Enums;
using GoatFarm.Web.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace GoatFarm.Web.Controllers;

public class TasksController : Controller
{
    private readonly ITaskService _taskService;
    private readonly IBreedingService _breedingService;
    private readonly IPermissionService _permissionService;
    private readonly IApprovalService _approvalService;

    public TasksController(
        ITaskService taskService,
        IBreedingService breedingService,
        IPermissionService permissionService,
        IApprovalService approvalService)
    {
        _taskService = taskService;
        _breedingService = breedingService;
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
        return View(await BuildPageAsync(filter, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> GetData(string? filter, CancellationToken cancellationToken) =>
        Json(await BuildPageAsync(filter, cancellationToken));

    private async Task<TasksPageViewModel> BuildPageAsync(string? filter, CancellationToken cancellationToken)
    {
        var page = await _taskService.GetPageAsync(CurrentUserId, await CanEditAsync(cancellationToken), filter, cancellationToken);
        // T1 — breeding › Today now-jobs as read-only rows (filter "all" / overdue / urgent only).
        var f = string.IsNullOrWhiteSpace(filter) ? "all" : filter.Trim();
        if (f is "all" or "urgent" or "overdue")
        {
            var jobs = await _breedingService.GetNowJobsAsync(cancellationToken);
            var breedRows = MapBreedingJobs(jobs, f);
            page.ToDo = breedRows.Concat(page.ToDo).ToList();
        }

        return page;
    }

    private static IReadOnlyList<TaskViewModel> MapBreedingJobs(IReadOnlyList<BreedingNowJobViewModel> jobs, string filter)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var rows = new List<TaskViewModel>();
        var i = 0;
        foreach (var j in jobs)
        {
            i++;
            var overdue = j.SortDate < today;
            var urgent = j.UrgencyKey == "no" || overdue;
            if (filter == "urgent" && !urgent) continue;
            if (filter == "overdue" && !overdue) continue;

            var view = string.IsNullOrWhiteSpace(j.BreedView) ? "today" : j.BreedView;
            rows.Add(new TaskViewModel
            {
                Id = -i,
                Title = (string.IsNullOrWhiteSpace(j.Icon) ? "" : j.Icon + " ") + j.Text,
                Notes = string.IsNullOrWhiteSpace(j.Subtext) ? null : j.Subtext,
                Type = FarmTaskType.Task,
                Priority = urgent ? FarmTaskPriority.Urgent : FarmTaskPriority.Normal,
                Status = FarmTaskStatus.ToDo,
                DueDate = j.SortDate,
                AssigneeName = "Breeding",
                IsOverdue = overdue,
                DueText = overdue ? "overdue" : j.WhenSubtext,
                CanChangeStatus = false,
                IsReadOnly = true,
                LinkUrl = "/Breeding?view=" + Uri.EscapeDataString(view),
                LinkLabel = "Open Breeding"
            });
        }

        return rows;
    }

    [HttpGet]
    public async Task<IActionResult> MyOpenCount(CancellationToken cancellationToken) =>
        Json(new { count = await _taskService.CountMyOpenTasksAsync(CurrentUserId, cancellationToken) });

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

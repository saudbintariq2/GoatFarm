using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using GoatFarm.Application.Common;
using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Goats;
using GoatFarm.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace GoatFarm.Web.Controllers;

public class GoatController : Controller
{
    private static readonly JsonSerializerOptions GoatJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly IGoatService _goatService;
    private readonly ILookupService _lookupService;
    private readonly IApprovalService _approvalService;
    private readonly IPermissionService _permissionService;
    private readonly ITreatmentService _treatmentService;

    public GoatController(
        IGoatService goatService,
        ILookupService lookupService,
        IApprovalService approvalService,
        IPermissionService permissionService,
        ITreatmentService treatmentService)
    {
        _goatService = goatService;
        _lookupService = lookupService;
        _approvalService = approvalService;
        _permissionService = permissionService;
        _treatmentService = treatmentService;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
    private string UserDisplay => User.FindFirst("FullName")?.Value ?? User.Identity?.Name ?? "";

    [HttpGet]
    public async Task<IActionResult> Index(
        string? filter,
        int page = 1,
        string? editTag = null,
        int? editId = null,
        string? q = null,
        CancellationToken cancellationToken = default)
    {
        ViewData["ActiveTab"] = "herd";
        var model = await _goatService.GetHerdPageAsync(filter, page, search: q, cancellationToken: cancellationToken);
        ViewBag.Breeds = await _lookupService.GetListAsync(LookupSettingKeys.Breeds, cancellationToken);

        GoatViewModel? editGoat = null;
        if (editId.HasValue)
            editGoat = await _goatService.GetByIdAsync(editId.Value, cancellationToken);
        else if (!string.IsNullOrWhiteSpace(editTag))
            editGoat = await _goatService.GetByTagAsync(editTag, cancellationToken);

        ViewBag.EditGoatJson = editGoat is null
            ? "null"
            : JsonSerializer.Serialize(editGoat, GoatJsonOptions);

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(string? filter, int page = 1, string? q = null, CancellationToken cancellationToken = default) =>
        Json(await _goatService.GetHerdPageAsync(filter, page, search: q, cancellationToken: cancellationToken));

    [HttpGet]
    public async Task<IActionResult> GetByTag(string tag, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return BadRequest(new { error = "Enter a tag / RFID ID." });

        var goat = await _goatService.GetByTagAsync(tag, cancellationToken);
        return goat is null
            ? new JsonResult(new { error = $"No goat found with tag \"{tag.Trim()}\"." }) { StatusCode = StatusCodes.Status404NotFound }
            : Json(goat);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateGoatViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);
        try
        {
            return Json(await _goatService.CreateAsync(model, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut]
    public async Task<IActionResult> Update(int id, [FromBody] CreateGoatViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);
        try
        {
            var result = await _goatService.UpdateAsync(id, model, cancellationToken);
            return result is null ? NotFound() : Json(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> BulkMove([FromBody] BulkMoveViewModel model, CancellationToken cancellationToken)
    {
        try
        {
            await _goatService.BulkMoveAsync(model, cancellationToken);
            return Ok(new { success = true });
        }
        catch (ConfirmationRequiredException ex)
        {
            return Conflict(new { error = ex.Message, warnings = ex.Warnings, requiresConfirmation = true });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateGroup([FromBody] CreateGroupRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "Group name required" });
        var name = await _goatService.CreateGroupAsync(request.Name, cancellationToken);
        return Json(new { name });
    }

    [HttpDelete]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        try
        {
            var role = await _permissionService.GetCurrentUserRoleAsync(cancellationToken) ?? FarmRoles.Staff;
            if (await _approvalService.NeedsApprovalAsync(UserId, role, FarmActions.Delete, cancellationToken))
            {
                var payload = JsonSerializer.Serialize(new { entity = "Goat", action = "Delete", id });
                await _approvalService.SubmitAsync(FarmTabs.Herd, "Delete", payload, UserId, UserDisplay, cancellationToken);
                return StatusCode(StatusCodes.Status202Accepted, new
                {
                    pendingApproval = true,
                    message = "Delete submitted for manager approval."
                });
            }

            var ok = await _goatService.DeleteAsync(id, cancellationToken);
            return ok ? Ok(new { success = true }) : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> ParentOptions(CancellationToken cancellationToken) =>
        Json(await _goatService.GetParentOptionsAsync(cancellationToken));

    [HttpPost]
    public async Task<IActionResult> RecordWeight([FromBody] RecordWeightViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Json(await _goatService.RecordWeightAsync(model, cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> RecordDeath([FromBody] RecordDeathViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Json(await _goatService.RecordDeathAsync(model, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> SaleFormData(CancellationToken cancellationToken) =>
        Json(await _goatService.GetSaleFormDataAsync(cancellationToken));

    [HttpPost]
    public async Task<IActionResult> RecordSale([FromBody] RecordSaleViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Json(await _goatService.RecordSaleAsync(model, cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> RecoverTreatment(int id, CancellationToken cancellationToken)
    {
        if (!await _treatmentService.RecoverAsync(id, cancellationToken))
            return NotFound(new { error = "Treatment not found." });
        return Json(new { success = true });
    }

    public record CreateGroupRequest(string Name);
}

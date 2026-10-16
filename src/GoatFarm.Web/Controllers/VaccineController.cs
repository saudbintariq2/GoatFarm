using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Reminders;
using GoatFarm.Application.ViewModels.Treatments;
using GoatFarm.Application.ViewModels.Vaccines;
using GoatFarm.Domain.Constants;
using GoatFarm.Domain.Enums;
using GoatFarm.Web.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace GoatFarm.Web.Controllers;

public class VaccineController : Controller
{
    private readonly IVaccineService _vaccineService;
    private readonly IReminderService _reminderService;
    private readonly ILookupService _lookupService;
    private readonly ITreatmentService _treatmentService;
    private readonly IApprovalService _approvalService;
    private readonly IPermissionService _permissionService;

    public VaccineController(
        IVaccineService vaccineService,
        IReminderService reminderService,
        ILookupService lookupService,
        ITreatmentService treatmentService,
        IApprovalService approvalService,
        IPermissionService permissionService)
    {
        _vaccineService = vaccineService;
        _reminderService = reminderService;
        _lookupService = lookupService;
        _treatmentService = treatmentService;
        _approvalService = approvalService;
        _permissionService = permissionService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? remindDays, string? month, CancellationToken cancellationToken)
    {
        ViewData["ActiveTab"] = "health";
        var model = await _vaccineService.GetVaccinePageAsync(remindDays, month, cancellationToken);
        ViewBag.Reminders = await _reminderService.GetRemindersAsync(cancellationToken);
        ViewBag.VaccineNames = await _lookupService.GetListAsync(LookupSettingKeys.VaccineNames, cancellationToken);
        ViewBag.VaccineUnits = await _lookupService.GetListAsync(LookupSettingKeys.VaccineUnits, cancellationToken);
        ViewBag.Treatments = await _treatmentService.GetPageAsync(cancellationToken);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Treatments(CancellationToken cancellationToken) =>
        Json(await _treatmentService.GetPageAsync(cancellationToken));

    [HttpPost]
    public async Task<IActionResult> SaveTreatment([FromBody] SaveTreatmentViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Json(await _treatmentService.SaveAsync(model, cancellationToken));
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteTreatment(int id, CancellationToken cancellationToken)
    {
        var pending = await ApprovalGate.GuardDeleteAsync(this, User, _approvalService, _permissionService,
            FarmTabs.Vaccines, "GoatTreatment", id, cancellationToken);
        if (pending is not null) return pending;
        var ok = await _treatmentService.DeleteAsync(id, cancellationToken);
        return ok ? Ok(new { success = true }) : NotFound();
    }

    [HttpGet]
    public async Task<IActionResult> GetData(int? remindDays, string? month, CancellationToken cancellationToken)
    {
        var page = await _vaccineService.GetVaccinePageAsync(remindDays, month, cancellationToken);
        var reminders = await _reminderService.GetRemindersAsync(cancellationToken);
        return Json(new { page, reminders });
    }

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] CreateVaccineViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Json(await _vaccineService.AddVaccineAsync(model, cancellationToken));
    }

    [HttpPut]
    public async Task<IActionResult> Update(int id, [FromBody] CreateVaccineViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var result = await _vaccineService.UpdateVaccineAsync(id, model, cancellationToken);
        return result is null ? NotFound() : Json(result);
    }

    [HttpDelete]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var pending = await ApprovalGate.GuardDeleteAsync(this, User, _approvalService, _permissionService,
            FarmTabs.Vaccines, "Vaccine", id, cancellationToken);
        if (pending is not null) return pending;
        var ok = await _vaccineService.DeleteVaccineAsync(id, cancellationToken);
        return ok ? Ok(new { success = true }) : NotFound();
    }

    [HttpPost]
    public async Task<IActionResult> MarkDone(int vaccineId, CancellationToken cancellationToken)
    {
        await _vaccineService.MarkVaccineDoneAsync(vaccineId, cancellationToken);
        return Ok(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> GiveToGroup([FromBody] GiveVaccineViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var count = await _vaccineService.GiveVaccineToGroupAsync(model, cancellationToken);
        return Json(new { success = true, count });
    }

    [HttpPut]
    public async Task<IActionResult> UpdateHistoryBatch([FromBody] UpdateVaccinationBatchViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ok = await _vaccineService.UpdateVaccinationBatchAsync(model, cancellationToken);
        return ok ? Ok(new { success = true }) : NotFound();
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteHistoryBatch(int vaccineId, DateOnly date, CancellationToken cancellationToken)
    {
        var pending = await ApprovalGate.GuardDeleteAsync(this, User, _approvalService, _permissionService,
            FarmTabs.Vaccines, "VaccinationBatch", new { entity = "VaccinationBatch", action = "Delete", vaccineId, date = date.ToString("yyyy-MM-dd") }, cancellationToken);
        if (pending is not null) return pending;
        var ok = await _vaccineService.DeleteVaccinationBatchAsync(vaccineId, date, cancellationToken);
        return ok ? Ok(new { success = true }) : NotFound();
    }

    [HttpPost]
    public async Task<IActionResult> SetReminderWindow([FromBody] ReminderWindowRequest request, CancellationToken cancellationToken)
    {
        await _vaccineService.SetReminderWindowAsync(request.Days, cancellationToken);
        return Ok(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> AddPurchase([FromBody] CreateVaccinePurchaseViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Json(await _vaccineService.AddVaccinePurchaseAsync(model, cancellationToken));
    }

    [HttpPut]
    public async Task<IActionResult> UpdatePurchase(int id, [FromBody] CreateVaccinePurchaseViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var result = await _vaccineService.UpdateVaccinePurchaseAsync(id, model, cancellationToken);
        return result is null ? NotFound() : Json(result);
    }

    [HttpDelete]
    public async Task<IActionResult> DeletePurchase(int id, CancellationToken cancellationToken)
    {
        var pending = await ApprovalGate.GuardDeleteAsync(this, User, _approvalService, _permissionService,
            FarmTabs.Vaccines, "VaccinePurchase", id, cancellationToken);
        if (pending is not null) return pending;
        var ok = await _vaccineService.DeleteVaccinePurchaseAsync(id, cancellationToken);
        return ok ? Ok(new { success = true }) : NotFound();
    }

    public record ReminderWindowRequest(int Days);
}

public class ReminderController : Controller
{
    private readonly IReminderService _reminderService;
    private readonly IApprovalService _approvalService;
    private readonly IPermissionService _permissionService;

    public ReminderController(IReminderService reminderService, IApprovalService approvalService, IPermissionService permissionService)
    {
        _reminderService = reminderService;
        _approvalService = approvalService;
        _permissionService = permissionService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        Json(await _reminderService.GetRemindersAsync(cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReminderViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Json(await _reminderService.AddReminderAsync(model, cancellationToken));
    }

    [HttpPut]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateReminderViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var result = await _reminderService.UpdateReminderAsync(id, model, cancellationToken);
        return result is null ? NotFound() : Json(result);
    }

    [HttpDelete]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var pending = await ApprovalGate.GuardDeleteAsync(this, User, _approvalService, _permissionService,
            FarmTabs.Vaccines, "Reminder", id, cancellationToken);
        if (pending is not null) return pending;
        var ok = await _reminderService.DeleteReminderAsync(id, cancellationToken);
        return ok ? Ok(new { success = true }) : NotFound();
    }
}

using GoatFarm.Application.Common;
using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Breeding;
using Microsoft.AspNetCore.Mvc;

namespace GoatFarm.Web.Controllers;

public class BreedingController : Controller
{
    private readonly IBreedingService _breedingService;
    private readonly IGoatService _goatService;

    public BreedingController(IBreedingService breedingService, IGoatService goatService)
    {
        _breedingService = breedingService;
        _goatService = goatService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? batch, string? buck, CancellationToken cancellationToken)
    {
        ViewData["ActiveTab"] = "breeding";
        return View(await _breedingService.GetBreedingPageAsync(batch, buck, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> GetData(string? batch, string? buck, CancellationToken cancellationToken) =>
        Json(await _breedingService.GetBreedingPageAsync(batch, buck, cancellationToken));

    [HttpGet]
    public async Task<IActionResult> DoeProfile(int id, CancellationToken cancellationToken)
    {
        var profile = await _breedingService.GetDoeProfileAsync(id, cancellationToken);
        return profile is null ? NotFound(new { error = "Doe not found." }) : Json(profile);
    }

    [HttpGet]
    public async Task<IActionResult> LookupTag(string tag, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return BadRequest(new { error = "Enter a tag / RFID ID." });

        var goat = await _goatService.GetByTagAsync(tag, cancellationToken);
        if (goat is null)
            return NotFound(new { error = "No goat with that tag" });

        var extra = goat.Gender != Domain.Enums.GoatGender.Female ? " — note: not marked female" : "";
        if (goat.MatedDate.HasValue)
            extra = goat.KidsCount > 0 ? " — confirmed pregnant" : " — crossed, waiting for scan";

        return Json(new
        {
            tag = goat.Tag,
            name = goat.Name,
            status = goat.StatusDisplay,
            extra
        });
    }

    [HttpGet]
    public async Task<IActionResult> CheckCross(string tag, string? buckTag, DateOnly? date, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return BadRequest(new { error = "Enter a doe tag." });
        return Json(await _breedingService.CheckCrossAsync(tag, buckTag, date, cancellationToken));
    }

    [HttpPost]
    public Task<IActionResult> RecordPrep([FromBody] RecordPrepViewModel model, CancellationToken cancellationToken) =>
        Run(() => _breedingService.RecordPrepAsync(model, cancellationToken));

    [HttpPost]
    public Task<IActionResult> RecordCross([FromBody] RecordCrossViewModel model, CancellationToken cancellationToken) =>
        Run(() => _breedingService.RecordCrossAsync(model, cancellationToken));

    [HttpPost]
    public Task<IActionResult> RecordUltrasound([FromBody] RecordUltrasoundViewModel model, CancellationToken cancellationToken) =>
        Run(() => _breedingService.RecordUltrasoundAsync(model, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> RecordBirth([FromBody] RecordBirthViewModel model, CancellationToken cancellationToken)
    {
        try
        {
            return Json(await _breedingService.RecordBirthAsync(model, cancellationToken));
        }
        catch (ConfirmationRequiredException ex)
        {
            return ConfirmationRequired(ex);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost]
    public Task<IActionResult> RecordAbortion([FromBody] RecordAbortionViewModel model, CancellationToken cancellationToken) =>
        Run(() => _breedingService.RecordAbortionAsync(model, cancellationToken));

    [HttpPost]
    public Task<IActionResult> RecordReturnedToHeat([FromBody] RecordReturnedToHeatViewModel model, CancellationToken cancellationToken) =>
        Run(() => _breedingService.RecordReturnedToHeatAsync(model, cancellationToken));

    [HttpPost]
    public Task<IActionResult> MarkKidded(int id, CancellationToken cancellationToken) =>
        Run(() => _breedingService.MarkKiddedAsync(id, cancellationToken));

    [HttpPost]
    public Task<IActionResult> CrossFromPrep(int id, [FromBody] RecordCrossViewModel model, CancellationToken cancellationToken) =>
        Run(() => _breedingService.RecordCrossFromPrepAsync(id, model.Date, model.BuckTag, model.ConfirmWarnings, cancellationToken));

    [HttpDelete]
    public async Task<IActionResult> RemovePrep(int id, CancellationToken cancellationToken)
    {
        await _breedingService.RemovePrepAsync(id, cancellationToken);
        return Ok(new { success = true });
    }

    [HttpDelete]
    public async Task<IActionResult> RemoveCross(int id, CancellationToken cancellationToken)
    {
        await _breedingService.RemoveCrossAsync(id, cancellationToken);
        return Ok(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> UpdateSeason([FromBody] UpdateBreedingSeasonViewModel model, CancellationToken cancellationToken)
    {
        try
        {
            var newBatchId = await _breedingService.UpdateSeasonAsync(model, cancellationToken);
            return Ok(new
            {
                success = true,
                newBatchId,
                selectedBatchId = newBatchId ?? model.SelectedBatchId
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> ReconcilePregnancy(CancellationToken cancellationToken) =>
        Ok(await _breedingService.ReconcilePregnancyAsync(cancellationToken));

    [HttpPost]
    public Task<IActionResult> AddDoe([FromBody] BreedingAddDoeViewModel model, CancellationToken cancellationToken) =>
        Run(() => _breedingService.AddDoeToPipelineAsync(model, cancellationToken));

    [HttpPost]
    public Task<IActionResult> StageAction([FromBody] BreedingStageActionViewModel model, CancellationToken cancellationToken) =>
        RunStage(() => _breedingService.ApplyStageActionAsync(model, cancellationToken));

    [HttpPost]
    public Task<IActionResult> ClearMistake(int id, CancellationToken cancellationToken) =>
        Run(() => _breedingService.ClearBreedingMistakeAsync(id, cancellationToken));

    [HttpGet]
    public async Task<IActionResult> Groups(CancellationToken cancellationToken) =>
        Json(await _breedingService.GetBreedingGroupsAsync(cancellationToken));

    [HttpGet]
    public async Task<IActionResult> GroupCards(CancellationToken cancellationToken) =>
        Json(await _breedingService.GetBreedingGroupCardsAsync(cancellationToken));

    [HttpGet]
    public async Task<IActionResult> ScanSheet(CancellationToken cancellationToken) =>
        Json(await _breedingService.GetScanSheetAsync(cancellationToken));

    [HttpPost]
    public async Task<IActionResult> SaveScanBatch([FromBody] BulkScanSaveViewModel model, CancellationToken cancellationToken)
    {
        try
        {
            return Json(await _breedingService.SaveScanBatchAsync(model, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost]
    public Task<IActionResult> CreateGroup([FromBody] CreateBreedingGroupViewModel model, CancellationToken cancellationToken) =>
        Run(() => _breedingService.CreateBreedingGroupAsync(model, cancellationToken));

    [HttpPost]
    public Task<IActionResult> CloseGroup([FromBody] CloseBreedingGroupViewModel model, CancellationToken cancellationToken) =>
        Run(() => _breedingService.CloseBreedingGroupAsync(model, cancellationToken));

    [HttpPost]
    public Task<IActionResult> TakeBuckOut([FromBody] CloseBreedingGroupViewModel model, CancellationToken cancellationToken) =>
        Run(() => _breedingService.TakeBuckOutGroupAsync(model, cancellationToken));

    [HttpPost]
    public Task<IActionResult> PutBuckIn([FromBody] BuckInBreedingGroupViewModel model, CancellationToken cancellationToken) =>
        Run(() => _breedingService.PutBuckInGroupAsync(model, cancellationToken));

    [HttpPost]
    public Task<IActionResult> RecordBuckCheck([FromBody] RecordBuckCheckViewModel model, CancellationToken cancellationToken) =>
        Run(() => _breedingService.RecordBuckCheckAsync(model, cancellationToken));

    [HttpPost]
    public Task<IActionResult> UpdateKidField([FromBody] UpdateKidFieldViewModel model, CancellationToken cancellationToken) =>
        Run(() => _breedingService.UpdateKidFieldAsync(model, cancellationToken));

    [HttpPost]
    public Task<IActionResult> RecordKidWeanWeight([FromBody] RecordKidWeanWeightViewModel model, CancellationToken cancellationToken) =>
        Run(() => _breedingService.RecordKidWeanWeightAsync(model, cancellationToken));

    [HttpPost]
    public Task<IActionResult> BulkWeanKids([FromBody] BulkWeanKidsViewModel model, CancellationToken cancellationToken) =>
        Run(() => _breedingService.BulkWeanKidsAsync(model, cancellationToken));

    [HttpPost]
    public Task<IActionResult> UpdateKidWeanRule([FromBody] UpdateKidWeanRuleViewModel model, CancellationToken cancellationToken) =>
        Run(() => _breedingService.UpdateKidWeanRuleAsync(model, cancellationToken));

    [HttpPost]
    public Task<IActionResult> RemoveGroupMember([FromBody] BreedingGroupMemberActionViewModel model, CancellationToken cancellationToken) =>
        Run(() => _breedingService.RemoveBreedingGroupMemberAsync(model, cancellationToken));

    [HttpDelete]
    public Task<IActionResult> DeleteGroup(string id, CancellationToken cancellationToken) =>
        Run(() => _breedingService.DeleteBreedingGroupAsync(id, cancellationToken));

    private async Task<IActionResult> RunStage(Func<Task> action)
    {
        try
        {
            await action();
            return Ok(new { success = true });
        }
        catch (ConfirmationRequiredException ex)
        {
            return ConfirmationRequired(ex);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private async Task<IActionResult> Run(Func<Task> action)
    {
        try
        {
            await action();
            return Ok(new { success = true });
        }
        catch (ConfirmationRequiredException ex)
        {
            return ConfirmationRequired(ex);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private IActionResult ConfirmationRequired(ConfirmationRequiredException ex) =>
        Conflict(new { error = ex.Message, warnings = ex.Warnings, requiresConfirmation = true });
}

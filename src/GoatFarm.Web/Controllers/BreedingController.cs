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
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewData["ActiveTab"] = "breeding";
        return View(await _breedingService.GetBreedingPageAsync(cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> GetData(CancellationToken cancellationToken) =>
        Json(await _breedingService.GetBreedingPageAsync(cancellationToken));

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

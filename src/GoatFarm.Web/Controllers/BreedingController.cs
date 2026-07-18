using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Breeding;
using Microsoft.AspNetCore.Mvc;

namespace GoatFarm.Web.Controllers;

[IgnoreAntiforgeryToken]
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
        if (goat.MatedDate.HasValue) extra = " — already expecting";

        return Json(new
        {
            tag = goat.Tag,
            name = goat.Name,
            status = goat.StatusDisplay,
            extra
        });
    }

    [HttpPost]
    public async Task<IActionResult> RecordPrep([FromBody] RecordPrepViewModel model, CancellationToken cancellationToken)
    {
        try
        {
            await _breedingService.RecordPrepAsync(model, cancellationToken);
            return Ok(new { success = true });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> RecordCross([FromBody] RecordCrossViewModel model, CancellationToken cancellationToken)
    {
        try
        {
            await _breedingService.RecordCrossAsync(model, cancellationToken);
            return Ok(new { success = true });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> RecordUltrasound([FromBody] RecordUltrasoundViewModel model, CancellationToken cancellationToken)
    {
        try
        {
            await _breedingService.RecordUltrasoundAsync(model, cancellationToken);
            return Ok(new { success = true });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> MarkKidded(int id, CancellationToken cancellationToken)
    {
        try
        {
            await _breedingService.MarkKiddedAsync(id, cancellationToken);
            return Ok(new { success = true });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> CrossFromPrep(int id, [FromBody] RecordCrossViewModel model, CancellationToken cancellationToken)
    {
        try
        {
            await _breedingService.RecordCrossFromPrepAsync(id, model.Date, model.BuckTag, cancellationToken);
            return Ok(new { success = true });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

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
}

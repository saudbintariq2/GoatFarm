using System.Text.Json;
using System.Text.Json.Serialization;
using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Goats;
using GoatFarm.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace GoatFarm.Web.Controllers;

[IgnoreAntiforgeryToken]
public class GoatController : Controller
{
    private static readonly JsonSerializerOptions GoatJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly IGoatService _goatService;

    public GoatController(IGoatService goatService) => _goatService = goatService;

    [HttpGet]
    public async Task<IActionResult> Index(
        string? filter,
        int page = 1,
        string? editTag = null,
        int? editId = null,
        CancellationToken cancellationToken = default)
    {
        ViewData["ActiveTab"] = "herd";
        var model = await _goatService.GetHerdPageAsync(filter, page, cancellationToken: cancellationToken);
        ViewBag.Breeds = LookupConstants.Breeds;

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
    public async Task<IActionResult> GetAll(string? filter, int page = 1, CancellationToken cancellationToken = default) =>
        Json(await _goatService.GetHerdPageAsync(filter, page, cancellationToken: cancellationToken));

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
        var result = await _goatService.CreateAsync(model, cancellationToken);
        return Json(result);
    }

    [HttpPut]
    public async Task<IActionResult> Update(int id, [FromBody] CreateGoatViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);
        var result = await _goatService.UpdateAsync(id, model, cancellationToken);
        return result is null ? NotFound() : Json(result);
    }

    [HttpPost]
    public async Task<IActionResult> BulkMove([FromBody] BulkMoveViewModel model, CancellationToken cancellationToken)
    {
        await _goatService.BulkMoveAsync(model, cancellationToken);
        return Ok(new { success = true });
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
        var ok = await _goatService.DeleteAsync(id, cancellationToken);
        return ok ? Ok(new { success = true }) : NotFound();
    }

    public record CreateGroupRequest(string Name);
}

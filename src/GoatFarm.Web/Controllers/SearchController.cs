using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Search;
using Microsoft.AspNetCore.Mvc;

namespace GoatFarm.Web.Controllers;

[IgnoreAntiforgeryToken]
public class SearchController : Controller
{
    private readonly ISearchService _searchService;

    public SearchController(ISearchService searchService) => _searchService = searchService;

    [HttpGet]
    public async Task<IActionResult> Index(string? tag, CancellationToken cancellationToken)
    {
        ViewData["ActiveTab"] = "search";
        var model = new SearchPageViewModel { InitialTag = tag?.Trim() };

        if (!string.IsNullOrWhiteSpace(tag))
        {
            model.Profile = await _searchService.GetProfileByTagAsync(tag, cancellationToken);
            if (model.Profile is null)
                model.Error = $"No goat found with tag \"{NormalizeTagForDisplay(tag)}\".";
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Lookup(string tag, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return JsonError("Enter a tag / RFID ID.", StatusCodes.Status400BadRequest);

        var profile = await _searchService.GetProfileByTagAsync(tag, cancellationToken);
        return profile is null
            ? JsonError($"No goat found with tag \"{NormalizeTagForDisplay(tag)}\".", StatusCodes.Status404NotFound)
            : Json(profile);
    }

    private static string NormalizeTagForDisplay(string tag) =>
        new string(tag.Where(c => !char.IsControl(c)).ToArray()).Trim();

    private JsonResult JsonError(string message, int statusCode) =>
        new(new { error = message }) { StatusCode = statusCode };
}

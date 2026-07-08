using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Feed;
using Microsoft.AspNetCore.Mvc;

namespace GoatFarm.Web.Controllers;

[IgnoreAntiforgeryToken]
public class FeedController : Controller
{
    private readonly IFeedService _feedService;

    public FeedController(IFeedService feedService) => _feedService = feedService;

    [HttpGet]
    public async Task<IActionResult> Index(string? status, CancellationToken cancellationToken)
    {
        ViewData["ActiveTab"] = "feed";
        return View(await _feedService.GetFeedPageAsync(status, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> GetData(string? status, CancellationToken cancellationToken) =>
        Json(await _feedService.GetFeedPageAsync(status, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> UpdatePrice([FromBody] UpdatePriceRequest request, CancellationToken cancellationToken)
    {
        await _feedService.UpdateFeedPriceAsync(request.FeedType, request.Price, cancellationToken);
        return Ok(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> UpdatePlan([FromBody] UpdateFeedPlanViewModel model, CancellationToken cancellationToken)
    {
        await _feedService.UpdateFeedPlanAsync(model, cancellationToken);
        return Ok(new { success = true });
    }

    public record UpdatePriceRequest(string FeedType, decimal Price);
}

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
    public async Task<IActionResult> Index(string? status, string? month, CancellationToken cancellationToken)
    {
        ViewData["ActiveTab"] = "feed";
        return View(await _feedService.GetFeedPageAsync(status, month, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> GetData(string? status, string? month, CancellationToken cancellationToken) =>
        Json(await _feedService.GetFeedPageAsync(status, month, cancellationToken));

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

    [HttpPost]
    public async Task<IActionResult> AddPurchase([FromBody] CreateFeedPurchaseViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Json(await _feedService.AddFeedPurchaseAsync(model, cancellationToken));
    }

    [HttpPut]
    public async Task<IActionResult> UpdatePurchase(int id, [FromBody] CreateFeedPurchaseViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var result = await _feedService.UpdateFeedPurchaseAsync(id, model, cancellationToken);
        return result is null ? NotFound() : Json(result);
    }

    [HttpDelete]
    public async Task<IActionResult> DeletePurchase(int id, CancellationToken cancellationToken)
    {
        var ok = await _feedService.DeleteFeedPurchaseAsync(id, cancellationToken);
        return ok ? Ok(new { success = true }) : NotFound();
    }

    [HttpPost]
    public async Task<IActionResult> AddFeedType([FromBody] AddFeedTypeViewModel model, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(model.DisplayName)) return BadRequest(new { error = "Enter feed name" });
        return Json(await _feedService.AddFeedTypeAsync(model, cancellationToken));
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteFeedType(string feedType, CancellationToken cancellationToken)
    {
        var ok = await _feedService.DeleteFeedTypeAsync(feedType, cancellationToken);
        return ok ? Ok(new { success = true }) : NotFound();
    }

    public record UpdatePriceRequest(string FeedType, decimal Price);
}

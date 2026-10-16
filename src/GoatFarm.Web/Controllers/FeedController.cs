using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Feed;
using GoatFarm.Domain.Constants;
using GoatFarm.Web.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace GoatFarm.Web.Controllers;

public class FeedController : Controller
{
    private readonly IFeedService _feedService;
    private readonly IApprovalService _approvalService;
    private readonly IPermissionService _permissionService;

    public FeedController(IFeedService feedService, IApprovalService approvalService, IPermissionService permissionService)
    {
        _feedService = feedService;
        _approvalService = approvalService;
        _permissionService = permissionService;
    }



    [HttpGet]

    public async Task<IActionResult> Index(string? status, string? month, string? tab, CancellationToken cancellationToken)

    {

        ViewData["ActiveTab"] = "feed";

        return View(await _feedService.GetFeedPageAsync(status, month, tab, cancellationToken));

    }



    [HttpGet]

    public async Task<IActionResult> GetData(string? status, string? month, string? tab, CancellationToken cancellationToken) =>

        Json(await _feedService.GetFeedPageAsync(status, month, tab, cancellationToken));



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

    public async Task<IActionResult> UpdateMixRecipe([FromBody] UpdateMixRecipeViewModel model, CancellationToken cancellationToken)

    {

        await _feedService.UpdateMixRecipeAsync(model, cancellationToken);

        return Ok(new { success = true });

    }



    [HttpGet]

    public async Task<IActionResult> GetMixRecipe(string status, CancellationToken cancellationToken) =>

        Json(await _feedService.GetMixRecipeForStatusAsync(status, cancellationToken));



    [HttpPost]

    public async Task<IActionResult> UpdateMixRecipeForStatus([FromBody] UpdateMixRecipeForStatusViewModel model, CancellationToken cancellationToken)

    {

        await _feedService.UpdateMixRecipeForStatusAsync(model, cancellationToken);

        return Ok(new { success = true });

    }



    [HttpGet]

    public async Task<IActionResult> GetFodderPool(CancellationToken cancellationToken) =>

        Json(await _feedService.GetFodderPoolAsync(cancellationToken));



    [HttpPost]

    public async Task<IActionResult> UpdateFodderPool([FromBody] UpdateFodderPoolViewModel model, CancellationToken cancellationToken) =>

        Json(await _feedService.UpdateFodderPoolAsync(model, cancellationToken));



    [HttpPost]

    public async Task<IActionResult> AddFodderPoolItem([FromBody] AddFodderPoolItemViewModel model, CancellationToken cancellationToken)

    {

        try

        {

            return Json(await _feedService.AddFodderPoolItemAsync(model, cancellationToken));

        }

        catch (InvalidOperationException ex)

        {

            return BadRequest(new { error = ex.Message });

        }

    }



    [HttpDelete]

    public async Task<IActionResult> RemoveFodderPoolItem(string itemId, CancellationToken cancellationToken) =>

        Json(await _feedService.RemoveFodderPoolItemAsync(itemId, cancellationToken));



    [HttpGet]

    public async Task<IActionResult> GetFeedSettings(CancellationToken cancellationToken) =>

        Json(await _feedService.GetFeedSettingsAsync(cancellationToken));



    [HttpPost]

    public async Task<IActionResult> UpdateFeedSettings([FromBody] UpdateFeedSettingsViewModel model, CancellationToken cancellationToken) =>

        Json(await _feedService.UpdateFeedSettingsAsync(model, cancellationToken));



    [HttpPost]

    public async Task<IActionResult> RunAutoUsage(CancellationToken cancellationToken) =>

        Json(new { daysApplied = await _feedService.RunAutoUsageAsync(cancellationToken) });



    [HttpPost]

    public async Task<IActionResult> SaveStockCheck([FromBody] SaveStockCheckViewModel model, CancellationToken cancellationToken) =>

        Json(await _feedService.SaveStockCheckAsync(model, cancellationToken));



    [HttpPost]

    public async Task<IActionResult> AddPurchase([FromBody] CreateFeedPurchaseViewModel model, CancellationToken cancellationToken)

    {

        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            return Json(await _feedService.AddFeedPurchaseAsync(model, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

    }



    [HttpPut]

    public async Task<IActionResult> UpdatePurchase(int id, [FromBody] CreateFeedPurchaseViewModel model, CancellationToken cancellationToken)

    {

        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var result = await _feedService.UpdateFeedPurchaseAsync(id, model, cancellationToken);
            return result is null ? NotFound() : Json(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

    }



    [HttpDelete]

    public async Task<IActionResult> DeletePurchase(int id, CancellationToken cancellationToken)
    {
        var pending = await ApprovalGate.GuardDeleteAsync(this, User, _approvalService, _permissionService,
            FarmTabs.Feed, "FeedPurchase", id, cancellationToken);
        if (pending is not null) return pending;
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
        var pending = await ApprovalGate.GuardDeleteAsync(this, User, _approvalService, _permissionService,
            FarmTabs.Feed, "FeedType", new { entity = "FeedType", action = "Delete", feedType }, cancellationToken);
        if (pending is not null) return pending;
        var ok = await _feedService.DeleteFeedTypeAsync(feedType, cancellationToken);
        return ok ? Ok(new { success = true }) : NotFound();
    }



    [HttpPost]

    public async Task<IActionResult> UpdateStock([FromBody] UpdateFeedStockViewModel model, CancellationToken cancellationToken)

    {

        await _feedService.UpdateFeedStockAsync(model, cancellationToken);

        return Ok(new { success = true });

    }



    public record UpdatePriceRequest(string FeedType, decimal Price);

}



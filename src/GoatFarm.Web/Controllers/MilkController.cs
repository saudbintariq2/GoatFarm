using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Milk;
using GoatFarm.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace GoatFarm.Web.Controllers;

[IgnoreAntiforgeryToken]
public class MilkController : Controller
{
    private readonly IMilkService _milkService;

    public MilkController(IMilkService milkService) => _milkService = milkService;

    [HttpGet]
    public async Task<IActionResult> Index(int prodPage = 1, int salePage = 1, int wastePage = 1, CancellationToken cancellationToken = default)
    {
        ViewData["ActiveTab"] = "milk";
        ViewBag.MilkBreeds = LookupConstants.MilkBreeds;
        var model = await _milkService.GetMilkPageAsync(prodPage, salePage, wastePage, cancellationToken: cancellationToken);
        ViewBag.ProdPage = model.ProdPage;
        ViewBag.SalePage = model.SalePage;
        ViewBag.WastePage = model.WastePage;
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> GetData(int prodPage = 1, int salePage = 1, int wastePage = 1, CancellationToken cancellationToken = default) =>
        Json(await _milkService.GetMilkPageAsync(prodPage, salePage, wastePage, cancellationToken: cancellationToken));

    [HttpPost]
    public async Task<IActionResult> AddProduction([FromBody] CreateMilkProductionViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Json(await _milkService.AddProductionAsync(model, cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> AddSale([FromBody] CreateMilkSaleViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Json(await _milkService.AddSaleAsync(model, cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> AddWaste([FromBody] CreateMilkWasteViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Json(await _milkService.AddWasteAsync(model, cancellationToken));
    }

    [HttpPut]
    public async Task<IActionResult> UpdateProduction(int id, [FromBody] CreateMilkProductionViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var result = await _milkService.UpdateProductionAsync(id, model, cancellationToken);
        return result is null ? NotFound() : Json(result);
    }

    [HttpPut]
    public async Task<IActionResult> UpdateSale(int id, [FromBody] CreateMilkSaleViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var result = await _milkService.UpdateSaleAsync(id, model, cancellationToken);
        return result is null ? NotFound() : Json(result);
    }

    [HttpPut]
    public async Task<IActionResult> UpdateWaste(int id, [FromBody] CreateMilkWasteViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var result = await _milkService.UpdateWasteAsync(id, model, cancellationToken);
        return result is null ? NotFound() : Json(result);
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteProduction(int id, CancellationToken cancellationToken)
    {
        var ok = await _milkService.DeleteProductionAsync(id, cancellationToken);
        return ok ? Ok(new { success = true }) : NotFound();
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteSale(int id, CancellationToken cancellationToken)
    {
        var ok = await _milkService.DeleteSaleAsync(id, cancellationToken);
        return ok ? Ok(new { success = true }) : NotFound();
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteWaste(int id, CancellationToken cancellationToken)
    {
        var ok = await _milkService.DeleteWasteAsync(id, cancellationToken);
        return ok ? Ok(new { success = true }) : NotFound();
    }
}

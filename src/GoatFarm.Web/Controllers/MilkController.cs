using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Milk;
using GoatFarm.Domain.Constants;
using GoatFarm.Web.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace GoatFarm.Web.Controllers;

public class MilkController : Controller
{
    private readonly IMilkService _milkService;
    private readonly ILookupService _lookupService;
    private readonly IApprovalService _approvalService;
    private readonly IPermissionService _permissionService;

    public MilkController(
        IMilkService milkService,
        ILookupService lookupService,
        IApprovalService approvalService,
        IPermissionService permissionService)
    {
        _milkService = milkService;
        _lookupService = lookupService;
        _approvalService = approvalService;
        _permissionService = permissionService;
    }

    [HttpGet]
    public IActionResult Index(int prodPage = 1, int salePage = 1, int wastePage = 1)
    {
        // A7 — legacy Milk tab removed from nav; milk work lives under Products.
        return RedirectToAction("Index", "Products");
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
    public IActionResult AddSale([FromBody] CreateMilkSaleViewModel model)
    {
        // N2 — milk sales belong in Products › Sales only (avoids double income).
        return Conflict(new { error = "Record milk sales under Products › Sales. The legacy Milk sale form is closed." });
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
    public IActionResult UpdateSale(int id, [FromBody] CreateMilkSaleViewModel model)
    {
        return Conflict(new { error = "Record milk sales under Products › Sales. The legacy Milk sale form is closed." });
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
        var pending = await ApprovalGate.GuardDeleteAsync(this, User, _approvalService, _permissionService,
            FarmTabs.Milk, "MilkProduction", id, cancellationToken);
        if (pending is not null) return pending;
        var ok = await _milkService.DeleteProductionAsync(id, cancellationToken);
        return ok ? Ok(new { success = true }) : NotFound();
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteSale(int id, CancellationToken cancellationToken)
    {
        var pending = await ApprovalGate.GuardDeleteAsync(this, User, _approvalService, _permissionService,
            FarmTabs.Milk, "MilkSale", id, cancellationToken);
        if (pending is not null) return pending;
        var ok = await _milkService.DeleteSaleAsync(id, cancellationToken);
        return ok ? Ok(new { success = true }) : NotFound();
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteWaste(int id, CancellationToken cancellationToken)
    {
        var pending = await ApprovalGate.GuardDeleteAsync(this, User, _approvalService, _permissionService,
            FarmTabs.Milk, "MilkWaste", id, cancellationToken);
        if (pending is not null) return pending;
        var ok = await _milkService.DeleteWasteAsync(id, cancellationToken);
        return ok ? Ok(new { success = true }) : NotFound();
    }
}

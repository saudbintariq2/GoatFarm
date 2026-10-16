using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Products;
using GoatFarm.Domain.Constants;
using GoatFarm.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GoatFarm.Web.Controllers;

public class ProductsController : Controller
{
    private readonly IProductsService _products;
    private readonly IAuditService _audit;

    public ProductsController(IProductsService products, IAuditService audit)
    {
        _products = products;
        _audit = audit;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
    private string UserDisplay => User.FindFirst("FullName")?.Value ?? User.Identity?.Name ?? "";

    [HttpGet]
    public async Task<IActionResult> Index(string? month, CancellationToken cancellationToken)
    {
        ViewData["ActiveTab"] = "products";
        return View(await _products.GetPageAsync(month, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> GetData(string? month, CancellationToken cancellationToken) =>
        Json(await _products.GetPageAsync(month, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> SaveCatalog([FromBody] SaveProductCatalogViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Json(await _products.SaveCatalogItemAsync(model, cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> SaveWarehouse([FromBody] SaveWarehouseViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Json(await _products.SaveWarehouseAsync(model, cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> SaveStockMovement([FromBody] SaveStockMovementViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        await _audit.LogAsync(FarmTabs.Products, AuditAction.Create, "Stock movement", UserId, UserDisplay, cancellationToken: cancellationToken);
        return Json(await _products.SaveStockMovementAsync(model, cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> SaveMilkingSession([FromBody] SaveMilkingSessionViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Json(await _products.SaveMilkingSessionAsync(model, cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> SaveSlaughter([FromBody] SaveSlaughterViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Json(await _products.SaveSlaughterAsync(model, cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> SaveSale([FromBody] SaveProductSaleViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Json(await _products.SaveProductSaleAsync(model, cancellationToken));
    }
}

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
        try
        {
            if (model.Id is > 0)
                await _audit.LogAsync(FarmTabs.Products, AuditAction.Update, "Product catalogue item", UserId, UserDisplay, cancellationToken: cancellationToken);
            else
                await _audit.LogAsync(FarmTabs.Products, AuditAction.Create, "Product catalogue item", UserId, UserDisplay, cancellationToken: cancellationToken);
            return Json(await _products.SaveCatalogItemAsync(model, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> DeleteCatalog([FromBody] DeleteTransportShipmentRequest model, CancellationToken cancellationToken)
    {
        if (model.Id <= 0) return BadRequest(new { message = "Invalid product id." });
        try
        {
            await _audit.LogAsync(FarmTabs.Products, AuditAction.Delete, "Product catalogue item", UserId, UserDisplay, cancellationToken: cancellationToken);
            return Json(new { ok = await _products.DeleteCatalogItemAsync(model.Id, cancellationToken) });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
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
        try
        {
            return Json(await _products.SaveSlaughterAsync(model, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> SaveMeatSplit([FromBody] SaveMeatSplitViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            await _audit.LogAsync(FarmTabs.Products, AuditAction.Create, "Meat carcass split", UserId, UserDisplay, cancellationToken: cancellationToken);
            return Json(await _products.SaveMeatSplitAsync(model, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> SaveMeatCut([FromBody] SaveMeatCutViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            await _audit.LogAsync(FarmTabs.Products, AuditAction.Create, "Meat carcass cut", UserId, UserDisplay, cancellationToken: cancellationToken);
            return Json(await _products.SaveMeatCutAsync(model, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> SaveSale([FromBody] SaveProductSaleViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            return Json(await _products.SaveProductSaleAsync(model, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> SaveMilkingSessionNames([FromBody] string[] sessions, CancellationToken cancellationToken)
    {
        await _products.SaveCustomMilkingSessionsAsync(sessions ?? [], cancellationToken);
        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> SaveProductDropdownOptions([FromBody] Dictionary<string, List<string>> options, CancellationToken cancellationToken)
    {
        await _products.SaveProductDropdownOptionsAsync(options ?? new Dictionary<string, List<string>>(), cancellationToken);
        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> SavePasteurization([FromBody] SavePasteurizationViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            await _audit.LogAsync(FarmTabs.Products, AuditAction.Create, "Pasteurization", UserId, UserDisplay, cancellationToken: cancellationToken);
            return Json(await _products.SavePasteurizationAsync(model, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> SaveTransportShipment([FromBody] SaveTransportShipmentViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            await _audit.LogAsync(FarmTabs.Products, AuditAction.Create, "Transport shipment", UserId, UserDisplay, cancellationToken: cancellationToken);
            return Json(await _products.SaveTransportShipmentAsync(model, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> SaveProductCostAllocation([FromBody] SaveProductCostAllocationViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            await _audit.LogAsync(FarmTabs.Products, AuditAction.Create, "Product cost allocation", UserId, UserDisplay, cancellationToken: cancellationToken);
            return Json(await _products.SaveProductCostAllocationAsync(model, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> DeleteProductCostAllocation([FromBody] DeleteTransportShipmentRequest model, CancellationToken cancellationToken)
    {
        if (model.Id <= 0) return BadRequest(new { message = "Invalid allocation id." });
        await _audit.LogAsync(FarmTabs.Products, AuditAction.Delete, "Product cost allocation", UserId, UserDisplay, cancellationToken: cancellationToken);
        return Json(new { ok = await _products.DeleteProductCostAllocationAsync(model.Id, cancellationToken) });
    }

    [HttpPost]
    public async Task<IActionResult> SaveProductPricing([FromBody] SaveProductPricingViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            await _audit.LogAsync(FarmTabs.Products, AuditAction.Update, "Product pricing", UserId, UserDisplay, cancellationToken: cancellationToken);
            return Json(await _products.SaveProductPricingAsync(model, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> DeleteProductPricing([FromBody] SaveProductPricingViewModel model, CancellationToken cancellationToken)
    {
        if (model.ProductId <= 0) return BadRequest(new { message = "Select a product." });
        await _audit.LogAsync(FarmTabs.Products, AuditAction.Delete, "Product pricing profile", UserId, UserDisplay, cancellationToken: cancellationToken);
        return Json(new { ok = await _products.DeleteProductPricingAsync(model.ProductId, cancellationToken) });
    }

    [HttpPost]
    public async Task<IActionResult> SaveDeliveryRoute([FromBody] SaveDeliveryRouteViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            await _audit.LogAsync(FarmTabs.Products, AuditAction.Create, "Delivery route pricing", UserId, UserDisplay, cancellationToken: cancellationToken);
            return Json(await _products.SaveDeliveryRouteAsync(model, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> DeleteDeliveryRoute([FromBody] DeleteTransportShipmentRequest model, CancellationToken cancellationToken)
    {
        if (model.Id <= 0) return BadRequest(new { message = "Invalid route id." });
        await _audit.LogAsync(FarmTabs.Products, AuditAction.Delete, "Delivery route pricing", UserId, UserDisplay, cancellationToken: cancellationToken);
        return Json(new { ok = await _products.DeleteDeliveryRouteAsync(model.Id, cancellationToken) });
    }

    [HttpPost]
    public async Task<IActionResult> SaveClientProductPrice([FromBody] SaveClientProductPriceViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            await _audit.LogAsync(FarmTabs.Products, AuditAction.Update, "Client product price", UserId, UserDisplay, cancellationToken: cancellationToken);
            return Json(await _products.SaveClientProductPriceAsync(model, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> DeleteClientProductPrice([FromBody] DeleteTransportShipmentRequest model, CancellationToken cancellationToken)
    {
        if (model.Id <= 0) return BadRequest(new { message = "Invalid client price id." });
        await _audit.LogAsync(FarmTabs.Products, AuditAction.Delete, "Client product price", UserId, UserDisplay, cancellationToken: cancellationToken);
        return Json(new { ok = await _products.DeleteClientProductPriceAsync(model.Id, cancellationToken) });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteTransportShipment([FromBody] DeleteTransportShipmentRequest model, CancellationToken cancellationToken)
    {
        if (model.Id <= 0) return BadRequest(new { message = "Invalid shipment id." });
        await _audit.LogAsync(FarmTabs.Products, AuditAction.Delete, "Transport shipment", UserId, UserDisplay, cancellationToken: cancellationToken);
        return Json(new { ok = await _products.DeleteTransportShipmentAsync(model.Id, cancellationToken) });
    }
}

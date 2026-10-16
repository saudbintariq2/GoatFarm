using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Products;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Infrastructure.Services.Extended;

public class ProductsService : IProductsService
{
    private readonly GoatFarmDbContext _context;

    public ProductsService(GoatFarmDbContext context) => _context = context;

    public async Task EnsureSeededAsync(CancellationToken cancellationToken = default)
    {
        if (await _context.ProductCatalogItems.AnyAsync(cancellationToken)) return;
        _context.ProductCatalogItems.Add(new ProductCatalogItem { Sku = "MILK-RAW", Name = "Raw milk", Unit = "L", DefaultPrice = 0 });
        _context.ProductCatalogItems.Add(new ProductCatalogItem { Sku = "MEAT", Name = "Goat meat", Unit = "kg", DefaultPrice = 0 });
        _context.Warehouses.Add(new Warehouse { Name = "Main cold store" });
        _context.Warehouses.Add(new Warehouse { Name = "Pasteurization tank" });
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<ProductsPageViewModel> GetPageAsync(string? month, CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken);
        var m = ParseMonth(month);
        var monthStart = new DateOnly(m.Year, m.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        var catalog = await _context.ProductCatalogItems.AsNoTracking().OrderBy(c => c.Name).ToListAsync(cancellationToken);
        var warehouses = await _context.Warehouses.AsNoTracking().OrderBy(w => w.Name).ToListAsync(cancellationToken);
        var moves = await _context.StockMovements.AsNoTracking().Include(s => s.Product).Include(s => s.Warehouse)
            .OrderByDescending(s => s.Date).Take(120).ToListAsync(cancellationToken);
        var sessions = await _context.MilkingSessions.AsNoTracking()
            .Where(s => s.Date >= monthStart && s.Date <= monthEnd)
            .OrderByDescending(s => s.Date).ToListAsync(cancellationToken);
        var slaughters = await _context.SlaughterRecords.AsNoTracking().Include(s => s.Goat)
            .OrderByDescending(s => s.Date).Take(60).ToListAsync(cancellationToken);
        var sales = await _context.ProductSales.AsNoTracking().Include(s => s.Customer)
            .Where(s => s.Date >= monthStart && s.Date <= monthEnd)
            .OrderByDescending(s => s.Date).ToListAsync(cancellationToken);
        var customers = await _context.BusinessCustomers.AsNoTracking()
            .Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync(cancellationToken);

        var stockValue = ComputeStockValue(catalog, moves);

        return new ProductsPageViewModel
        {
            Month = $"{m.Year:0000}-{m.Month:00}",
            StockValue = stockValue,
            Customers = customers.Select(c => new ProductCustomerRowViewModel { Id = c.Id, Name = c.Name }).ToList(),
            Catalog = catalog.Select(c => new ProductCatalogRowViewModel
            {
                Id = c.Id, Sku = c.Sku, Name = c.Name, Unit = c.Unit, DefaultPrice = c.DefaultPrice
            }).ToList(),
            Warehouses = warehouses.Select(w => new WarehouseRowViewModel { Id = w.Id, Name = w.Name, Location = w.Location }).ToList(),
            Movements = moves.Select(mv => new StockMovementRowViewModel
            {
                Id = mv.Id,
                DateDisplay = mv.Date.ToString("yyyy-MM-dd"),
                Type = mv.Type.ToString(),
                Product = mv.Product.Name,
                Warehouse = mv.Warehouse.Name,
                Qty = mv.Qty
            }).ToList(),
            Sessions = sessions.Select(s => new MilkingSessionRowViewModel
            {
                Id = s.Id,
                DateDisplay = s.Date.ToString("yyyy-MM-dd"),
                Session = s.Session,
                Breed = s.Breed,
                Liters = s.Liters,
                RejectedLiters = s.RejectedLiters
            }).ToList(),
            Slaughters = slaughters.Select(s => new SlaughterRowViewModel
            {
                Id = s.Id,
                DateDisplay = s.Date.ToString("yyyy-MM-dd"),
                Tag = s.Goat?.Tag ?? s.TagText ?? "",
                CarcassKg = s.CarcassKg,
                SaleAmount = s.SaleAmount
            }).ToList(),
            Sales = sales.Select(s => new ProductSaleRowViewModel
            {
                Id = s.Id,
                DateDisplay = s.Date.ToString("yyyy-MM-dd"),
                ProductName = s.ProductName,
                Customer = s.Customer?.Name,
                Qty = s.Qty,
                Amount = s.Amount,
                IsCredit = s.IsCredit
            }).ToList()
        };
    }

    public async Task<object> SaveCatalogItemAsync(SaveProductCatalogViewModel model, CancellationToken cancellationToken = default)
    {
        ProductCatalogItem row;
        if (model.Id is > 0)
            row = await _context.ProductCatalogItems.FirstAsync(c => c.Id == model.Id, cancellationToken);
        else { row = new ProductCatalogItem(); _context.ProductCatalogItems.Add(row); }
        row.Sku = model.Sku.Trim();
        row.Name = model.Name.Trim();
        row.Unit = model.Unit.Trim();
        row.DefaultPrice = model.DefaultPrice;
        row.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return new { id = row.Id };
    }

    public async Task<object> SaveWarehouseAsync(SaveWarehouseViewModel model, CancellationToken cancellationToken = default)
    {
        Warehouse row;
        if (model.Id is > 0)
            row = await _context.Warehouses.FirstAsync(w => w.Id == model.Id, cancellationToken);
        else { row = new Warehouse(); _context.Warehouses.Add(row); }
        row.Name = model.Name.Trim();
        row.Location = model.Location;
        row.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return new { id = row.Id };
    }

    public async Task<object> SaveStockMovementAsync(SaveStockMovementViewModel model, CancellationToken cancellationToken = default)
    {
        StockMovement row;
        if (model.Id is > 0)
            row = await _context.StockMovements.FirstAsync(s => s.Id == model.Id, cancellationToken);
        else { row = new StockMovement(); _context.StockMovements.Add(row); }
        row.Date = model.Date;
        row.Type = model.Type;
        row.ProductId = model.ProductId;
        row.WarehouseId = model.WarehouseId;
        row.ToWarehouseId = model.ToWarehouseId;
        row.Qty = model.Qty;
        row.UnitCost = model.UnitCost;
        row.Notes = model.Notes;
        row.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return new { id = row.Id };
    }

    public async Task<object> SaveMilkingSessionAsync(SaveMilkingSessionViewModel model, CancellationToken cancellationToken = default)
    {
        MilkingSession row;
        if (model.Id is > 0)
            row = await _context.MilkingSessions.FirstAsync(s => s.Id == model.Id, cancellationToken);
        else { row = new MilkingSession(); _context.MilkingSessions.Add(row); }
        row.Date = model.Date;
        row.Session = model.Session;
        row.Breed = model.Breed;
        row.Liters = model.Liters;
        row.RejectedLiters = model.RejectedLiters;
        row.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return new { id = row.Id };
    }

    public async Task<object> SaveSlaughterAsync(SaveSlaughterViewModel model, CancellationToken cancellationToken = default)
    {
        SlaughterRecord row;
        if (model.Id is > 0)
            row = await _context.SlaughterRecords.FirstAsync(s => s.Id == model.Id, cancellationToken);
        else { row = new SlaughterRecord(); _context.SlaughterRecords.Add(row); }
        row.Date = model.Date;
        row.GoatId = model.GoatId;
        row.TagText = model.TagText;
        row.LiveWeightKg = model.LiveWeightKg;
        row.CarcassKg = model.CarcassKg;
        row.SaleAmount = model.SaleAmount;
        row.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return new { id = row.Id };
    }

    public async Task<object> SaveProductSaleAsync(SaveProductSaleViewModel model, CancellationToken cancellationToken = default)
    {
        var amount = model.Qty * model.Rate;
        ProductSale row;
        if (model.Id is > 0)
            row = await _context.ProductSales.FirstAsync(s => s.Id == model.Id, cancellationToken);
        else { row = new ProductSale(); _context.ProductSales.Add(row); }
        row.Date = model.Date;
        row.CustomerId = model.CustomerId;
        row.ProductId = model.ProductId;
        row.ProductName = model.ProductName.Trim();
        row.Qty = model.Qty;
        row.Rate = model.Rate;
        row.Amount = amount;
        row.IsCredit = model.IsCredit;
        row.Notes = model.Notes;
        row.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        int? invoiceId = null;
        if (model.IsCredit && model.CustomerId is > 0 && amount > 0)
        {
            var due = model.Date.AddDays(14);
            var desc = $"Product sale: {row.ProductName} × {model.Qty}";
            if (!string.IsNullOrWhiteSpace(model.Notes)) desc += " — " + model.Notes.Trim();
            var inv = new CustomerInvoice
            {
                CustomerId = model.CustomerId.Value,
                Date = model.Date,
                DueDate = due,
                Description = desc,
                Amount = amount,
                PaidAmount = 0,
                Status = InvoiceStatus.Open,
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow
            };
            _context.CustomerInvoices.Add(inv);
            await _context.SaveChangesAsync(cancellationToken);
            invoiceId = inv.Id;
        }

        return new { id = row.Id, invoiceId };
    }

    private static decimal ComputeStockValue(IReadOnlyList<ProductCatalogItem> catalog, IReadOnlyList<StockMovement> moves)
    {
        var qty = new Dictionary<(int product, int wh), decimal>();
        foreach (var m in moves.OrderBy(x => x.Date).ThenBy(x => x.Id))
        {
            var key = (m.ProductId, m.WarehouseId);
            qty.TryAdd(key, 0);
            qty[key] += m.Type switch
            {
                StockMovementType.Receive or StockMovementType.Adjust => m.Qty,
                StockMovementType.Issue or StockMovementType.Spoilage => -m.Qty,
                StockMovementType.Transfer => -m.Qty,
                _ => 0
            };
            if (m.Type == StockMovementType.Transfer && m.ToWarehouseId is > 0)
            {
                var key2 = (m.ProductId, m.ToWarehouseId.Value);
                qty.TryAdd(key2, 0);
                qty[key2] += m.Qty;
            }
        }

        var price = catalog.ToDictionary(c => c.Id, c => c.DefaultPrice);
        decimal total = 0;
        foreach (var ((pid, _), q) in qty)
        {
            if (q <= 0) continue;
            total += q * price.GetValueOrDefault(pid, 0);
        }
        return total;
    }

    private static DateTime ParseMonth(string? month)
    {
        if (!string.IsNullOrWhiteSpace(month) && DateTime.TryParse(month + "-01", out var parsed))
            return parsed;
        return DateTime.Today;
    }
}

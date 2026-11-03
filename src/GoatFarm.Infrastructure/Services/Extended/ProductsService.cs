using System.Text.Json;
using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Ledger;
using GoatFarm.Application.ViewModels.Products;
using GoatFarm.Domain.Constants;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Infrastructure.Services.Extended;

public class ProductsService : IProductsService
{
    private readonly GoatFarmDbContext _context;
    private readonly ILedgerService _ledger;

    public ProductsService(GoatFarmDbContext context, ILedgerService ledger)
    {
        _context = context;
        _ledger = ledger;
    }

    public async Task EnsureSeededAsync(CancellationToken cancellationToken = default)
    {
        if (!await _context.ProductCatalogItems.AnyAsync(cancellationToken))
        {
            _context.ProductCatalogItems.Add(new ProductCatalogItem { Sku = "MILK-RAW", Name = "Raw milk (chilled)", Unit = "L", DefaultPrice = 0 });
            _context.ProductCatalogItems.Add(new ProductCatalogItem { Sku = "MILK-PAST", Name = "Pasteurized milk (bulk)", Unit = "L", DefaultPrice = 0 });
            _context.ProductCatalogItems.Add(new ProductCatalogItem { Sku = "MEAT", Name = "Goat meat", Unit = "kg", DefaultPrice = 0 });
        }
        else
        {
            if (!await _context.ProductCatalogItems.AnyAsync(c => c.Sku == "MILK-PAST", cancellationToken))
                _context.ProductCatalogItems.Add(new ProductCatalogItem { Sku = "MILK-PAST", Name = "Pasteurized milk (bulk)", Unit = "L", DefaultPrice = 0 });
        }

        await EnsureMilkTanksAsync(cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureMilkTanksAsync(CancellationToken cancellationToken)
    {
        var wh = await _context.Warehouses.ToListAsync(cancellationToken);
        if (!wh.Any(w => InferKind(w) == "raw_tank"))
            _context.Warehouses.Add(new Warehouse { Name = "Farm Raw Milk Tank", Location = "raw_tank" });
        if (!wh.Any(w => InferKind(w) == "past_tank"))
            _context.Warehouses.Add(new Warehouse { Name = "Pasteurized Milk Tank 1", Location = "past_tank" });
        if (!wh.Any(w => InferKind(w) == "dairy_store"))
            _context.Warehouses.Add(new Warehouse { Name = "Dairy cold store", Location = "dairy_store" });
        if (!wh.Any(w => InferKind(w) == "meat_store"))
            _context.Warehouses.Add(new Warehouse { Name = "Meat chiller", Location = "meat_store" });
    }

    public async Task<ProductsPageViewModel> GetPageAsync(string? month, CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken);
        var ledgerPage = await _ledger.GetPageAsync(month, cancellationToken);
        var m = ParseMonth(month);
        var monthStart = new DateOnly(m.Year, m.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        var catalog = await _context.ProductCatalogItems.AsNoTracking().OrderBy(c => c.Name).ToListAsync(cancellationToken);
        var warehouses = await _context.Warehouses.AsNoTracking().OrderBy(w => w.Name).ToListAsync(cancellationToken);
        var allMoves = await _context.StockMovements.AsNoTracking()
            .Include(s => s.Product).Include(s => s.Warehouse).Include(s => s.ToWarehouse)
            .ToListAsync(cancellationToken);
        var legacyMilk = await _context.MilkProductions.AsNoTracking()
            .OrderByDescending(p => p.Date).ThenByDescending(p => p.Id)
            .Take(120).ToListAsync(cancellationToken);
        var moves = allMoves.OrderByDescending(s => s.Date).Take(120).ToList();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var allSessions = await _context.MilkingSessions.AsNoTracking().ToListAsync(cancellationToken);
        var sessions = allSessions
            .Where(s => s.Date >= monthStart && s.Date <= monthEnd)
            .OrderByDescending(s => s.Date).ToList();
        var recentSessions = allSessions
            .OrderByDescending(s => s.Date).ThenByDescending(s => s.Id)
            .Take(150).ToList();
        var customMilkingSessions = await LoadCustomMilkingSessionsAsync(cancellationToken);
        var productDropdownOptions = await LoadProductDropdownOptionsAsync(cancellationToken);
        var milkProdMonth = await _context.MilkProductions.AsNoTracking()
            .Where(p => p.Date >= monthStart && p.Date <= monthEnd).ToListAsync(cancellationToken);
        var milkProdToday = milkProdMonth.Where(p => p.Date == today).Sum(p => p.Liters);
        var milkSalesMonth = await _context.MilkSales.AsNoTracking()
            .Where(s => s.Date >= monthStart && s.Date <= monthEnd).ToListAsync(cancellationToken);
        var milkWasteMonth = await _context.MilkWastes.AsNoTracking()
            .Where(w => w.Date >= monthStart && w.Date <= monthEnd).ToListAsync(cancellationToken);
        var allSlaughters = await _context.SlaughterRecords.AsNoTracking().Include(s => s.Goat)
            .OrderByDescending(s => s.Date).ToListAsync(cancellationToken);
        var slaughters = allSlaughters.Take(60).ToList();
        var allLegacyMilk = await _context.MilkProductions.AsNoTracking()
            .OrderByDescending(p => p.Date).ToListAsync(cancellationToken);
        var goatSalesAnalytics = await (
            from s in _context.GoatSales.AsNoTracking()
            join g in _context.Goats.AsNoTracking() on s.GoatId equals g.Id into gj
            from g in gj.DefaultIfEmpty()
            orderby s.Date descending
            select new BreedPerformanceGoatSaleRowViewModel
            {
                DateDisplay = s.Date.ToString("yyyy-MM-dd"),
                Breed = !string.IsNullOrWhiteSpace(s.Breed) ? s.Breed! : (g != null ? g.Breed : ""),
                Tag = s.Tag,
                Price = s.Price,
                WeightKg = s.WeightKg,
                Cost = g != null ? g.PurchasePrice : 0
            }).ToListAsync(cancellationToken);
        var sales = await _context.ProductSales.AsNoTracking().Include(s => s.Customer)
            .Where(s => s.Date >= monthStart && s.Date <= monthEnd)
            .OrderByDescending(s => s.Date).ToListAsync(cancellationToken);
        var customers = await _context.BusinessCustomers.AsNoTracking()
            .Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync(cancellationToken);
        var goatRows = await _context.Goats.AsNoTracking()
            .Where(g => !g.IsArchived)
            .OrderBy(g => g.Breed).ThenBy(g => g.Tag)
            .Select(g => new
            {
                g.Id,
                g.Tag,
                g.Name,
                g.Breed,
                g.Gender,
                g.Status,
                g.PurchasePrice,
                LiveWeightKg = g.WeightRecords
                    .OrderByDescending(w => w.Date)
                    .ThenByDescending(w => w.Id)
                    .Select(w => (decimal?)w.Kg)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);
        var herdGoats = goatRows.Select(g => new ProductMilkingGoatRowViewModel
        {
            Id = g.Id,
            Tag = g.Tag,
            Name = g.Name,
            Breed = g.Breed,
            Gender = g.Gender == GoatGender.Male ? "Male" : "Female",
            StatusKey = GoatStatusKey(g.Status),
            Status = GoatStatusLabel(g.Status),
            PurchasePrice = g.PurchasePrice,
            LiveWeightKg = g.LiveWeightKg ?? 0
        }).ToList();

        var stockValue = ComputeStockValue(catalog, allMoves);
        var monthSalesTotal = sales.Sum(s => s.Amount);
        var balances = ComputeBalances(allMoves);
        var rawProductId = catalog.FirstOrDefault(c => c.Sku.Equals("MILK-RAW", StringComparison.OrdinalIgnoreCase))?.Id;
        var pastProductId = catalog.FirstOrDefault(c => c.Sku.Equals("MILK-PAST", StringComparison.OrdinalIgnoreCase))?.Id;

        var whRows = warehouses.Select(w =>
        {
            var kind = InferKind(w);
            var qty = SumWarehouseQty(balances, w.Id, kind, rawProductId, pastProductId, catalog);
            return new WarehouseRowViewModel
            {
                Id = w.Id,
                Name = w.Name,
                Location = w.Location,
                Kind = kind,
                LitersOrKg = qty,
                Capacity = kind is "raw_tank" or "past_tank" ? 500 : null,
                TargetTempC = kind is "raw_tank" or "past_tank" ? 4 : null,
                IsActive = w.IsActive
            };
        }).ToList();

        var milkedTodaySessions = allSessions.Where(s => s.Date == today).Sum(s => Math.Max(0, s.Liters - s.RejectedLiters));
        var milkedMonthSessions = allSessions.Where(s => s.Date >= monthStart && s.Date <= monthEnd)
            .Sum(s => Math.Max(0, s.Liters - s.RejectedLiters));
        var milkedToday = milkProdToday + milkedTodaySessions;
        var milkedMonth = milkProdMonth.Sum(p => p.Liters) + milkedMonthSessions;
        var bulkSoldMonth = milkSalesMonth.Sum(s => s.Liters) + sales
            .Where(s => s.ProductName.Contains("milk", StringComparison.OrdinalIgnoreCase) ||
                        s.ProductName.Contains("Milk", StringComparison.OrdinalIgnoreCase))
            .Sum(s => s.Qty);

        var rawTankLiters = whRows.Where(w => w.Kind == "raw_tank").Sum(w => w.LitersOrKg);
        var pastTankLiters = whRows.Where(w => w.Kind == "past_tank").Sum(w => w.LitersOrKg);
        if (rawTankLiters <= 0 && pastTankLiters <= 0)
        {
            var ledgerLeft = milkProdMonth.Sum(p => p.Liters) + milkedMonthSessions
                             - milkSalesMonth.Sum(s => s.Liters) - milkWasteMonth.Sum(w => w.Liters);
            rawTankLiters = Math.Max(0, ledgerLeft);
        }

        SyncTankDisplayQty(whRows, "raw_tank", rawTankLiters);
        SyncTankDisplayQty(whRows, "past_tank", pastTankLiters);

        var pasteurizeMoves = allMoves.Where(mv => mv.Type == StockMovementType.Transfer
            && mv.Date >= monthStart && mv.Date <= monthEnd
            && rawProductId is > 0 && pastProductId is > 0
            && mv.ProductId == rawProductId && mv.ToWarehouseId.HasValue).ToList();

        var dairyProducts = balances
            .Where(b => b.qty > 0.0001m)
            .Select(b =>
            {
                var p = catalog.FirstOrDefault(c => c.Id == b.productId);
                if (p is null || p.Sku.StartsWith("MILK-", StringComparison.OrdinalIgnoreCase)) return null;
                var wh = warehouses.FirstOrDefault(w => w.Id == b.warehouseId);
                if (wh is null || InferKind(wh) != "dairy_store") return null;
                return new ProductsDairyStockRowViewModel
                {
                    ProductId = p.Id,
                    WarehouseId = b.warehouseId,
                    Name = p.Name,
                    Unit = p.Unit,
                    Qty = b.qty
                };
            })
            .Where(x => x != null)
            .Cast<ProductsDairyStockRowViewModel>()
            .ToList();

        var milkDash = new ProductsMilkDashboardViewModel
        {
            RawTankLiters = rawTankLiters,
            PastTankLiters = pastTankLiters,
            MilkedTodayLiters = milkedToday,
            MilkedMonthLiters = milkedMonth,
            BulkSoldMonthLiters = bulkSoldMonth,
            PasteurizeRunsMonth = pasteurizeMoves.Count,
            PasteurizeInMonth = pasteurizeMoves.Sum(m => m.Qty),
            PasteurizeOutMonth = pasteurizeMoves.Sum(m => m.Qty),
            DairyProducts = dairyProducts
        };

        var meatMonth = slaughters.Where(s => s.Date >= monthStart && s.Date <= monthEnd).ToList();
        var meatDash = new ProductsMeatDashboardViewModel
        {
            WholeCount = slaughters.Count,
            WholeKg = slaughters.Sum(s => s.CarcassKg),
            SlaughteredMonth = meatMonth.Count,
            SlaughteredCarcassKgMonth = meatMonth.Sum(s => s.CarcassKg)
        };

        var milkInTanks = rawTankLiters + pastTankLiters;

        return new ProductsPageViewModel
        {
            Month = $"{m.Year:0000}-{m.Month:00}",
            StockValue = stockValue,
            MonthSalesTotal = monthSalesTotal,
            MilkInTanksLiters = milkInTanks,
            Milk = milkDash,
            Meat = meatDash,
            CustomMilkingSessions = customMilkingSessions,
            ProductDropdownOptions = productDropdownOptions,
            RecentSessions = MapSessions(recentSessions),
            HerdGoats = herdGoats,
            MoneyAccounts = ledgerPage.Accounts,
            Customers = customers.Select(c => new ProductCustomerRowViewModel { Id = c.Id, Name = c.Name }).ToList(),
            Catalog = catalog.Select(c => new ProductCatalogRowViewModel
            {
                Id = c.Id,
                Sku = c.Sku,
                Name = c.Name,
                Category = c.Category,
                Pack = c.PackSize,
                Unit = c.Unit,
                DefaultPrice = c.DefaultPrice,
                MilkTreatment = c.MilkTreatment,
                MeatType = c.MeatType,
                IsActive = c.IsActive
            }).ToList(),
            Warehouses = whRows,
            Movements = moves.Select(mv => new StockMovementRowViewModel
            {
                Id = mv.Id,
                ProductId = mv.ProductId,
                DateDisplay = mv.Date.ToString("yyyy-MM-dd"),
                Type = mv.Type.ToString(),
                Product = mv.Product.Name,
                ProductSku = mv.Product.Sku,
                Warehouse = mv.Warehouse.Name,
                ToWarehouse = mv.ToWarehouse?.Name,
                Qty = mv.Qty,
                Notes = mv.Notes
            }).ToList(),
            LegacyMilkEntries = legacyMilk.Select(p => new MilkLegacyRowViewModel
            {
                Id = p.Id,
                DateDisplay = p.Date.ToString("yyyy-MM-dd"),
                Breed = p.Breed,
                Liters = p.Liters,
                Comment = p.Comment
            }).ToList(),
            Sessions = MapSessions(sessions),
            Slaughters = slaughters.Select(MapSlaughterRow).ToList(),
            AnalyticsMilkingSessions = MapSessions(allSessions),
            AnalyticsLegacyMilk = allLegacyMilk.Select(p => new MilkLegacyRowViewModel
            {
                Id = p.Id,
                DateDisplay = p.Date.ToString("yyyy-MM-dd"),
                Breed = p.Breed,
                Liters = p.Liters,
                Comment = p.Comment
            }).ToList(),
            AnalyticsSlaughters = allSlaughters.Select(MapSlaughterRow).ToList(),
            AnalyticsGoatSales = goatSalesAnalytics,
            TransportShipments = (await _context.TransportShipments.AsNoTracking()
                .OrderByDescending(t => t.Date).ThenByDescending(t => t.Id)
                .Take(120)
                .ToListAsync(cancellationToken))
                .Select(MapTransportRow).ToList(),
            CostAllocations = (await _context.ProductCostAllocations.AsNoTracking()
                .OrderByDescending(c => c.Date).ThenByDescending(c => c.Id)
                .Take(120)
                .ToListAsync(cancellationToken))
                .Select(c => MapCostAllocationRow(c, catalog)).ToList(),
            ProductPricing = (await _context.ProductPricingProfiles.AsNoTracking().ToListAsync(cancellationToken))
                .Select(p => new ProductPricingRowViewModel
                {
                    ProductId = p.ProductId,
                    Base = p.BaseCost,
                    Packaging = p.PackagingCost,
                    Transport = p.TransportCost,
                    Storage = p.StorageCost,
                    Other = p.OtherCost,
                    Sell = p.SellPrice
                }).ToList(),
            DeliveryRoutes = (await _context.DeliveryRoutePricings.AsNoTracking()
                .Include(r => r.Product)
                .OrderBy(r => r.Name)
                .ToListAsync(cancellationToken))
                .Select(r => new DeliveryRouteRowViewModel
                {
                    Id = r.Id,
                    Name = r.Name,
                    ProductId = r.ProductId,
                    ProductName = r.Product.Name,
                    Qty = r.TypicalQty,
                    Cost = r.TransportCost
                }).ToList(),
            ClientPrices = (await _context.ClientProductPrices.AsNoTracking()
                .Include(x => x.Customer)
                .Include(x => x.Product)
                .OrderBy(x => x.Customer.Name)
                .ToListAsync(cancellationToken))
                .Select(x => new ClientProductPriceRowViewModel
                {
                    Id = x.Id,
                    ClientId = x.CustomerId,
                    ClientName = x.Customer.Name,
                    ProductId = x.ProductId,
                    ProductName = x.Product.Name,
                    Rate = x.Rate
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
        row.Unit = (model.Unit ?? "L").Trim();
        row.DefaultPrice = model.DefaultPrice;
        if (!string.IsNullOrWhiteSpace(model.Category))
            row.Category = model.Category.Trim();
        row.PackSize = string.IsNullOrWhiteSpace(model.Pack) ? null : model.Pack.Trim();
        row.MilkTreatment = string.IsNullOrWhiteSpace(model.MilkTreatment) ? null : model.MilkTreatment.Trim();
        row.MeatType = string.IsNullOrWhiteSpace(model.MeatType) ? null : model.MeatType.Trim();
        if (model.IsActive.HasValue)
            row.IsActive = model.IsActive.Value;
        else if (model.Id is not > 0)
            row.IsActive = true;
        row.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return new { id = row.Id };
    }

    public async Task<bool> DeleteCatalogItemAsync(int id, CancellationToken cancellationToken = default)
    {
        var row = await _context.ProductCatalogItems.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (row is null) return false;

        var sku = row.Sku.Trim().ToUpperInvariant();
        if (sku is "MILK-RAW" or "MILK-PAST" or "MEAT")
            throw new InvalidOperationException("Built-in system products cannot be deleted.");

        if (await _context.StockMovements.AnyAsync(m => m.ProductId == id, cancellationToken))
            throw new InvalidOperationException("This product has stock movements. Disable it instead of deleting.");

        if (await _context.ProductSales.AnyAsync(s => s.ProductId == id, cancellationToken))
            throw new InvalidOperationException("This product has sales records. Disable it instead of deleting.");

        var pricing = await _context.ProductPricingProfiles.Where(p => p.ProductId == id).ToListAsync(cancellationToken);
        if (pricing.Count > 0) _context.ProductPricingProfiles.RemoveRange(pricing);

        var routes = await _context.DeliveryRoutePricings.Where(r => r.ProductId == id).ToListAsync(cancellationToken);
        if (routes.Count > 0) _context.DeliveryRoutePricings.RemoveRange(routes);

        var clientRates = await _context.ClientProductPrices.Where(x => x.ProductId == id).ToListAsync(cancellationToken);
        if (clientRates.Count > 0) _context.ClientProductPrices.RemoveRange(clientRates);

        var allocations = await _context.ProductCostAllocations.Where(c => c.ProductId == id).ToListAsync(cancellationToken);
        foreach (var a in allocations)
            a.ProductId = null;

        _context.ProductCatalogItems.Remove(row);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<object> SaveWarehouseAsync(SaveWarehouseViewModel model, CancellationToken cancellationToken = default)
    {
        Warehouse row;
        if (model.Id is > 0)
            row = await _context.Warehouses.FirstAsync(w => w.Id == model.Id, cancellationToken);
        else { row = new Warehouse(); _context.Warehouses.Add(row); }
        row.Name = model.Name.Trim();
        if (!string.IsNullOrWhiteSpace(model.Location))
            row.Location = model.Location.Trim();
        if (model.IsActive.HasValue)
            row.IsActive = model.IsActive.Value;
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

        var usable = Math.Max(0, model.Liters - model.RejectedLiters);
        if (usable > 0 && model.Id is null or <= 0)
        {
            var raw = await _context.ProductCatalogItems.FirstOrDefaultAsync(c => c.Sku == "MILK-RAW", cancellationToken);
            var wh = model.WarehouseId is > 0
                ? await _context.Warehouses.FirstOrDefaultAsync(w => w.Id == model.WarehouseId, cancellationToken)
                : await _context.Warehouses.FirstOrDefaultAsync(w => w.Location == "raw_tank", cancellationToken);
            if (raw != null && wh != null)
            {
                _context.StockMovements.Add(new StockMovement
                {
                    Date = model.Date,
                    Type = StockMovementType.Receive,
                    ProductId = raw.Id,
                    WarehouseId = wh.Id,
                    Qty = usable,
                    UnitCost = 0,
                    Notes = $"Milking session {row.Session}",
                    CreatedDate = DateTime.UtcNow,
                    UpdatedDate = DateTime.UtcNow
                });
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

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

    public async Task<object> SavePasteurizationAsync(SavePasteurizationViewModel model, CancellationToken cancellationToken = default)
    {
        if (model.InputLiters <= 0 || model.OutputLiters <= 0)
            throw new InvalidOperationException("Enter raw and pasteurized litres.");
        if (model.OutputLiters > model.InputLiters)
            throw new InvalidOperationException("Pasteurized litres cannot exceed raw litres taken.");

        var rawWh = await _context.Warehouses.FirstOrDefaultAsync(w => w.Id == model.SourceWarehouseId && w.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("Choose a valid raw milk tank.");
        var pastWh = await _context.Warehouses.FirstOrDefaultAsync(w => w.Id == model.DestWarehouseId && w.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("Choose a valid pasteurized milk tank.");
        if (InferKind(rawWh) != "raw_tank" || InferKind(pastWh) != "past_tank")
            throw new InvalidOperationException("Pasteurization must go from a raw tank to a pasteurized tank.");

        var rawProduct = await _context.ProductCatalogItems.FirstOrDefaultAsync(c => c.Sku == "MILK-RAW", cancellationToken)
            ?? throw new InvalidOperationException("Raw milk product (MILK-RAW) is not set up.");
        var pastProduct = await _context.ProductCatalogItems.FirstOrDefaultAsync(c => c.Sku == "MILK-PAST", cancellationToken)
            ?? throw new InvalidOperationException("Pasteurized milk product (MILK-PAST) is not set up.");

        var runRef = string.IsNullOrWhiteSpace(model.RunRef) ? $"PZ-{model.Date:yyyyMMdd}" : model.RunRef.Trim();
        var meta = new List<string> { runRef };
        if (!string.IsNullOrWhiteSpace(model.HeatTempC)) meta.Add($"heat {model.HeatTempC}°C");
        if (!string.IsNullOrWhiteSpace(model.CoolTempC)) meta.Add($"cool {model.CoolTempC}°C");
        if (!string.IsNullOrWhiteSpace(model.Note)) meta.Add(model.Note.Trim());
        var note = string.Join(" · ", meta);

        _context.StockMovements.Add(new StockMovement
        {
            Date = model.Date,
            Type = StockMovementType.Issue,
            ProductId = rawProduct.Id,
            WarehouseId = rawWh.Id,
            Qty = model.InputLiters,
            Notes = note
        });
        _context.StockMovements.Add(new StockMovement
        {
            Date = model.Date,
            Type = StockMovementType.Receive,
            ProductId = pastProduct.Id,
            WarehouseId = pastWh.Id,
            Qty = model.OutputLiters,
            Notes = note
        });

        if (model.ProcessCost > 0)
        {
            if (model.PaidFromAccountId is not > 0)
                throw new InvalidOperationException("Choose Paid from: Cash or a Bank account (same as Accounting → Expenses).");
            var acct = await _context.MoneyAccounts.FirstOrDefaultAsync(
                a => a.Id == model.PaidFromAccountId && a.IsActive, cancellationToken);
            if (acct is null)
                throw new InvalidOperationException("That payment account was not found. Set up accounts under Finance → Bank & Cash.");

            const string expenseType = "Milk processing";
            _context.Expenses.Add(new Expense
            {
                Type = expenseType,
                Amount = model.ProcessCost,
                Date = model.Date,
                Comment = $"{runRef} · Pasteurization{(string.IsNullOrWhiteSpace(model.Note) ? "" : " · " + model.Note.Trim())}"
            });
            _context.LedgerTransactions.Add(new LedgerTransaction
            {
                Date = model.Date,
                Kind = LedgerTransactionKind.Expense,
                AccountId = acct.Id,
                Amount = model.ProcessCost,
                Category = expenseType,
                Notes = $"Pasteurization {runRef} · {rawWh.Name} → {pastWh.Name}"
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
        return new { ok = true, runRef };
    }

    public async Task<object> SaveTransportShipmentAsync(SaveTransportShipmentViewModel model, CancellationToken cancellationToken = default)
    {
        if (model.Qty <= 0) throw new InvalidOperationException("Enter the total quantity moved.");
        if (string.IsNullOrWhiteSpace(model.ToLocation)) throw new InvalidOperationException("Enter the destination city or route.");
        if (model.Cost < 0) throw new InvalidOperationException("Transport cost cannot be negative.");

        var cpu = model.Qty > 0 ? model.Cost / model.Qty : 0;
        var refNo = string.IsNullOrWhiteSpace(model.DispatchRef)
            ? $"TR-{DateTime.UtcNow:yyyyMMddHHmmss}"
            : model.DispatchRef.Trim();
        var from = string.IsNullOrWhiteSpace(model.FromLocation) ? "Farm" : model.FromLocation.Trim();
        var to = model.ToLocation.Trim();
        var noteParts = new List<string> { refNo, $"{model.ShipmentType} {model.Qty} {model.Unit}", $"{from} to {to}", $"{cpu:N2}/{model.Unit}" };
        if (!string.IsNullOrWhiteSpace(model.Note)) noteParts.Add(model.Note.Trim());
        var expenseComment = string.Join(" · ", noteParts);

        var row = new TransportShipment
        {
            Date = model.Date,
            ShipmentType = model.ShipmentType.Trim(),
            Qty = model.Qty,
            Unit = model.Unit.Trim(),
            FromLocation = from,
            ToLocation = to,
            Cost = model.Cost,
            CostPerUnit = cpu,
            Transporter = model.Transporter?.Trim(),
            DispatchRef = refNo,
            Note = model.Note?.Trim(),
            PaidFromAccountId = model.PaidFromAccountId,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };
        _context.TransportShipments.Add(row);

        if (model.Cost > 0)
        {
            const string expenseType = "Transport / delivery";
            var expense = new Expense
            {
                Type = expenseType,
                Amount = model.Cost,
                Date = model.Date,
                Comment = expenseComment,
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow
            };
            _context.Expenses.Add(expense);
            await _context.SaveChangesAsync(cancellationToken);
            row.ExpenseId = expense.Id;

            if (model.PaidFromAccountId is > 0)
            {
                var acct = await _context.MoneyAccounts.FirstOrDefaultAsync(
                    a => a.Id == model.PaidFromAccountId && a.IsActive, cancellationToken);
                if (acct is null)
                    throw new InvalidOperationException("That payment account was not found. Set up accounts under Finance → Bank & Cash.");
                var tx = new LedgerTransaction
                {
                    Date = model.Date,
                    Kind = LedgerTransactionKind.Expense,
                    AccountId = acct.Id,
                    Amount = model.Cost,
                    Category = expenseType,
                    Notes = $"Transport {refNo} · {model.ShipmentType} · {from} to {to}",
                    CreatedDate = DateTime.UtcNow,
                    UpdatedDate = DateTime.UtcNow
                };
                _context.LedgerTransactions.Add(tx);
                await _context.SaveChangesAsync(cancellationToken);
                row.LedgerTransactionId = tx.Id;
            }
        }

        row.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return new { id = row.Id, dispatchRef = refNo };
    }

    public async Task<bool> DeleteTransportShipmentAsync(int id, CancellationToken cancellationToken = default)
    {
        var row = await _context.TransportShipments.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (row is null) return false;
        if (row.ExpenseId is > 0)
        {
            var exp = await _context.Expenses.FirstOrDefaultAsync(e => e.Id == row.ExpenseId, cancellationToken);
            if (exp is not null) _context.Expenses.Remove(exp);
        }
        if (row.LedgerTransactionId is > 0)
        {
            var tx = await _context.LedgerTransactions.FirstOrDefaultAsync(t => t.Id == row.LedgerTransactionId, cancellationToken);
            if (tx is not null) _context.LedgerTransactions.Remove(tx);
        }
        _context.TransportShipments.Remove(row);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<object> SaveProductCostAllocationAsync(SaveProductCostAllocationViewModel model, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(model.CostType)) throw new InvalidOperationException("Choose a cost type.");
        if (model.Amount <= 0) throw new InvalidOperationException("Enter the amount.");
        var scope = (model.Scope ?? "general").Trim().ToLowerInvariant();
        if (scope == "product" && model.ProductId is not > 0)
            throw new InvalidOperationException("Choose the product for this allocation.");

        var productId = model.ProductId;
        if (productId is not > 0 && !string.IsNullOrWhiteSpace(model.Ref) &&
            (scope == "shipment" || scope == "batch"))
        {
            var refText = model.Ref.Trim();
            if (int.TryParse(refText, out var moveId))
            {
                var move = await _context.StockMovements.AsNoTracking()
                    .FirstOrDefaultAsync(m => m.Id == moveId, cancellationToken);
                if (move is not null) productId = move.ProductId;
            }
        }

        var noteParts = new List<string>();
        if (scope != "general") noteParts.Add($"Allocated: {scope}" + (string.IsNullOrWhiteSpace(model.Ref) ? "" : " " + model.Ref.Trim()));
        if (!string.IsNullOrWhiteSpace(model.Note)) noteParts.Add(model.Note.Trim());
        var expenseComment = noteParts.Count > 0 ? string.Join(" · ", noteParts) : model.CostType.Trim();

        var row = new ProductCostAllocation
        {
            Date = model.Date,
            CostType = model.CostType.Trim(),
            Amount = model.Amount,
            Scope = scope,
            ProductId = productId,
            Ref = model.Ref?.Trim(),
            Qty = model.Qty,
            Note = model.Note?.Trim(),
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };
        _context.ProductCostAllocations.Add(row);

        var expense = new Expense
        {
            Type = model.CostType.Trim(),
            Amount = model.Amount,
            Date = model.Date,
            Comment = expenseComment,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };
        _context.Expenses.Add(expense);
        await _context.SaveChangesAsync(cancellationToken);
        row.ExpenseId = expense.Id;
        row.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return new { id = row.Id };
    }

    public async Task<bool> DeleteProductCostAllocationAsync(int id, CancellationToken cancellationToken = default)
    {
        var row = await _context.ProductCostAllocations.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (row is null) return false;
        if (row.ExpenseId is > 0)
        {
            var exp = await _context.Expenses.FirstOrDefaultAsync(e => e.Id == row.ExpenseId, cancellationToken);
            if (exp is not null) _context.Expenses.Remove(exp);
        }
        _context.ProductCostAllocations.Remove(row);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<object> SaveProductPricingAsync(SaveProductPricingViewModel model, CancellationToken cancellationToken = default)
    {
        if (model.ProductId <= 0) throw new InvalidOperationException("Select a product.");
        var product = await _context.ProductCatalogItems.FirstOrDefaultAsync(p => p.Id == model.ProductId, cancellationToken)
            ?? throw new InvalidOperationException("Product not found.");
        var row = await _context.ProductPricingProfiles.FirstOrDefaultAsync(p => p.ProductId == model.ProductId, cancellationToken);
        if (row is null)
        {
            row = new ProductPricingProfile { ProductId = model.ProductId, CreatedDate = DateTime.UtcNow };
            _context.ProductPricingProfiles.Add(row);
        }
        row.BaseCost = model.Base;
        row.PackagingCost = model.Packaging;
        row.TransportCost = model.Transport;
        row.StorageCost = model.Storage;
        row.OtherCost = model.Other;
        row.SellPrice = model.Sell;
        row.UpdatedDate = DateTime.UtcNow;
        if (model.Sell > 0)
        {
            product.DefaultPrice = model.Sell;
            product.UpdatedDate = DateTime.UtcNow;
        }
        await _context.SaveChangesAsync(cancellationToken);
        return new { id = row.Id, productId = row.ProductId };
    }

    public async Task<bool> DeleteProductPricingAsync(int productId, CancellationToken cancellationToken = default)
    {
        if (productId <= 0) return false;
        var row = await _context.ProductPricingProfiles.FirstOrDefaultAsync(p => p.ProductId == productId, cancellationToken);
        if (row is null) return false;
        _context.ProductPricingProfiles.Remove(row);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<object> SaveDeliveryRouteAsync(SaveDeliveryRouteViewModel model, CancellationToken cancellationToken = default)
    {
        var name = (model.Name ?? "").Trim();
        if (name.Length == 0) throw new InvalidOperationException("Enter destination / route name.");
        if (model.ProductId <= 0) throw new InvalidOperationException("Select a product.");
        if (model.Qty <= 0) throw new InvalidOperationException("Enter typical shipment quantity.");
        if (model.Cost < 0) throw new InvalidOperationException("Enter transport cost.");
        var row = new DeliveryRoutePricing
        {
            Name = name,
            ProductId = model.ProductId,
            TypicalQty = model.Qty,
            TransportCost = model.Cost,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };
        _context.DeliveryRoutePricings.Add(row);
        await _context.SaveChangesAsync(cancellationToken);
        return new { id = row.Id };
    }

    public async Task<bool> DeleteDeliveryRouteAsync(int id, CancellationToken cancellationToken = default)
    {
        var row = await _context.DeliveryRoutePricings.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (row is null) return false;
        _context.DeliveryRoutePricings.Remove(row);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<object> SaveClientProductPriceAsync(SaveClientProductPriceViewModel model, CancellationToken cancellationToken = default)
    {
        if (model.ClientId <= 0) throw new InvalidOperationException("Select a client.");
        if (model.ProductId <= 0) throw new InvalidOperationException("Select a product.");
        if (model.Rate <= 0) throw new InvalidOperationException("Enter special rate.");
        var row = await _context.ClientProductPrices
            .FirstOrDefaultAsync(x => x.CustomerId == model.ClientId && x.ProductId == model.ProductId, cancellationToken);
        if (row is null)
        {
            row = new ClientProductPrice
            {
                CustomerId = model.ClientId,
                ProductId = model.ProductId,
                CreatedDate = DateTime.UtcNow
            };
            _context.ClientProductPrices.Add(row);
        }
        row.Rate = model.Rate;
        row.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return new { id = row.Id };
    }

    public async Task<bool> DeleteClientProductPriceAsync(int id, CancellationToken cancellationToken = default)
    {
        var row = await _context.ClientProductPrices.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (row is null) return false;
        _context.ClientProductPrices.Remove(row);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task SaveProductDropdownOptionsAsync(Dictionary<string, List<string>> options, CancellationToken cancellationToken = default)
    {
        var cleaned = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, values) in options)
        {
            if (string.IsNullOrWhiteSpace(key)) continue;
            var list = (values ?? [])
                .Select(v => v.Trim())
                .Where(v => v.Length > 0 && !v.StartsWith("__", StringComparison.Ordinal))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (list.Count > 0) cleaned[key] = list;
        }
        var json = JsonSerializer.Serialize(cleaned);
        var row = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == AppSettingKeys.ProductDropdownOptions, cancellationToken);
        if (row is null)
            _context.AppSettings.Add(new AppSetting { Key = AppSettingKeys.ProductDropdownOptions, Value = json });
        else
            row.Value = json;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveCustomMilkingSessionsAsync(IReadOnlyList<string> sessions, CancellationToken cancellationToken = default)
    {
        var cleaned = sessions
            .Select(s => s.Trim())
            .Where(s => s.Length > 0 && !s.StartsWith("__", StringComparison.Ordinal))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var json = JsonSerializer.Serialize(cleaned);
        var row = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == AppSettingKeys.ProductMilkingSessions, cancellationToken);
        if (row is null)
            _context.AppSettings.Add(new AppSetting { Key = AppSettingKeys.ProductMilkingSessions, Value = json });
        else
            row.Value = json;
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<string>> LoadCustomMilkingSessionsAsync(CancellationToken cancellationToken)
    {
        var row = await _context.AppSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == AppSettingKeys.ProductMilkingSessions, cancellationToken);
        if (string.IsNullOrWhiteSpace(row?.Value)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(row.Value)?
                .Where(s => !string.IsNullOrWhiteSpace(s)).ToList() ?? [];
        }
        catch { return []; }
    }

    private async Task<Dictionary<string, List<string>>> LoadProductDropdownOptionsAsync(CancellationToken cancellationToken)
    {
        var row = await _context.AppSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == AppSettingKeys.ProductDropdownOptions, cancellationToken);
        if (string.IsNullOrWhiteSpace(row?.Value)) return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, List<string>>>(row.Value)
                   ?? new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string GoatStatusKey(GoatStatus status) => status switch
    {
        GoatStatus.Kid => "kid",
        GoatStatus.Milking => "milking",
        GoatStatus.Pregnant => "pregnant",
        GoatStatus.Dry => "dry",
        GoatStatus.Buck => "buck",
        GoatStatus.Sale => "sale",
        GoatStatus.Meat => "meat",
        _ => status.ToString().ToLowerInvariant()
    };

    private static string GoatStatusLabel(GoatStatus status) => status switch
    {
        GoatStatus.Kid => "Kid",
        GoatStatus.Milking => "Milking",
        GoatStatus.Pregnant => "Pregnant",
        GoatStatus.Dry => "Dry",
        GoatStatus.Buck => "Buck",
        GoatStatus.Sale => "For sale",
        GoatStatus.Meat => "Meat",
        _ => status.ToString()
    };

    private static ProductCostAllocationRowViewModel MapCostAllocationRow(
        ProductCostAllocation c, IReadOnlyList<ProductCatalogItem> catalog)
    {
        var p = c.ProductId is > 0 ? catalog.FirstOrDefault(x => x.Id == c.ProductId) : null;
        return new ProductCostAllocationRowViewModel
        {
            Id = c.Id,
            DateDisplay = c.Date.ToString("yyyy-MM-dd"),
            CostType = c.CostType,
            Amount = c.Amount,
            Scope = c.Scope,
            ProductId = c.ProductId,
            ProductName = p?.Name,
            Ref = c.Ref,
            Qty = c.Qty,
            Note = c.Note
        };
    }

    private static TransportShipmentRowViewModel MapTransportRow(TransportShipment t) => new()
    {
        Id = t.Id,
        DateDisplay = t.Date.ToString("yyyy-MM-dd"),
        ShipmentType = t.ShipmentType,
        Qty = t.Qty,
        Unit = t.Unit,
        FromLocation = t.FromLocation,
        ToLocation = t.ToLocation,
        Cost = t.Cost,
        CostPerUnit = t.CostPerUnit,
        Transporter = t.Transporter,
        DispatchRef = t.DispatchRef,
        Note = t.Note
    };

    private static SlaughterRowViewModel MapSlaughterRow(SlaughterRecord s) => new()
    {
        Id = s.Id,
        DateDisplay = s.Date.ToString("yyyy-MM-dd"),
        Tag = s.Goat?.Tag ?? s.TagText ?? "",
        Breed = s.Goat?.Breed ?? "",
        LiveWeightKg = s.LiveWeightKg,
        CarcassKg = s.CarcassKg,
        SaleAmount = s.SaleAmount
    };

    private static List<MilkingSessionRowViewModel> MapSessions(IEnumerable<MilkingSession> sessions) =>
        sessions.Select(s => new MilkingSessionRowViewModel
        {
            Id = s.Id,
            DateDisplay = s.Date.ToString("yyyy-MM-dd"),
            Session = s.Session,
            Breed = s.Breed,
            Liters = s.Liters,
            RejectedLiters = s.RejectedLiters
        }).ToList();

    private readonly record struct BalanceRow(int productId, int warehouseId, decimal qty);

    private static List<BalanceRow> ComputeBalances(IReadOnlyList<StockMovement> moves)
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

        return qty.Select(kv => new BalanceRow(kv.Key.product, kv.Key.wh, kv.Value)).ToList();
    }

    private static decimal ComputeStockValue(IReadOnlyList<ProductCatalogItem> catalog, IReadOnlyList<StockMovement> moves)
    {
        var price = catalog.ToDictionary(c => c.Id, c => c.DefaultPrice);
        decimal total = 0;
        foreach (var b in ComputeBalances(moves))
        {
            if (b.qty <= 0) continue;
            total += b.qty * price.GetValueOrDefault(b.productId, 0);
        }
        return total;
    }

    private static void SyncTankDisplayQty(List<WarehouseRowViewModel> whRows, string kind, decimal totalLiters)
    {
        var tanks = whRows.Where(w => w.Kind == kind && w.IsActive).ToList();
        if (tanks.Count == 0 || totalLiters <= 0) return;
        if (tanks.Sum(t => t.LitersOrKg) > 0.0001m) return;
        tanks[0].LitersOrKg = totalLiters;
    }

    private static decimal SumWarehouseQty(
        IReadOnlyList<BalanceRow> balances, int warehouseId, string kind,
        int? rawProductId, int? pastProductId, IReadOnlyList<ProductCatalogItem> catalog)
    {
        decimal Sum(int? pid) => balances.Where(b => b.warehouseId == warehouseId && (!pid.HasValue || b.productId == pid))
            .Sum(b => b.qty);
        return kind switch
        {
            "raw_tank" => Sum(rawProductId),
            "past_tank" => Sum(pastProductId),
            "dairy_store" => balances.Where(b => b.warehouseId == warehouseId).Sum(b =>
            {
                var p = catalog.FirstOrDefault(c => c.Id == b.productId);
                return p != null && !p.Sku.StartsWith("MILK-", StringComparison.OrdinalIgnoreCase) ? b.qty : 0;
            }),
            "meat_store" => balances.Where(b => b.warehouseId == warehouseId).Sum(b => b.qty),
            _ => Sum(null)
        };
    }

    internal static string InferKind(Warehouse w)
    {
        var loc = (w.Location ?? "").Trim().ToLowerInvariant();
        if (loc is "raw_tank" or "past_tank" or "dairy_store" or "meat_store" or "other") return loc;
        var name = w.Name.ToLowerInvariant();
        if (name.Contains("pasteur")) return "past_tank";
        if (name.Contains("raw") && name.Contains("tank")) return "raw_tank";
        if (name.Contains("cold") || name.Contains("dairy")) return "dairy_store";
        if (name.Contains("meat") || name.Contains("chiller") || name.Contains("freezer")) return "meat_store";
        if (name.Contains("tank")) return "raw_tank";
        return "other";
    }

    private static DateTime ParseMonth(string? month)
    {
        if (!string.IsNullOrWhiteSpace(month) && DateTime.TryParse(month + "-01", out var parsed))
            return parsed;
        return DateTime.Today;
    }
}

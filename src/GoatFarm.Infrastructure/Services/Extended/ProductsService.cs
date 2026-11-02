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
            _context.ProductCatalogItems.Add(new ProductCatalogItem { Sku = "MILK-RAW", Name = "Raw milk (bulk)", Unit = "L", DefaultPrice = 0 });
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
        var milkProdMonth = await _context.MilkProductions.AsNoTracking()
            .Where(p => p.Date >= monthStart && p.Date <= monthEnd).ToListAsync(cancellationToken);
        var milkProdToday = milkProdMonth.Where(p => p.Date == today).Sum(p => p.Liters);
        var milkSalesMonth = await _context.MilkSales.AsNoTracking()
            .Where(s => s.Date >= monthStart && s.Date <= monthEnd).ToListAsync(cancellationToken);
        var milkWasteMonth = await _context.MilkWastes.AsNoTracking()
            .Where(w => w.Date >= monthStart && w.Date <= monthEnd).ToListAsync(cancellationToken);
        var slaughters = await _context.SlaughterRecords.AsNoTracking().Include(s => s.Goat)
            .OrderByDescending(s => s.Date).Take(60).ToListAsync(cancellationToken);
        var sales = await _context.ProductSales.AsNoTracking().Include(s => s.Customer)
            .Where(s => s.Date >= monthStart && s.Date <= monthEnd)
            .OrderByDescending(s => s.Date).ToListAsync(cancellationToken);
        var customers = await _context.BusinessCustomers.AsNoTracking()
            .Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync(cancellationToken);
        var herdGoats = (await _context.Goats.AsNoTracking()
            .Where(g => !g.IsArchived && g.Gender == GoatGender.Female)
            .OrderBy(g => g.Breed).ThenBy(g => g.Tag)
            .ToListAsync(cancellationToken))
            .Select(g => new ProductMilkingGoatRowViewModel
            {
                Id = g.Id,
                Tag = g.Tag,
                Name = g.Name,
                Breed = g.Breed,
                Gender = "Female",
                StatusKey = GoatStatusKey(g.Status),
                Status = GoatStatusLabel(g.Status)
            })
            .ToList();

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
            RecentSessions = MapSessions(recentSessions),
            HerdGoats = herdGoats,
            MoneyAccounts = ledgerPage.Accounts,
            Customers = customers.Select(c => new ProductCustomerRowViewModel { Id = c.Id, Name = c.Name }).ToList(),
            Catalog = catalog.Select(c => new ProductCatalogRowViewModel
            {
                Id = c.Id, Sku = c.Sku, Name = c.Name, Unit = c.Unit, DefaultPrice = c.DefaultPrice
            }).ToList(),
            Warehouses = whRows,
            Movements = moves.Select(mv => new StockMovementRowViewModel
            {
                Id = mv.Id,
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

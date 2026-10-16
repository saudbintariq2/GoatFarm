using System.Text.Json;
using GoatFarm.Domain.Common;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Infrastructure.Services;

internal static class BackupV241Helper
{
    public static async Task<object> ExportAsync(GoatFarmDbContext context, CancellationToken cancellationToken)
    {
        return new
        {
            auditLog = await context.AuditLogEntries.AsNoTracking().OrderBy(a => a.Id).ToListAsync(cancellationToken),
            approvalRequests = await context.ApprovalRequests.AsNoTracking().OrderBy(a => a.Id).ToListAsync(cancellationToken),
            tradeItems = await context.TradeItems.AsNoTracking().OrderBy(a => a.Id).ToListAsync(cancellationToken),
            tradeStores = await context.TradeStores.AsNoTracking().OrderBy(a => a.Id).ToListAsync(cancellationToken),
            tradeAccounts = await context.TradeAccounts.AsNoTracking().OrderBy(a => a.Id).ToListAsync(cancellationToken),
            tradeParties = await context.TradeParties.AsNoTracking().OrderBy(a => a.Id).ToListAsync(cancellationToken),
            tradeExpenseCategories = await context.TradeExpenseCategories.AsNoTracking().OrderBy(a => a.Id).ToListAsync(cancellationToken),
            tradeEntries = await context.TradeEntries.AsNoTracking().OrderBy(a => a.Id).ToListAsync(cancellationToken),
            farmProjects = await context.FarmProjects.AsNoTracking().OrderBy(a => a.Id).ToListAsync(cancellationToken),
            projectExpenditures = await context.ProjectExpenditures.AsNoTracking().OrderBy(a => a.Id).ToListAsync(cancellationToken),
            fixedAssetRegisters = await context.FixedAssetRegisters.AsNoTracking().OrderBy(a => a.Id).ToListAsync(cancellationToken),
            solarProductionLogs = await context.SolarProductionLogs.AsNoTracking().OrderBy(a => a.Id).ToListAsync(cancellationToken),
            moneyAccounts = await context.MoneyAccounts.AsNoTracking().OrderBy(a => a.Id).ToListAsync(cancellationToken),
            ledgerTransactions = await context.LedgerTransactions.AsNoTracking().OrderBy(a => a.Id).ToListAsync(cancellationToken),
            businessCustomers = await context.BusinessCustomers.AsNoTracking().OrderBy(a => a.Id).ToListAsync(cancellationToken),
            customerInvoices = await context.CustomerInvoices.AsNoTracking().OrderBy(a => a.Id).ToListAsync(cancellationToken),
            supplierPayables = await context.SupplierPayables.AsNoTracking().OrderBy(a => a.Id).ToListAsync(cancellationToken),
            budgetLines = await context.BudgetLines.AsNoTracking().OrderBy(a => a.Id).ToListAsync(cancellationToken),
            profitSharePartners = await context.ProfitSharePartners.AsNoTracking().OrderBy(a => a.Id).ToListAsync(cancellationToken),
            productCatalogItems = await context.ProductCatalogItems.AsNoTracking().OrderBy(a => a.Id).ToListAsync(cancellationToken),
            warehouses = await context.Warehouses.AsNoTracking().OrderBy(a => a.Id).ToListAsync(cancellationToken),
            stockMovements = await context.StockMovements.AsNoTracking().OrderBy(a => a.Id).ToListAsync(cancellationToken),
            milkingSessions = await context.MilkingSessions.AsNoTracking().OrderBy(a => a.Id).ToListAsync(cancellationToken),
            slaughterRecords = await context.SlaughterRecords.AsNoTracking().OrderBy(a => a.Id).ToListAsync(cancellationToken),
            productSales = await context.ProductSales.AsNoTracking().OrderBy(a => a.Id).ToListAsync(cancellationToken),
            goatTreatments = await context.GoatTreatments.AsNoTracking().OrderBy(a => a.Id).ToListAsync(cancellationToken),
            customFeedingGroups = await context.CustomFeedingGroups.AsNoTracking().OrderBy(a => a.Id).ToListAsync(cancellationToken)
        };
    }

    public static async Task ClearV241Async(GoatFarmDbContext context, CancellationToken cancellationToken)
    {
        context.TradeEntries.RemoveRange(await context.TradeEntries.ToListAsync(cancellationToken));
        context.TradeItems.RemoveRange(await context.TradeItems.ToListAsync(cancellationToken));
        context.TradeStores.RemoveRange(await context.TradeStores.ToListAsync(cancellationToken));
        context.TradeAccounts.RemoveRange(await context.TradeAccounts.ToListAsync(cancellationToken));
        context.TradeParties.RemoveRange(await context.TradeParties.ToListAsync(cancellationToken));
        context.TradeExpenseCategories.RemoveRange(await context.TradeExpenseCategories.ToListAsync(cancellationToken));
        context.ProjectExpenditures.RemoveRange(await context.ProjectExpenditures.ToListAsync(cancellationToken));
        context.FixedAssetRegisters.RemoveRange(await context.FixedAssetRegisters.ToListAsync(cancellationToken));
        context.SolarProductionLogs.RemoveRange(await context.SolarProductionLogs.ToListAsync(cancellationToken));
        context.FarmProjects.RemoveRange(await context.FarmProjects.ToListAsync(cancellationToken));
        context.LedgerTransactions.RemoveRange(await context.LedgerTransactions.ToListAsync(cancellationToken));
        context.CustomerInvoices.RemoveRange(await context.CustomerInvoices.ToListAsync(cancellationToken));
        context.SupplierPayables.RemoveRange(await context.SupplierPayables.ToListAsync(cancellationToken));
        context.BudgetLines.RemoveRange(await context.BudgetLines.ToListAsync(cancellationToken));
        context.ProfitSharePartners.RemoveRange(await context.ProfitSharePartners.ToListAsync(cancellationToken));
        context.MoneyAccounts.RemoveRange(await context.MoneyAccounts.ToListAsync(cancellationToken));
        context.BusinessCustomers.RemoveRange(await context.BusinessCustomers.ToListAsync(cancellationToken));
        context.StockMovements.RemoveRange(await context.StockMovements.ToListAsync(cancellationToken));
        context.MilkingSessions.RemoveRange(await context.MilkingSessions.ToListAsync(cancellationToken));
        context.SlaughterRecords.RemoveRange(await context.SlaughterRecords.ToListAsync(cancellationToken));
        context.ProductSales.RemoveRange(await context.ProductSales.ToListAsync(cancellationToken));
        context.ProductCatalogItems.RemoveRange(await context.ProductCatalogItems.ToListAsync(cancellationToken));
        context.Warehouses.RemoveRange(await context.Warehouses.ToListAsync(cancellationToken));
        context.GoatTreatments.RemoveRange(await context.GoatTreatments.ToListAsync(cancellationToken));
        context.CustomFeedingGroups.RemoveRange(await context.CustomFeedingGroups.ToListAsync(cancellationToken));
        context.ApprovalRequests.RemoveRange(await context.ApprovalRequests.ToListAsync(cancellationToken));
        context.AuditLogEntries.RemoveRange(await context.AuditLogEntries.ToListAsync(cancellationToken));
        await context.SaveChangesAsync(cancellationToken);
    }

    public static async Task ImportAsync(GoatFarmDbContext context, JsonElement root, IReadOnlyDictionary<int, int> goatIdMap, CancellationToken cancellationToken)
    {
        if (!root.TryGetProperty("v241Extended", out var ext) || ext.ValueKind != JsonValueKind.Object)
            return;

        ImportList(ext, "tradeItems", el => context.TradeItems.Add(WithId(MapTradeItem(el), el)));
        ImportList(ext, "tradeStores", el => context.TradeStores.Add(WithId(MapTradeStore(el), el)));
        ImportList(ext, "tradeAccounts", el => context.TradeAccounts.Add(WithId(MapTradeAccount(el), el)));
        ImportList(ext, "tradeParties", el => context.TradeParties.Add(WithId(MapTradeParty(el), el)));
        ImportList(ext, "tradeExpenseCategories", el => context.TradeExpenseCategories.Add(WithId(MapTradeCategory(el), el)));
        await context.SaveChangesAsync(cancellationToken);

        var itemMap = await context.TradeItems.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Id, cancellationToken);
        var storeMap = await context.TradeStores.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Id, cancellationToken);
        var accountMap = await context.TradeAccounts.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Id, cancellationToken);
        var partyMap = await context.TradeParties.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Id, cancellationToken);
        var catMap = await context.TradeExpenseCategories.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Id, cancellationToken);

        if (ext.TryGetProperty("tradeEntries", out var entries) && entries.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in entries.EnumerateArray())
            {
                var entry = new TradeEntry
                {
                    Type = ParseEnum<TradeEntryType>(GetStr(el, "type")) ?? TradeEntryType.Buy,
                    Date = GetDate(el, "date") ?? DateOnly.FromDateTime(DateTime.Today),
                    ItemId = RemapNullable(GetInt(el, "itemId"), itemMap),
                    StoreId = RemapNullable(GetInt(el, "storeId"), storeMap),
                    ToStoreId = RemapNullable(GetInt(el, "toStoreId"), storeMap),
                    PartyId = RemapNullable(GetInt(el, "partyId"), partyMap),
                    AccountId = RemapNullable(GetInt(el, "accountId"), accountMap),
                    CategoryId = RemapNullable(GetInt(el, "categoryId"), catMap),
                    Qty = GetDec(el, "qty"),
                    Rate = GetDec(el, "rate"),
                    Amount = GetDec(el, "amount"),
                    Notes = GetStr(el, "notes"),
                    IsCancelled = GetBool(el, "isCancelled")
                };
                WithId(entry, el);
                context.TradeEntries.Add(entry);
            }
        }

        ImportList(ext, "farmProjects", el => context.FarmProjects.Add(WithId(MapProject(el), el)));
        await context.SaveChangesAsync(cancellationToken);
        var projectMap = await context.FarmProjects.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.Id, cancellationToken);

        if (ext.TryGetProperty("projectExpenditures", out var exps) && exps.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in exps.EnumerateArray())
            {
                var pid = GetInt(el, "projectId");
                if (pid is null || !projectMap.ContainsKey(pid.Value)) continue;
                context.ProjectExpenditures.Add(new ProjectExpenditure
                {
                    ProjectId = pid.Value,
                    Date = GetDate(el, "date") ?? DateOnly.FromDateTime(DateTime.Today),
                    Description = GetStr(el, "description") ?? "",
                    Amount = GetDec(el, "amount"),
                    Vendor = GetStr(el, "vendor")
                });
            }
        }

        ImportList(ext, "fixedAssetRegisters", el =>
        {
            var a = MapFixedAsset(el);
            a.ProjectId = RemapNullable(GetInt(el, "projectId"), projectMap);
            context.FixedAssetRegisters.Add(a);
        });
        ImportList(ext, "solarProductionLogs", el => context.SolarProductionLogs.Add(MapSolar(el)));
        ImportList(ext, "moneyAccounts", el => context.MoneyAccounts.Add(MapMoneyAccount(el)));
        await context.SaveChangesAsync(cancellationToken);
        var moneyMap = await context.MoneyAccounts.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Id, cancellationToken);

        ImportList(ext, "businessCustomers", el => context.BusinessCustomers.Add(MapCustomer(el)));
        await context.SaveChangesAsync(cancellationToken);
        var custMap = await context.BusinessCustomers.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Id, cancellationToken);

        if (ext.TryGetProperty("ledgerTransactions", out var ltx) && ltx.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in ltx.EnumerateArray())
            {
                context.LedgerTransactions.Add(new LedgerTransaction
                {
                    Date = GetDate(el, "date") ?? DateOnly.FromDateTime(DateTime.Today),
                    Kind = ParseEnum<LedgerTransactionKind>(GetStr(el, "kind")) ?? LedgerTransactionKind.Income,
                    AccountId = RemapNullable(GetInt(el, "accountId"), moneyMap),
                    CounterAccountId = RemapNullable(GetInt(el, "counterAccountId"), moneyMap),
                    Amount = GetDec(el, "amount"),
                    Category = GetStr(el, "category"),
                    Reference = GetStr(el, "reference"),
                    Notes = GetStr(el, "notes")
                });
            }
        }

        if (ext.TryGetProperty("customerInvoices", out var inv) && inv.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in inv.EnumerateArray())
            {
                var cid = GetInt(el, "customerId");
                if (cid is null || !custMap.ContainsKey(cid.Value)) continue;
                context.CustomerInvoices.Add(new CustomerInvoice
                {
                    CustomerId = cid.Value,
                    Date = GetDate(el, "date") ?? DateOnly.FromDateTime(DateTime.Today),
                    DueDate = GetDate(el, "dueDate"),
                    Description = GetStr(el, "description") ?? "",
                    Amount = GetDec(el, "amount"),
                    PaidAmount = GetDec(el, "paidAmount"),
                    Status = ParseEnum<InvoiceStatus>(GetStr(el, "status")) ?? InvoiceStatus.Open
                });
            }
        }

        ImportList(ext, "supplierPayables", el => context.SupplierPayables.Add(MapPayable(el)));
        ImportList(ext, "budgetLines", el => context.BudgetLines.Add(MapBudget(el)));
        ImportList(ext, "profitSharePartners", el => context.ProfitSharePartners.Add(MapPartner(el)));
        ImportList(ext, "productCatalogItems", el => context.ProductCatalogItems.Add(MapCatalog(el)));
        ImportList(ext, "warehouses", el => context.Warehouses.Add(MapWarehouse(el)));
        await context.SaveChangesAsync(cancellationToken);

        var prodMap = await context.ProductCatalogItems.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Id, cancellationToken);
        var whMap = await context.Warehouses.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Id, cancellationToken);

        if (ext.TryGetProperty("stockMovements", out var sm) && sm.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in sm.EnumerateArray())
            {
                var pid = GetInt(el, "productId");
                var wid = GetInt(el, "warehouseId");
                if (pid is null || wid is null || !prodMap.ContainsKey(pid.Value) || !whMap.ContainsKey(wid.Value)) continue;
                context.StockMovements.Add(new StockMovement
                {
                    Date = GetDate(el, "date") ?? DateOnly.FromDateTime(DateTime.Today),
                    Type = ParseEnum<StockMovementType>(GetStr(el, "type")) ?? StockMovementType.Receive,
                    ProductId = pid.Value,
                    WarehouseId = wid.Value,
                    ToWarehouseId = RemapNullable(GetInt(el, "toWarehouseId"), whMap),
                    Qty = GetDec(el, "qty"),
                    UnitCost = GetDec(el, "unitCost"),
                    Notes = GetStr(el, "notes")
                });
            }
        }

        ImportList(ext, "milkingSessions", el => context.MilkingSessions.Add(MapSession(el)));
        if (ext.TryGetProperty("slaughterRecords", out var sl) && sl.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in sl.EnumerateArray())
            {
                context.SlaughterRecords.Add(new SlaughterRecord
                {
                    Date = GetDate(el, "date") ?? DateOnly.FromDateTime(DateTime.Today),
                    GoatId = RemapNullable(GetInt(el, "goatId"), goatIdMap),
                    TagText = GetStr(el, "tagText"),
                    LiveWeightKg = GetDec(el, "liveWeightKg"),
                    CarcassKg = GetDec(el, "carcassKg"),
                    SaleAmount = GetDec(el, "saleAmount"),
                    Notes = GetStr(el, "notes")
                });
            }
        }

        if (ext.TryGetProperty("productSales", out var ps) && ps.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in ps.EnumerateArray())
            {
                context.ProductSales.Add(new ProductSale
                {
                    Date = GetDate(el, "date") ?? DateOnly.FromDateTime(DateTime.Today),
                    CustomerId = RemapNullable(GetInt(el, "customerId"), custMap),
                    ProductId = RemapNullable(GetInt(el, "productId"), prodMap),
                    ProductName = GetStr(el, "productName") ?? "",
                    Qty = GetDec(el, "qty"),
                    Rate = GetDec(el, "rate"),
                    Amount = GetDec(el, "amount"),
                    IsCredit = GetBool(el, "isCredit"),
                    Notes = GetStr(el, "notes")
                });
            }
        }

        if (ext.TryGetProperty("goatTreatments", out var tr) && tr.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in tr.EnumerateArray())
            {
                var gid = GetInt(el, "goatId");
                if (gid is null || !goatIdMap.TryGetValue(gid.Value, out var mapped)) continue;
                context.GoatTreatments.Add(new GoatTreatment
                {
                    GoatId = mapped,
                    StartDate = GetDate(el, "startDate") ?? DateOnly.FromDateTime(DateTime.Today),
                    EndDate = GetDate(el, "endDate"),
                    Diagnosis = GetStr(el, "diagnosis") ?? "",
                    Medicine = GetStr(el, "medicine") ?? "",
                    Cost = GetDec(el, "cost"),
                    WithdrawalDays = GetInt(el, "withdrawalDays"),
                    Notes = GetStr(el, "notes")
                });
            }
        }

        ImportList(ext, "customFeedingGroups", el => context.CustomFeedingGroups.Add(new CustomFeedingGroup
        {
            Name = GetStr(el, "name") ?? "",
            Description = GetStr(el, "description"),
            MixKgPerDay = GetDec(el, "mixKgPerDay"),
            FodderKgPerDay = GetDec(el, "fodderKgPerDay")
        }));

        await context.SaveChangesAsync(cancellationToken);
    }

    private static T WithId<T>(T entity, JsonElement el) where T : BaseEntity
    {
        if (GetInt(el, "id") is int id && id > 0)
            entity.Id = id;
        return entity;
    }

    private static void ImportList(JsonElement parent, string key, Action<JsonElement> add)
    {
        if (!parent.TryGetProperty(key, out var arr) || arr.ValueKind != JsonValueKind.Array) return;
        foreach (var el in arr.EnumerateArray()) add(el);
    }

    private static int? RemapNullable(int? oldId, IReadOnlyDictionary<int, int> map) =>
        oldId is null ? null : map.TryGetValue(oldId.Value, out var n) ? n : null;

    private static string? GetStr(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static int? GetInt(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var n) ? n : null;

    private static decimal GetDec(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetDecimal(out var d) ? d : 0;

    private static bool GetBool(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.True;

    private static DateOnly? GetDate(JsonElement el, string name)
    {
        var s = GetStr(el, name);
        return DateOnly.TryParse(s, out var d) ? d : null;
    }

    private static T? ParseEnum<T>(string? s) where T : struct, Enum =>
        string.IsNullOrWhiteSpace(s) ? null : Enum.TryParse<T>(s, true, out var v) ? v : null;

    private static TradeItem MapTradeItem(JsonElement el) => new()
    {
        Name = GetStr(el, "name") ?? "",
        Unit = GetStr(el, "unit") ?? "kg",
        UnitNote = GetStr(el, "unitNote"),
        DefaultPrice = GetDec(el, "defaultPrice"),
        IsActive = !GetBool(el, "isInactive")
    };

    private static TradeStore MapTradeStore(JsonElement el) => new() { Name = GetStr(el, "name") ?? "", IsActive = true };
    private static TradeAccount MapTradeAccount(JsonElement el) => new()
    {
        Name = GetStr(el, "name") ?? "",
        Type = ParseEnum<TradeAccountType>(GetStr(el, "type")) ?? TradeAccountType.Cash,
        OpeningBalance = GetDec(el, "openingBalance"),
        IsActive = true
    };

    private static TradeParty MapTradeParty(JsonElement el) => new()
    {
        Name = GetStr(el, "name") ?? "",
        Role = ParseEnum<TradePartyRole>(GetStr(el, "role")) ?? TradePartyRole.Both,
        Phone = GetStr(el, "phone"),
        Place = GetStr(el, "place"),
        OpeningBalance = GetDec(el, "openingBalance"),
        IsActive = true
    };

    private static TradeExpenseCategory MapTradeCategory(JsonElement el) => new() { Name = GetStr(el, "name") ?? "", IsActive = true };

    private static FarmProject MapProject(JsonElement el) => new()
    {
        Name = GetStr(el, "name") ?? "",
        Description = GetStr(el, "description"),
        Status = ParseEnum<ProjectStatus>(GetStr(el, "status")) ?? ProjectStatus.Active,
        StartDate = GetDate(el, "startDate"),
        TargetEndDate = GetDate(el, "targetEndDate"),
        BudgetAmount = GetDec(el, "budgetAmount")
    };

    private static FixedAssetRegister MapFixedAsset(JsonElement el) => new()
    {
        Name = GetStr(el, "name") ?? "",
        Category = ParseEnum<FixedAssetCategory>(GetStr(el, "category")) ?? FixedAssetCategory.Other,
        Cost = GetDec(el, "cost"),
        PurchaseDate = GetDate(el, "purchaseDate"),
        Notes = GetStr(el, "notes"),
        IsActive = true
    };

    private static SolarProductionLog MapSolar(JsonElement el) => new()
    {
        Date = GetDate(el, "date") ?? DateOnly.FromDateTime(DateTime.Today),
        KwhGenerated = GetDec(el, "kwhGenerated"),
        SavingsEstimate = el.TryGetProperty("savingsEstimate", out var s) && s.ValueKind == JsonValueKind.Number ? s.GetDecimal() : null,
        Notes = GetStr(el, "notes")
    };

    private static MoneyAccount MapMoneyAccount(JsonElement el) => new()
    {
        Name = GetStr(el, "name") ?? "",
        Kind = ParseEnum<MoneyAccountKind>(GetStr(el, "kind")) ?? MoneyAccountKind.Cash,
        OpeningBalance = GetDec(el, "openingBalance"),
        IsActive = true
    };

    private static BusinessCustomer MapCustomer(JsonElement el) => new()
    {
        Name = GetStr(el, "name") ?? "",
        Phone = GetStr(el, "phone"),
        Place = GetStr(el, "place"),
        OpeningBalance = GetDec(el, "openingBalance"),
        IsActive = true
    };

    private static SupplierPayable MapPayable(JsonElement el) => new()
    {
        SupplierName = GetStr(el, "supplierName") ?? "",
        Date = GetDate(el, "date") ?? DateOnly.FromDateTime(DateTime.Today),
        DueDate = GetDate(el, "dueDate"),
        Description = GetStr(el, "description") ?? "",
        Amount = GetDec(el, "amount"),
        PaidAmount = GetDec(el, "paidAmount"),
        Status = ParseEnum<PayableStatus>(GetStr(el, "status")) ?? PayableStatus.Open
    };

    private static BudgetLine MapBudget(JsonElement el) => new()
    {
        Year = GetInt(el, "year") ?? DateTime.Today.Year,
        Month = GetInt(el, "month") ?? DateTime.Today.Month,
        Category = GetStr(el, "category") ?? "",
        PlannedAmount = GetDec(el, "plannedAmount")
    };

    private static ProfitSharePartner MapPartner(JsonElement el) => new()
    {
        Name = GetStr(el, "name") ?? "",
        SharePercent = GetDec(el, "sharePercent"),
        IsActive = true
    };

    private static ProductCatalogItem MapCatalog(JsonElement el) => new()
    {
        Sku = GetStr(el, "sku") ?? "",
        Name = GetStr(el, "name") ?? "",
        Unit = GetStr(el, "unit") ?? "L",
        DefaultPrice = GetDec(el, "defaultPrice"),
        IsActive = true
    };

    private static Warehouse MapWarehouse(JsonElement el) => new()
    {
        Name = GetStr(el, "name") ?? "",
        Location = GetStr(el, "location"),
        IsActive = true
    };

    private static MilkingSession MapSession(JsonElement el) => new()
    {
        Date = GetDate(el, "date") ?? DateOnly.FromDateTime(DateTime.Today),
        Session = GetStr(el, "session") ?? "AM",
        Breed = GetStr(el, "breed") ?? "Mixed",
        Liters = GetDec(el, "liters"),
        RejectedLiters = GetDec(el, "rejectedLiters"),
        Notes = GetStr(el, "notes")
    };
}

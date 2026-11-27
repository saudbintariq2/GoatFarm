using System.Text.Json;
using System.Text.Json.Serialization;
using GoatFarm.Domain.Common;
using GoatFarm.Domain.Constants;
using GoatFarm.Domain.Entities;
using GoatFarm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Infrastructure.Services;

/// <summary>
/// Format 4 section <c>native</c>: every farm table exactly as stored, plus every farm setting. Restore keeps the
/// original ids so references such as <c>fin-income:12</c>, <c>payable:7</c> and breeding-group members stay valid.
/// </summary>
internal static class BackupNativeHelper
{
    public const string Section = "native";

    /// <summary>Settings that are not farm data: access rules are never restored, alerts and backup status belong to the server.</summary>
    private static readonly HashSet<string> SkippedSettings = new(StringComparer.Ordinal)
    {
        "RolePermissions",
        AppSettingKeys.FarmAlerts,
        AppSettingKeys.BackupStatus
    };

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private sealed record Table(
        string Key,
        Func<GoatFarmDbContext, CancellationToken, Task<object>> Export,
        Func<GoatFarmDbContext, JsonElement, RestoreContext, CancellationToken, Task<int>> Import);

    private sealed class RestoreContext
    {
        public Dictionary<string, string> UserIds { get; } = new(StringComparer.Ordinal);
        public List<(Goat Goat, int? SireId, int? DamId, int? KiddingRecordId)> GoatLinks { get; } = [];

        public string? MapUser(string? oldId) =>
            oldId is null ? null : UserIds.TryGetValue(oldId, out var id) ? id : null;
    }

    /// <summary>Parents before children; each table is saved before the next so foreign keys always resolve.</summary>
    private static readonly Table[] Tables =
    [
        T<GoatGroup>("goatGroups", c => c.GoatGroups),
        T<CustomFeedingGroup>("customFeedingGroups", c => c.CustomFeedingGroups),
        T<FeedPrice>("feedPrices", c => c.FeedPrices),
        T<FeedPlan>("feedPlans", c => c.FeedPlans),
        T<FeedPlanItem>("feedPlanItems", c => c.FeedPlanItems),
        T<MoneyAccount>("moneyAccounts", c => c.MoneyAccounts),
        T<BusinessCustomer>("businessCustomers", c => c.BusinessCustomers),
        T<Vaccine>("vaccines", c => c.Vaccines),
        T<Employee>("employees", c => c.Employees),
        T<RecurringCost>("recurringCosts", c => c.RecurringCosts),
        T<Asset>("assets", c => c.Assets),
        T<OwnerInvestment>("ownerInvestments", c => c.OwnerInvestments),
        T<Income>("incomes", c => c.Incomes),
        T<Expense>("expenses", c => c.Expenses),
        T<FeedPurchase>("feedPurchases", c => c.FeedPurchases),
        T<VaccinePurchase>("vaccinePurchases", c => c.VaccinePurchases),
        T<MilkProduction>("milkProductions", c => c.MilkProductions),
        T<MilkSale>("milkSales", c => c.MilkSales),
        T<MilkWaste>("milkWastes", c => c.MilkWastes),
        T<Reminder>("reminders", c => c.Reminders),
        T<FeedUsageRecord>("feedUsageRecords", c => c.FeedUsageRecords),
        T<Goat>("goats", c => c.Goats, (g, r) =>
        {
            r.GoatLinks.Add((g, g.SireId, g.DamId, g.KiddingRecordId));
            g.SireId = null;
            g.DamId = null;
            g.KiddingRecordId = null;
            return true;
        }),
        T<BreedingEvent>("breedingEvents", c => c.BreedingEvents),
        T<KiddingRecord>("kiddingRecords", c => c.KiddingRecords),
        T<VaccinationHistory>("vaccinationHistories", c => c.VaccinationHistories),
        T<BreedingEmptyLog>("breedingEmptyLogs", c => c.BreedingEmptyLogs),
        T<GoatWeightRecord>("goatWeightRecords", c => c.GoatWeightRecords),
        T<GoatBcsRecord>("goatBcsRecords", c => c.GoatBcsRecords),
        T<GoatHeatEvent>("goatHeatEvents", c => c.GoatHeatEvents),
        T<BuckHealthCheck>("buckHealthChecks", c => c.BuckHealthChecks),
        T<DeathRecord>("deathRecords", c => c.DeathRecords),
        T<CustomerInvoice>("customerInvoices", c => c.CustomerInvoices),
        T<GoatSale>("goatSales", c => c.GoatSales),
        T<FarmTask>("farmTasks", c => c.FarmTasks, (t, r) =>
        {
            t.AssigneeUserId = r.MapUser(t.AssigneeUserId);
            t.CreatedByUserId = r.MapUser(t.CreatedByUserId);
            return true;
        }),
        T<TradeItem>("tradeItems", c => c.TradeItems),
        T<TradeStore>("tradeStores", c => c.TradeStores),
        T<TradeAccount>("tradeAccounts", c => c.TradeAccounts),
        T<TradeParty>("tradeParties", c => c.TradeParties),
        T<TradeExpenseCategory>("tradeExpenseCategories", c => c.TradeExpenseCategories),
        T<TradeEntry>("tradeEntries", c => c.TradeEntries),
        T<FarmProject>("farmProjects", c => c.FarmProjects),
        T<ProjectExpenditure>("projectExpenditures", c => c.ProjectExpenditures),
        T<FixedAssetRegister>("fixedAssetRegisters", c => c.FixedAssetRegisters),
        T<SolarProductionLog>("solarProductionLogs", c => c.SolarProductionLogs),
        T<LedgerTransaction>("ledgerTransactions", c => c.LedgerTransactions),
        T<SupplierPayable>("supplierPayables", c => c.SupplierPayables),
        T<BudgetLine>("budgetLines", c => c.BudgetLines),
        T<ProfitSharePartner>("profitSharePartners", c => c.ProfitSharePartners),
        T<ProductCatalogItem>("productCatalogItems", c => c.ProductCatalogItems),
        T<Warehouse>("warehouses", c => c.Warehouses),
        T<StockMovement>("stockMovements", c => c.StockMovements),
        T<MilkingSession>("milkingSessions", c => c.MilkingSessions),
        T<SlaughterRecord>("slaughterRecords", c => c.SlaughterRecords),
        T<ProductSale>("productSales", c => c.ProductSales),
        T<TransportShipment>("transportShipments", c => c.TransportShipments),
        T<ProductCostAllocation>("productCostAllocations", c => c.ProductCostAllocations),
        T<ProductPricingProfile>("productPricingProfiles", c => c.ProductPricingProfiles),
        T<DeliveryRoutePricing>("deliveryRoutePricings", c => c.DeliveryRoutePricings),
        T<ClientProductPrice>("clientProductPrices", c => c.ClientProductPrices),
        T<GoatTreatment>("goatTreatments", c => c.GoatTreatments),
        T<ApprovalRequest>("approvalRequests", c => c.ApprovalRequests, (a, r) =>
        {
            a.RequestedByUserId = r.MapUser(a.RequestedByUserId) ?? a.RequestedByUserId;
            a.ReviewedByUserId = r.MapUser(a.ReviewedByUserId) ?? a.ReviewedByUserId;
            return true;
        }),
        T<AuditLogEntry>("auditLogEntries", c => c.AuditLogEntries, (a, r) =>
        {
            a.UserId = r.MapUser(a.UserId) ?? a.UserId;
            return true;
        })
    ];

    private static Table T<TEntity>(
        string key,
        Func<GoatFarmDbContext, DbSet<TEntity>> set,
        Func<TEntity, RestoreContext, bool>? prepare = null) where TEntity : BaseEntity => new(
        key,
        async (c, ct) => await set(c).AsNoTracking().OrderBy(e => e.Id).ToListAsync(ct),
        async (c, arr, r, ct) =>
        {
            var rows = arr.Deserialize<List<TEntity>>(ReadOptions) ?? [];
            var kept = rows.Where(row => prepare?.Invoke(row, r) ?? true).ToList();
            set(c).AddRange(kept);
            await c.SaveChangesAsync(ct);
            return kept.Count;
        });

    public static async Task<object> ExportAsync(GoatFarmDbContext context, CancellationToken cancellationToken)
    {
        var tables = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var table in Tables)
            tables[table.Key] = await table.Export(context, cancellationToken);

        var settings = await context.AppSettings.AsNoTracking()
            .Where(s => !SkippedSettings.Contains(s.Key))
            .OrderBy(s => s.Key)
            .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);

        var userEmails = await context.Users.AsNoTracking()
            .Where(u => u.Email != null)
            .ToDictionaryAsync(u => u.Id, u => u.Email!, cancellationToken);

        return new { tables, settings, userEmails };
    }

    public static bool HasNativeSection(JsonElement root) =>
        root.TryGetProperty(Section, out var native) && native.ValueKind == JsonValueKind.Object
        && native.TryGetProperty("tables", out var tables) && tables.ValueKind == JsonValueKind.Object;

    /// <summary>Call on an empty farm (after <see cref="BackupImportHelper.ClearFarmDataAsync"/>) inside the restore transaction.</summary>
    public static async Task ImportAsync(GoatFarmDbContext context, JsonElement root, CancellationToken cancellationToken)
    {
        var native = root.GetProperty(Section);
        var tables = native.GetProperty("tables");
        var restore = new RestoreContext();
        await LoadUserMapAsync(context, native, restore, cancellationToken);

        foreach (var table in Tables)
        {
            if (!tables.TryGetProperty(table.Key, out var arr) || arr.ValueKind != JsonValueKind.Array) continue;
            await table.Import(context, arr, restore, cancellationToken);

            if (table.Key == "kiddingRecords")
            {
                foreach (var (goat, sire, dam, kidding) in restore.GoatLinks)
                {
                    goat.SireId = sire;
                    goat.DamId = dam;
                    goat.KiddingRecordId = kidding;
                }
                await context.SaveChangesAsync(cancellationToken);
            }
        }

        if (native.TryGetProperty("settings", out var settings) && settings.ValueKind == JsonValueKind.Object)
        {
            var existing = await context.AppSettings.ToDictionaryAsync(s => s.Key, StringComparer.Ordinal, cancellationToken);
            foreach (var prop in settings.EnumerateObject())
            {
                if (SkippedSettings.Contains(prop.Name)) continue;
                var value = prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() : prop.Value.GetRawText();
                if (existing.TryGetValue(prop.Name, out var row)) row.Value = value ?? string.Empty;
                else context.AppSettings.Add(new AppSetting { Key = prop.Name, Value = value ?? string.Empty });
            }
            await context.SaveChangesAsync(cancellationToken);
        }

        await ResetIdentitySequencesAsync(context, cancellationToken);
        context.ChangeTracker.Clear();
    }

    private static async Task LoadUserMapAsync(GoatFarmDbContext context, JsonElement native, RestoreContext restore, CancellationToken cancellationToken)
    {
        var users = await context.Users.AsNoTracking()
            .Select(u => new { u.Id, u.Email })
            .ToListAsync(cancellationToken);
        foreach (var u in users)
            restore.UserIds[u.Id] = u.Id;

        if (!native.TryGetProperty("userEmails", out var emails) || emails.ValueKind != JsonValueKind.Object) return;
        var byEmail = users.Where(u => u.Email != null)
            .GroupBy(u => u.Email!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
        foreach (var prop in emails.EnumerateObject())
        {
            if (restore.UserIds.ContainsKey(prop.Name)) continue;
            if (prop.Value.GetString() is { } email && byEmail.TryGetValue(email, out var id))
                restore.UserIds[prop.Name] = id;
        }
    }

    /// <summary>Rows were inserted with their original ids, so each identity sequence must continue after the highest id.</summary>
    private static async Task ResetIdentitySequencesAsync(GoatFarmDbContext context, CancellationToken cancellationToken)
    {
        if (!string.Equals(context.Database.ProviderName, "Npgsql.EntityFrameworkCore.PostgreSQL", StringComparison.Ordinal))
            return;

        foreach (var entityType in context.Model.GetEntityTypes())
        {
            if (!typeof(BaseEntity).IsAssignableFrom(entityType.ClrType)) continue;
            var table = entityType.GetTableName();
            if (table is null) continue;
            var schema = entityType.GetSchema();
            var qualified = schema is null ? $"\"{table}\"" : $"\"{schema}\".\"{table}\"";
            var sql =
                $"SELECT setval(pg_get_serial_sequence('{qualified}', 'Id'), COALESCE((SELECT MAX(\"Id\") FROM {qualified}), 0) + 1, false)";
            await context.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        }
    }
}

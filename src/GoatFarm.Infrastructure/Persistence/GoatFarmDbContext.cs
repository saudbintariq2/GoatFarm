using GoatFarm.Domain.Entities;
using GoatFarm.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Infrastructure.Persistence;

public class GoatFarmDbContext : IdentityDbContext<ApplicationUser, IdentityRole, string>
{
    public GoatFarmDbContext(DbContextOptions<GoatFarmDbContext> options) : base(options) { }

    public DbSet<Goat> Goats => Set<Goat>();
    public DbSet<GoatGroup> GoatGroups => Set<GoatGroup>();
    public DbSet<FeedPrice> FeedPrices => Set<FeedPrice>();
    public DbSet<FeedPlan> FeedPlans => Set<FeedPlan>();
    public DbSet<FeedPlanItem> FeedPlanItems => Set<FeedPlanItem>();
    public DbSet<FeedPurchase> FeedPurchases => Set<FeedPurchase>();
    public DbSet<RecurringCost> RecurringCosts => Set<RecurringCost>();
    public DbSet<VaccinePurchase> VaccinePurchases => Set<VaccinePurchase>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<Income> Incomes => Set<Income>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<OwnerInvestment> OwnerInvestments => Set<OwnerInvestment>();
    public DbSet<MilkProduction> MilkProductions => Set<MilkProduction>();
    public DbSet<MilkSale> MilkSales => Set<MilkSale>();
    public DbSet<MilkWaste> MilkWastes => Set<MilkWaste>();
    public DbSet<Vaccine> Vaccines => Set<Vaccine>();
    public DbSet<VaccinationHistory> VaccinationHistories => Set<VaccinationHistory>();
    public DbSet<Reminder> Reminders => Set<Reminder>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<BreedingEmptyLog> BreedingEmptyLogs => Set<BreedingEmptyLog>();
    public DbSet<GoatWeightRecord> GoatWeightRecords => Set<GoatWeightRecord>();
    public DbSet<DeathRecord> DeathRecords => Set<DeathRecord>();
    public DbSet<GoatSale> GoatSales => Set<GoatSale>();
    public DbSet<FeedUsageRecord> FeedUsageRecords => Set<FeedUsageRecord>();
    public DbSet<FarmTask> FarmTasks => Set<FarmTask>();
    public DbSet<BreedingEvent> BreedingEvents => Set<BreedingEvent>();
    public DbSet<KiddingRecord> KiddingRecords => Set<KiddingRecord>();
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();
    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();
    public DbSet<TradeItem> TradeItems => Set<TradeItem>();
    public DbSet<TradeStore> TradeStores => Set<TradeStore>();
    public DbSet<TradeAccount> TradeAccounts => Set<TradeAccount>();
    public DbSet<TradeParty> TradeParties => Set<TradeParty>();
    public DbSet<TradeExpenseCategory> TradeExpenseCategories => Set<TradeExpenseCategory>();
    public DbSet<TradeEntry> TradeEntries => Set<TradeEntry>();
    public DbSet<FarmProject> FarmProjects => Set<FarmProject>();
    public DbSet<ProjectExpenditure> ProjectExpenditures => Set<ProjectExpenditure>();
    public DbSet<FixedAssetRegister> FixedAssetRegisters => Set<FixedAssetRegister>();
    public DbSet<SolarProductionLog> SolarProductionLogs => Set<SolarProductionLog>();
    public DbSet<MoneyAccount> MoneyAccounts => Set<MoneyAccount>();
    public DbSet<LedgerTransaction> LedgerTransactions => Set<LedgerTransaction>();
    public DbSet<BusinessCustomer> BusinessCustomers => Set<BusinessCustomer>();
    public DbSet<CustomerInvoice> CustomerInvoices => Set<CustomerInvoice>();
    public DbSet<SupplierPayable> SupplierPayables => Set<SupplierPayable>();
    public DbSet<BudgetLine> BudgetLines => Set<BudgetLine>();
    public DbSet<ProfitSharePartner> ProfitSharePartners => Set<ProfitSharePartner>();
    public DbSet<ProductCatalogItem> ProductCatalogItems => Set<ProductCatalogItem>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<MilkingSession> MilkingSessions => Set<MilkingSession>();
    public DbSet<SlaughterRecord> SlaughterRecords => Set<SlaughterRecord>();
    public DbSet<ProductSale> ProductSales => Set<ProductSale>();
    public DbSet<GoatTreatment> GoatTreatments => Set<GoatTreatment>();
    public DbSet<CustomFeedingGroup> CustomFeedingGroups => Set<CustomFeedingGroup>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(GoatFarmDbContext).Assembly);
    }
}

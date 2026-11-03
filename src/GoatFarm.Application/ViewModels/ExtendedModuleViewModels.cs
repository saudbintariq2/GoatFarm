using System.ComponentModel.DataAnnotations;
using GoatFarm.Domain.Enums;

namespace GoatFarm.Application.ViewModels.Admin {

public class AuditNoticeViewModel
{
    public int Id { get; set; }
    public string Module { get; set; } = "";
    public string Summary { get; set; } = "";
    public string UserDisplay { get; set; } = "";
    public string WhenDisplay { get; set; } = "";
}

public class AuditRowViewModel
{
    public int Id { get; set; }
    public string Module { get; set; } = "";
    public string Action { get; set; } = "";
    public string Summary { get; set; } = "";
    public string UserDisplay { get; set; } = "";
    public string WhenDisplay { get; set; } = "";
}

public class AdminPageViewModel
{
    public IReadOnlyList<AuditRowViewModel> AuditLog { get; set; } = [];
    public int PendingApprovals { get; set; }
}

}

namespace GoatFarm.Application.ViewModels.Approval {

public class ApprovalRequestViewModel
{
    public int Id { get; set; }
    public string Module { get; set; } = "";
    public string ActionKey { get; set; } = "";
    public string Status { get; set; } = "";
    public string RequestedByDisplay { get; set; } = "";
    public string WhenDisplay { get; set; } = "";
    public string PayloadJson { get; set; } = "{}";
}

public class ApprovalPageViewModel
{
    public IReadOnlyList<ApprovalRequestViewModel> Pending { get; set; } = [];
    public IReadOnlyList<ApprovalRequestViewModel> Recent { get; set; } = [];
}

}

namespace GoatFarm.Application.ViewModels.Trading {

public class TradingPageViewModel
{
    public string Month { get; set; } = "";
    public decimal StockValue { get; set; }
    public decimal CashBalance { get; set; }
    public decimal MonthProfit { get; set; }
    public decimal MonthSales { get; set; }
    public decimal MonthBuys { get; set; }
    public decimal MonthExpenses { get; set; }
    public IReadOnlyList<TradePeriodPnLRowViewModel> PeriodPnL { get; set; } = [];
    public IReadOnlyList<TradeMasterRowViewModel> Items { get; set; } = [];
    public IReadOnlyList<TradeMasterRowViewModel> Stores { get; set; } = [];
    public IReadOnlyList<TradeAccountRowViewModel> Accounts { get; set; } = [];
    public IReadOnlyList<TradeMasterRowViewModel> Parties { get; set; } = [];
    public IReadOnlyList<TradeMasterRowViewModel> Categories { get; set; } = [];
    public IReadOnlyList<TradeEntryRowViewModel> Entries { get; set; } = [];
    public IReadOnlyList<object> TradeRecs { get; set; } = [];
}

public class TradePeriodPnLRowViewModel
{
    public string Month { get; set; } = "";
    public decimal Sales { get; set; }
    public decimal Buys { get; set; }
    public decimal Expenses { get; set; }
    public decimal Profit { get; set; }
}

public class TradeMasterRowViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Extra { get; set; }
    public bool IsActive { get; set; }
}

public class TradeAccountRowViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public decimal OpeningBalance { get; set; }
    public decimal Balance { get; set; }
    public bool IsActive { get; set; }
}

public class TradeEntryRowViewModel
{
    public int Id { get; set; }
    public string Type { get; set; } = "";
    public string DateDisplay { get; set; } = "";
    public string Label { get; set; } = "";
    public decimal Qty { get; set; }
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
}

public class SaveTradeMasterViewModel
{
    public string Kind { get; set; } = "";
    public int? Id { get; set; }
    [Required, MaxLength(200)] public string Name { get; set; } = "";
    public string? Unit { get; set; }
    public string? Type { get; set; }
    public string? Notes { get; set; }
    public string? Role { get; set; }
    public string? Phone { get; set; }
    public string? Place { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal DefaultPrice { get; set; }
    public string? UnitNote { get; set; }
    public bool IsActive { get; set; } = true;
}

public class SaveTradeEntryViewModel
{
    public int? Id { get; set; }
    [Required] public string Type { get; set; } = "";
    [Required] public DateOnly Date { get; set; }
    public int? ItemId { get; set; }
    public int? StoreId { get; set; }
    public int? ToStoreId { get; set; }
    public int? ToAccountId { get; set; }
    public int? PartyId { get; set; }
    public int? AccountId { get; set; }
    public int? CategoryId { get; set; }
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
    public decimal Freight { get; set; }
    public decimal Labour { get; set; }
    public decimal Paid { get; set; }
    public decimal Received { get; set; }
    public string? Payment { get; set; }
    public string? BillNo { get; set; }
    public string? Reason { get; set; }
    public string? Party { get; set; }
    public DateOnly? DueDate { get; set; }
    public string? Notes { get; set; }
    public string? CancelReason { get; set; }
}

public class CancelTradeEntryViewModel
{
    public int Id { get; set; }
    public string? Reason { get; set; }
}

}

namespace GoatFarm.Application.ViewModels.Projects {

public class ProjectsPageViewModel
{
    public IReadOnlyList<ProjectRowViewModel> Projects { get; set; } = [];
    public IReadOnlyList<ProjectExpenditureRowViewModel> Expenditures { get; set; } = [];
    public IReadOnlyList<FixedAssetRowViewModel> FixedAssets { get; set; } = [];
    public IReadOnlyList<SolarLogRowViewModel> SolarLogs { get; set; } = [];
    public IReadOnlyList<ProjectAccountOptionViewModel> Accounts { get; set; } = [];
    public IReadOnlyList<string> ProjectCategories { get; set; } = [];
    public IReadOnlyList<string> CostTypes { get; set; } = [];
    public IReadOnlyList<string> AssetCategories { get; set; } = [];
    public IReadOnlyList<string> FundingSources { get; set; } = [];
    public decimal TotalBudget { get; set; }
    public decimal TotalSpent { get; set; }
    public decimal TotalCip { get; set; }
    public decimal TotalCapex { get; set; }
    public decimal ActiveBudget { get; set; }
}

public class ProjectAccountOptionViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public class ProjectRowViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Note { get; set; }
    public string Category { get; set; } = "";
    public string Status { get; set; } = "";
    public string Funding { get; set; } = "";
    public decimal BudgetAmount { get; set; }
    public decimal Spent { get; set; }
    public decimal Cip { get; set; }
    public decimal Remaining { get; set; }
    public string? StartDate { get; set; }
}

public class ProjectExpenditureRowViewModel
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public string ProjectName { get; set; } = "";
    public string DateDisplay { get; set; } = "";
    public string CostType { get; set; } = "";
    public string Description { get; set; } = "";
    public string? Item { get; set; }
    public string? Model { get; set; }
    public decimal Qty { get; set; }
    public string? Unit { get; set; }
    public string? Spec { get; set; }
    public string? Warranty { get; set; }
    public string? Supplier { get; set; }
    public string? FundingSource { get; set; }
    public string PaymentStatus { get; set; } = "";
    public string? AccountName { get; set; }
    public int? AccountId { get; set; }
    public string? DueDate { get; set; }
    public string? Note { get; set; }
    public decimal Amount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal Payable { get; set; }
}

public class FixedAssetRowViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public decimal Cost { get; set; }
    public string? PurchaseDate { get; set; }
    public int? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public string? Location { get; set; }
    public string? Supplier { get; set; }
    public bool ProjectCapitalized { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal Due { get; set; }
}

public class SolarLogRowViewModel
{
    public int Id { get; set; }
    public int? ProjectId { get; set; }
    public string ProjectName { get; set; } = "";
    public string DateDisplay { get; set; } = "";
    public decimal KwhGenerated { get; set; }
    public decimal? SavingsEstimate { get; set; }
    public decimal MaintenanceAmount { get; set; }
    public decimal? InstalledCapacityKw { get; set; }
    public string? Notes { get; set; }
}

public class SaveProjectViewModel
{
    public int? Id { get; set; }
    [Required] public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string? Funding { get; set; }
    public string? Status { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? TargetEndDate { get; set; }
    public decimal BudgetAmount { get; set; }
}

public class SaveProjectExpenditureViewModel
{
    public int? Id { get; set; }
    public int ProjectId { get; set; }
    public DateOnly Date { get; set; }
    public string? CostType { get; set; }
    public string? Description { get; set; }
    public string? Item { get; set; }
    public decimal Qty { get; set; }
    public string? Unit { get; set; }
    public string? Model { get; set; }
    public string? Spec { get; set; }
    public DateOnly? WarrantyUntil { get; set; }
    public decimal Amount { get; set; }
    public decimal PaidAmount { get; set; }
    public string? PaymentStatus { get; set; }
    public int? AccountId { get; set; }
    public string? FundingSource { get; set; }
    public DateOnly? DueDate { get; set; }
    public string? Vendor { get; set; }
    public string? Note { get; set; }
}

public class SaveFixedAssetViewModel
{
    public int? Id { get; set; }
    [Required] public string Name { get; set; } = "";
    public string? Category { get; set; }
    public decimal Cost { get; set; }
    public DateOnly? PurchaseDate { get; set; }
    public int? ProjectId { get; set; }
    public string? Location { get; set; }
    public string? Supplier { get; set; }
    public decimal PaidAmount { get; set; }
    public int? AccountId { get; set; }
    public DateOnly? WarrantyUntil { get; set; }
    public string? Notes { get; set; }
}

public class SaveSolarLogViewModel
{
    public int? Id { get; set; }
    public int? ProjectId { get; set; }
    public DateOnly Date { get; set; }
    public decimal KwhGenerated { get; set; }
    public decimal? SavingsEstimate { get; set; }
    public decimal MaintenanceAmount { get; set; }
    public decimal? InstalledCapacityKw { get; set; }
    public string? Notes { get; set; }
}

public class PayProjectExpenditureViewModel
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public int AccountId { get; set; }
}

public class CapitalizeProjectViewModel
{
    public int ProjectId { get; set; }
    [Required] public string AssetName { get; set; } = "";
    public DateOnly? InServiceDate { get; set; }
}

}

namespace GoatFarm.Application.ViewModels.Ledger {

public class LedgerPageViewModel
{
    public string Month { get; set; } = "";
    public decimal Receivables { get; set; }
    public decimal Payables { get; set; }
    public decimal CashOnHand { get; set; }
    public IReadOnlyList<MoneyAccountRowViewModel> Accounts { get; set; } = [];
    public IReadOnlyList<LedgerTransactionRowViewModel> Transactions { get; set; } = [];
    public IReadOnlyList<CustomerInvoiceRowViewModel> Invoices { get; set; } = [];
    public IReadOnlyList<SupplierPayableRowViewModel> PayablesList { get; set; } = [];
    public IReadOnlyList<BudgetRowViewModel> Budget { get; set; } = [];
    public IReadOnlyList<ProfitPartnerRowViewModel> Partners { get; set; } = [];
    public IReadOnlyList<BusinessCustomerRowViewModel> Customers { get; set; } = [];
}

public class MoneyAccountRowViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "";
    public decimal Balance { get; set; }
    public decimal OpeningBalance { get; set; }
    public bool IsActive { get; set; } = true;
}

public class LedgerTransactionRowViewModel
{
    public int Id { get; set; }
    public string DateDisplay { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Account { get; set; } = "";
    public decimal Amount { get; set; }
    public string? Category { get; set; }
    public string? Notes { get; set; }
    public int? AccountId { get; set; }
    public int? CounterAccountId { get; set; }
    public string? CounterAccount { get; set; }
    public string? Reference { get; set; }
}

public class CustomerInvoiceRowViewModel
{
    public int Id { get; set; }
    public string Customer { get; set; } = "";
    public string DateDisplay { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal PaidAmount { get; set; }
    public string Status { get; set; } = "";
    public string? DueDateDisplay { get; set; }
}

public class SupplierPayableRowViewModel
{
    public int Id { get; set; }
    public string SupplierName { get; set; } = "";
    public string DateDisplay { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal PaidAmount { get; set; }
    public string Status { get; set; } = "";
    public string? DueDateDisplay { get; set; }
}

public class BudgetRowViewModel
{
    public int Id { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public string Category { get; set; } = "";
    public decimal PlannedAmount { get; set; }
}

public class ProfitPartnerRowViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal SharePercent { get; set; }
    public bool IsActive { get; set; } = true;
}

public class BusinessCustomerRowViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Phone { get; set; }
}

public class FinancialStatementsViewModel
{
    public string Month { get; set; } = "";
    public decimal Revenue { get; set; }
    public decimal Expenses { get; set; }
    public decimal NetProfit { get; set; }
    public decimal CashIn { get; set; }
    public decimal CashOut { get; set; }
    public decimal AssetsTotal { get; set; }
    public decimal LiabilitiesTotal { get; set; }
    public decimal EquityEstimate { get; set; }
}

public class SaveMoneyAccountViewModel
{
    public int? Id { get; set; }
    [Required] public string Name { get; set; } = "";
    public MoneyAccountKind Kind { get; set; }
    public decimal OpeningBalance { get; set; }
    public bool IsActive { get; set; } = true;
}

public class SaveLedgerTransactionViewModel
{
    public int? Id { get; set; }
    public DateOnly Date { get; set; }
    public LedgerTransactionKind Kind { get; set; }
    public int? AccountId { get; set; }
    public int? CounterAccountId { get; set; }
    public decimal Amount { get; set; }
    public string? Category { get; set; }
    public string? Notes { get; set; }
}

public class SaveCustomerInvoiceViewModel
{
    public int? Id { get; set; }
    public int CustomerId { get; set; }
    public DateOnly Date { get; set; }
    public DateOnly? DueDate { get; set; }
    [Required] public string Description { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal PaidAmount { get; set; }
    public int? AccountId { get; set; }
}

public class SaveSupplierPayableViewModel
{
    public int? Id { get; set; }
    [Required] public string SupplierName { get; set; } = "";
    public DateOnly Date { get; set; }
    public DateOnly? DueDate { get; set; }
    [Required] public string Description { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal PaidAmount { get; set; }
    public int? AccountId { get; set; }
}

public class SaveBudgetLineViewModel
{
    public int? Id { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    [Required] public string Category { get; set; } = "";
    public decimal PlannedAmount { get; set; }
}

public class SaveProfitSharePartnerViewModel
{
    public int? Id { get; set; }
    [Required] public string Name { get; set; } = "";
    public decimal SharePercent { get; set; }
    public bool IsActive { get; set; } = true;
}

public class SettleOpenBalanceViewModel
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public int AccountId { get; set; }
    public DateOnly Date { get; set; }
}

public class SaveBusinessCustomerViewModel
{
    public int? Id { get; set; }
    [Required] public string Name { get; set; } = "";
    public string? Phone { get; set; }
    public string? Place { get; set; }
}

}

namespace GoatFarm.Application.ViewModels.Products
{

using GoatFarm.Application.ViewModels.Ledger;

public class ProductsPageViewModel
{
    public string Month { get; set; } = "";
    public decimal StockValue { get; set; }
    public decimal MonthSalesTotal { get; set; }
    public decimal MilkInTanksLiters { get; set; }
    public ProductsMilkDashboardViewModel Milk { get; set; } = new();
    public ProductsMeatDashboardViewModel Meat { get; set; } = new();
    public IReadOnlyList<string> CustomMilkingSessions { get; set; } = [];
    public IReadOnlyList<MilkingSessionRowViewModel> RecentSessions { get; set; } = [];
    public IReadOnlyList<ProductCustomerRowViewModel> Customers { get; set; } = [];
    public IReadOnlyList<ProductCatalogRowViewModel> Catalog { get; set; } = [];
    public IReadOnlyList<WarehouseRowViewModel> Warehouses { get; set; } = [];
    public IReadOnlyList<StockMovementRowViewModel> Movements { get; set; } = [];
    public IReadOnlyList<MilkingSessionRowViewModel> Sessions { get; set; } = [];
    public IReadOnlyList<SlaughterRowViewModel> Slaughters { get; set; } = [];
    public IReadOnlyList<ProductSaleRowViewModel> Sales { get; set; } = [];
    public IReadOnlyList<ProductMilkingGoatRowViewModel> HerdGoats { get; set; } = [];
    public IReadOnlyList<MoneyAccountRowViewModel> MoneyAccounts { get; set; } = [];
    public IReadOnlyList<MilkLegacyRowViewModel> LegacyMilkEntries { get; set; } = [];
    /// <summary>All milking sessions for breed performance (client filters by month).</summary>
    public IReadOnlyList<MilkingSessionRowViewModel> AnalyticsMilkingSessions { get; set; } = [];
    /// <summary>Legacy single-line milk entries for breed performance.</summary>
    public IReadOnlyList<MilkLegacyRowViewModel> AnalyticsLegacyMilk { get; set; } = [];
    public IReadOnlyList<SlaughterRowViewModel> AnalyticsSlaughters { get; set; } = [];
    public IReadOnlyList<BreedPerformanceGoatSaleRowViewModel> AnalyticsGoatSales { get; set; } = [];
    public IReadOnlyList<TransportShipmentRowViewModel> TransportShipments { get; set; } = [];
    public IReadOnlyList<ProductCostAllocationRowViewModel> CostAllocations { get; set; } = [];
    /// <summary>User-added Product tab dropdown values (costTypes, shipmentTypes, units, etc.).</summary>
    public Dictionary<string, List<string>> ProductDropdownOptions { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<ProductPricingRowViewModel> ProductPricing { get; set; } = [];
    public IReadOnlyList<DeliveryRouteRowViewModel> DeliveryRoutes { get; set; } = [];
    public IReadOnlyList<ClientProductPriceRowViewModel> ClientPrices { get; set; } = [];
}

public class ProductPricingRowViewModel
{
    public int ProductId { get; set; }
    public decimal Base { get; set; }
    public decimal Packaging { get; set; }
    public decimal Transport { get; set; }
    public decimal Storage { get; set; }
    public decimal Other { get; set; }
    public decimal Sell { get; set; }
}

public class SaveProductPricingViewModel
{
    public int ProductId { get; set; }
    public decimal Base { get; set; }
    public decimal Packaging { get; set; }
    public decimal Transport { get; set; }
    public decimal Storage { get; set; }
    public decimal Other { get; set; }
    public decimal Sell { get; set; }
}

public class DeliveryRouteRowViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int ProductId { get; set; }
    public string? ProductName { get; set; }
    public decimal Qty { get; set; }
    public decimal Cost { get; set; }
}

public class SaveDeliveryRouteViewModel
{
    public string Name { get; set; } = "";
    public int ProductId { get; set; }
    public decimal Qty { get; set; }
    public decimal Cost { get; set; }
}

public class ClientProductPriceRowViewModel
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public string? ClientName { get; set; }
    public int ProductId { get; set; }
    public string? ProductName { get; set; }
    public decimal Rate { get; set; }
}

public class SaveClientProductPriceViewModel
{
    public int ClientId { get; set; }
    public int ProductId { get; set; }
    public decimal Rate { get; set; }
}

public class ProductCostAllocationRowViewModel
{
    public int Id { get; set; }
    public string DateDisplay { get; set; } = "";
    public string CostType { get; set; } = "";
    public decimal Amount { get; set; }
    public string Scope { get; set; } = "";
    public int? ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? Ref { get; set; }
    public decimal Qty { get; set; }
    public string? Note { get; set; }
}

public class SaveProductCostAllocationViewModel
{
    public DateOnly Date { get; set; }
    public string CostType { get; set; } = "";
    public decimal Amount { get; set; }
    public string Scope { get; set; } = "general";
    public int? ProductId { get; set; }
    public string? Ref { get; set; }
    public decimal Qty { get; set; }
    public string? Note { get; set; }
}

public class TransportShipmentRowViewModel
{
    public int Id { get; set; }
    public string DateDisplay { get; set; } = "";
    public string ShipmentType { get; set; } = "";
    public decimal Qty { get; set; }
    public string Unit { get; set; } = "";
    public string FromLocation { get; set; } = "";
    public string ToLocation { get; set; } = "";
    public decimal Cost { get; set; }
    public decimal CostPerUnit { get; set; }
    public string? Transporter { get; set; }
    public string DispatchRef { get; set; } = "";
    public string? Note { get; set; }
}

public class DeleteTransportShipmentRequest
{
    public int Id { get; set; }
}

public class SaveTransportShipmentViewModel
{
    public DateOnly Date { get; set; }
    public string ShipmentType { get; set; } = "Milk";
    public decimal Qty { get; set; }
    public string Unit { get; set; } = "L";
    public string? FromLocation { get; set; }
    public string ToLocation { get; set; } = "";
    public decimal Cost { get; set; }
    public string? Transporter { get; set; }
    public string? DispatchRef { get; set; }
    public string? Note { get; set; }
    public int? PaidFromAccountId { get; set; }
}

public class BreedPerformanceGoatSaleRowViewModel
{
    public string DateDisplay { get; set; } = "";
    public string Breed { get; set; } = "";
    public string Tag { get; set; } = "";
    public decimal Price { get; set; }
    public decimal? WeightKg { get; set; }
    public decimal Cost { get; set; }
}

public class ProductMilkingGoatRowViewModel
{
    public int Id { get; set; }
    public string Tag { get; set; } = "";
    public string? Name { get; set; }
    public string Breed { get; set; } = "";
    public string Gender { get; set; } = "";
    /// <summary>Lowercase key, e.g. milking, pregnant (matches prototype herd status).</summary>
    public string StatusKey { get; set; } = "";
    public string Status { get; set; } = "";
    public decimal PurchasePrice { get; set; }
    public decimal LiveWeightKg { get; set; }
}

public class ProductCustomerRowViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public class ProductCatalogRowViewModel
{
    public int Id { get; set; }
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "other";
    public string? Pack { get; set; }
    public string Unit { get; set; } = "";
    public decimal DefaultPrice { get; set; }
    public string? MilkTreatment { get; set; }
    public string? MeatType { get; set; }
    public bool IsActive { get; set; } = true;
}

public class WarehouseRowViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Location { get; set; }
    public string Kind { get; set; } = "other";
    public decimal LitersOrKg { get; set; }
    public decimal? Capacity { get; set; }
    public decimal? TargetTempC { get; set; }
    public bool IsActive { get; set; } = true;
}

public class ProductsMilkDashboardViewModel
{
    public decimal RawTankLiters { get; set; }
    public decimal PastTankLiters { get; set; }
    public decimal MilkedTodayLiters { get; set; }
    public decimal MilkedMonthLiters { get; set; }
    public decimal BulkSoldMonthLiters { get; set; }
    public int PasteurizeRunsMonth { get; set; }
    public decimal PasteurizeInMonth { get; set; }
    public decimal PasteurizeOutMonth { get; set; }
    public IReadOnlyList<ProductsDairyStockRowViewModel> DairyProducts { get; set; } = [];
}

public class ProductsDairyStockRowViewModel
{
    public int ProductId { get; set; }
    public int WarehouseId { get; set; }
    public string Name { get; set; } = "";
    public string? Pack { get; set; }
    public string Unit { get; set; } = "";
    public decimal Qty { get; set; }
}

public class ProductsMeatDashboardViewModel
{
    public int WholeCount { get; set; }
    public decimal WholeKg { get; set; }
    public int HalfCount { get; set; }
    public decimal HalfKg { get; set; }
    public decimal CutsKg { get; set; }
    public int CutsLines { get; set; }
    public int SlaughteredMonth { get; set; }
    public decimal SlaughteredCarcassKgMonth { get; set; }
}

public class StockMovementRowViewModel
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string DateDisplay { get; set; } = "";
    public string Type { get; set; } = "";
    public string Product { get; set; } = "";
    public string ProductSku { get; set; } = "";
    public string Warehouse { get; set; } = "";
    public string? ToWarehouse { get; set; }
    public decimal Qty { get; set; }
    public string? Notes { get; set; }
}

public class MilkLegacyRowViewModel
{
    public int Id { get; set; }
    public string DateDisplay { get; set; } = "";
    public string Breed { get; set; } = "";
    public decimal Liters { get; set; }
    public string? Comment { get; set; }
}

public class MilkingSessionRowViewModel
{
    public int Id { get; set; }
    public string DateDisplay { get; set; } = "";
    public string Session { get; set; } = "";
    public string Breed { get; set; } = "";
    public decimal Liters { get; set; }
    public decimal RejectedLiters { get; set; }
}

public class SlaughterRowViewModel
{
    public int Id { get; set; }
    public string DateDisplay { get; set; } = "";
    public string Tag { get; set; } = "";
    public string Breed { get; set; } = "";
    public decimal LiveWeightKg { get; set; }
    public decimal CarcassKg { get; set; }
    public decimal SaleAmount { get; set; }
}

public class ProductSaleRowViewModel
{
    public int Id { get; set; }
    public string DateDisplay { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string? Customer { get; set; }
    public decimal Qty { get; set; }
    public decimal Amount { get; set; }
    public bool IsCredit { get; set; }
}

public class SaveProductCatalogViewModel
{
    public int? Id { get; set; }
    [Required] public string Sku { get; set; } = "";
    [Required] public string Name { get; set; } = "";
    public string? Category { get; set; }
    public string? Pack { get; set; }
    public string Unit { get; set; } = "L";
    public decimal DefaultPrice { get; set; }
    public string? MilkTreatment { get; set; }
    public string? MeatType { get; set; }
    public bool? IsActive { get; set; }
}

public class SaveWarehouseViewModel
{
    public int? Id { get; set; }
    [Required] public string Name { get; set; } = "";
    public string? Location { get; set; }
    public bool? IsActive { get; set; }
}

public class SaveStockMovementViewModel
{
    public int? Id { get; set; }
    public DateOnly Date { get; set; }
    public StockMovementType Type { get; set; }
    public int ProductId { get; set; }
    public int WarehouseId { get; set; }
    public int? ToWarehouseId { get; set; }
    public decimal Qty { get; set; }
    public decimal UnitCost { get; set; }
    public string? Notes { get; set; }
}

public class SavePasteurizationViewModel
{
    public DateOnly Date { get; set; }
    public int SourceWarehouseId { get; set; }
    public int DestWarehouseId { get; set; }
    public decimal InputLiters { get; set; }
    public decimal OutputLiters { get; set; }
    public decimal ProcessCost { get; set; }
    public int? PaidFromAccountId { get; set; }
    public string? Note { get; set; }
    public string? RunRef { get; set; }
    public string? HeatTempC { get; set; }
    public string? CoolTempC { get; set; }
}

public class SaveMilkingSessionViewModel
{
    public int? Id { get; set; }
    public DateOnly Date { get; set; }
    public string Session { get; set; } = "AM";
    public string Breed { get; set; } = "Mixed";
    public decimal Liters { get; set; }
    public decimal RejectedLiters { get; set; }
    public int? WarehouseId { get; set; }
}

public class SaveSlaughterViewModel
{
    public int? Id { get; set; }
    public DateOnly Date { get; set; }
    public int? GoatId { get; set; }
    public string? TagText { get; set; }
    public decimal LiveWeightKg { get; set; }
    public decimal CarcassKg { get; set; }
    public decimal SaleAmount { get; set; }
}

public class SaveProductSaleViewModel
{
    public int? Id { get; set; }
    public DateOnly Date { get; set; }
    public int? CustomerId { get; set; }
    public int? ProductId { get; set; }
    [Required] public string ProductName { get; set; } = "";
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
    public bool IsCredit { get; set; }
    public string? Notes { get; set; }
}

}

namespace GoatFarm.Application.ViewModels.Treatments {

public class TreatmentsPageViewModel
{
    public IReadOnlyList<TreatmentRowViewModel> Active { get; set; } = [];
    public IReadOnlyList<TreatmentRowViewModel> History { get; set; } = [];
}

public class TreatmentRowViewModel
{
    public int Id { get; set; }
    public int GoatId { get; set; }
    public string Tag { get; set; } = "";
    public string Diagnosis { get; set; } = "";
    public string Medicine { get; set; } = "";
    public string StartDate { get; set; } = "";
    public string? EndDate { get; set; }
    public decimal Cost { get; set; }
    public int? WithdrawalDays { get; set; }
}

public class SaveTreatmentViewModel
{
    public int? Id { get; set; }
    public int GoatId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    [Required] public string Diagnosis { get; set; } = "";
    [Required] public string Medicine { get; set; } = "";
    public decimal Cost { get; set; }
    public int? WithdrawalDays { get; set; }
    public string? Notes { get; set; }
    /// <summary>Display label from lookup (e.g. Under treatment, Sick).</summary>
    public string? HealthStateLabel { get; set; }
    /// <summary>Cash/bank account when <see cref="Cost"/> &gt; 0.</summary>
    public int? AccountId { get; set; }
}

}

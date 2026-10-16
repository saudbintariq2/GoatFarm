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
    public decimal OpeningBalance { get; set; }
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
    public int? PartyId { get; set; }
    public int? AccountId { get; set; }
    public int? CategoryId { get; set; }
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
}

}

namespace GoatFarm.Application.ViewModels.Projects {

public class ProjectsPageViewModel
{
    public IReadOnlyList<ProjectRowViewModel> Projects { get; set; } = [];
    public IReadOnlyList<ProjectExpenditureRowViewModel> Expenditures { get; set; } = [];
    public IReadOnlyList<FixedAssetRowViewModel> FixedAssets { get; set; } = [];
    public IReadOnlyList<SolarLogRowViewModel> SolarLogs { get; set; } = [];
    public decimal TotalCapex { get; set; }
    public decimal ActiveBudget { get; set; }
}

public class ProjectRowViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Status { get; set; } = "";
    public decimal BudgetAmount { get; set; }
    public decimal Spent { get; set; }
    public string? StartDate { get; set; }
}

public class ProjectExpenditureRowViewModel
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public string ProjectName { get; set; } = "";
    public string DateDisplay { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
}

public class FixedAssetRowViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public decimal Cost { get; set; }
    public string? PurchaseDate { get; set; }
    public string? ProjectName { get; set; }
}

public class SolarLogRowViewModel
{
    public int Id { get; set; }
    public string DateDisplay { get; set; } = "";
    public decimal KwhGenerated { get; set; }
    public decimal? SavingsEstimate { get; set; }
}

public class SaveProjectViewModel
{
    public int? Id { get; set; }
    [Required] public string Name { get; set; } = "";
    public string? Description { get; set; }
    public ProjectStatus Status { get; set; } = ProjectStatus.Active;
    public DateOnly? StartDate { get; set; }
    public DateOnly? TargetEndDate { get; set; }
    public decimal BudgetAmount { get; set; }
}

public class SaveProjectExpenditureViewModel
{
    public int? Id { get; set; }
    public int ProjectId { get; set; }
    public DateOnly Date { get; set; }
    [Required] public string Description { get; set; } = "";
    public decimal Amount { get; set; }
    public string? Vendor { get; set; }
}

public class SaveFixedAssetViewModel
{
    public int? Id { get; set; }
    [Required] public string Name { get; set; } = "";
    public FixedAssetCategory Category { get; set; }
    public decimal Cost { get; set; }
    public DateOnly? PurchaseDate { get; set; }
    public int? ProjectId { get; set; }
    public string? Notes { get; set; }
}

public class SaveSolarLogViewModel
{
    public int? Id { get; set; }
    public DateOnly Date { get; set; }
    public decimal KwhGenerated { get; set; }
    public decimal? SavingsEstimate { get; set; }
    public string? Notes { get; set; }
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
}

public class SaveBusinessCustomerViewModel
{
    public int? Id { get; set; }
    [Required] public string Name { get; set; } = "";
    public string? Phone { get; set; }
    public string? Place { get; set; }
}

}

namespace GoatFarm.Application.ViewModels.Products {

public class ProductsPageViewModel
{
    public string Month { get; set; } = "";
    public decimal StockValue { get; set; }
    public IReadOnlyList<ProductCustomerRowViewModel> Customers { get; set; } = [];
    public IReadOnlyList<ProductCatalogRowViewModel> Catalog { get; set; } = [];
    public IReadOnlyList<WarehouseRowViewModel> Warehouses { get; set; } = [];
    public IReadOnlyList<StockMovementRowViewModel> Movements { get; set; } = [];
    public IReadOnlyList<MilkingSessionRowViewModel> Sessions { get; set; } = [];
    public IReadOnlyList<SlaughterRowViewModel> Slaughters { get; set; } = [];
    public IReadOnlyList<ProductSaleRowViewModel> Sales { get; set; } = [];
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
    public string Unit { get; set; } = "";
    public decimal DefaultPrice { get; set; }
}

public class WarehouseRowViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Location { get; set; }
}

public class StockMovementRowViewModel
{
    public int Id { get; set; }
    public string DateDisplay { get; set; } = "";
    public string Type { get; set; } = "";
    public string Product { get; set; } = "";
    public string Warehouse { get; set; } = "";
    public decimal Qty { get; set; }
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
    public string Unit { get; set; } = "L";
    public decimal DefaultPrice { get; set; }
}

public class SaveWarehouseViewModel
{
    public int? Id { get; set; }
    [Required] public string Name { get; set; } = "";
    public string? Location { get; set; }
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

public class SaveMilkingSessionViewModel
{
    public int? Id { get; set; }
    public DateOnly Date { get; set; }
    public string Session { get; set; } = "AM";
    public string Breed { get; set; } = "Mixed";
    public decimal Liters { get; set; }
    public decimal RejectedLiters { get; set; }
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
}

}

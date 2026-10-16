using GoatFarm.Domain.Common;
using GoatFarm.Domain.Enums;

namespace GoatFarm.Domain.Entities;

public class AuditLogEntry : BaseEntity
{
    public string Module { get; set; } = string.Empty;
    public AuditAction Action { get; set; }
    public string? EntityType { get; set; }
    public int? EntityId { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string? DetailJson { get; set; }
    public string? UserId { get; set; }
    public string? UserDisplay { get; set; }
    public bool ShowInNotices { get; set; } = true;
}

public class ApprovalRequest : BaseEntity
{
    public string Module { get; set; } = string.Empty;
    public string ActionKey { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
    public string RequestedByUserId { get; set; } = string.Empty;
    public string? RequestedByDisplay { get; set; }
    public string? ReviewedByUserId { get; set; }
    public string? ReviewNote { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
}

public class TradeItem : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = "kg";
    public string? UnitNote { get; set; }
    public decimal DefaultPrice { get; set; }
    public bool IsActive { get; set; } = true;
}

public class TradeStore : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public class TradeAccount : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public TradeAccountType Type { get; set; }
    public decimal OpeningBalance { get; set; }
    public bool IsActive { get; set; } = true;
}

public class TradeParty : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public TradePartyRole Role { get; set; }
    public string? Phone { get; set; }
    public string? Place { get; set; }
    public decimal OpeningBalance { get; set; }
    public bool IsActive { get; set; } = true;
}

public class TradeExpenseCategory : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public class TradeEntry : BaseEntity
{
    public TradeEntryType Type { get; set; }
    public DateOnly Date { get; set; }
    public int? ItemId { get; set; }
    public TradeItem? Item { get; set; }
    public int? StoreId { get; set; }
    public TradeStore? Store { get; set; }
    public int? ToStoreId { get; set; }
    public TradeStore? ToStore { get; set; }
    public int? PartyId { get; set; }
    public TradeParty? Party { get; set; }
    public int? AccountId { get; set; }
    public TradeAccount? Account { get; set; }
    public int? CategoryId { get; set; }
    public TradeExpenseCategory? Category { get; set; }
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
    public bool IsCancelled { get; set; }
}

public class FarmProject : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ProjectStatus Status { get; set; } = ProjectStatus.Planned;
    public DateOnly? StartDate { get; set; }
    public DateOnly? TargetEndDate { get; set; }
    public decimal BudgetAmount { get; set; }
}

public class ProjectExpenditure : BaseEntity
{
    public int ProjectId { get; set; }
    public FarmProject Project { get; set; } = null!;
    public DateOnly Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Vendor { get; set; }
}

public class FixedAssetRegister : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public FixedAssetCategory Category { get; set; }
    public decimal Cost { get; set; }
    public DateOnly? PurchaseDate { get; set; }
    public int? ProjectId { get; set; }
    public FarmProject? Project { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
}

public class SolarProductionLog : BaseEntity
{
    public DateOnly Date { get; set; }
    public decimal KwhGenerated { get; set; }
    public decimal? SavingsEstimate { get; set; }
    public string? Notes { get; set; }
}

public class MoneyAccount : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public MoneyAccountKind Kind { get; set; }
    public decimal OpeningBalance { get; set; }
    public bool IsActive { get; set; } = true;
}

public class LedgerTransaction : BaseEntity
{
    public DateOnly Date { get; set; }
    public LedgerTransactionKind Kind { get; set; }
    public int? AccountId { get; set; }
    public MoneyAccount? Account { get; set; }
    public int? CounterAccountId { get; set; }
    public MoneyAccount? CounterAccount { get; set; }
    public decimal Amount { get; set; }
    public string? Category { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
}

public class BusinessCustomer : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Place { get; set; }
    public decimal OpeningBalance { get; set; }
    public bool IsActive { get; set; } = true;
}

public class CustomerInvoice : BaseEntity
{
    public int CustomerId { get; set; }
    public BusinessCustomer Customer { get; set; } = null!;
    public DateOnly Date { get; set; }
    public DateOnly? DueDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal PaidAmount { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Open;
}

public class SupplierPayable : BaseEntity
{
    public string SupplierName { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public DateOnly? DueDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal PaidAmount { get; set; }
    public PayableStatus Status { get; set; } = PayableStatus.Open;
}

public class BudgetLine : BaseEntity
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string Category { get; set; } = string.Empty;
    public decimal PlannedAmount { get; set; }
}

public class ProfitSharePartner : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public decimal SharePercent { get; set; }
    public bool IsActive { get; set; } = true;
}

public class ProductCatalogItem : BaseEntity
{
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = "L";
    public decimal DefaultPrice { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Warehouse : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Location { get; set; }
    public bool IsActive { get; set; } = true;
}

public class StockMovement : BaseEntity
{
    public DateOnly Date { get; set; }
    public StockMovementType Type { get; set; }
    public int ProductId { get; set; }
    public ProductCatalogItem Product { get; set; } = null!;
    public int WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;
    public int? ToWarehouseId { get; set; }
    public Warehouse? ToWarehouse { get; set; }
    public decimal Qty { get; set; }
    public decimal UnitCost { get; set; }
    public string? Notes { get; set; }
}

public class MilkingSession : BaseEntity
{
    public DateOnly Date { get; set; }
    public string Session { get; set; } = "AM";
    public string Breed { get; set; } = "Mixed";
    public decimal Liters { get; set; }
    public decimal RejectedLiters { get; set; }
    public string? Notes { get; set; }
}

public class SlaughterRecord : BaseEntity
{
    public DateOnly Date { get; set; }
    public int? GoatId { get; set; }
    public Goat? Goat { get; set; }
    public string? TagText { get; set; }
    public decimal LiveWeightKg { get; set; }
    public decimal CarcassKg { get; set; }
    public decimal SaleAmount { get; set; }
    public string? Notes { get; set; }
}

public class ProductSale : BaseEntity
{
    public DateOnly Date { get; set; }
    public int? CustomerId { get; set; }
    public BusinessCustomer? Customer { get; set; }
    public int? ProductId { get; set; }
    public ProductCatalogItem? Product { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
    public bool IsCredit { get; set; }
    public string? Notes { get; set; }
}

public class GoatTreatment : BaseEntity
{
    public int GoatId { get; set; }
    public Goat Goat { get; set; } = null!;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string Diagnosis { get; set; } = string.Empty;
    public string Medicine { get; set; } = string.Empty;
    public decimal Cost { get; set; }
    public int? WithdrawalDays { get; set; }
    public string? Notes { get; set; }
}

public class CustomFeedingGroup : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal MixKgPerDay { get; set; }
    public decimal FodderKgPerDay { get; set; }
}

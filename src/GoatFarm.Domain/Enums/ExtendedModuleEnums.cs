namespace GoatFarm.Domain.Enums;

public enum AuditAction
{
    Create = 0,
    Update = 1,
    Delete = 2,
    Approve = 3,
    Reject = 4,
    Import = 5,
    Export = 6
}

public enum ApprovalStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Cancelled = 3
}

public enum TradeAccountType
{
    Cash = 0,
    Bank = 1
}

public enum TradePartyRole
{
    Customer = 0,
    Supplier = 1,
    Both = 2
}

public enum TradeEntryType
{
    Buy = 0,
    Sell = 1,
    Expense = 2,
    PaymentIn = 3,
    PaymentOut = 4,
    Transfer = 5
}

public enum MoneyAccountKind
{
    Cash = 0,
    Bank = 1
}

public enum LedgerTransactionKind
{
    Income = 0,
    Expense = 1,
    Transfer = 2,
    InvoicePayment = 3,
    PayablePayment = 4
}

public enum InvoiceStatus
{
    Open = 0,
    Partial = 1,
    Paid = 2,
    Void = 3
}

public enum PayableStatus
{
    Open = 0,
    Partial = 1,
    Paid = 2,
    Void = 3
}

public enum StockMovementType
{
    Receive = 0,
    Issue = 1,
    Transfer = 2,
    Spoilage = 3,
    Adjust = 4
}

public enum ProjectStatus
{
    Planned = 0,
    Active = 1,
    Completed = 2,
    OnHold = 3
}

public enum FixedAssetCategory
{
    Machinery = 0,
    Building = 1,
    Land = 2,
    Solar = 3,
    Other = 4
}

namespace GoatFarm.Domain.Constants;

/// <summary>Which module record created an Income, Expense or ledger row.</summary>
public static class FinanceSourceTypes
{
    public const string GoatSale = "GoatSale";
    public const string GoatPurchase = "GoatPurchase";
    public const string Death = "Death";
    public const string SupplierBill = "SupplierBill";
    public const string Transport = "Transport";
    public const string MilkProcessing = "MilkProcessing";
    public const string FeedPurchasePl = "FeedPurchasePl";
    public const string FodderPl = "FodderPl";
    public const string Treatment = "Treatment";
    public const string ProductCostAllocation = "ProductCostAllocation";

    /// <summary>
    /// Expenses that record a cost without moving cash (paid later through a bill, spread from a purchase that
    /// already moved cash, or a write-off). They must never get their own Cash/Bank row.
    /// </summary>
    public static bool IsNonCashExpense(string? sourceType, string? type = null, string? comment = null)
    {
        if (sourceType is SupplierBill or Death or FeedPurchasePl or FodderPl)
            return true;
        if (string.Equals(type, "Livestock loss – death", StringComparison.Ordinal))
            return true;
        if (comment is null) return false;
        return (comment.StartsWith("feed_purchase:", StringComparison.Ordinal)
                || comment.StartsWith("fodder_pool:", StringComparison.Ordinal))
               && comment.Contains(":pl:", StringComparison.Ordinal);
    }
}

/// <summary>
/// Ledger references that belong to exactly one source record. Each of these may have at most one ledger row,
/// enforced by a unique index on (SourceType, SourceId).
/// </summary>
public static class LedgerSourceKey
{
    private static readonly string[] SingleRowPrefixes =
    [
        "fin-income", "fin-expense", "fin-funding", "feed_purchase", "vaccine_purchase",
        "fixed_asset", "capex", "fodder_pool", "product_sale"
    ];

    public static string ProductSale(int id) => $"product_sale:{id}";

    public static bool TryParse(string? reference, out string sourceType, out string sourceId)
    {
        sourceType = "";
        sourceId = "";
        if (string.IsNullOrWhiteSpace(reference)) return false;
        var split = reference.IndexOf(':');
        if (split <= 0 || split == reference.Length - 1) return false;
        var prefix = reference[..split];
        var rest = reference[(split + 1)..];
        if (rest.Contains(':')) return false;
        if (!SingleRowPrefixes.Contains(prefix, StringComparer.Ordinal)) return false;
        sourceType = prefix;
        sourceId = rest;
        return true;
    }
}

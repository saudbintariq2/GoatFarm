namespace GoatFarm.Application.Common;

/// <summary>Manual Finance expenses that duplicate auto-tracked module purchases.</summary>
public static class FinanceExpenseRules
{
    public const string ManualVaccineType = "Vaccines";

    public static bool IsDuplicateVaccinePurchase(string? expenseType) =>
        string.Equals(expenseType?.Trim(), ManualVaccineType, StringComparison.OrdinalIgnoreCase);

    public static bool IsFodderPoolManaged(string? comment) =>
        !string.IsNullOrEmpty(comment) &&
        comment.StartsWith("fodder_pool:", StringComparison.OrdinalIgnoreCase);

    public const string FodderPoolEditBlockedMessage =
        "This cost comes from Feed › Green fodder cost. Edit it on the Feed tab only.";
}

/// <summary>Shared rules so Finance, Dashboard and Reports count each sale exactly once.</summary>
public static class FinanceIncomeRules
{
    public const string OtherFarmSaleLabel = "Other farm sale";
    public const string LiveGoatSaleLabel = "Live goat sale";
    /// <summary>Legacy Milk tab label — never add to P&amp;L; milk income comes from Products › Sales only.</summary>
    public const string LegacyMilkLabel = "Milk sales";

    public static bool IsMilkProduct(string? productName) =>
        !string.IsNullOrWhiteSpace(productName) &&
        productName.Contains("milk", StringComparison.OrdinalIgnoreCase);

    /// <summary>Invoices created by Products › Sales or Herd › Sell goat are already counted from their sale rows.</summary>
    public static bool IsSaleGeneratedInvoice(string? description) =>
        !string.IsNullOrEmpty(description) &&
        (description.StartsWith("Product sale", StringComparison.OrdinalIgnoreCase)
         || description.StartsWith("Live goat sale", StringComparison.OrdinalIgnoreCase));
}

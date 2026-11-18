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

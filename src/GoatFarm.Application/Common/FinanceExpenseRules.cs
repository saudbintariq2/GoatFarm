namespace GoatFarm.Application.Common;

/// <summary>Manual Finance expenses that duplicate auto-tracked module purchases.</summary>
public static class FinanceExpenseRules
{
    public const string ManualVaccineType = "Vaccines";

    public static bool IsDuplicateVaccinePurchase(string? expenseType) =>
        string.Equals(expenseType?.Trim(), ManualVaccineType, StringComparison.OrdinalIgnoreCase);
}

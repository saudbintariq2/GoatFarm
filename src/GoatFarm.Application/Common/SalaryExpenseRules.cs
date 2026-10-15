namespace GoatFarm.Application.Common;

public enum SalaryExpenseKind
{
    None,
    SalaryOrWage,
    SalaryAdvance
}

/// <summary>
/// Staff salaries are counted automatically every month from the Employees list (current monthly salary × months).
/// Any manual expense or recurring cost for a salary, wage or salary advance is therefore counted a second time.
/// </summary>
public static class SalaryExpenseRules
{
    // English, Roman Urdu and Urdu words farm staff use for salary / advance.
    private static readonly string[] SalaryWords = ["salary", "salaries", "wage", "tankhwah", "tankhah", "tankha", "تنخواہ", "اجرت"];
    private static readonly string[] AdvanceWords = ["advance", "peshgi", "پیشگی", "ایڈوانس"];
    private static readonly string[] StaffWords = ["staff", "worker", "labour", "labor", "employee", "mazdoor", "ملازم", "مزدور"];

    public static SalaryExpenseKind Classify(string? type)
    {
        var t = (type ?? string.Empty).Trim().ToLowerInvariant();
        if (t.Length == 0) return SalaryExpenseKind.None;
        var salary = SalaryWords.Any(t.Contains);
        var advance = AdvanceWords.Any(t.Contains);
        if (advance && (salary || StaffWords.Any(t.Contains) || AdvanceWords.Contains(t)))
            return SalaryExpenseKind.SalaryAdvance;
        return salary ? SalaryExpenseKind.SalaryOrWage : SalaryExpenseKind.None;
    }

    /// <summary>Free text (a comment, or a type that is not itself a salary type) that suggests a salary payment.</summary>
    public static bool MentionsSalary(string? text)
    {
        var t = (text ?? string.Empty).ToLowerInvariant();
        return t.Length > 0 && (SalaryWords.Any(t.Contains) || AdvanceWords.Any(t.Contains));
    }

    public const string AdvanceBlockedMessage =
        "A salary advance is part of the monthly salary that is already counted automatically from the Employees list, so it cannot be added as an expense. Adding it here would count the same money twice.";

    public const string SalaryBlockedMessage =
        "Staff salaries are added to running costs automatically from the Employees list. Update the employee's monthly salary instead of adding a salary expense.";

    public const string RecurringBlockedMessage =
        "Staff salaries are already counted every month from the Employees list. Add the person on the Employees list instead of as a recurring cost, or the salary is counted twice.";
}

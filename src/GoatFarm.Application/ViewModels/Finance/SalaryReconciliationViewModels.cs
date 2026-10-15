namespace GoatFarm.Application.ViewModels.Finance;

public enum SalaryReconciliationOutcome
{
    /// <summary>A salary/wage/advance row that names a person on the Employees list: that salary is already counted.</summary>
    ConfirmedDoubleCount,
    /// <summary>Looks like a salary payment but cannot be matched to an employee — the owner must check it.</summary>
    NeedsReview,
    /// <summary>No employee salaries are on file, so nothing is counted automatically to overlap with this row.</summary>
    NotDoubleCounted
}

/// <summary>Read-only. Nothing in this report changes any record.</summary>
public class SalaryReconciliationViewModel
{
    public decimal AutoSalaryMonthly { get; set; }
    public int EmployeeCount { get; set; }
    public IReadOnlyList<SalaryReconciliationRowViewModel> Rows { get; set; } = [];
    public IReadOnlyList<SalaryReconciliationMonthViewModel> Months { get; set; } = [];
    public decimal ConfirmedTotal { get; set; }
    public decimal NeedsReviewTotal { get; set; }
    public decimal NotDoubleCountedTotal { get; set; }
    /// <summary>Recurring costs are counted every month, so this is a monthly figure, not part of the totals above.</summary>
    public decimal RecurringFlaggedMonthly { get; set; }
    public bool HasFindings => Rows.Count > 0;
}

public class SalaryReconciliationRowViewModel
{
    /// <summary>"Expense" or "Recurring cost".</summary>
    public string Source { get; set; } = string.Empty;
    public int Id { get; set; }
    public DateOnly? Date { get; set; }
    public string DateDisplay => Date?.ToString("yyyy-MM-dd") ?? "every month";
    public string Type { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public decimal Amount { get; set; }
    public SalaryReconciliationOutcome Outcome { get; set; }
    public string OutcomeLabel => Outcome switch
    {
        SalaryReconciliationOutcome.ConfirmedDoubleCount => "Counted twice",
        SalaryReconciliationOutcome.NeedsReview => "Needs review",
        _ => "Not counted twice"
    };
    public string Reason { get; set; } = string.Empty;
    public string? MatchedEmployee { get; set; }
}

/// <summary>Running costs for the month as reported today, and how much of that the flagged rows add.</summary>
public class SalaryReconciliationMonthViewModel
{
    public string Month { get; set; } = string.Empty;
    public decimal AutoSalary { get; set; }
    public decimal ConfirmedAmount { get; set; }
    public decimal NeedsReviewAmount { get; set; }
}

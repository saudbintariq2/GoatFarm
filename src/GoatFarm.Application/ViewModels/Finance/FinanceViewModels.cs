using System.ComponentModel.DataAnnotations;

namespace GoatFarm.Application.ViewModels.Finance;

public class AssetViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public decimal Cost { get; set; }
    public string? Comment { get; set; }
}

public class IncomeViewModel
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateOnly Date { get; set; }
    public string DateDisplay => Date.ToString("yyyy-MM-dd");
    public string? Comment { get; set; }
    public int? AccountId { get; set; }
    public string? AccountName { get; set; }
}

public class ExpenseViewModel
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateOnly Date { get; set; }
    public string DateDisplay => Date.ToString("yyyy-MM-dd");
    public string? Comment { get; set; }
    public int? AccountId { get; set; }
    public string? AccountName { get; set; }
}

public class RecurringCostViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Period { get; set; } = "month";
    public decimal MonthlyAmount { get; set; }
}

public class CreateRecurringCostViewModel
{
    [Required(ErrorMessage = "Enter a name")]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Enter amount")]
    public decimal Amount { get; set; }

    [Required]
    public string Period { get; set; } = "month";
}

public class EmployeeViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Role { get; set; }
    public decimal MonthlySalary { get; set; }
}

public class CreateEmployeeViewModel
{
    [Required(ErrorMessage = "Enter employee name")]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Role { get; set; }

    [Required]
    [Range(0, double.MaxValue, ErrorMessage = "Enter salary")]
    public decimal MonthlySalary { get; set; }
}

public class OwnerInvestmentViewModel
{
    public int Id { get; set; }
    public string Note { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateOnly Date { get; set; }
    public string DateDisplay => Date.ToString("yyyy-MM-dd");
    public int? AccountId { get; set; }
    public string? AccountName { get; set; }
}

public class CreateAssetViewModel
{
    [Required(ErrorMessage = "Enter asset name")]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Type { get; set; } = "Machinery";

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Enter cost")]
    public decimal Cost { get; set; }

    [StringLength(500)]
    public string? Comment { get; set; }
}

public class CreateIncomeViewModel
{
    [Required]
    public string Type { get; set; } = string.Empty;

    [Required]
    public DateOnly Date { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Enter amount")]
    public decimal Amount { get; set; }

    [StringLength(500)]
    public string? Comment { get; set; }

    public int? AccountId { get; set; }
}

public class CreateExpenseViewModel
{
    [Required]
    public string Type { get; set; } = string.Empty;

    [Required]
    public DateOnly Date { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Enter amount")]
    public decimal Amount { get; set; }

    [StringLength(500)]
    public string? Comment { get; set; }

    /// <summary>Set after the user has seen and accepted a possible-duplicate warning.</summary>
    public bool ConfirmPossibleDuplicate { get; set; }

    public int? AccountId { get; set; }
}

public class CreateOwnerInvestmentViewModel
{
    [Required(ErrorMessage = "Enter a note")]
    [StringLength(200)]
    public string Note { get; set; } = string.Empty;

    [Required]
    public DateOnly Date { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Enter amount")]
    public decimal Amount { get; set; }

    public string? Shareholder { get; set; }
    public string? FundingType { get; set; }
    public int? AccountId { get; set; }
}

public class FinanceLineViewModel
{
    public string Label { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class FinanceTrendMonthViewModel
{
    public string Month { get; set; } = string.Empty;
    public decimal Income { get; set; }
    public decimal Expense { get; set; }
}

public class SaleRegisterRowViewModel
{
    public string Source { get; set; } = string.Empty;
    public int Id { get; set; }
    public string DateDisplay { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Customer { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal Received { get; set; }
    public decimal Due { get; set; }
    public string? DueDate { get; set; }
    public string? Note { get; set; }
}

public class CapexRowViewModel
{
    public string DateDisplay { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Supplier { get; set; }
    public decimal Amount { get; set; }
}

public class FinanceStatementViewModel
{
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public decimal Income { get; set; }
    public decimal Expense { get; set; }
    public IReadOnlyList<FinanceLineViewModel> IncomeLines { get; set; } = [];
    public IReadOnlyList<FinanceLineViewModel> ExpenseLines { get; set; } = [];
}

public class FinancePageViewModel
{
    public string Month { get; set; } = string.Empty;
    public decimal Profit { get; set; }
    public bool IsLoss { get; set; }
    public string ProfitNote { get; set; } = string.Empty;
    public decimal Capital { get; set; }
    public decimal LivestockValue { get; set; }
    public int BoughtGoatCount { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal TotalExpense { get; set; }
    public decimal FeedMonthly { get; set; }
    public decimal FeedBoughtMonthly { get; set; }
    public decimal FeedBoughtKg { get; set; }
    public decimal MedicineMonthly { get; set; }
    public decimal RecurringMonthly { get; set; }
    public decimal StaffSalaryMonthly { get; set; }
    public decimal VaccineBoughtMonthly { get; set; }
    public decimal ManualExpenseMonthly { get; set; }
    public decimal MilkIncome { get; set; }
    public decimal MilkLitersSold { get; set; }
    public decimal OwnerInvestmentMonthTotal { get; set; }
    public decimal OwnerInvestmentTotal { get; set; }
    public IReadOnlyList<AssetViewModel> Assets { get; set; } = [];
    public IReadOnlyList<IncomeViewModel> Incomes { get; set; } = [];
    public IReadOnlyList<ExpenseViewModel> Expenses { get; set; } = [];
    public IReadOnlyList<RecurringCostViewModel> RecurringCosts { get; set; } = [];
    public IReadOnlyList<EmployeeViewModel> Employees { get; set; } = [];
    public IReadOnlyList<OwnerInvestmentViewModel> OwnerInvestments { get; set; } = [];
    public IReadOnlyList<OwnerInvestmentViewModel> FundingHistory { get; set; } = [];
    public decimal DisplayIncome { get; set; }
    public decimal DisplayExpense { get; set; }
    public decimal FeedStockValue { get; set; }
    public decimal ProjectSpend { get; set; }
    public decimal FixedAssetRegisterValue { get; set; }
    public decimal OpeningCash { get; set; }
    public decimal GoatsBoughtMonth { get; set; }
    public IReadOnlyList<FinanceLineViewModel> IncomeLines { get; set; } = [];
    public IReadOnlyList<FinanceLineViewModel> ExpenseLines { get; set; } = [];
    public IReadOnlyList<FinanceTrendMonthViewModel> Trend { get; set; } = [];
    public IReadOnlyList<SaleRegisterRowViewModel> Sales { get; set; } = [];
    public IReadOnlyList<CapexRowViewModel> Capex { get; set; } = [];
    public CreateAssetViewModel NewAsset { get; set; } = new();
    public CreateIncomeViewModel NewIncome { get; set; } = new() { Date = DateOnly.FromDateTime(DateTime.Today) };
    public CreateExpenseViewModel NewExpense { get; set; } = new() { Date = DateOnly.FromDateTime(DateTime.Today) };
    public CreateOwnerInvestmentViewModel NewOwnerInvestment { get; set; } = new() { Date = DateOnly.FromDateTime(DateTime.Today) };
    public FinanceBalanceSheetViewModel BalanceSheet { get; set; } = new();
    public FinanceIntegrityViewModel Integrity { get; set; } = new();
    public IReadOnlyList<FinanceMonthlyClosingRowViewModel> MonthlyClosing { get; set; } = [];
}

public class FinanceMonthlyClosingRowViewModel
{
    public string Month { get; set; } = string.Empty;
    public decimal OpeningCash { get; set; }
    public decimal MoneyIn { get; set; }
    public decimal MoneyOut { get; set; }
    public decimal ClosingCash { get; set; }
    public decimal ReceivablesOpen { get; set; }
    public decimal PayablesOpen { get; set; }
}

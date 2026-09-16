namespace GoatFarm.Application.ViewModels.Reports;

public class ReportsPageViewModel
{
    public string Period { get; set; } = "month";
    public string? CustomFrom { get; set; }
    public string? CustomTo { get; set; }
    public string RangeLabel { get; set; } = string.Empty;
    public int MonthCount { get; set; } = 1;
    public decimal TotalIncome { get; set; }
    public decimal TotalExpense { get; set; }
    public decimal TotalOwnerInvestment { get; set; }
    public decimal Profit { get; set; }
    public bool PreRevenue { get; set; }
    public string ProfitLabel { get; set; } = "Profit";
    public string NoteHtml { get; set; } = string.Empty;
    public IReadOnlyList<ReportOwnerRowViewModel> OwnerRows { get; set; } = [];
    public IReadOnlyList<ReportCategoryRowViewModel> ExpenseCategories { get; set; } = [];
    public IReadOnlyList<ReportCategoryRowViewModel> IncomeSources { get; set; } = [];
    public IReadOnlyList<ReportMonthRowViewModel> MonthRows { get; set; } = [];
    public IReadOnlyList<ReportDayRowViewModel> DayRows { get; set; } = [];
    public IReadOnlyList<ReportExpenseEntryViewModel> ExpenseEntries { get; set; } = [];
}

public class ReportOwnerRowViewModel
{
    public string Date { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class ReportCategoryRowViewModel
{
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int Percent { get; set; }
    public string? Extra { get; set; }
    public int BarWidth { get; set; }
}

public class ReportMonthRowViewModel
{
    public string Month { get; set; } = string.Empty;
    public decimal Income { get; set; }
    public decimal Expense { get; set; }
    public decimal Profit { get; set; }
}

public class ReportDayRowViewModel
{
    public string Date { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public decimal Income { get; set; }
    public decimal Expense { get; set; }
    public decimal OwnerInvestment { get; set; }
}

public class ReportExpenseEntryViewModel
{
    public string Date { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public decimal Amount { get; set; }
}

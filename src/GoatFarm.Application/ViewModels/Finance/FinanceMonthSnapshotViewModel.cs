namespace GoatFarm.Application.ViewModels.Finance;

public class FinanceMonthSnapshotViewModel
{
    public string Month { get; set; } = string.Empty;
    public decimal TotalIncome { get; set; }
    public decimal TotalExpense { get; set; }
    public decimal Profit { get; set; }
    public bool IsLoss { get; set; }
    public decimal Capital { get; set; }
    public IReadOnlyList<FinanceLineViewModel> IncomeLines { get; set; } = [];
    public IReadOnlyList<FinanceLineViewModel> ExpenseLines { get; set; } = [];
    public IReadOnlyList<IncomeViewModel> Incomes { get; set; } = [];
    public IReadOnlyList<ExpenseViewModel> Expenses { get; set; } = [];
}

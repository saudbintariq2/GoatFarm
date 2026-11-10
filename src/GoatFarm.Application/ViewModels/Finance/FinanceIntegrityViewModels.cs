namespace GoatFarm.Application.ViewModels.Finance;

public class FinanceIntegrityIssueViewModel
{
    public string Severity { get; set; } = "warn";
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class FinanceIntegrityViewModel
{
    public IReadOnlyList<FinanceIntegrityIssueViewModel> Issues { get; set; } = [];
    public int CriticalCount { get; set; }
    public int WarningCount { get; set; }
    public int UnlinkedFundingCount { get; set; }
    public int UnlinkedIncomeCount { get; set; }
    public int UnlinkedExpenseCount { get; set; }
}

public class FinanceBalanceSheetViewModel
{
    public decimal CashAndBank { get; set; }
    public decimal Receivables { get; set; }
    public decimal LivestockValue { get; set; }
    public decimal FixedAssetsValue { get; set; }
    public decimal ProjectSpend { get; set; }
    public decimal FeedStockValue { get; set; }
    public decimal TotalAssets { get; set; }
    public decimal PayablesOpen { get; set; }
    public decimal LoansOpen { get; set; }
    public decimal TotalLiabilities { get; set; }
    public decimal OwnerFundingTotal { get; set; }
    public decimal CumulativeProfit { get; set; }
    public decimal Equity { get; set; }
    public decimal NetWorth { get; set; }
    public bool IsBalanced { get; set; }
    public decimal ImbalanceAmount { get; set; }
}

public class RelinkUnpostedMoneyViewModel
{
    public int DefaultAccountId { get; set; }
    public bool DryRun { get; set; }
}

public class RelinkUnpostedMoneyResultViewModel
{
    public int FundingLinked { get; set; }
    public int IncomeLinked { get; set; }
    public int ExpenseLinked { get; set; }
    public IReadOnlyList<string> Messages { get; set; } = [];
}

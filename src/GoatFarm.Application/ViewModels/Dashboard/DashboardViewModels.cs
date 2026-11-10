using GoatFarm.Application.ViewModels.Approval;
using GoatFarm.Application.ViewModels.Breeding;
using GoatFarm.Application.ViewModels.Feed;
using GoatFarm.Application.ViewModels.Finance;
using GoatFarm.Application.ViewModels.Goats;
using GoatFarm.Application.ViewModels.Ledger;
using GoatFarm.Application.ViewModels.Reminders;
using GoatFarm.Application.ViewModels.Vaccines;

namespace GoatFarm.Application.ViewModels.Dashboard;

public class DashboardHerdSliceViewModel
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class DashboardTrendPointViewModel
{
    public string Month { get; set; } = string.Empty;
    public string MonthLabel { get; set; } = string.Empty;
    public int HerdTotal { get; set; }
    public decimal MilkLiters { get; set; }
    public int Births { get; set; }
    public decimal MoneyIn { get; set; }
}

public class DashboardBreedingMonthViewModel
{
    public string MonthLabel { get; set; } = string.Empty;
    public int Crosses { get; set; }
    public int Births { get; set; }
}

public class DashboardRecentGoatViewModel
{
    public int Id { get; set; }
    public string Tag { get; set; } = string.Empty;
    public string Breed { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string WhenDisplay { get; set; } = string.Empty;
}

public class DashboardBreedSliceViewModel
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class DashboardMilkDayViewModel
{
    public string Label { get; set; } = string.Empty;
    public decimal Liters { get; set; }
    public bool IsToday { get; set; }
}

public class DashboardActivityViewModel
{
    public string Icon { get; set; } = "•";
    public string Text { get; set; } = string.Empty;
    public string When { get; set; } = string.Empty;
    public DateOnly SortDate { get; set; }
    public string? Href { get; set; }
}

public class DashboardLedgerRowViewModel
{
    public DateOnly Date { get; set; }
    public string DateDisplay { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public decimal Amount { get; set; }
    public string AmountDisplay { get; set; } = string.Empty;
}

public class DashboardTodayItemViewModel
{
    public string Icon { get; set; } = "•";
    public string Text { get; set; } = string.Empty;
    public string? Subtext { get; set; }
    public string? Href { get; set; }
    public string Tone { get; set; } = "var(--ink)";
}

public class DashboardAlertViewModel
{
    public string Severity { get; set; } = "info";
    public string Message { get; set; } = string.Empty;
    public string? Href { get; set; }
}

public class DashboardViewModel
{
    public string Month { get; set; } = string.Empty;

    public HerdStatsViewModel HerdStats { get; set; } = new();

    public int BreedingPrepCount { get; set; }
    public int BreedingExpectingCount { get; set; }
    public string BreedingNextDueText { get; set; } = string.Empty;
    public IReadOnlyList<BreedingExpectingRowViewModel> BreedingUpcoming { get; set; } = [];

    public decimal FeedCostMonthly { get; set; }
    public IReadOnlyList<FeedStockRowViewModel> FeedStock { get; set; } = [];
    public int FeedStockLowCount { get; set; }

    public decimal LitersProduced { get; set; }
    public decimal LitersSold { get; set; }
    public decimal LitersWasted { get; set; }
    public decimal LitersLeft { get; set; }

    public decimal TotalIncome { get; set; }
    public decimal TotalExpense { get; set; }
    public decimal Capital { get; set; }
    public decimal Profit { get; set; }
    public bool IsLoss { get; set; }
    /// <summary>True when income, expense, capital and profit were removed because the user lacks Finance view.</summary>
    public bool FinanceHidden { get; set; }

    public int DueNowCount { get; set; }
    public string DueNowNote { get; set; } = string.Empty;
    public IReadOnlyList<VaccineDueRowViewModel> DueNow { get; set; } = [];
    public IReadOnlyList<ReminderViewModel> Reminders { get; set; } = [];

    public decimal MilkTodayLiters { get; set; }
    public IReadOnlyList<DashboardMilkDayViewModel> MilkLast7Days { get; set; } = [];
    public IReadOnlyList<DashboardBreedSliceViewModel> BreedDistribution { get; set; } = [];
    public IReadOnlyList<DashboardHerdSliceViewModel> HerdComposition { get; set; } = [];
    public IReadOnlyList<DashboardTrendPointViewModel> SixMonthTrend { get; set; } = [];
    public IReadOnlyList<DashboardBreedingMonthViewModel> BreedingLast12Months { get; set; } = [];
    public IReadOnlyList<DashboardRecentGoatViewModel> RecentGoats { get; set; } = [];
    public IReadOnlyList<ApprovalRequestViewModel> PendingApprovalItems { get; set; } = [];
    public int HealthHealthy { get; set; }
    public int HealthUnderObservation { get; set; }
    public int HealthSick { get; set; }
    public int PendingVaccinations { get; set; }
    public IReadOnlyList<DashboardActivityViewModel> RecentActivities { get; set; } = [];
    public IReadOnlyList<DashboardLedgerRowViewModel> RecentSales { get; set; } = [];
    public IReadOnlyList<DashboardLedgerRowViewModel> RecentExpenses { get; set; } = [];
    public decimal ReceivablesOpen { get; set; }
    public decimal PayablesOpen { get; set; }
    public decimal ProductStockValue { get; set; }
    public int PendingApprovals { get; set; }

    public IReadOnlyList<DashboardTodayItemViewModel> TodayAtFarm { get; set; } = [];
    public IReadOnlyList<MoneyAccountRowViewModel> CashAccounts { get; set; } = [];
    public decimal CashAndBankTotal { get; set; }
    public IReadOnlyList<DashboardAlertViewModel> TopAlerts { get; set; } = [];
    public IReadOnlyList<FinanceLineViewModel> MonthIncomeLines { get; set; } = [];
    public IReadOnlyList<FinanceLineViewModel> MonthExpenseLines { get; set; } = [];
    public int IntegrityCriticalCount { get; set; }
}

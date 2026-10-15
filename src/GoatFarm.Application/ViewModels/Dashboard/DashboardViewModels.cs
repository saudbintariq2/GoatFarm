using GoatFarm.Application.ViewModels.Breeding;
using GoatFarm.Application.ViewModels.Feed;
using GoatFarm.Application.ViewModels.Goats;
using GoatFarm.Application.ViewModels.Reminders;
using GoatFarm.Application.ViewModels.Vaccines;

namespace GoatFarm.Application.ViewModels.Dashboard;

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
    public int HealthHealthy { get; set; }
    public int HealthUnderObservation { get; set; }
    public int HealthSick { get; set; }
    public int PendingVaccinations { get; set; }
    public IReadOnlyList<DashboardActivityViewModel> RecentActivities { get; set; } = [];
    public IReadOnlyList<DashboardLedgerRowViewModel> RecentSales { get; set; } = [];
    public IReadOnlyList<DashboardLedgerRowViewModel> RecentExpenses { get; set; } = [];
}

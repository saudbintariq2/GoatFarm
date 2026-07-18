using GoatFarm.Application.ViewModels.Breeding;
using GoatFarm.Application.ViewModels.Feed;
using GoatFarm.Application.ViewModels.Goats;
using GoatFarm.Application.ViewModels.Reminders;
using GoatFarm.Application.ViewModels.Vaccines;

namespace GoatFarm.Application.ViewModels.Dashboard;

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

    public int DueNowCount { get; set; }
    public string DueNowNote { get; set; } = string.Empty;
    public IReadOnlyList<VaccineDueRowViewModel> DueNow { get; set; } = [];
    public IReadOnlyList<ReminderViewModel> Reminders { get; set; } = [];
}

using GoatFarm.Application.ViewModels.Dashboard;
using GoatFarm.Application.ViewModels.Reports;

namespace GoatFarm.Application.Common;

/// <summary>
/// Removes farm income, expenses, profit, owner funding, payroll-derived totals and capital from page data
/// for users without Finance view. Always returns a copy: the dashboard model is cached and shared between users.
/// Module data the user can already see on its own page (feed purchases, vaccine purchases, goat purchase prices) stays.
/// </summary>
public static class FinanceVisibility
{
    public const string HiddenNote = "Income, expenses, profit and capital are hidden — they need Finance access.";

    public static DashboardViewModel WithoutFinance(DashboardViewModel m) => new()
    {
        Month = m.Month,
        HerdStats = m.HerdStats,
        BreedingPrepCount = m.BreedingPrepCount,
        BreedingExpectingCount = m.BreedingExpectingCount,
        BreedingNextDueText = m.BreedingNextDueText,
        BreedingUpcoming = m.BreedingUpcoming,
        FeedCostMonthly = m.FeedCostMonthly,
        FeedStock = m.FeedStock,
        FeedStockLowCount = m.FeedStockLowCount,
        LitersProduced = m.LitersProduced,
        LitersSold = m.LitersSold,
        LitersWasted = m.LitersWasted,
        LitersLeft = m.LitersLeft,
        DueNowCount = m.DueNowCount,
        DueNowNote = m.DueNowNote,
        DueNow = m.DueNow,
        Reminders = m.Reminders,
        MilkTodayLiters = m.MilkTodayLiters,
        MilkLast7Days = m.MilkLast7Days,
        BreedDistribution = m.BreedDistribution,
        HealthHealthy = m.HealthHealthy,
        HealthUnderObservation = m.HealthUnderObservation,
        HealthSick = m.HealthSick,
        PendingVaccinations = m.PendingVaccinations,
        RecentActivities = m.RecentActivities,
        RecentSales = [],
        RecentExpenses = [],
        FinanceHidden = true
    };

    public static ReportsPageViewModel WithoutFinance(ReportsPageViewModel m) => new()
    {
        Period = m.Period,
        GroupBy = m.GroupBy,
        CustomFrom = m.CustomFrom,
        CustomTo = m.CustomTo,
        RangeLabel = m.RangeLabel,
        MonthCount = m.MonthCount,
        NoteHtml = HiddenNote,
        TrendHeadLabel = m.TrendHeadLabel,
        TrendSubLabel = m.TrendSubLabel,
        FinanceHidden = true,
        Dashboard = new ReportDashboardSectionViewModel
        {
            Kpis = NonFinancial(m.Dashboard.Kpis),
            Ratios = NonFinancial(m.Dashboard.Ratios),
            Alerts = m.Dashboard.Alerts
        },
        Herd = m.Herd,
        Breeding = m.Breeding,
        Growth = m.Growth,
        Feed = new ReportFeedSectionViewModel
        {
            Kpis = NonFinancial(m.Feed.Kpis),
            Stock = m.Feed.Stock,
            CostByGroup = m.Feed.CostByGroup,
            Purchases = m.Feed.Purchases,
            PurchasesTotalKg = m.Feed.PurchasesTotalKg,
            PurchasesTotalAmount = m.Feed.PurchasesTotalAmount
        },
        Health = new ReportHealthSectionViewModel
        {
            Kpis = NonFinancial(m.Health.Kpis),
            Vaccination = m.Health.Vaccination,
            Deaths = m.Health.Deaths,
            DeathsTotalValue = m.Health.DeathsTotalValue
        },
        Inventory = new ReportInventorySectionViewModel
        {
            Kpis = NonFinancial(m.Inventory.Kpis),
            FeedStock = m.Inventory.FeedStock,
            FeedStockTotalValue = m.Inventory.FeedStockTotalValue
        },
        Alerts = m.Alerts,
        Comparison = new ReportComparisonSectionViewModel { Rows = m.Comparison.Rows.Where(r => !r.IsFinancial).ToList() }
    };

    private static List<ReportKpiViewModel> NonFinancial(IEnumerable<ReportKpiViewModel> kpis) =>
        kpis.Where(k => !k.IsFinancial).ToList();
}

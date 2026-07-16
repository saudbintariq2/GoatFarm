using GoatFarm.Application.Common;
using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Dashboard;
using GoatFarm.Application.ViewModels.Goats;

namespace GoatFarm.Infrastructure.Services;

public class StatisticsService : IStatisticsService
{
    private readonly IGoatService _goatService;
    private readonly IFeedService _feedService;
    private readonly IFinanceService _financeService;
    private readonly IMilkService _milkService;
    private readonly IVaccineService _vaccineService;
    private readonly IReminderService _reminderService;

    public StatisticsService(
        IGoatService goatService,
        IFeedService feedService,
        IFinanceService financeService,
        IMilkService milkService,
        IVaccineService vaccineService,
        IReminderService reminderService)
    {
        _goatService = goatService;
        _feedService = feedService;
        _financeService = financeService;
        _milkService = milkService;
        _vaccineService = vaccineService;
        _reminderService = reminderService;
    }

    public async Task<DashboardViewModel> GetDashboardAsync(string? month, CancellationToken cancellationToken = default)
    {
        month ??= MonthHelper.CurrentMonthKey();
        var finance = await _financeService.GetFinancePageAsync(month, cancellationToken);
        var milk = await _milkService.GetMilkPageAsync(cancellationToken: cancellationToken);
        var vaccine = await _vaccineService.GetVaccinePageAsync(null, month, cancellationToken);
        var reminders = await _reminderService.GetRemindersAsync(cancellationToken);
        var stats = await GetHerdStatsAsync(cancellationToken);

        return new DashboardViewModel
        {
            Month = month,
            HerdStats = stats,
            FeedCostMonthly = _feedService.CalculateFarmFeedMonthly(),
            LitersProduced = milk.LitersProduced,
            LitersSold = milk.LitersSold,
            LitersWasted = milk.LitersWasted,
            LitersLeft = milk.LitersLeft,
            TotalIncome = finance.TotalIncome,
            TotalExpense = finance.TotalExpense,
            Capital = finance.Capital,
            Profit = finance.Profit,
            IsLoss = finance.IsLoss,
            DueNowCount = vaccine.DueNowCount,
            DueNowNote = vaccine.DueNowNote,
            DueNow = vaccine.DueNow,
            Reminders = reminders
        };
    }

    public async Task<HerdStatsViewModel> GetHerdStatsAsync(CancellationToken cancellationToken = default)
    {
        var page = await _goatService.GetHerdPageAsync("all", cancellationToken: cancellationToken);
        return page.Stats;
    }
}

using GoatFarm.Application.Common;
using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Dashboard;
using GoatFarm.Application.ViewModels.Goats;
using Microsoft.Extensions.Caching.Memory;

namespace GoatFarm.Infrastructure.Services;

public class StatisticsService : IStatisticsService
{
    private static readonly TimeSpan DashboardCacheDuration = TimeSpan.FromSeconds(60);

    private readonly IGoatService _goatService;
    private readonly IFeedService _feedService;
    private readonly IFinanceService _financeService;
    private readonly IMilkService _milkService;
    private readonly IVaccineService _vaccineService;
    private readonly IReminderService _reminderService;
    private readonly IBreedingService _breedingService;
    private readonly IMemoryCache _cache;

    public StatisticsService(
        IGoatService goatService,
        IFeedService feedService,
        IFinanceService financeService,
        IMilkService milkService,
        IVaccineService vaccineService,
        IReminderService reminderService,
        IBreedingService breedingService,
        IMemoryCache cache)
    {
        _goatService = goatService;
        _feedService = feedService;
        _financeService = financeService;
        _milkService = milkService;
        _vaccineService = vaccineService;
        _reminderService = reminderService;
        _breedingService = breedingService;
        _cache = cache;
    }

    public Task<DashboardViewModel> GetDashboardAsync(string? month, CancellationToken cancellationToken = default)
    {
        month ??= MonthHelper.CurrentMonthKey();
        var cacheKey = $"dashboard:{month}";

        return _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = DashboardCacheDuration;
            return await BuildDashboardAsync(month, cancellationToken);
        })!;
    }

    private async Task<DashboardViewModel> BuildDashboardAsync(string month, CancellationToken cancellationToken)
    {
        var finance = await _financeService.GetFinancePageAsync(month, cancellationToken);
        var milk = await _milkService.GetMilkPageAsync(cancellationToken: cancellationToken);
        var vaccine = await _vaccineService.GetVaccinePageAsync(null, month, cancellationToken);
        var reminders = await _reminderService.GetRemindersAsync(cancellationToken);
        var breeding = await _breedingService.GetBreedingPageAsync(cancellationToken);
        var feed = await _feedService.GetFeedPageAsync(null, month, cancellationToken);
        var stats = await GetHerdStatsAsync(cancellationToken);

        var stockLow = feed.Stock.Count(s => s.DaysLeft is < 7);

        return new DashboardViewModel
        {
            Month = month,
            HerdStats = stats,
            BreedingPrepCount = breeding.PrepCount,
            BreedingExpectingCount = breeding.ExpectingCount,
            BreedingNextDueText = breeding.NextDueText,
            BreedingUpcoming = breeding.ExpectingRows.Take(5).ToList(),
            FeedCostMonthly = _feedService.CalculateFarmFeedMonthly(),
            FeedStock = feed.Stock,
            FeedStockLowCount = stockLow,
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

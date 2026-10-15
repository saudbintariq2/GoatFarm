using GoatFarm.Application.Common;
using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Dashboard;
using GoatFarm.Application.ViewModels.Goats;
using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
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
    private readonly GoatFarmDbContext _context;
    private readonly IMemoryCache _cache;

    public StatisticsService(
        IGoatService goatService,
        IFeedService feedService,
        IFinanceService financeService,
        IMilkService milkService,
        IVaccineService vaccineService,
        IReminderService reminderService,
        IBreedingService breedingService,
        GoatFarmDbContext context,
        IMemoryCache cache)
    {
        _goatService = goatService;
        _feedService = feedService;
        _financeService = financeService;
        _milkService = milkService;
        _vaccineService = vaccineService;
        _reminderService = reminderService;
        _breedingService = breedingService;
        _context = context;
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
        var feed = await _feedService.GetFeedPageAsync(null, month, cancellationToken: cancellationToken);
        var stats = await GetHerdStatsAsync(cancellationToken);

        var stockLow = feed.Stock.Count(s => s.DaysLeft is < 7);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var weekStart = today.AddDays(-6);

        var milkToday = await _context.MilkProductions.AsNoTracking()
            .Where(p => p.Date == today)
            .SumAsync(p => p.Liters, cancellationToken);

        var milkGrouped = await _context.MilkProductions.AsNoTracking()
            .Where(p => p.Date >= weekStart && p.Date <= today)
            .GroupBy(p => p.Date)
            .Select(g => new { g.Key, Liters = g.Sum(p => p.Liters) })
            .ToListAsync(cancellationToken);
        var milkByDate = milkGrouped.ToDictionary(x => x.Key, x => x.Liters);

        var milkLast7 = Enumerable.Range(0, 7)
            .Select(i =>
            {
                var d = weekStart.AddDays(i);
                milkByDate.TryGetValue(d, out var liters);
                return new DashboardMilkDayViewModel
                {
                    Label = d.ToString("MMM d"),
                    Liters = liters,
                    IsToday = d == today
                };
            })
            .ToList();

        var breeds = await _context.Goats.AsNoTracking()
            .Where(g => !g.IsArchived)
            .GroupBy(g => string.IsNullOrWhiteSpace(g.Breed) ? "Mixed" : g.Breed.Trim())
            .Select(g => new DashboardBreedSliceViewModel { Name = g.Key, Count = g.Count() })
            .OrderByDescending(b => b.Count)
            .Take(6)
            .ToListAsync(cancellationToken);

        var healthRows = await _context.Goats.AsNoTracking()
            .Where(g => !g.IsArchived)
            .GroupBy(g => g.HealthState)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var healthHealthy = healthRows.FirstOrDefault(h => h.Key == GoatHealthState.Healthy)?.Count ?? 0;
        var healthSick = healthRows.FirstOrDefault(h => h.Key == GoatHealthState.Sick)?.Count ?? 0;
        var healthObs = healthRows.Where(h => h.Key is GoatHealthState.UnderTreatment or GoatHealthState.Recovering)
            .Sum(h => h.Count);

        var pendingVacc = vaccine.DueNowCount + reminders.Count(r =>
            r.ReminderDate >= today && r.ReminderDate <= today.AddDays(7) &&
            (r.Title.Contains("vaccin", StringComparison.OrdinalIgnoreCase) ||
             r.Title.Contains("dose", StringComparison.OrdinalIgnoreCase)));

        var activities = await BuildRecentActivitiesAsync(today, cancellationToken);

        var recentSales = finance.Incomes
            .OrderByDescending(i => i.Date)
            .Take(5)
            .Select(i => new DashboardLedgerRowViewModel
            {
                Date = i.Date,
                DateDisplay = i.Date.ToString("d MMM"),
                Title = i.Type,
                Detail = i.Comment,
                Amount = i.Amount,
                AmountDisplay = DisplayHelper.FormatRs(i.Amount)
            })
            .ToList();

        var recentExpenses = finance.Expenses
            .OrderByDescending(e => e.Date)
            .Take(5)
            .Select(e => new DashboardLedgerRowViewModel
            {
                Date = e.Date,
                DateDisplay = e.Date.ToString("d MMM"),
                Title = e.Type,
                Detail = e.Comment,
                Amount = e.Amount,
                AmountDisplay = DisplayHelper.FormatRs(e.Amount)
            })
            .ToList();

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
            Reminders = reminders,
            MilkTodayLiters = milkToday,
            MilkLast7Days = milkLast7,
            BreedDistribution = breeds,
            HealthHealthy = healthHealthy,
            HealthUnderObservation = healthObs,
            HealthSick = healthSick,
            PendingVaccinations = pendingVacc,
            RecentActivities = activities,
            RecentSales = recentSales,
            RecentExpenses = recentExpenses
        };
    }

    private async Task<List<DashboardActivityViewModel>> BuildRecentActivitiesAsync(DateOnly today, CancellationToken cancellationToken)
    {
        var items = new List<DashboardActivityViewModel>();

        var milkRows = await _context.MilkProductions.AsNoTracking()
            .OrderByDescending(p => p.Date)
            .Take(3)
            .ToListAsync(cancellationToken);
        foreach (var p in milkRows)
            items.Add(new DashboardActivityViewModel
            {
                Icon = "🥛",
                Text = $"Milk recorded — {p.Liters:N1} L",
                When = ActivityWhen(p.Date, today),
                SortDate = p.Date,
                Href = "/Milk"
            });

        var feedRows = await _context.FeedPurchases.AsNoTracking()
            .OrderByDescending(p => p.Date)
            .Take(2)
            .ToListAsync(cancellationToken);
        foreach (var p in feedRows)
            items.Add(new DashboardActivityViewModel
            {
                Icon = "🌾",
                Text = $"Feed purchased — {p.Kg:N0} kg",
                When = ActivityWhen(p.Date, today),
                SortDate = p.Date,
                Href = "/Feed"
            });

        var crosses = await _context.BreedingEvents.AsNoTracking()
            .Where(e => e.Outcome == BreedingOutcome.Open || e.Outcome == BreedingOutcome.ConfirmedPregnant)
            .OrderByDescending(e => e.MatedDate)
            .Take(2)
            .ToListAsync(cancellationToken);
        foreach (var e in crosses)
            items.Add(new DashboardActivityViewModel
            {
                Icon = "💞",
                Text = "Breeding cross recorded",
                When = ActivityWhen(e.MatedDate, today),
                SortDate = e.MatedDate,
                Href = "/Breeding"
            });

        return items
            .OrderByDescending(a => a.SortDate)
            .Take(8)
            .ToList();
    }

    private static string ActivityWhen(DateOnly date, DateOnly today)
    {
        var days = date.DayNumber - today.DayNumber;
        return days switch
        {
            0 => "Today",
            -1 => "Yesterday",
            > 0 and <= 7 => date.ToString("d MMM"),
            _ => date.ToString("d MMM yyyy")
        };
    }

    public async Task<HerdStatsViewModel> GetHerdStatsAsync(CancellationToken cancellationToken = default)
    {
        var page = await _goatService.GetHerdPageAsync("all", cancellationToken: cancellationToken);
        return page.Stats;
    }
}

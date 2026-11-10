using GoatFarm.Application.Common;
using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Approval;
using GoatFarm.Application.ViewModels.Breeding;
using GoatFarm.Application.ViewModels.Dashboard;
using GoatFarm.Application.ViewModels.Feed;
using GoatFarm.Application.ViewModels.Finance;
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
    private readonly ILedgerService _ledgerService;
    private readonly IApprovalService _approvalService;
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
        ILedgerService ledgerService,
        IApprovalService approvalService,
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
        _ledgerService = ledgerService;
        _approvalService = approvalService;
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
        var finance = await _financeService.GetFinanceMonthSnapshotAsync(month, cancellationToken);
        var integrity = await _financeService.GetIntegrityCheckAsync(cancellationToken);
        var cashAccounts = (await _ledgerService.GetAccountBalancesAsync(cancellationToken))
            .Where(a => a.IsActive).ToList();
        var milk = await _milkService.GetMilkPageAsync(cancellationToken: cancellationToken);
        var vaccine = await _vaccineService.GetVaccinePageAsync(null, month, cancellationToken);
        var reminders = await _reminderService.GetRemindersAsync(cancellationToken);
        var breeding = await _breedingService.GetBreedingPageAsync(cancellationToken: cancellationToken);
        var feedStock = await _feedService.GetFeedStockRowsAsync(cancellationToken);
        var stats = await GetHerdStatsAsync(cancellationToken);

        var stockLow = feedStock.Count(s => s.DaysLeft is < 7);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var todayAtFarm = await BuildTodayAtFarmAsync(breeding.NowJobs, feedStock, today, cancellationToken);
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

        var herdComposition = await BuildHerdCompositionAsync(cancellationToken);
        var sixMonthTrend = await BuildSixMonthTrendAsync(month, cancellationToken);
        var breedingLast12 = await BuildBreedingLast12MonthsAsync(month, cancellationToken);
        var recentGoats = await BuildRecentGoatsAsync(cancellationToken);
        var approvalPage = await _approvalService.GetPageAsync(cancellationToken);
        var pendingApprovalItems = approvalPage.Pending.Take(8).ToList();

        var healthRows = await _context.Goats.AsNoTracking()
            .Where(g => !g.IsArchived)
            .GroupBy(g => g.HealthState)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var healthHealthy = healthRows.FirstOrDefault(h => h.Key == GoatHealthState.Healthy)?.Count ?? 0;
        var healthSick = healthRows.FirstOrDefault(h => h.Key == GoatHealthState.Sick)?.Count ?? 0;
        var healthObs = healthRows.Where(h => h.Key is GoatHealthState.UnderTreatment or GoatHealthState.Recovering
                or GoatHealthState.Quarantine or GoatHealthState.Injured or GoatHealthState.WeakOffFeed)
            .Sum(h => h.Count);

        var pendingVacc = vaccine.DueNowCount + reminders.Count(r =>
            r.ReminderDate >= today && r.ReminderDate <= today.AddDays(7) &&
            (r.Title.Contains("vaccin", StringComparison.OrdinalIgnoreCase) ||
             r.Title.Contains("dose", StringComparison.OrdinalIgnoreCase)));

        var receivables = await _context.CustomerInvoices.AsNoTracking()
            .Where(i => i.Status == InvoiceStatus.Open || i.Status == InvoiceStatus.Partial)
            .SumAsync(i => i.Amount - i.PaidAmount, cancellationToken);
        var payables = await _context.SupplierPayables.AsNoTracking()
            .Where(p => p.Status == PayableStatus.Open || p.Status == PayableStatus.Partial)
            .SumAsync(p => p.Amount - p.PaidAmount, cancellationToken);
        var pendingApprovals = await _context.ApprovalRequests.CountAsync(a => a.Status == ApprovalStatus.Pending, cancellationToken);
        var topAlerts = BuildTopAlerts(integrity, feedStock, vaccine.DueNowCount, receivables, payables, pendingApprovals);

        var productStockValue = await ComputeProductStockValueAsync(cancellationToken);

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
            FeedStock = feedStock,
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
            HerdComposition = herdComposition,
            SixMonthTrend = sixMonthTrend,
            BreedingLast12Months = breedingLast12,
            RecentGoats = recentGoats,
            PendingApprovalItems = pendingApprovalItems,
            HealthHealthy = healthHealthy,
            HealthUnderObservation = healthObs,
            HealthSick = healthSick,
            PendingVaccinations = pendingVacc,
            RecentActivities = activities,
            RecentSales = recentSales,
            RecentExpenses = recentExpenses,
            ReceivablesOpen = receivables,
            PayablesOpen = payables,
            ProductStockValue = productStockValue,
            PendingApprovals = pendingApprovals,
            TodayAtFarm = todayAtFarm,
            CashAccounts = cashAccounts,
            CashAndBankTotal = cashAccounts.Sum(a => a.Balance),
            TopAlerts = topAlerts,
            MonthIncomeLines = finance.IncomeLines,
            MonthExpenseLines = finance.ExpenseLines,
            IntegrityCriticalCount = integrity.CriticalCount
        };
    }

    private async Task<List<DashboardTodayItemViewModel>> BuildTodayAtFarmAsync(
        IReadOnlyList<BreedingNowJobViewModel> nowJobs,
        IReadOnlyList<FeedStockRowViewModel> feedStock,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var items = new List<DashboardTodayItemViewModel>();

        foreach (var job in nowJobs.Take(4))
        {
            items.Add(new DashboardTodayItemViewModel
            {
                Icon = job.Icon,
                Text = job.Text,
                Subtext = job.Tags.Count > 0 ? string.Join(", ", job.Tags.Take(4)) : null,
                Href = string.IsNullOrEmpty(job.FilterLink) ? "/Breeding" : $"/Goat?filter={Uri.EscapeDataString(job.FilterLink)}",
                Tone = job.Tone
            });
        }

        var birthSince = today.AddDays(-14);
        var recentBirths = await _context.KiddingRecords.AsNoTracking()
            .Include(k => k.Doe)
            .Where(k => k.KiddingDate >= birthSince)
            .OrderByDescending(k => k.KiddingDate)
            .Take(3)
            .ToListAsync(cancellationToken);
        foreach (var k in recentBirths)
        {
            items.Add(new DashboardTodayItemViewModel
            {
                Icon = "🍼",
                Text = $"{k.Doe.Tag} kidded — {k.LiveKids} live kid(s)",
                Subtext = k.KiddingDate.ToString("d MMM"),
                Href = "/Breeding",
                Tone = "var(--green-dark)"
            });
        }

        var openTreatments = await _context.GoatTreatments.AsNoTracking()
            .Include(t => t.Goat)
            .Where(t => t.EndDate == null || t.EndDate >= today)
            .OrderBy(t => t.EndDate ?? today.AddDays(30))
            .Take(4)
            .ToListAsync(cancellationToken);
        foreach (var t in openTreatments)
        {
            items.Add(new DashboardTodayItemViewModel
            {
                Icon = "💊",
                Text = $"{t.Goat.Tag} on treatment — {t.Medicine}",
                Subtext = t.Diagnosis,
                Href = $"/Goat?tag={Uri.EscapeDataString(t.Goat.Tag)}",
                Tone = "var(--amber)"
            });
        }

        foreach (var s in feedStock
                     .Where(s => s is { DaysLeft: > 0 and < 14 } or { IsLowStock: true, DaysLeft: null })
                     .OrderBy(s => s.DaysLeft ?? 0)
                     .Take(3))
        {
            var detail = s.DaysLeft is > 0
                ? $"about {s.DaysLeft} day(s) of stock left"
                : "stock below 20% — check purchases";
            items.Add(new DashboardTodayItemViewModel
            {
                Icon = "🌾",
                Text = $"{s.DisplayName} — {detail}",
                Href = "/Feed",
                Tone = "#8a261c"
            });
        }

        return items.Take(12).ToList();
    }

    private static List<DashboardAlertViewModel> BuildTopAlerts(
        FinanceIntegrityViewModel integrity,
        IReadOnlyList<FeedStockRowViewModel> feedStock,
        int vaccineDueCount,
        decimal receivables,
        decimal payables,
        int pendingApprovals)
    {
        var alerts = new List<DashboardAlertViewModel>();

        foreach (var issue in integrity.Issues.Where(i => i.Severity == "critical").Take(3))
            alerts.Add(new DashboardAlertViewModel { Severity = "critical", Message = issue.Message, Href = "/Finance?tab=overview" });

        foreach (var issue in integrity.Issues.Where(i => i.Severity == "warning").Take(2))
            alerts.Add(new DashboardAlertViewModel { Severity = "warning", Message = issue.Message, Href = "/Finance?tab=overview" });

        var feedCritical = feedStock.Where(s => s.DaysLeft is > 0 and < 7).ToList();
        if (feedCritical.Count > 0)
            alerts.Add(new DashboardAlertViewModel
            {
                Severity = "warning",
                Message = $"{feedCritical.Count} feed type(s) with under a week of stock",
                Href = "/Feed"
            });

        if (vaccineDueCount > 0)
            alerts.Add(new DashboardAlertViewModel
            {
                Severity = "warning",
                Message = $"{vaccineDueCount} vaccination(s) due now",
                Href = "/Vaccine"
            });

        if (receivables > 0)
            alerts.Add(new DashboardAlertViewModel
            {
                Severity = "info",
                Message = $"Customers owe {DisplayHelper.FormatRs(receivables)}",
                Href = "/Finance?tab=salesar"
            });

        if (payables > 0)
            alerts.Add(new DashboardAlertViewModel
            {
                Severity = "info",
                Message = $"Open supplier bills {DisplayHelper.FormatRs(payables)}",
                Href = "/Finance?tab=salesar"
            });

        if (pendingApprovals > 0)
            alerts.Add(new DashboardAlertViewModel
            {
                Severity = "info",
                Message = $"{pendingApprovals} change(s) waiting for approval",
                Href = "/Admin"
            });

        return alerts.Take(8).ToList();
    }

    private async Task<decimal> ComputeProductStockValueAsync(CancellationToken cancellationToken)
    {
        var catalog = await _context.ProductCatalogItems.AsNoTracking().ToListAsync(cancellationToken);
        var moves = await _context.StockMovements.AsNoTracking().OrderBy(m => m.Date).ToListAsync(cancellationToken);
        var qty = new Dictionary<(int product, int wh), decimal>();
        foreach (var m in moves)
        {
            var key = (m.ProductId, m.WarehouseId);
            qty.TryAdd(key, 0);
            qty[key] += m.Type switch
            {
                StockMovementType.Receive or StockMovementType.Adjust => m.Qty,
                StockMovementType.Issue or StockMovementType.Spoilage => -m.Qty,
                StockMovementType.Transfer => -m.Qty,
                _ => 0
            };
            if (m.Type == StockMovementType.Transfer && m.ToWarehouseId is > 0)
            {
                var key2 = (m.ProductId, m.ToWarehouseId.Value);
                qty.TryAdd(key2, 0);
                qty[key2] += m.Qty;
            }
        }

        var prices = catalog.ToDictionary(c => c.Id, c => c.DefaultPrice);
        decimal total = 0;
        foreach (var ((pid, _), q) in qty)
        {
            if (q <= 0) continue;
            total += q * prices.GetValueOrDefault(pid, 0);
        }
        return total;
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

        var auditRows = await _context.AuditLogEntries.AsNoTracking()
            .Where(a => a.ShowInNotices)
            .OrderByDescending(a => a.CreatedDate)
            .Take(5)
            .ToListAsync(cancellationToken);
        foreach (var a in auditRows)
        {
            var d = DateOnly.FromDateTime(a.CreatedDate.ToLocalTime());
            items.Add(new DashboardActivityViewModel
            {
                Icon = "📝",
                Text = a.Summary,
                When = ActivityWhen(d, today),
                SortDate = d,
                Href = "/Admin"
            });
        }

        return items
            .OrderByDescending(a => a.SortDate)
            .Take(10)
            .ToList();
    }

    private async Task<IReadOnlyList<DashboardHerdSliceViewModel>> BuildHerdCompositionAsync(CancellationToken cancellationToken)
    {
        var rows = await _context.Goats.AsNoTracking()
            .Where(g => !g.IsArchived)
            .Select(g => new { g.Status })
            .ToListAsync(cancellationToken);

        var kids = rows.Count(g => g.Status == GoatStatus.Kid);
        var bucks = rows.Count(g => g.Status == GoatStatus.Buck);
        var forSale = rows.Count(g => g.Status == GoatStatus.Sale);
        var does = rows.Count(g => g.Status is GoatStatus.Milking or GoatStatus.Pregnant or GoatStatus.Dry);

        return
        [
            new DashboardHerdSliceViewModel { Name = "Does", Count = does },
            new DashboardHerdSliceViewModel { Name = "Bucks", Count = bucks },
            new DashboardHerdSliceViewModel { Name = "Kids", Count = kids },
            new DashboardHerdSliceViewModel { Name = "For sale", Count = forSale }
        ];
    }

    private async Task<IReadOnlyList<DashboardTrendPointViewModel>> BuildSixMonthTrendAsync(
        string anchorMonth, CancellationToken cancellationToken)
    {
        var (currentStart, _) = MonthHelper.GetMonthRange(anchorMonth);
        var monthStarts = Enumerable.Range(0, 6).Select(i => currentStart.AddMonths(i - 5)).ToList();
        var rangeStart = monthStarts[0];
        var rangeEnd = monthStarts[^1].AddMonths(1);

        var goats = await _context.Goats.AsNoTracking()
            .Where(g => !g.IsArchived)
            .Select(g => new { g.EventDate })
            .ToListAsync(cancellationToken);

        var milkByMonth = (await _context.MilkProductions.AsNoTracking()
                .Where(p => p.Date >= rangeStart && p.Date < rangeEnd)
                .GroupBy(p => new { p.Date.Year, p.Date.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Liters = g.Sum(p => p.Liters) })
                .ToListAsync(cancellationToken))
            .ToDictionary(x => (x.Year, x.Month), x => x.Liters);

        var birthsByMonth = (await _context.KiddingRecords.AsNoTracking()
                .Where(k => k.KiddingDate >= rangeStart && k.KiddingDate < rangeEnd)
                .GroupBy(k => new { k.KiddingDate.Year, k.KiddingDate.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Births = g.Sum(k => k.LiveKids) })
                .ToListAsync(cancellationToken))
            .ToDictionary(x => (x.Year, x.Month), x => x.Births);

        var incomeDates = await _context.Incomes.AsNoTracking()
            .Where(i => i.Date >= rangeStart && i.Date < rangeEnd)
            .Select(i => new { i.Date, i.Amount })
            .ToListAsync(cancellationToken);
        var milkSaleDates = await _context.MilkSales.AsNoTracking()
            .Where(s => s.Date >= rangeStart && s.Date < rangeEnd)
            .Select(s => new { s.Date, s.Amount })
            .ToListAsync(cancellationToken);
        var productSaleDates = await _context.ProductSales.AsNoTracking()
            .Where(s => s.Date >= rangeStart && s.Date < rangeEnd)
            .Select(s => new { s.Date, s.Amount })
            .ToListAsync(cancellationToken);
        var invoiceDates = await _context.CustomerInvoices.AsNoTracking()
            .Where(i => i.Date >= rangeStart && i.Date < rangeEnd && i.Status != InvoiceStatus.Void)
            .Where(i => !i.Description.StartsWith("Product sale:") && !i.Description.StartsWith("Live goat sale"))
            .Select(i => new { i.Date, i.Amount })
            .ToListAsync(cancellationToken);

        var points = new List<DashboardTrendPointViewModel>();
        foreach (var start in monthStarts)
        {
            var endEx = start.AddMonths(1);
            var monthKey = start.ToString("yyyy-MM");
            var herd = goats.Count(g => g.EventDate < endEx);
            milkByMonth.TryGetValue((start.Year, start.Month), out var milk);
            birthsByMonth.TryGetValue((start.Year, start.Month), out var births);

            decimal moneyIn = 0;
            moneyIn += incomeDates.Where(i => i.Date >= start && i.Date < endEx).Sum(i => i.Amount);
            moneyIn += milkSaleDates.Where(s => s.Date >= start && s.Date < endEx).Sum(s => s.Amount);
            moneyIn += productSaleDates.Where(s => s.Date >= start && s.Date < endEx).Sum(s => s.Amount);
            moneyIn += invoiceDates.Where(i => i.Date >= start && i.Date < endEx).Sum(i => i.Amount);

            points.Add(new DashboardTrendPointViewModel
            {
                Month = monthKey,
                MonthLabel = start.ToString("MMM yy"),
                HerdTotal = herd,
                MilkLiters = milk,
                Births = births,
                MoneyIn = moneyIn
            });
        }

        return points;
    }

    private async Task<IReadOnlyList<DashboardBreedingMonthViewModel>> BuildBreedingLast12MonthsAsync(
        string anchorMonth, CancellationToken cancellationToken)
    {
        var (currentStart, _) = MonthHelper.GetMonthRange(anchorMonth);
        var rangeStart = currentStart.AddMonths(-11);
        var rangeEnd = currentStart.AddMonths(1);
        var monthStarts = Enumerable.Range(0, 12).Select(i => rangeStart.AddMonths(i)).ToList();

        var crossesByMonth = (await _context.BreedingEvents.AsNoTracking()
                .Where(e => e.MatedDate >= rangeStart && e.MatedDate < rangeEnd)
                .GroupBy(e => new { e.MatedDate.Year, e.MatedDate.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
                .ToListAsync(cancellationToken))
            .ToDictionary(x => (x.Year, x.Month), x => x.Count);

        var birthsByMonth = (await _context.KiddingRecords.AsNoTracking()
                .Where(k => k.KiddingDate >= rangeStart && k.KiddingDate < rangeEnd)
                .GroupBy(k => new { k.KiddingDate.Year, k.KiddingDate.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Births = g.Sum(k => k.LiveKids) })
                .ToListAsync(cancellationToken))
            .ToDictionary(x => (x.Year, x.Month), x => x.Births);

        return monthStarts.Select(start =>
        {
            crossesByMonth.TryGetValue((start.Year, start.Month), out var crosses);
            birthsByMonth.TryGetValue((start.Year, start.Month), out var births);
            return new DashboardBreedingMonthViewModel
            {
                MonthLabel = start.ToString("MMM"),
                Crosses = crosses,
                Births = births
            };
        }).ToList();
    }

    private async Task<IReadOnlyList<DashboardRecentGoatViewModel>> BuildRecentGoatsAsync(CancellationToken cancellationToken)
    {
        var rows = await _context.Goats.AsNoTracking()
            .Where(g => !g.IsArchived)
            .OrderByDescending(g => g.EventDate)
            .ThenByDescending(g => g.Id)
            .Take(8)
            .Select(g => new
            {
                g.Id,
                g.Tag,
                g.Breed,
                g.Status,
                g.EventDate
            })
            .ToListAsync(cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.Today);
        return rows.Select(g => new DashboardRecentGoatViewModel
        {
            Id = g.Id,
            Tag = g.Tag,
            Breed = string.IsNullOrWhiteSpace(g.Breed) ? "Mixed" : g.Breed.Trim(),
            Status = g.Status.ToString(),
            WhenDisplay = ActivityWhen(g.EventDate, today)
        }).ToList();
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

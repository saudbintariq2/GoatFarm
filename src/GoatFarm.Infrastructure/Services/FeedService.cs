using GoatFarm.Application.Common;
using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Feed;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Infrastructure.Services;

public class FeedService : IFeedService
{
    private static readonly GoatStatus[] StatusOrder =
    [
        GoatStatus.Kid, GoatStatus.Milking, GoatStatus.Pregnant,
        GoatStatus.Dry, GoatStatus.Buck, GoatStatus.Sale
    ];

    private readonly GoatFarmDbContext _context;
    private readonly IGoatService _goatService;

    public FeedService(GoatFarmDbContext context, IGoatService goatService)
    {
        _context = context;
        _goatService = goatService;
    }

    public async Task<FeedPageViewModel> GetFeedPageAsync(string? statusKey, string? month = null, CancellationToken cancellationToken = default)
    {
        month ??= MonthHelper.CurrentMonthKey();
        var selected = string.IsNullOrWhiteSpace(statusKey) ? GoatStatus.Kid : DisplayHelper.ParseStatusKey(statusKey);
        var prices = await GetPriceDictionaryAsync(cancellationToken);
        var feedCatalog = await GetFeedCatalogAsync(cancellationToken);
        var plans = await _context.FeedPlans.Include(p => p.Items).AsNoTracking().ToListAsync(cancellationToken);

        var priceVms = feedCatalog.Select(p => new FeedPriceViewModel
        {
            FeedType = p.FeedType,
            DisplayName = p.DisplayName,
            PricePerKg = p.PricePerKg,
            StockKg = p.StockKg
        }).ToList();

        var currentPlanEntity = plans.FirstOrDefault(p => p.StatusKey == selected) ?? plans.First();
        var currentPlan = BuildPlanViewModel(currentPlanEntity, prices, feedCatalog);
        currentPlan.GoatCount = _goatService.CountByStatus(selected);

        var summary = new List<FeedSummaryRowViewModel>();
        decimal totalFeed = 0, totalMed = 0, totalGoats = 0, totalDaily = 0;

        foreach (var st in StatusOrder)
        {
            var count = _goatService.CountByStatus(st);
            if (count == 0 && st == GoatStatus.Sale) continue;

            var plan = plans.FirstOrDefault(p => p.StatusKey == st);
            if (plan is null) continue;

            var planVm = BuildPlanViewModel(plan, prices, feedCatalog);
            var feedM = planVm.DailyFeedCost * 30 * count;
            var medM = plan.MedicineCostPerGoatPerMonth * count;
            var (text, css) = DisplayHelper.GetStatusDisplay(st);

            summary.Add(new FeedSummaryRowViewModel
            {
                StatusKey = DisplayHelper.StatusKey(st),
                StatusDisplay = text,
                StatusCssClass = css,
                GoatCount = count,
                FeedMonthly = feedM,
                MedicineMonthly = medM,
                TotalMonthly = feedM + medM
            });

            totalFeed += feedM;
            totalMed += medM;
            totalGoats += count;
            totalDaily += planVm.DailyFeedCost * count;
        }

        var buying = BuildBuyingList(plans, prices, feedCatalog);
        var dailyUse = BuildDailyUseMap(plans, feedCatalog);
        var stock = BuildStockRows(feedCatalog, dailyUse);
        var (monthStart, monthEnd) = MonthHelper.GetMonthRange(month);
        var purchases = await _context.FeedPurchases.AsNoTracking()
            .Where(p => p.Date >= monthStart && p.Date < monthEnd)
            .OrderByDescending(p => p.Date)
            .ToListAsync(cancellationToken);

        var nameMap = feedCatalog.ToDictionary(f => f.FeedType, f => f.DisplayName);
        var purchaseVms = purchases.Select(p => new FeedPurchaseViewModel
        {
            Id = p.Id,
            Date = p.Date,
            FeedType = p.FeedType,
            FeedDisplayName = nameMap.GetValueOrDefault(p.FeedType, p.FeedType),
            Kg = p.Kg,
            RatePerKg = p.RatePerKg,
            Amount = p.Amount,
            Comment = p.Comment
        }).ToList();

        return new FeedPageViewModel
        {
            Prices = priceVms,
            CurrentPlan = currentPlan,
            Summary = summary,
            BuyingList = buying,
            FeedPurchases = purchaseVms,
            FeedBoughtMonthTotal = purchaseVms.Sum(p => p.Amount),
            FeedBoughtKgTotal = purchaseVms.Sum(p => p.Kg),
            FeedMonth = month,
            GrandMonthly = totalFeed + totalMed,
            GrandDaily = totalDaily + totalMed / 30m,
            TotalGoats = (int)totalGoats,
            SelectedStatusKey = DisplayHelper.StatusKey(selected),
            StatusOptions = StatusOrder.Select(s => (DisplayHelper.StatusKey(s), DisplayHelper.GetStatusDisplay(s).Text)).ToList(),
            Stock = stock
        };
    }

    public async Task UpdateFeedPriceAsync(string feedType, decimal price, CancellationToken cancellationToken = default)
    {
        var entity = await _context.FeedPrices.FirstOrDefaultAsync(p => p.FeedType == feedType, cancellationToken);
        if (entity is null) return;
        entity.PricePerKg = price;
        entity.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateFeedPlanAsync(UpdateFeedPlanViewModel model, CancellationToken cancellationToken = default)
    {
        var status = DisplayHelper.ParseStatusKey(model.StatusKey);
        var plan = await _context.FeedPlans.Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.StatusKey == status, cancellationToken);
        if (plan is null) return;

        plan.MedicineCostPerGoatPerMonth = model.MedicineCostPerGoatPerMonth;
        plan.UpdatedDate = DateTime.UtcNow;

        foreach (var item in plan.Items)
        {
            if (model.Rations.TryGetValue(item.FeedType, out var grams))
                item.GramsPerDay = grams;
            item.UpdatedDate = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<FeedPurchaseViewModel> AddFeedPurchaseAsync(CreateFeedPurchaseViewModel model, CancellationToken cancellationToken = default)
    {
        var amount = Math.Round(model.Kg * model.RatePerKg);
        var entity = new FeedPurchase
        {
            Date = model.Date,
            FeedType = model.FeedType,
            Kg = model.Kg,
            RatePerKg = model.RatePerKg,
            Amount = amount,
            Comment = string.IsNullOrWhiteSpace(model.Comment) ? null : model.Comment.Trim()
        };
        _context.FeedPurchases.Add(entity);
        await AdjustFeedStockAsync(model.FeedType, model.Kg, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        var displayName = await _context.FeedPrices.AsNoTracking()
            .Where(p => p.FeedType == model.FeedType)
            .Select(p => p.DisplayName)
            .FirstOrDefaultAsync(cancellationToken) ?? model.FeedType;

        return new FeedPurchaseViewModel
        {
            Id = entity.Id,
            Date = entity.Date,
            FeedType = entity.FeedType,
            FeedDisplayName = displayName,
            Kg = entity.Kg,
            RatePerKg = entity.RatePerKg,
            Amount = entity.Amount,
            Comment = entity.Comment
        };
    }

    public async Task<FeedPurchaseViewModel?> UpdateFeedPurchaseAsync(int id, CreateFeedPurchaseViewModel model, CancellationToken cancellationToken = default)
    {
        var entity = await _context.FeedPurchases.FindAsync([id], cancellationToken);
        if (entity is null) return null;

        var oldKg = entity.Kg;
        var oldFeedType = entity.FeedType;

        entity.Date = model.Date;
        entity.FeedType = model.FeedType;
        entity.Kg = model.Kg;
        entity.RatePerKg = model.RatePerKg;
        entity.Amount = Math.Round(model.Kg * model.RatePerKg);
        entity.Comment = string.IsNullOrWhiteSpace(model.Comment) ? null : model.Comment.Trim();
        entity.UpdatedDate = DateTime.UtcNow;

        if (oldFeedType == model.FeedType)
            await AdjustFeedStockAsync(model.FeedType, model.Kg - oldKg, cancellationToken);
        else
        {
            await AdjustFeedStockAsync(oldFeedType, -oldKg, cancellationToken);
            await AdjustFeedStockAsync(model.FeedType, model.Kg, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);

        var displayName = await _context.FeedPrices.AsNoTracking()
            .Where(p => p.FeedType == model.FeedType)
            .Select(p => p.DisplayName)
            .FirstOrDefaultAsync(cancellationToken) ?? model.FeedType;

        return new FeedPurchaseViewModel
        {
            Id = entity.Id,
            Date = entity.Date,
            FeedType = entity.FeedType,
            FeedDisplayName = displayName,
            Kg = entity.Kg,
            RatePerKg = entity.RatePerKg,
            Amount = entity.Amount,
            Comment = entity.Comment
        };
    }

    public async Task<bool> DeleteFeedPurchaseAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.FeedPurchases.FindAsync([id], cancellationToken);
        if (entity is null) return false;
        await AdjustFeedStockAsync(entity.FeedType, -entity.Kg, cancellationToken);
        _context.FeedPurchases.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task UpdateFeedStockAsync(UpdateFeedStockViewModel model, CancellationToken cancellationToken = default)
    {
        var entity = await _context.FeedPrices.FirstOrDefaultAsync(p => p.FeedType == model.FeedType, cancellationToken);
        if (entity is null) return;
        entity.StockKg = Math.Max(0, model.StockKg);
        entity.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<FeedPriceViewModel> AddFeedTypeAsync(AddFeedTypeViewModel model, CancellationToken cancellationToken = default)
    {
        var displayName = model.DisplayName.Trim();
        var feedType = GenerateFeedKey(displayName);
        if (await _context.FeedPrices.AnyAsync(p => p.FeedType == feedType, cancellationToken))
        {
            var i = 2;
            var baseKey = feedType;
            while (await _context.FeedPrices.AnyAsync(p => p.FeedType == feedType, cancellationToken))
                feedType = $"{baseKey}_{i++}";
        }

        var price = new FeedPrice
        {
            FeedType = feedType,
            DisplayName = displayName,
            PricePerKg = model.PricePerKg,
            StockKg = 0
        };
        _context.FeedPrices.Add(price);

        var plans = await _context.FeedPlans.Include(p => p.Items).ToListAsync(cancellationToken);
        foreach (var plan in plans)
        {
            if (plan.Items.All(i => i.FeedType != feedType))
            {
                plan.Items.Add(new FeedPlanItem { FeedType = feedType, GramsPerDay = 0 });
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        return new FeedPriceViewModel { FeedType = feedType, DisplayName = displayName, PricePerKg = model.PricePerKg, StockKg = 0 };
    }

    public async Task<bool> DeleteFeedTypeAsync(string feedType, CancellationToken cancellationToken = default)
    {
        var price = await _context.FeedPrices.FirstOrDefaultAsync(p => p.FeedType == feedType, cancellationToken);
        if (price is null) return false;

        var planItems = await _context.FeedPlanItems.Where(i => i.FeedType == feedType).ToListAsync(cancellationToken);
        _context.FeedPlanItems.RemoveRange(planItems);
        _context.FeedPrices.Remove(price);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public decimal CalculateDailyFeedCost(FeedPlanViewModel plan, IReadOnlyDictionary<string, decimal> prices)
    {
        decimal cost = 0;
        foreach (var item in plan.Items)
        {
            var price = prices.GetValueOrDefault(item.FeedType, 0);
            cost += item.GramsPerDay / 1000m * price;
        }
        return cost;
    }

    public decimal CalculateFarmFeedMonthly() =>
        CalculateFarmFeedMonthlyInternal(includeMedicine: true);

    public decimal CalculateFarmMedicineMonthly() =>
        CalculateFarmFeedMonthlyInternal(includeMedicine: true, medicineOnly: true);

    public decimal GetFeedPurchasedMonthly(string month)
    {
        var (monthStart, monthEnd) = MonthHelper.GetMonthRange(month);
        return _context.FeedPurchases.AsNoTracking()
            .Where(p => p.Date >= monthStart && p.Date < monthEnd)
            .Sum(p => p.Amount);
    }

    public decimal GetFeedPurchasedKg(string month)
    {
        var (monthStart, monthEnd) = MonthHelper.GetMonthRange(month);
        return _context.FeedPurchases.AsNoTracking()
            .Where(p => p.Date >= monthStart && p.Date < monthEnd)
            .Sum(p => p.Kg);
    }

    private decimal CalculateFarmFeedMonthlyInternal(bool includeMedicine, bool medicineOnly = false)
    {
        var prices = _context.FeedPrices.AsNoTracking().ToDictionary(p => p.FeedType, p => p.PricePerKg);
        var plans = _context.FeedPlans.Include(p => p.Items).AsNoTracking().ToList();
        decimal total = 0;
        foreach (var st in StatusOrder)
        {
            var n = _goatService.CountByStatus(st);
            var plan = plans.FirstOrDefault(p => p.StatusKey == st);
            if (plan is null) continue;

            if (medicineOnly)
                total += plan.MedicineCostPerGoatPerMonth * n;
            else if (includeMedicine)
            {
                var feedCatalog = _context.FeedPrices.AsNoTracking().ToList();
                var planVm = BuildPlanViewModel(plan, prices, feedCatalog);
                total += planVm.DailyFeedCost * 30 * n + plan.MedicineCostPerGoatPerMonth * n;
            }
            else
            {
                var feedCatalog = _context.FeedPrices.AsNoTracking().ToList();
                var planVm = BuildPlanViewModel(plan, prices, feedCatalog);
                total += planVm.DailyFeedCost * 30 * n;
            }
        }
        return total;
    }

    private async Task<List<FeedPrice>> GetFeedCatalogAsync(CancellationToken cancellationToken) =>
        await _context.FeedPrices.AsNoTracking().OrderBy(p => p.Id).ToListAsync(cancellationToken);

    private async Task<Dictionary<string, decimal>> GetPriceDictionaryAsync(CancellationToken cancellationToken) =>
        await _context.FeedPrices.AsNoTracking()
            .ToDictionaryAsync(p => p.FeedType, p => p.PricePerKg, cancellationToken);

    private FeedPlanViewModel BuildPlanViewModel(
        FeedPlan plan,
        IReadOnlyDictionary<string, decimal> prices,
        IReadOnlyList<FeedPrice> feedCatalog)
    {
        var items = feedCatalog.Select(f =>
        {
            var grams = plan.Items.FirstOrDefault(i => i.FeedType == f.FeedType)?.GramsPerDay ?? 0;
            var price = prices.GetValueOrDefault(f.FeedType, 0);
            return new FeedPlanItemViewModel
            {
                FeedType = f.FeedType,
                DisplayName = f.DisplayName,
                GramsPerDay = grams,
                DailyCost = grams / 1000m * price
            };
        }).ToList();

        var dailyFeed = items.Sum(i => i.DailyCost);
        var (text, _) = DisplayHelper.GetStatusDisplay(plan.StatusKey);

        return new FeedPlanViewModel
        {
            StatusKey = DisplayHelper.StatusKey(plan.StatusKey),
            StatusDisplay = text,
            MedicineCostPerGoatPerMonth = plan.MedicineCostPerGoatPerMonth,
            Items = items,
            DailyFeedCost = dailyFeed,
            DailyTotalCost = dailyFeed,
            MonthlyTotalCost = dailyFeed * 30 + plan.MedicineCostPerGoatPerMonth
        };
    }

    private IReadOnlyDictionary<string, decimal> BuildDailyUseMap(
        IReadOnlyList<FeedPlan> plans,
        IReadOnlyList<FeedPrice> feedCatalog)
    {
        var kg = feedCatalog.ToDictionary(f => f.FeedType, _ => 0m);
        foreach (var st in StatusOrder)
        {
            var n = _goatService.CountByStatus(st);
            var plan = plans.FirstOrDefault(p => p.StatusKey == st);
            if (plan is null) continue;
            foreach (var f in feedCatalog)
            {
                var grams = plan.Items.FirstOrDefault(i => i.FeedType == f.FeedType)?.GramsPerDay ?? 0;
                kg[f.FeedType] += grams / 1000m * n;
            }
        }
        return kg;
    }

    private static IReadOnlyList<FeedStockRowViewModel> BuildStockRows(
        IReadOnlyList<FeedPrice> feedCatalog,
        IReadOnlyDictionary<string, decimal> dailyUse)
    {
        return feedCatalog.Select(f =>
        {
            var st = f.StockKg;
            var du = dailyUse.GetValueOrDefault(f.FeedType, 0);
            decimal? days = du > 0 ? st / du : null;
            var floorDays = days.HasValue ? (int)Math.Floor(days.Value) : (int?)null;
            var daysText = floorDays.HasValue
                ? $"~{floorDays} day{(floorDays == 1 ? "" : "s")}"
                : "—";
            var color = days switch
            {
                null => "",
                < 3 => "color:#8a261c",
                < 7 => "color:var(--amber)",
                _ => "color:var(--green-dark)"
            };
            return new FeedStockRowViewModel
            {
                FeedType = f.FeedType,
                DisplayName = f.DisplayName,
                StockKg = st,
                KgPerDay = du,
                DaysLeft = days,
                DaysLeftText = daysText,
                DaysLeftColor = color
            };
        }).ToList();
    }

    private async Task AdjustFeedStockAsync(string feedType, decimal deltaKg, CancellationToken cancellationToken)
    {
        if (deltaKg == 0) return;
        var price = await _context.FeedPrices.FirstOrDefaultAsync(p => p.FeedType == feedType, cancellationToken);
        if (price is null) return;
        price.StockKg = Math.Max(0, price.StockKg + deltaKg);
        price.UpdatedDate = DateTime.UtcNow;
    }

    private IReadOnlyList<FeedBuyingRowViewModel> BuildBuyingList(
        IReadOnlyList<FeedPlan> plans,
        IReadOnlyDictionary<string, decimal> prices,
        IReadOnlyList<FeedPrice> feedCatalog)
    {
        var kg = feedCatalog.ToDictionary(f => f.FeedType, _ => 0m);
        foreach (var st in StatusOrder)
        {
            var n = _goatService.CountByStatus(st);
            var plan = plans.FirstOrDefault(p => p.StatusKey == st);
            if (plan is null) continue;
            foreach (var f in feedCatalog)
            {
                var grams = plan.Items.FirstOrDefault(i => i.FeedType == f.FeedType)?.GramsPerDay ?? 0;
                kg[f.FeedType] += grams / 1000m * n;
            }
        }

        return feedCatalog
            .Select(f =>
            {
                var d = kg[f.FeedType];
                if (d <= 0) return null;
                var m = d * 30;
                return new FeedBuyingRowViewModel
                {
                    DisplayName = f.DisplayName,
                    KgPerDay = d,
                    KgPerMonth = m,
                    CostPerMonth = m * prices.GetValueOrDefault(f.FeedType, 0)
                };
            })
            .Where(x => x is not null)
            .Cast<FeedBuyingRowViewModel>()
            .ToList();
    }

    private static string GenerateFeedKey(string name)
    {
        var k = new string(name.ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '_')
            .ToArray());
        k = string.Join('_', k.Split('_', StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(k) ? "feed" : k;
    }
}

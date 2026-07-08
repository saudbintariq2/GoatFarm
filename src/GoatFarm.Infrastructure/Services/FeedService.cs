using GoatFarm.Application.Common;
using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Feed;
using GoatFarm.Domain.Constants;
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

    public async Task<FeedPageViewModel> GetFeedPageAsync(string? statusKey, CancellationToken cancellationToken = default)
    {
        var selected = string.IsNullOrWhiteSpace(statusKey) ? GoatStatus.Kid : DisplayHelper.ParseStatusKey(statusKey);
        var prices = await GetPriceDictionaryAsync(cancellationToken);
        var plans = await _context.FeedPlans.Include(p => p.Items).AsNoTracking().ToListAsync(cancellationToken);

        var priceVms = await _context.FeedPrices.AsNoTracking()
            .OrderBy(p => p.Id)
            .Select(p => new FeedPriceViewModel
            {
                FeedType = p.FeedType,
                DisplayName = p.DisplayName,
                PricePerKg = p.PricePerKg
            }).ToListAsync(cancellationToken);

        var currentPlanEntity = plans.FirstOrDefault(p => p.StatusKey == selected) ?? plans.First();
        var currentPlan = BuildPlanViewModel(currentPlanEntity, prices);
        currentPlan.GoatCount = _goatService.CountByStatus(selected);

        var summary = new List<FeedSummaryRowViewModel>();
        decimal totalFeed = 0, totalMed = 0, totalGoats = 0, totalDaily = 0;

        foreach (var st in StatusOrder)
        {
            var count = _goatService.CountByStatus(st);
            if (count == 0 && st == GoatStatus.Sale) continue;

            var plan = plans.FirstOrDefault(p => p.StatusKey == st);
            if (plan is null) continue;

            var planVm = BuildPlanViewModel(plan, prices);
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

        var buying = BuildBuyingList(plans, prices);

        return new FeedPageViewModel
        {
            Prices = priceVms,
            CurrentPlan = currentPlan,
            Summary = summary,
            BuyingList = buying,
            GrandMonthly = totalFeed + totalMed,
            GrandDaily = totalDaily + totalMed / 30m,
            TotalGoats = (int)totalGoats,
            SelectedStatusKey = DisplayHelper.StatusKey(selected),
            StatusOptions = StatusOrder.Select(s => (DisplayHelper.StatusKey(s), DisplayHelper.GetStatusDisplay(s).Text)).ToList()
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

    public decimal CalculateFarmFeedMonthly()
    {
        var prices = _context.FeedPrices.AsNoTracking().ToDictionary(p => p.FeedType, p => p.PricePerKg);
        var plans = _context.FeedPlans.Include(p => p.Items).AsNoTracking().ToList();
        decimal total = 0;
        foreach (var st in StatusOrder)
        {
            var n = _goatService.CountByStatus(st);
            var plan = plans.FirstOrDefault(p => p.StatusKey == st);
            if (plan is null) continue;
            var planVm = BuildPlanViewModel(plan, prices);
            total += planVm.DailyFeedCost * 30 * n + plan.MedicineCostPerGoatPerMonth * n;
        }
        return total;
    }

    private async Task<Dictionary<string, decimal>> GetPriceDictionaryAsync(CancellationToken cancellationToken) =>
        await _context.FeedPrices.AsNoTracking()
            .ToDictionaryAsync(p => p.FeedType, p => p.PricePerKg, cancellationToken);

    private FeedPlanViewModel BuildPlanViewModel(Domain.Entities.FeedPlan plan, IReadOnlyDictionary<string, decimal> prices)
    {
        var items = FeedTypes.All.Select(f =>
        {
            var grams = plan.Items.FirstOrDefault(i => i.FeedType == f.Key)?.GramsPerDay ?? 0;
            var price = prices.GetValueOrDefault(f.Key, 0);
            return new FeedPlanItemViewModel
            {
                FeedType = f.Key,
                DisplayName = f.Name,
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

    private IReadOnlyList<FeedBuyingRowViewModel> BuildBuyingList(
        IReadOnlyList<Domain.Entities.FeedPlan> plans,
        IReadOnlyDictionary<string, decimal> prices)
    {
        var kg = FeedTypes.All.ToDictionary(f => f.Key, _ => 0m);
        foreach (var st in StatusOrder)
        {
            var n = _goatService.CountByStatus(st);
            var plan = plans.FirstOrDefault(p => p.StatusKey == st);
            if (plan is null) continue;
            foreach (var f in FeedTypes.All)
            {
                var grams = plan.Items.FirstOrDefault(i => i.FeedType == f.Key)?.GramsPerDay ?? 0;
                kg[f.Key] += grams / 1000m * n;
            }
        }

        return FeedTypes.All
            .Select(f =>
            {
                var d = kg[f.Key];
                if (d <= 0) return null;
                var m = d * 30;
                return new FeedBuyingRowViewModel
                {
                    DisplayName = f.Name,
                    KgPerDay = d,
                    KgPerMonth = m,
                    CostPerMonth = m * prices.GetValueOrDefault(f.Key, 0)
                };
            })
            .Where(x => x is not null)
            .Cast<FeedBuyingRowViewModel>()
            .ToList();
    }
}

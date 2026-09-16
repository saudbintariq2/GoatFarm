using System.Text.Json;
using GoatFarm.Application.Common;
using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Feed;
using GoatFarm.Domain.Constants;
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
        var prices = await GetPriceDictionaryAsync(cancellationToken);
        var feedCatalog = await GetFeedCatalogAsync(cancellationToken);
        var mixFeedKeys = MixRecipeHelper.MixFeedKeys(feedCatalog.Select(f => f.FeedType)).ToList();
        var recipe = await GetMixRecipeAsync(cancellationToken);
        var mixCostPerKg = MixRecipeHelper.MixCostPerKg(recipe, prices, mixFeedKeys);
        var mixTotalKg = MixRecipeHelper.MixTotalKg(recipe, mixFeedKeys);
        var mixBatchCost = MixRecipeHelper.MixBatchCost(recipe, prices, mixFeedKeys);

        var plans = await _context.FeedPlans.AsNoTracking().ToListAsync(cancellationToken);

        var mixPrices = feedCatalog
            .Where(f => mixFeedKeys.Contains(f.FeedType, StringComparer.OrdinalIgnoreCase))
            .Select(p => new FeedPriceViewModel
            {
                FeedType = p.FeedType,
                DisplayName = p.DisplayName,
                PricePerKg = p.PricePerKg,
                StockKg = p.StockKg
            }).ToList();

        var mixRecipeVm = mixFeedKeys.Select(k =>
        {
            var feed = feedCatalog.First(f => f.FeedType == k);
            var kg = recipe.GetValueOrDefault(k);
            return new MixRecipeItemViewModel
            {
                FeedType = k,
                DisplayName = feed.DisplayName,
                KgInBatch = kg,
                BatchCost = kg * prices.GetValueOrDefault(k),
                Percent = mixTotalKg > 0 ? (int)Math.Round(kg / mixTotalKg * 100) : 0
            };
        }).ToList();

        var groupPlans = new List<FeedGroupPlanRowViewModel>();
        var categoryCosts = new List<FeedCategoryCostRowViewModel>();
        decimal totalFeed = 0, totalMed = 0, totalGoats = 0, totalDaily = 0, totalFodder = 0;

        foreach (var st in StatusOrder)
        {
            var count = _goatService.CountByStatus(st);
            if (count == 0 && st == GoatStatus.Sale) continue;

            var plan = plans.FirstOrDefault(p => p.StatusKey == st);
            if (plan is null) continue;

            var dailyFeed = MixRecipeHelper.PlanDailyFeedCost(plan.MixKgPerDay, mixCostPerKg);
            var feedM = dailyFeed * 30 * count;
            var medM = plan.MedicineCostPerGoatPerMonth * count;
            var fodder = plan.FodderKgPerDay * count;
            var (text, css) = DisplayHelper.GetStatusDisplay(st);
            var stKey = DisplayHelper.StatusKey(st);

            groupPlans.Add(new FeedGroupPlanRowViewModel
            {
                StatusKey = stKey,
                StatusDisplay = text,
                StatusCssClass = css,
                GoatCount = count,
                MixKgPerDay = plan.MixKgPerDay,
                FodderKgPerDay = plan.FodderKgPerDay,
                MedicineCostPerGoatPerMonth = plan.MedicineCostPerGoatPerMonth,
                MonthlyTotal = feedM + medM
            });

            categoryCosts.Add(new FeedCategoryCostRowViewModel
            {
                StatusKey = stKey,
                StatusDisplay = text,
                StatusCssClass = css,
                GoatCount = count,
                MixKgPerDay = plan.MixKgPerDay * count,
                DailyCost = dailyFeed * count,
                MonthlyCost = feedM + medM,
                FodderKgPerDay = fodder
            });

            totalFeed += feedM;
            totalMed += medM;
            totalGoats += count;
            totalDaily += dailyFeed * count;
            totalFodder += fodder;
        }

        var totalMonthly = categoryCosts.Sum(c => c.MonthlyCost);
        foreach (var row in categoryCosts)
            row.SharePercent = totalMonthly > 0 ? (int)Math.Round(row.MonthlyCost / totalMonthly * 100) : 0;

        var buying = BuildBuyingList(plans, recipe, prices, mixFeedKeys, mixCostPerKg);
        var dailyUse = BuildDailyUseMap(plans, recipe, mixFeedKeys);
        var stock = BuildStockRows(feedCatalog.Where(f => mixFeedKeys.Contains(f.FeedType)).ToList(), dailyUse);

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

        var allPrices = feedCatalog.Select(p => new FeedPriceViewModel
        {
            FeedType = p.FeedType,
            DisplayName = p.DisplayName,
            PricePerKg = p.PricePerKg,
            StockKg = p.StockKg
        }).ToList();

        return new FeedPageViewModel
        {
            MixPrices = mixPrices,
            AllPrices = allPrices,
            MixRecipe = mixRecipeVm,
            MixTotalKg = mixTotalKg,
            MixBatchCost = mixBatchCost,
            MixCostPerKg = mixCostPerKg,
            GroupPlans = groupPlans,
            CategoryCosts = categoryCosts,
            FodderKgPerDayTotal = totalFodder,
            BuyingList = buying,
            FeedPurchases = purchaseVms,
            FeedBoughtMonthTotal = purchaseVms.Sum(p => p.Amount),
            FeedBoughtKgTotal = purchaseVms.Sum(p => p.Kg),
            FeedMonth = month,
            GrandMonthly = totalFeed + totalMed,
            GrandDaily = totalDaily + totalMed / 30m,
            TotalGoats = (int)totalGoats,
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
        var plan = await _context.FeedPlans.FirstOrDefaultAsync(p => p.StatusKey == status, cancellationToken);
        if (plan is null) return;

        plan.MixKgPerDay = model.MixKgPerDay;
        plan.FodderKgPerDay = model.FodderKgPerDay;
        plan.MedicineCostPerGoatPerMonth = model.MedicineCostPerGoatPerMonth;
        plan.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateMixRecipeAsync(UpdateMixRecipeViewModel model, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(model.Recipe);
        var setting = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == AppSettingKeys.MixRecipe, cancellationToken);
        if (setting is null)
            _context.AppSettings.Add(new AppSetting { Key = AppSettingKeys.MixRecipe, Value = json });
        else
        {
            setting.Value = json;
            setting.UpdatedDate = DateTime.UtcNow;
        }
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, decimal>> GetMixRecipeAsync(CancellationToken cancellationToken = default)
    {
        var setting = await _context.AppSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == AppSettingKeys.MixRecipe, cancellationToken);
        if (setting is null || string.IsNullOrWhiteSpace(setting.Value))
            return new Dictionary<string, decimal>(MixRecipeHelper.DefaultRecipe, StringComparer.OrdinalIgnoreCase);

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, decimal>>(setting.Value);
            return parsed ?? new Dictionary<string, decimal>(MixRecipeHelper.DefaultRecipe, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, decimal>(MixRecipeHelper.DefaultRecipe, StringComparer.OrdinalIgnoreCase);
        }
    }

    public decimal GetMixCostPerKg()
    {
        var prices = _context.FeedPrices.AsNoTracking().ToDictionary(p => p.FeedType, p => p.PricePerKg);
        var mixKeys = MixRecipeHelper.MixFeedKeys(prices.Keys).ToList();
        var recipe = GetMixRecipeAsync().GetAwaiter().GetResult();
        return MixRecipeHelper.MixCostPerKg(recipe, prices, mixKeys);
    }

    public decimal GetFarmFodderKgPerDay()
    {
        var plans = _context.FeedPlans.AsNoTracking().ToList();
        decimal total = 0;
        foreach (var st in StatusOrder)
        {
            var n = _goatService.CountByStatus(st);
            var plan = plans.FirstOrDefault(p => p.StatusKey == st);
            if (plan is null) continue;
            total += plan.FodderKgPerDay * n;
        }
        return total;
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

        var recipeDict = new Dictionary<string, decimal>(await GetMixRecipeAsync(cancellationToken));
        if (!string.Equals(feedType, FeedTypes.Fodder, StringComparison.OrdinalIgnoreCase))
        {
            recipeDict[feedType] = 0;
            await UpdateMixRecipeAsync(new UpdateMixRecipeViewModel { Recipe = recipeDict }, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return new FeedPriceViewModel { FeedType = feedType, DisplayName = displayName, PricePerKg = model.PricePerKg, StockKg = 0 };
    }

    public async Task<bool> DeleteFeedTypeAsync(string feedType, CancellationToken cancellationToken = default)
    {
        if (string.Equals(feedType, FeedTypes.Fodder, StringComparison.OrdinalIgnoreCase)) return false;

        var price = await _context.FeedPrices.FirstOrDefaultAsync(p => p.FeedType == feedType, cancellationToken);
        if (price is null) return false;

        var planItems = await _context.FeedPlanItems.Where(i => i.FeedType == feedType).ToListAsync(cancellationToken);
        _context.FeedPlanItems.RemoveRange(planItems);
        _context.FeedPrices.Remove(price);

        var recipe = new Dictionary<string, decimal>(await GetMixRecipeAsync(cancellationToken));
        recipe.Remove(feedType);
        await UpdateMixRecipeAsync(new UpdateMixRecipeViewModel { Recipe = recipe }, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public decimal CalculateDailyFeedCost(decimal mixKgPerDay) =>
        MixRecipeHelper.PlanDailyFeedCost(mixKgPerDay, GetMixCostPerKg());

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
        var mixCostPerKg = GetMixCostPerKg();
        var plans = _context.FeedPlans.AsNoTracking().ToList();
        decimal total = 0;
        foreach (var st in StatusOrder)
        {
            var n = _goatService.CountByStatus(st);
            var plan = plans.FirstOrDefault(p => p.StatusKey == st);
            if (plan is null) continue;

            if (medicineOnly)
                total += plan.MedicineCostPerGoatPerMonth * n;
            else if (includeMedicine)
                total += MixRecipeHelper.PlanDailyFeedCost(plan.MixKgPerDay, mixCostPerKg) * 30 * n
                    + plan.MedicineCostPerGoatPerMonth * n;
            else
                total += MixRecipeHelper.PlanDailyFeedCost(plan.MixKgPerDay, mixCostPerKg) * 30 * n;
        }
        return total;
    }

    private async Task<List<FeedPrice>> GetFeedCatalogAsync(CancellationToken cancellationToken) =>
        await _context.FeedPrices.AsNoTracking().OrderBy(p => p.Id).ToListAsync(cancellationToken);

    private async Task<Dictionary<string, decimal>> GetPriceDictionaryAsync(CancellationToken cancellationToken) =>
        await _context.FeedPrices.AsNoTracking()
            .ToDictionaryAsync(p => p.FeedType, p => p.PricePerKg, cancellationToken);

    private IReadOnlyDictionary<string, decimal> BuildDailyUseMap(
        IReadOnlyList<FeedPlan> plans,
        IReadOnlyDictionary<string, decimal> recipe,
        IReadOnlyList<string> mixFeedKeys)
    {
        var mixKgDay = StatusOrder.Sum(st =>
        {
            var n = _goatService.CountByStatus(st);
            var plan = plans.FirstOrDefault(p => p.StatusKey == st);
            return plan is null ? 0 : plan.MixKgPerDay * n;
        });

        var total = MixRecipeHelper.MixTotalKg(recipe, mixFeedKeys);
        var kg = mixFeedKeys.ToDictionary(k => k, _ => 0m);
        if (total <= 0) return kg;

        foreach (var k in mixFeedKeys)
            kg[k] = mixKgDay * (recipe.GetValueOrDefault(k) / total);
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
        IReadOnlyDictionary<string, decimal> recipe,
        IReadOnlyDictionary<string, decimal> prices,
        IReadOnlyList<string> mixFeedKeys,
        decimal mixCostPerKg)
    {
        var mixKgDay = StatusOrder.Sum(st =>
        {
            var n = _goatService.CountByStatus(st);
            var plan = plans.FirstOrDefault(p => p.StatusKey == st);
            return plan is null ? 0 : plan.MixKgPerDay * n;
        });
        var fodderKgDay = StatusOrder.Sum(st =>
        {
            var n = _goatService.CountByStatus(st);
            var plan = plans.FirstOrDefault(p => p.StatusKey == st);
            return plan is null ? 0 : plan.FodderKgPerDay * n;
        });

        var total = MixRecipeHelper.MixTotalKg(recipe, mixFeedKeys);
        var rows = new List<FeedBuyingRowViewModel>();

        foreach (var k in mixFeedKeys)
        {
            if (total <= 0) continue;
            var share = recipe.GetValueOrDefault(k) / total;
            var d = mixKgDay * share;
            if (d <= 0) continue;
            var m = d * 30;
            var feed = _context.FeedPrices.AsNoTracking().FirstOrDefault(f => f.FeedType == k);
            rows.Add(new FeedBuyingRowViewModel
            {
                DisplayName = feed?.DisplayName ?? k,
                KgPerDay = d,
                KgPerMonth = m,
                CostPerMonth = m * prices.GetValueOrDefault(k),
                IsOwnLand = false
            });
        }

        if (fodderKgDay > 0)
        {
            rows.Add(new FeedBuyingRowViewModel
            {
                DisplayName = "Green fodder (chaara)",
                KgPerDay = fodderKgDay,
                KgPerMonth = fodderKgDay * 30,
                CostPerMonth = 0,
                IsOwnLand = true
            });
        }

        return rows;
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

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

    private readonly IMilkService _milkService;



    public FeedService(GoatFarmDbContext context, IGoatService goatService, IMilkService milkService)

    {

        _context = context;

        _goatService = goatService;

        _milkService = milkService;

    }



    public async Task<FeedPageViewModel> GetFeedPageAsync(

        string? statusKey,

        string? month = null,

        string? subTab = null,

        CancellationToken cancellationToken = default)

    {

        await RunAutoUsageAsync(cancellationToken);



        month ??= MonthHelper.CurrentMonthKey();

        subTab = string.IsNullOrWhiteSpace(subTab) ? "store" : subTab;



        var prices = await GetPriceDictionaryAsync(cancellationToken);

        var feedCatalog = await GetFeedCatalogAsync(cancellationToken);

        var mixFeedKeys = MixRecipeHelper.MixFeedKeys(feedCatalog.Select(f => f.FeedType)).ToList();

        var allRecipes = await GetAllMixRecipesAsync(cancellationToken);

        var plans = await _context.FeedPlans.AsNoTracking().ToListAsync(cancellationToken);

        var fodderPoolDto = await LoadFodderPoolDtoAsync(cancellationToken);

        var feedSettings = await LoadFeedSettingsDtoAsync(cancellationToken);

        var fodderPoolVm = BuildFodderPoolViewModel(fodderPoolDto, plans);

        var feedSettingsVm = BuildFeedSettingsViewModel(feedSettings);



        var activeRecipeStatus = string.IsNullOrWhiteSpace(statusKey)

            ? DisplayHelper.StatusKey(GoatStatus.Milking)

            : statusKey;

        var activeRecipe = BuildMixRecipeStatusViewModel(

            activeRecipeStatus, allRecipes, mixFeedKeys, feedCatalog, prices);



        var mixPrices = feedCatalog

            .Where(f => mixFeedKeys.Contains(f.FeedType, StringComparer.OrdinalIgnoreCase))

            .Select(p => new FeedPriceViewModel

            {

                FeedType = p.FeedType,

                DisplayName = p.DisplayName,

                PricePerKg = p.PricePerKg,

                StockKg = p.StockKg

            }).ToList();



        var legacyRecipe = allRecipes.GetValueOrDefault(activeRecipeStatus)

            ?? new Dictionary<string, decimal>(MixRecipeHelper.DefaultRecipe, StringComparer.OrdinalIgnoreCase);

        var mixTotalKg = MixRecipeHelper.MixTotalKg(legacyRecipe, mixFeedKeys);

        var mixBatchCost = MixRecipeHelper.MixBatchCost(legacyRecipe, prices, mixFeedKeys);

        var mixCostPerKg = MixRecipeHelper.MixCostPerKg(legacyRecipe, prices, mixFeedKeys);

        var mixRecipeVm = BuildMixRecipeItems(legacyRecipe, mixFeedKeys, feedCatalog, prices);



        var dailyUse = BuildDailyUseMap(plans, allRecipes, mixFeedKeys);

        var stockFeeds = feedCatalog.Where(f => FeedTypes.IsPurchasable(f.FeedType)).ToList();

        var purchasedTotals = await GetPurchasedTotalsAsync(cancellationToken);

        var usedTotals = await GetUsedTotalsAsync(cancellationToken);

        var stock = BuildStockRows(stockFeeds, dailyUse, prices, purchasedTotals, usedTotals);

        var storeStats = BuildStoreStats(stock, dailyUse, prices);

        var lowStockItems = stock.Where(s => s.IsLowStock).Select(s => new FeedLowStockItemViewModel

        {

            FeedType = s.FeedType,

            DisplayName = s.DisplayName

        }).ToList();

        var reorderNote = BuildReorderNote(stock, dailyUse);



        var groupPlans = new List<FeedGroupPlanRowViewModel>();

        var categoryCosts = new List<FeedCategoryCostRowViewModel>();

        decimal totalFeedMonthly = 0, totalDaily = 0, totalGoats = 0, totalFodder = 0, totalFodderDry = 0, totalMixKg = 0;

        var fodderDailyPool = FodderPoolHelper.DailyTotal(fodderPoolDto);

        var totalFodderShare = StatusOrder.Sum(st =>

        {

            var n = _goatService.CountByStatus(st);

            var plan = plans.FirstOrDefault(p => p.StatusKey == st);

            return plan is null ? 0 : plan.FodderKgPerDay * n;

        });



        foreach (var st in StatusOrder)

        {

            var count = _goatService.CountByStatus(st);

            if (count == 0 && st == GoatStatus.Sale) continue;



            var plan = plans.FirstOrDefault(p => p.StatusKey == st);

            if (plan is null) continue;



            var stKey = DisplayHelper.StatusKey(st);

            var (text, css) = DisplayHelper.GetStatusDisplay(st);

            var statusRecipe = allRecipes.GetValueOrDefault(stKey) ?? legacyRecipe;

            var statusMixCost = MixRecipeHelper.MixCostPerKg(statusRecipe, prices, mixFeedKeys);

            var fodderCostPerGoat = CalculateFodderCostPerGoat(
                plan.FodderKgPerDay, fodderDailyPool, totalFodderShare,
                StatusOrder.Sum(s => _goatService.CountByStatus(s)), fodderPoolDto.Mode);

            var dryPrice = prices.GetValueOrDefault(FeedTypes.FodderDry);

            var dailyPerGoat = MixRecipeHelper.PlanDailyFeedCostV41(

                plan.MixKgPerDay, statusMixCost, plan.FodderDryKgPerDay, dryPrice, fodderCostPerGoat);

            var feedM = dailyPerGoat * 30 * count;

            var fodder = plan.FodderKgPerDay * count;

            var mixKgGroup = plan.MixKgPerDay * count;



            groupPlans.Add(new FeedGroupPlanRowViewModel

            {

                StatusKey = stKey,

                StatusDisplay = text,

                StatusCssClass = css,

                GoatCount = count,

                MixKgPerDay = plan.MixKgPerDay,

                FodderKgPerDay = plan.FodderKgPerDay,

                FodderDryKgPerDay = plan.FodderDryKgPerDay,

                FodderCostPerGoatPerDay = fodderCostPerGoat,

                MixCostPerKg = statusMixCost,

                DailyCostPerGoat = dailyPerGoat,

                MedicineCostPerGoatPerMonth = plan.MedicineCostPerGoatPerMonth,

                MonthlyTotal = feedM

            });



            categoryCosts.Add(new FeedCategoryCostRowViewModel

            {

                StatusKey = stKey,

                StatusDisplay = text,

                StatusCssClass = css,

                GoatCount = count,

                MixKgPerDay = mixKgGroup,

                DailyCost = dailyPerGoat * count,

                MonthlyCost = feedM,

                FodderKgPerDay = fodder

            });



            totalFeedMonthly += feedM;

            totalDaily += dailyPerGoat * count;

            totalGoats += count;

            totalFodder += fodder;

            totalFodderDry += plan.FodderDryKgPerDay * count;

            totalMixKg += mixKgGroup;

        }



        var totalMonthly = categoryCosts.Sum(c => c.MonthlyCost);

        foreach (var row in categoryCosts)

            row.SharePercent = totalMonthly > 0 ? (int)Math.Round(row.MonthlyCost / totalMonthly * 100) : 0;



        var buying = BuildBuyingList(plans, allRecipes, prices, mixFeedKeys);

        var buyingTotalKg = buying.Where(b => !b.IsOwnLand).Sum(b => b.KgPerMonth);

        var buyingTotalCost = buying.Where(b => !b.IsOwnLand).Sum(b => b.CostPerMonth);



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



        var milkLiters = _milkService.GetMilkLitersProduced(month);

        decimal? feedPerLitre = milkLiters > 0 ? totalFeedMonthly / milkLiters : null;



        var grandHead = $"{(int)totalGoats} goats · {totalMixKg:F1} kg mix + {totalFodder:F0} kg green daily";

        if (fodderPoolVm.YearlyTotal > 0)

            grandHead += $" · incl. {DisplayHelper.FormatRs(fodderPoolVm.DailyTotal)}/day fodder land";



        var planFodderModeHtml = fodderPoolVm.YearlyTotal <= 0

            ? "<span style=\"color:var(--amber)\">Green fodder has no cost yet — fill in the fodder-land costs above.</span>"

            : $"<span class=\"breed\">Fodder land costs <b>{DisplayHelper.FormatRs(fodderPoolVm.DailyTotal)}/day</b>, split " +

              (fodderPoolDto.Mode == "equal" ? "<b>equally per goat</b>" : "<b>by the Green kg share</b>") +

              " · change this in the panel above.</span>";



        return new FeedPageViewModel

        {

            ActiveSubTab = subTab,

            MixPrices = mixPrices,

            AllPrices = allPrices,

            MixRecipe = mixRecipeVm,

            MixTotalKg = mixTotalKg,

            MixBatchCost = mixBatchCost,

            MixCostPerKg = mixCostPerKg,

            GroupPlans = groupPlans,

            CategoryCosts = categoryCosts,

            FodderKgPerDayTotal = totalFodder,

            FodderDryKgPerDayTotal = totalFodderDry,

            MixKgPerDayTotal = totalMixKg,

            BuyingList = buying,

            BuyingListTotalKg = buyingTotalKg,

            BuyingListTotalCost = buyingTotalCost,

            FeedPurchases = purchaseVms,

            FeedBoughtMonthTotal = purchaseVms.Sum(p => p.Amount),

            FeedBoughtKgTotal = purchaseVms.Sum(p => p.Kg),

            FeedMonth = month,

            GrandMonthly = totalFeedMonthly,

            GrandDaily = totalDaily,

            GrandHeadText = grandHead,

            FeedCostPerLitre = feedPerLitre,

            FeedCostPerLitreText = feedPerLitre.HasValue

                ? $"Rs {feedPerLitre.Value:F1} per litre of milk"

                : "— per litre (no milk logged yet)",

            MilkLitersThisMonth = milkLiters,

            TotalGoats = (int)totalGoats,

            Stock = stock,

            StoreStats = storeStats,

            LowStockItems = lowStockItems,

            ReorderNote = reorderNote,

            StockCheckRows = stock.Select(s => new StockCheckRowViewModel

            {

                FeedType = s.FeedType,

                DisplayName = s.DisplayName,

                BookKg = s.StockKg

            }).ToList(),

            FodderPool = fodderPoolVm,

            FeedSettings = feedSettingsVm,

            PlanFodderModeHtml = planFodderModeHtml,

            ActiveRecipeStatusKey = activeRecipeStatus,

            ActiveRecipe = activeRecipe

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

        plan.FodderDryKgPerDay = model.FodderDryKgPerDay;

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



    public async Task<MixRecipeStatusViewModel> GetMixRecipeForStatusAsync(string statusKey, CancellationToken cancellationToken = default)

    {

        var feedCatalog = await GetFeedCatalogAsync(cancellationToken);

        var prices = await GetPriceDictionaryAsync(cancellationToken);

        var mixFeedKeys = MixRecipeHelper.MixFeedKeys(feedCatalog.Select(f => f.FeedType)).ToList();

        var allRecipes = await GetAllMixRecipesAsync(cancellationToken);

        return BuildMixRecipeStatusViewModel(statusKey, allRecipes, mixFeedKeys, feedCatalog, prices);

    }



    public async Task UpdateMixRecipeForStatusAsync(UpdateMixRecipeForStatusViewModel model, CancellationToken cancellationToken = default)

    {

        var allRecipes = await GetAllMixRecipesAsync(cancellationToken);

        allRecipes[model.StatusKey] = new Dictionary<string, decimal>(model.Recipe, StringComparer.OrdinalIgnoreCase);

        await SaveAllMixRecipesAsync(allRecipes, cancellationToken);

    }



    public async Task<FodderPoolViewModel> GetFodderPoolAsync(CancellationToken cancellationToken = default)

    {

        var dto = await LoadFodderPoolDtoAsync(cancellationToken);

        var plans = await _context.FeedPlans.AsNoTracking().ToListAsync(cancellationToken);

        return BuildFodderPoolViewModel(dto, plans);

    }



    public async Task<FodderPoolViewModel> UpdateFodderPoolAsync(UpdateFodderPoolViewModel model, CancellationToken cancellationToken = default)

    {

        var dto = new FodderPoolDto

        {

            Acres = model.Acres,

            Mode = string.IsNullOrWhiteSpace(model.Mode) ? "share" : model.Mode,

            Items = model.Items.Select(i => new FodderPoolItemDto

            {

                Id = i.Id,

                Label = i.Label,

                Amount = i.Amount

            }).ToList()

        };

        await SaveFodderPoolDtoAsync(dto, cancellationToken);

        var plans = await _context.FeedPlans.AsNoTracking().ToListAsync(cancellationToken);

        return BuildFodderPoolViewModel(dto, plans);

    }



    public async Task<FodderPoolViewModel> AddFodderPoolItemAsync(AddFodderPoolItemViewModel model, CancellationToken cancellationToken = default)

    {

        var dto = await LoadFodderPoolDtoAsync(cancellationToken);

        var label = model.Label.Trim();

        if (dto.Items.Any(i => string.Equals(i.Label, label, StringComparison.OrdinalIgnoreCase)))

            throw new InvalidOperationException($"\"{label}\" is already in the list — edit its amount instead.");



        dto.Items.Add(new FodderPoolItemDto

        {

            Id = Guid.NewGuid().ToString("N")[..8],

            Label = label,

            Amount = model.Amount

        });

        await SaveFodderPoolDtoAsync(dto, cancellationToken);

        var plans = await _context.FeedPlans.AsNoTracking().ToListAsync(cancellationToken);

        return BuildFodderPoolViewModel(dto, plans);

    }



    public async Task<FodderPoolViewModel> RemoveFodderPoolItemAsync(string itemId, CancellationToken cancellationToken = default)

    {

        var dto = await LoadFodderPoolDtoAsync(cancellationToken);

        dto.Items.RemoveAll(i => i.Id == itemId);

        await SaveFodderPoolDtoAsync(dto, cancellationToken);

        var plans = await _context.FeedPlans.AsNoTracking().ToListAsync(cancellationToken);

        return BuildFodderPoolViewModel(dto, plans);

    }



    public async Task<FeedSettingsViewModel> GetFeedSettingsAsync(CancellationToken cancellationToken = default)

    {

        var dto = await LoadFeedSettingsDtoAsync(cancellationToken);

        return BuildFeedSettingsViewModel(dto);

    }



    public async Task<FeedSettingsViewModel> UpdateFeedSettingsAsync(UpdateFeedSettingsViewModel model, CancellationToken cancellationToken = default)

    {

        var dto = await LoadFeedSettingsDtoAsync(cancellationToken);

        dto.AutoUsage = model.AutoUsage;

        await SaveFeedSettingsDtoAsync(dto, cancellationToken);

        return BuildFeedSettingsViewModel(dto);

    }



    public async Task<int> RunAutoUsageAsync(CancellationToken cancellationToken = default)

    {

        var settings = await LoadFeedSettingsDtoAsync(cancellationToken);

        if (!settings.AutoUsage) return 0;



        var today = DateOnly.FromDateTime(DateTime.Today);

        if (string.IsNullOrWhiteSpace(settings.LastUsageDate))

        {

            settings.LastUsageDate = today.ToString("yyyy-MM-dd");

            await SaveFeedSettingsDtoAsync(settings, cancellationToken);

            return 0;

        }



        if (!DateOnly.TryParse(settings.LastUsageDate, out var lastDate))

        {

            settings.LastUsageDate = today.ToString("yyyy-MM-dd");

            await SaveFeedSettingsDtoAsync(settings, cancellationToken);

            return 0;

        }



        if (lastDate >= today) return 0;



        var plans = await _context.FeedPlans.AsNoTracking().ToListAsync(cancellationToken);

        var allRecipes = await GetAllMixRecipesAsync(cancellationToken);

        var mixFeedKeys = MixRecipeHelper.MixFeedKeys(

            await _context.FeedPrices.AsNoTracking().Select(p => p.FeedType).ToListAsync(cancellationToken)).ToList();

        var dailyUse = BuildDailyUseMap(plans, allRecipes, mixFeedKeys)

            .Where(kv => !FeedTypes.IsGreenFodder(kv.Key) && kv.Value > 0)

            .ToDictionary(kv => kv.Key, kv => kv.Value);



        if (dailyUse.Count == 0)

        {

            settings.LastUsageDate = today.ToString("yyyy-MM-dd");

            await SaveFeedSettingsDtoAsync(settings, cancellationToken);

            return 0;

        }



        var priceEntities = await _context.FeedPrices
            .Where(p => dailyUse.Keys.Contains(p.FeedType))
            .ToListAsync(cancellationToken);

        var applied = 0;
        var day = lastDate;
        while (day < today && applied < 120)
        {
            day = day.AddDays(1);
            foreach (var (feedType, kgPerDay) in dailyUse)
            {
                var kg = Math.Round(kgPerDay, 2);
                if (kg <= 0) continue;

                _context.FeedUsageRecords.Add(new FeedUsageRecord
                {
                    Date = day,
                    FeedType = feedType,
                    Kg = kg,
                    IsAutomatic = true,
                    IsStockCheck = false
                });

                var price = priceEntities.FirstOrDefault(p =>
                    string.Equals(p.FeedType, feedType, StringComparison.OrdinalIgnoreCase));
                if (price is not null)
                {
                    price.StockKg = Math.Max(0, price.StockKg - kg);
                    price.UpdatedDate = DateTime.UtcNow;
                }
            }
            applied++;
        }



        settings.LastUsageDate = day.ToString("yyyy-MM-dd");

        await SaveFeedSettingsDtoAsync(settings, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return applied;

    }



    public async Task<StockCheckResultViewModel> SaveStockCheckAsync(SaveStockCheckViewModel model, CancellationToken cancellationToken = default)

    {

        var entries = model.Counts.Where(kv => kv.Value >= 0).ToList();

        if (entries.Count == 0)

            return new StockCheckResultViewModel { Success = false, Message = "Type what you actually counted in the store." };



        var today = DateOnly.FromDateTime(DateTime.Today);

        var lines = new List<string>();

        decimal underage = 0;



        foreach (var (feedType, actual) in entries)

        {

            var price = await _context.FeedPrices.FirstOrDefaultAsync(p => p.FeedType == feedType, cancellationToken);

            if (price is null) continue;



            var book = price.StockKg;

            var diff = actual - book;

            if (Math.Abs(diff) >= 0.05m)

            {

                lines.Add($"{price.DisplayName} {(diff > 0 ? "+" : "")}{diff:F1} kg");

                if (diff < 0)

                {

                    underage += -diff;

                    _context.FeedUsageRecords.Add(new FeedUsageRecord

                    {

                        Date = today,

                        FeedType = feedType,

                        Kg = -diff,

                        IsAutomatic = false,

                        IsStockCheck = true

                    });

                }

            }



            price.StockKg = Math.Max(0, actual);

            if (actual > price.StockKgFull)

                price.StockKgFull = actual;

            price.UpdatedDate = DateTime.UtcNow;

        }



        await _context.SaveChangesAsync(cancellationToken);



        var detailHtml = lines.Count > 0
            ? $"<span class=\"breed\">{string.Join(" · ", lines)}</span>" +
              (underage > 0
                  ? $"<div class=\"breed\" style=\"margin-top:6px\">You used about <b>{underage:F1} kg</b> more than the plan. If this repeats every month, raise the kg/goat in your feed plan so costs are realistic.</div>"
                  : "")
            : null;

        return new StockCheckResultViewModel
        {
            Success = true,
            Message = lines.Count > 0 ? "✓ Stock corrected." : "✓ Store matches the books exactly.",
            DetailHtml = detailHtml,
            UnderageKg = underage
        };

    }



    public decimal GetMixCostPerKg() => GetMixCostPerKg(DisplayHelper.StatusKey(GoatStatus.Milking));



    public decimal GetMixCostPerKg(string statusKey)

    {

        var prices = _context.FeedPrices.AsNoTracking().ToDictionary(p => p.FeedType, p => p.PricePerKg);

        var mixKeys = MixRecipeHelper.MixFeedKeys(prices.Keys).ToList();

        var recipe = GetMixRecipeForStatusAsync(statusKey).GetAwaiter().GetResult();

        var dict = recipe.Items.ToDictionary(i => i.FeedType, i => i.KgInBatch, StringComparer.OrdinalIgnoreCase);

        return MixRecipeHelper.MixCostPerKg(dict, prices, mixKeys);

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

        await AdjustFeedStockAsync(model.FeedType, model.Kg, setFullLevel: true, cancellationToken);

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

            await AdjustFeedStockAsync(model.FeedType, model.Kg - oldKg, setFullLevel: model.Kg > oldKg, cancellationToken);

        else

        {

            await AdjustFeedStockAsync(oldFeedType, -oldKg, cancellationToken: cancellationToken);

            await AdjustFeedStockAsync(model.FeedType, model.Kg, setFullLevel: true, cancellationToken);

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

        await AdjustFeedStockAsync(entity.FeedType, -entity.Kg, cancellationToken: cancellationToken);

        _context.FeedPurchases.Remove(entity);

        await _context.SaveChangesAsync(cancellationToken);

        return true;

    }



    public async Task UpdateFeedStockAsync(UpdateFeedStockViewModel model, CancellationToken cancellationToken = default)

    {

        var entity = await _context.FeedPrices.FirstOrDefaultAsync(p => p.FeedType == model.FeedType, cancellationToken);

        if (entity is null) return;

        entity.StockKg = Math.Max(0, model.StockKg);

        if (entity.StockKg > entity.StockKgFull)

            entity.StockKgFull = entity.StockKg;

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

            StockKg = 0,

            StockKgFull = 0

        };

        _context.FeedPrices.Add(price);



        if (FeedTypes.IsMixIngredient(feedType))

        {

            var allRecipes = await GetAllMixRecipesAsync(cancellationToken);

            foreach (var recipe in allRecipes.Values)

                recipe[feedType] = 0;

            await SaveAllMixRecipesAsync(allRecipes, cancellationToken);

        }



        await _context.SaveChangesAsync(cancellationToken);

        return new FeedPriceViewModel { FeedType = feedType, DisplayName = displayName, PricePerKg = model.PricePerKg, StockKg = 0 };

    }



    public async Task<bool> DeleteFeedTypeAsync(string feedType, CancellationToken cancellationToken = default)

    {

        if (string.Equals(feedType, FeedTypes.Fodder, StringComparison.OrdinalIgnoreCase) ||

            string.Equals(feedType, FeedTypes.FodderDry, StringComparison.OrdinalIgnoreCase))

            return false;



        var price = await _context.FeedPrices.FirstOrDefaultAsync(p => p.FeedType == feedType, cancellationToken);

        if (price is null) return false;



        var planItems = await _context.FeedPlanItems.Where(i => i.FeedType == feedType).ToListAsync(cancellationToken);

        _context.FeedPlanItems.RemoveRange(planItems);

        _context.FeedPrices.Remove(price);



        var allRecipes = await GetAllMixRecipesAsync(cancellationToken);

        foreach (var recipe in allRecipes.Values)

            recipe.Remove(feedType);

        await SaveAllMixRecipesAsync(allRecipes, cancellationToken);



        await _context.SaveChangesAsync(cancellationToken);

        return true;

    }



    public decimal CalculateDailyFeedCost(decimal mixKgPerDay, string? statusKey = null)

    {

        statusKey ??= DisplayHelper.StatusKey(GoatStatus.Milking);

        var prices = _context.FeedPrices.AsNoTracking().ToDictionary(p => p.FeedType, p => p.PricePerKg);

        var mixCost = GetMixCostPerKg(statusKey);

        var plan = _context.FeedPlans.AsNoTracking().FirstOrDefault(p => DisplayHelper.StatusKey(p.StatusKey) == statusKey);

        if (plan is null)

            return MixRecipeHelper.PlanDailyFeedCost(mixKgPerDay, mixCost);



        var fodderPool = LoadFodderPoolDtoAsync().GetAwaiter().GetResult();

        var fodderDaily = FodderPoolHelper.DailyTotal(fodderPool);

        var totalShare = StatusOrder.Sum(st =>

        {

            var n = _goatService.CountByStatus(st);

            var p = _context.FeedPlans.AsNoTracking().FirstOrDefault(x => x.StatusKey == st);

            return p is null ? 0 : p.FodderKgPerDay * n;

        });

        var totalHeads = StatusOrder.Sum(st => _goatService.CountByStatus(st));

        var fodderCost = CalculateFodderCostPerGoat(plan.FodderKgPerDay, fodderDaily, totalShare, totalHeads, fodderPool.Mode);

        return MixRecipeHelper.PlanDailyFeedCostV41(

            mixKgPerDay, mixCost, plan.FodderDryKgPerDay, prices.GetValueOrDefault(FeedTypes.FodderDry), fodderCost);

    }



    public decimal CalculateFarmFeedMonthly() => CalculateFarmFeedMonthlyInternal(feedOnly: true);



    public decimal CalculateFarmMedicineMonthly() => 0;



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



    private decimal CalculateFarmFeedMonthlyInternal(bool feedOnly)

    {

        var prices = _context.FeedPrices.AsNoTracking().ToDictionary(p => p.FeedType, p => p.PricePerKg);

        var plans = _context.FeedPlans.AsNoTracking().ToList();

        var allRecipes = GetAllMixRecipesAsync().GetAwaiter().GetResult();

        var mixFeedKeys = MixRecipeHelper.MixFeedKeys(prices.Keys).ToList();

        var fodderPool = LoadFodderPoolDtoAsync().GetAwaiter().GetResult();

        var fodderDaily = FodderPoolHelper.DailyTotal(fodderPool);

        var totalShare = StatusOrder.Sum(st =>

        {

            var n = _goatService.CountByStatus(st);

            var plan = plans.FirstOrDefault(p => p.StatusKey == st);

            return plan is null ? 0 : plan.FodderKgPerDay * n;

        });

        var totalHeads = StatusOrder.Sum(st => _goatService.CountByStatus(st));

        decimal total = 0;



        foreach (var st in StatusOrder)

        {

            var n = _goatService.CountByStatus(st);

            var plan = plans.FirstOrDefault(p => p.StatusKey == st);

            if (plan is null) continue;



            var stKey = DisplayHelper.StatusKey(st);

            var recipe = allRecipes.GetValueOrDefault(stKey) ?? new Dictionary<string, decimal>(MixRecipeHelper.DefaultRecipe);

            var mixCost = MixRecipeHelper.MixCostPerKg(recipe, prices, mixFeedKeys);

            var fodderCost = CalculateFodderCostPerGoat(plan.FodderKgPerDay, fodderDaily, totalShare, totalHeads, fodderPool.Mode);

            var daily = MixRecipeHelper.PlanDailyFeedCostV41(

                plan.MixKgPerDay, mixCost, plan.FodderDryKgPerDay, prices.GetValueOrDefault(FeedTypes.FodderDry), fodderCost);

            total += daily * 30 * n;

        }

        return total;

    }



    private async Task<Dictionary<string, Dictionary<string, decimal>>> GetAllMixRecipesAsync(CancellationToken cancellationToken = default)

    {

        var setting = await _context.AppSettings.AsNoTracking()

            .FirstOrDefaultAsync(s => s.Key == AppSettingKeys.MixRecipes, cancellationToken);



        if (setting is not null && !string.IsNullOrWhiteSpace(setting.Value))

        {

            try

            {

                var parsed = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, decimal>>>(setting.Value);

                if (parsed is not null)

                    return new Dictionary<string, Dictionary<string, decimal>>(parsed, StringComparer.OrdinalIgnoreCase);

            }

            catch { /* fall through */ }

        }



        var legacy = await GetMixRecipeAsync(cancellationToken);

        var result = new Dictionary<string, Dictionary<string, decimal>>(StringComparer.OrdinalIgnoreCase);

        foreach (var st in StatusOrder)

        {

            var key = DisplayHelper.StatusKey(st);

            if (MixRecipeHelper.DefaultRecipesPerStatus.TryGetValue(st, out var def))

                result[key] = new Dictionary<string, decimal>(def, StringComparer.OrdinalIgnoreCase);

            else

                result[key] = new Dictionary<string, decimal>(legacy, StringComparer.OrdinalIgnoreCase);

        }

        return result;

    }



    private async Task SaveAllMixRecipesAsync(

        Dictionary<string, Dictionary<string, decimal>> recipes,

        CancellationToken cancellationToken)

    {

        var json = JsonSerializer.Serialize(recipes);

        var setting = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == AppSettingKeys.MixRecipes, cancellationToken);

        if (setting is null)

            _context.AppSettings.Add(new AppSetting { Key = AppSettingKeys.MixRecipes, Value = json });

        else

        {

            setting.Value = json;

            setting.UpdatedDate = DateTime.UtcNow;

        }

        await _context.SaveChangesAsync(cancellationToken);

    }



    private async Task<FodderPoolDto> LoadFodderPoolDtoAsync(CancellationToken cancellationToken = default)

    {

        var setting = await _context.AppSettings.AsNoTracking()

            .FirstOrDefaultAsync(s => s.Key == AppSettingKeys.FodderPool, cancellationToken);

        if (setting is null || string.IsNullOrWhiteSpace(setting.Value))

            return FodderPoolHelper.DefaultPool();



        try

        {

            return JsonSerializer.Deserialize<FodderPoolDto>(setting.Value) ?? FodderPoolHelper.DefaultPool();

        }

        catch

        {

            return FodderPoolHelper.DefaultPool();

        }

    }



    private async Task SaveFodderPoolDtoAsync(FodderPoolDto dto, CancellationToken cancellationToken)

    {

        var json = JsonSerializer.Serialize(dto);

        var setting = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == AppSettingKeys.FodderPool, cancellationToken);

        if (setting is null)

            _context.AppSettings.Add(new AppSetting { Key = AppSettingKeys.FodderPool, Value = json });

        else

        {

            setting.Value = json;

            setting.UpdatedDate = DateTime.UtcNow;

        }

        await _context.SaveChangesAsync(cancellationToken);

    }



    private async Task<FeedSettingsDto> LoadFeedSettingsDtoAsync(CancellationToken cancellationToken = default)

    {

        var setting = await _context.AppSettings.AsNoTracking()

            .FirstOrDefaultAsync(s => s.Key == AppSettingKeys.FeedSettings, cancellationToken);

        if (setting is null || string.IsNullOrWhiteSpace(setting.Value))

            return new FeedSettingsDto();



        try

        {

            return JsonSerializer.Deserialize<FeedSettingsDto>(setting.Value) ?? new FeedSettingsDto();

        }

        catch

        {

            return new FeedSettingsDto();

        }

    }



    private async Task SaveFeedSettingsDtoAsync(FeedSettingsDto dto, CancellationToken cancellationToken)

    {

        var json = JsonSerializer.Serialize(dto);

        var setting = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == AppSettingKeys.FeedSettings, cancellationToken);

        if (setting is null)

            _context.AppSettings.Add(new AppSetting { Key = AppSettingKeys.FeedSettings, Value = json });

        else

        {

            setting.Value = json;

            setting.UpdatedDate = DateTime.UtcNow;

        }

        await _context.SaveChangesAsync(cancellationToken);

    }



    private static decimal CalculateFodderCostPerGoat(
        decimal fodderKgPerGoat,
        decimal fodderDailyPool,
        decimal totalFodderShare,
        int totalGoatsForPool,
        string mode)

    {

        if (fodderDailyPool <= 0) return 0;

        if (totalGoatsForPool <= 0) return 0;



        if (string.Equals(mode, "equal", StringComparison.OrdinalIgnoreCase))

            return fodderDailyPool / totalGoatsForPool;



        if (totalFodderShare <= 0)

            return fodderDailyPool / totalGoatsForPool;



        return fodderDailyPool * (fodderKgPerGoat / totalFodderShare);

    }



    private FodderPoolViewModel BuildFodderPoolViewModel(FodderPoolDto dto, IReadOnlyList<FeedPlan> plans)

    {

        var yearly = FodderPoolHelper.AnnualTotal(dto);

        var daily = FodderPoolHelper.DailyTotal(dto);

        var heads = StatusOrder.Sum(st => _goatService.CountByStatus(st));

        var kgDay = StatusOrder.Sum(st =>

        {

            var n = _goatService.CountByStatus(st);

            var plan = plans.FirstOrDefault(p => p.StatusKey == st);

            return plan is null ? 0 : plan.FodderKgPerDay * n;

        });



        decimal? perKg = kgDay > 0 ? daily / kgDay : null;

        decimal? perAcre = dto.Acres > 0 ? yearly / dto.Acres : null;



        var note = yearly <= 0

            ? "<span style=\"color:var(--amber)\">Add your yearly fodder-land costs above to give green fodder a real cost.</span>"

            : $"<span class=\"breed\">Works out to about <b>Rs {(perKg?.ToString("F1") ?? "—")} per kg</b> of green fodder" +

              (dto.Acres > 0 ? $" · <b>{DisplayHelper.FormatRs(yearly / dto.Acres)}</b> per acre / year" : "") +

              " · compare with buying chaara from the market to see if growing it is worth it.</span>";



        return new FodderPoolViewModel

        {

            Acres = dto.Acres,

            Mode = dto.Mode ?? "share",

            Items = dto.Items.Select(i => new FodderPoolItemViewModel

            {

                Id = i.Id,

                Label = i.Label,

                Amount = i.Amount

            }).ToList(),

            YearlyTotal = yearly,

            DailyTotal = daily,

            AveragePerGoatDay = heads > 0 ? daily / heads : 0,

            CostPerKgGreen = perKg,

            CostPerAcreYear = perAcre,

            NoteHtml = note

        };

    }



    private static FeedSettingsViewModel BuildFeedSettingsViewModel(FeedSettingsDto dto) => new()

    {

        AutoUsage = dto.AutoUsage,

        LastUsageDate = dto.LastUsageDate,

        AutoInfoHtml = !string.IsNullOrWhiteSpace(dto.LastUsageDate)

            ? $"<span class=\"breed\">Stock reduces automatically each day from your feed plan · up to date to <b>{dto.LastUsageDate}</b></span>"

            : "<span class=\"breed\">Stock will start reducing automatically from tomorrow.</span>"

    };



    private MixRecipeStatusViewModel BuildMixRecipeStatusViewModel(

        string statusKey,

        IReadOnlyDictionary<string, Dictionary<string, decimal>> allRecipes,

        IReadOnlyList<string> mixFeedKeys,

        IReadOnlyList<FeedPrice> feedCatalog,

        IReadOnlyDictionary<string, decimal> prices)

    {

        var status = DisplayHelper.ParseStatusKey(statusKey);

        var (text, _) = DisplayHelper.GetStatusDisplay(status);

        var recipe = allRecipes.GetValueOrDefault(statusKey)

            ?? new Dictionary<string, decimal>(MixRecipeHelper.DefaultRecipe, StringComparer.OrdinalIgnoreCase);

        var items = BuildMixRecipeItems(recipe, mixFeedKeys, feedCatalog, prices);

        var totalKg = MixRecipeHelper.MixTotalKg(recipe, mixFeedKeys);

        var batchCost = MixRecipeHelper.MixBatchCost(recipe, prices, mixFeedKeys);

        var costPerKg = MixRecipeHelper.MixCostPerKg(recipe, prices, mixFeedKeys);



        var compare = StatusOrder.Select(st =>

        {

            var key = DisplayHelper.StatusKey(st);

            var r = allRecipes.GetValueOrDefault(key) ?? recipe;

            return new MixRecipeCostCompareViewModel

            {

                StatusKey = key,

                StatusDisplay = DisplayHelper.GetStatusDisplay(st).Text,

                CostPerKg = MixRecipeHelper.MixCostPerKg(r, prices, mixFeedKeys),

                IsActive = string.Equals(key, statusKey, StringComparison.OrdinalIgnoreCase)

            };

        }).ToList();



        return new MixRecipeStatusViewModel

        {

            StatusKey = statusKey,

            StatusDisplay = text,

            Items = items,

            TotalKg = totalKg,

            BatchCost = batchCost,

            CostPerKg = costPerKg,

            AllStatusCosts = compare

        };

    }



    private static List<MixRecipeItemViewModel> BuildMixRecipeItems(

        IReadOnlyDictionary<string, decimal> recipe,

        IReadOnlyList<string> mixFeedKeys,

        IReadOnlyList<FeedPrice> feedCatalog,

        IReadOnlyDictionary<string, decimal> prices)

    {

        var mixTotalKg = MixRecipeHelper.MixTotalKg(recipe, mixFeedKeys);

        return mixFeedKeys.Select(k =>

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

    }



    private async Task<List<FeedPrice>> GetFeedCatalogAsync(CancellationToken cancellationToken) =>

        await _context.FeedPrices.AsNoTracking().OrderBy(p => p.Id).ToListAsync(cancellationToken);



    private async Task<Dictionary<string, decimal>> GetPriceDictionaryAsync(CancellationToken cancellationToken) =>

        await _context.FeedPrices.AsNoTracking()

            .ToDictionaryAsync(p => p.FeedType, p => p.PricePerKg, cancellationToken);



    private async Task<Dictionary<string, decimal>> GetPurchasedTotalsAsync(CancellationToken cancellationToken) =>

        await _context.FeedPurchases.AsNoTracking()

            .GroupBy(p => p.FeedType)

            .Select(g => new { FeedType = g.Key, Total = g.Sum(x => x.Kg) })

            .ToDictionaryAsync(x => x.FeedType, x => x.Total, cancellationToken);



    private async Task<Dictionary<string, decimal>> GetUsedTotalsAsync(CancellationToken cancellationToken) =>

        await _context.FeedUsageRecords.AsNoTracking()

            .GroupBy(u => u.FeedType)

            .Select(g => new { FeedType = g.Key, Total = g.Sum(x => x.Kg) })

            .ToDictionaryAsync(x => x.FeedType, x => x.Total, cancellationToken);



    private IReadOnlyDictionary<string, decimal> BuildDailyUseMap(

        IReadOnlyList<FeedPlan> plans,

        IReadOnlyDictionary<string, Dictionary<string, decimal>> allRecipes,

        IReadOnlyList<string> mixFeedKeys)

    {

        var kg = mixFeedKeys.ToDictionary(k => k, _ => 0m, StringComparer.OrdinalIgnoreCase);

        kg[FeedTypes.Fodder] = 0;

        kg[FeedTypes.FodderDry] = 0;



        foreach (var st in StatusOrder)

        {

            var n = _goatService.CountByStatus(st);

            var plan = plans.FirstOrDefault(p => p.StatusKey == st);

            if (plan is null || n == 0) continue;



            var stKey = DisplayHelper.StatusKey(st);

            var recipe = allRecipes.GetValueOrDefault(stKey) ?? new Dictionary<string, decimal>();

            var mixKgDay = plan.MixKgPerDay * n;

            var total = MixRecipeHelper.MixTotalKg(recipe, mixFeedKeys);

            if (total > 0)

            {

                foreach (var k in mixFeedKeys)

                    kg[k] += mixKgDay * (recipe.GetValueOrDefault(k) / total);

            }



            kg[FeedTypes.Fodder] += plan.FodderKgPerDay * n;

            kg[FeedTypes.FodderDry] += plan.FodderDryKgPerDay * n;

        }



        return kg;

    }



    private static FeedStoreStatsViewModel BuildStoreStats(

        IReadOnlyList<FeedStockRowViewModel> stock,

        IReadOnlyDictionary<string, decimal> dailyUse,

        IReadOnlyDictionary<string, decimal> prices)

    {

        var totalKg = stock.Sum(s => s.StockKg);

        var totalVal = stock.Sum(s => s.StockKg * prices.GetValueOrDefault(s.FeedType));

        var dailyKg = stock.Sum(s => s.KgPerDay);

        return new FeedStoreStatsViewModel

        {

            TotalStockKg = totalKg,

            DailyUsageKg = dailyKg,

            StockValue = totalVal,

            LowStockCount = stock.Count(s => s.IsLowStock)

        };

    }



    private static FeedReorderNoteViewModel? BuildReorderNote(

        IReadOnlyList<FeedStockRowViewModel> stock,

        IReadOnlyDictionary<string, decimal> dailyUse)

    {

        var soon = stock

            .Select(s =>

            {

                var du = dailyUse.GetValueOrDefault(s.FeedType);

                if (du <= 0 || s.StockKgFull <= 0) return null;

                var minLevel = s.StockKgFull * 0.2m;

                var daysToMin = (int)Math.Floor((s.StockKg - minLevel) / du);

                var daysOut = (int)Math.Floor(s.StockKg / du);

                return new { s.FeedType, s.DisplayName, DaysToMinimum = daysToMin, DaysUntilEmpty = daysOut };

            })

            .Where(x => x is not null)

            .OrderBy(x => x!.DaysToMinimum)

            .FirstOrDefault();



        if (soon is null || soon.DaysToMinimum > 21) return null;



        return new FeedReorderNoteViewModel

        {

            FeedType = soon.FeedType,

            DisplayName = soon.DisplayName,

            DaysToMinimum = soon.DaysToMinimum,

            DaysUntilEmpty = soon.DaysUntilEmpty,

            IsCritical = soon.DaysToMinimum <= 7

        };

    }



    private static IReadOnlyList<FeedStockRowViewModel> BuildStockRows(

        IReadOnlyList<FeedPrice> feedCatalog,

        IReadOnlyDictionary<string, decimal> dailyUse,

        IReadOnlyDictionary<string, decimal> prices,

        IReadOnlyDictionary<string, decimal> purchasedTotals,

        IReadOnlyDictionary<string, decimal> usedTotals)

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

                < 7 => "color:#8a261c;font-weight:800",

                < 14 => "color:var(--amber);font-weight:700",

                _ => "color:var(--green-dark);font-weight:700"

            };

            var pct = f.StockKgFull > 0 ? st / f.StockKgFull : (decimal?)null;

            var isLow = pct is <= 0.2m;



            return new FeedStockRowViewModel

            {

                FeedType = f.FeedType,

                DisplayName = f.DisplayName,

                StockKg = st,

                StockKgFull = f.StockKgFull,

                PurchasedKg = purchasedTotals.GetValueOrDefault(f.FeedType),

                UsedKg = usedTotals.GetValueOrDefault(f.FeedType),

                KgPerDay = du,

                DaysLeft = days,

                DaysLeftText = floorDays.HasValue ? $"{floorDays} days" : "—",

                DaysLeftColor = color,

                StockPercent = pct,

                IsLowStock = isLow

            };

        }).ToList();

    }



    private async Task AdjustFeedStockAsync(

        string feedType,

        decimal deltaKg,

        bool setFullLevel = false,

        CancellationToken cancellationToken = default)

    {

        if (deltaKg == 0) return;

        var price = await _context.FeedPrices.FirstOrDefaultAsync(p => p.FeedType == feedType, cancellationToken);

        if (price is null) return;

        price.StockKg = Math.Max(0, price.StockKg + deltaKg);

        if (setFullLevel || price.StockKg > price.StockKgFull)

            price.StockKgFull = price.StockKg;

        price.UpdatedDate = DateTime.UtcNow;

    }



    private IReadOnlyList<FeedBuyingRowViewModel> BuildBuyingList(

        IReadOnlyList<FeedPlan> plans,

        IReadOnlyDictionary<string, Dictionary<string, decimal>> allRecipes,

        IReadOnlyDictionary<string, decimal> prices,

        IReadOnlyList<string> mixFeedKeys)

    {

        var need = mixFeedKeys.ToDictionary(k => k, _ => 0m, StringComparer.OrdinalIgnoreCase);

        decimal mixKgDay = 0, fodderKgDay = 0, dryKgDay = 0;



        foreach (var st in StatusOrder)

        {

            var n = _goatService.CountByStatus(st);

            var plan = plans.FirstOrDefault(p => p.StatusKey == st);

            if (plan is null || n == 0) continue;



            var stKey = DisplayHelper.StatusKey(st);

            var recipe = allRecipes.GetValueOrDefault(stKey) ?? new Dictionary<string, decimal>();

            var kgDay = plan.MixKgPerDay * n;

            mixKgDay += kgDay;

            var total = MixRecipeHelper.MixTotalKg(recipe, mixFeedKeys);

            if (total > 0)

            {

                foreach (var k in mixFeedKeys)

                    need[k] += kgDay * (recipe.GetValueOrDefault(k) / total);

            }



            fodderKgDay += plan.FodderKgPerDay * n;

            dryKgDay += plan.FodderDryKgPerDay * n;

        }



        var rows = new List<FeedBuyingRowViewModel>();

        foreach (var k in mixFeedKeys)

        {

            var d = need.GetValueOrDefault(k);

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



        if (dryKgDay > 0)

        {

            var dryFeed = _context.FeedPrices.AsNoTracking().FirstOrDefault(f => f.FeedType == FeedTypes.FodderDry);

            var m = dryKgDay * 30;

            rows.Add(new FeedBuyingRowViewModel

            {

                DisplayName = dryFeed?.DisplayName ?? "Dry fodder (toori / bhoosa)",

                KgPerDay = dryKgDay,

                KgPerMonth = m,

                CostPerMonth = m * prices.GetValueOrDefault(FeedTypes.FodderDry),

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



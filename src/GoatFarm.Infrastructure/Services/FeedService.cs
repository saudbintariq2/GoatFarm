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

        GoatStatus.Dry, GoatStatus.Buck, GoatStatus.Sale, GoatStatus.Meat

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

        await EnsureMilkPowderAsync(cancellationToken);

        await CatchUpKidMilkAsync(cancellationToken);



        month ??= MonthHelper.CurrentMonthKey();

        subTab = string.IsNullOrWhiteSpace(subTab) ? "store" : subTab;



        var prices = await GetPriceDictionaryAsync(cancellationToken);

        var feedCatalog = await GetFeedCatalogAsync(cancellationToken);

        var mixFeedKeys = MixRecipeHelper.MixFeedKeys(

            feedCatalog.Where(f => !FeedTypes.IsDirectUse(f.UseType)).Select(f => f.FeedType)).ToList();

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

                StockKg = p.StockKg,

                UseType = string.IsNullOrWhiteSpace(p.UseType) ? FeedTypes.UseRecipe : p.UseType

            }).ToList();



        var legacyRecipe = allRecipes.GetValueOrDefault(activeRecipeStatus)

            ?? new Dictionary<string, decimal>(MixRecipeHelper.DefaultRecipe, StringComparer.OrdinalIgnoreCase);

        var mixTotalKg = MixRecipeHelper.MixTotalKg(legacyRecipe, mixFeedKeys);

        var mixBatchCost = MixRecipeHelper.MixBatchCost(legacyRecipe, prices, mixFeedKeys);

        var mixCostPerKg = MixRecipeHelper.MixCostPerKg(legacyRecipe, prices, mixFeedKeys);

        var mixRecipeVm = BuildMixRecipeItems(legacyRecipe, mixFeedKeys, feedCatalog, prices);

        var herd = await LoadHerdSnapshotAsync(cancellationToken);
        var customGroups = await _context.CustomFeedingGroups.AsNoTracking().OrderBy(g => g.Name).ToListAsync(cancellationToken);
        var directPlans = await LoadDirectPlansAsync(cancellationToken);
        var moneyAccounts = await LoadMoneyAccountsAsync(cancellationToken);

        var dailyUse = BuildDailyUseMap(plans, allRecipes, mixFeedKeys, herd, customGroups, feedCatalog, directPlans);

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
        var totalHeads = FeedPlanHeadcount(herd, customGroups);
        var totalFodderShare = StatusOrder.Sum(st =>
        {
            var n = CountFeedPlan(herd, st);
            var plan = plans.FirstOrDefault(p => p.StatusKey == st);
            return plan is null ? 0 : plan.FodderKgPerDay * n;
        }) + customGroups.Sum(g => g.FodderKgPerDay * CountCustomGroup(herd, g.Id));

        foreach (var st in StatusOrder)
        {
            var count = CountFeedPlan(herd, st);
            if (count == 0 && st is GoatStatus.Sale or GoatStatus.Meat) continue;
            var plan = plans.FirstOrDefault(p => p.StatusKey == st);
            if (plan is null) continue;

            var stKey = DisplayHelper.StatusKey(st);
            var (text, css) = DisplayHelper.GetStatusDisplay(st);
            if (st == GoatStatus.Kid) text = "Weaned Kids";
            var recipeKey = stKey;
            var statusRecipe = allRecipes.GetValueOrDefault(recipeKey) ?? legacyRecipe;
            var statusMixCost = MixRecipeHelper.MixCostPerKg(statusRecipe, prices, mixFeedKeys);
            var fodderCostPerGoat = CalculateFodderCostPerGoat(
                plan.FodderKgPerDay, fodderDailyPool, totalFodderShare, totalHeads, fodderPoolDto.Mode);
            var dryPrice = prices.GetValueOrDefault(FeedTypes.FodderDry);
            var directCost = DirectCostPerGoat(stKey, feedCatalog, prices, directPlans);
            var dailyPerGoat = MixRecipeHelper.PlanDailyFeedCostV41(
                plan.MixKgPerDay, statusMixCost, plan.FodderDryKgPerDay, dryPrice, fodderCostPerGoat) + directCost;
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
                MonthlyTotal = feedM,
                RecipeFrom = stKey,
                DirectKg = DirectKgFor(stKey, feedCatalog, directPlans)
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

        foreach (var fg in customGroups)
        {
            var count = CountCustomGroup(herd, fg.Id);
            var fgKey = FeedGroupKey(fg.Id);
            var recipeKey = string.IsNullOrWhiteSpace(fg.RecipeFrom) ? "milking" : fg.RecipeFrom;
            var statusRecipe = allRecipes.GetValueOrDefault(recipeKey) ?? legacyRecipe;
            var statusMixCost = MixRecipeHelper.MixCostPerKg(statusRecipe, prices, mixFeedKeys);
            var fodderCostPerGoat = CalculateFodderCostPerGoat(
                fg.FodderKgPerDay, fodderDailyPool, totalFodderShare, totalHeads, fodderPoolDto.Mode);
            var dryPrice = prices.GetValueOrDefault(FeedTypes.FodderDry);
            var directCost = DirectCostPerGoat(fgKey, feedCatalog, prices, directPlans);
            var dailyPerGoat = MixRecipeHelper.PlanDailyFeedCostV41(
                fg.MixKgPerDay, statusMixCost, fg.FodderDryKgPerDay, dryPrice, fodderCostPerGoat) + directCost;
            var feedM = dailyPerGoat * 30 * count;
            var fodder = fg.FodderKgPerDay * count;
            var mixKgGroup = fg.MixKgPerDay * count;

            groupPlans.Add(new FeedGroupPlanRowViewModel
            {
                StatusKey = fgKey,
                StatusDisplay = fg.Name,
                StatusCssClass = "chip-dry",
                GoatCount = count,
                MixKgPerDay = fg.MixKgPerDay,
                FodderKgPerDay = fg.FodderKgPerDay,
                FodderDryKgPerDay = fg.FodderDryKgPerDay,
                FodderCostPerGoatPerDay = fodderCostPerGoat,
                MixCostPerKg = statusMixCost,
                DailyCostPerGoat = dailyPerGoat,
                MonthlyTotal = feedM,
                IsCustomGroup = true,
                RecipeFrom = recipeKey,
                DirectKg = DirectKgFor(fgKey, feedCatalog, directPlans)
            });

            categoryCosts.Add(new FeedCategoryCostRowViewModel
            {
                StatusKey = fgKey,
                StatusDisplay = fg.Name,
                StatusCssClass = "chip-dry",
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
            totalFodderDry += fg.FodderDryKgPerDay * count;
            totalMixKg += mixKgGroup;
        }

        var totalMonthly = categoryCosts.Sum(c => c.MonthlyCost);

        foreach (var row in categoryCosts)

            row.SharePercent = totalMonthly > 0 ? (int)Math.Round(row.MonthlyCost / totalMonthly * 100) : 0;



        var buying = BuildBuyingList(plans, allRecipes, prices, mixFeedKeys, herd, customGroups, feedCatalog, directPlans);

        var buyingTotalKg = buying.Where(b => !b.IsOwnLand).Sum(b => b.KgPerMonth);

        var buyingTotalCost = buying.Where(b => !b.IsOwnLand).Sum(b => b.CostPerMonth);



        var (monthStart, monthEnd) = MonthHelper.GetMonthRange(month);

        var purchases = await _context.FeedPurchases.AsNoTracking()

            .Where(p => p.Date >= monthStart && p.Date < monthEnd)

            .OrderByDescending(p => p.Date)

            .ToListAsync(cancellationToken);



        var nameMap = feedCatalog.ToDictionary(f => f.FeedType, f => f.DisplayName, StringComparer.OrdinalIgnoreCase);
        var acctName = moneyAccounts.ToDictionary(a => a.Id, a => a.Name);
        var purchaseVms = purchases.Select(p => MapPurchase(p, nameMap, acctName)).ToList();

        var allPrices = feedCatalog.Select(p => new FeedPriceViewModel
        {
            FeedType = p.FeedType,
            DisplayName = p.DisplayName,
            PricePerKg = p.PricePerKg,
            StockKg = p.StockKg,
            UseType = string.IsNullOrWhiteSpace(p.UseType) ? FeedTypes.UseRecipe : p.UseType
        }).ToList();



        var milkLiters = _milkService.GetMilkLitersProduced(month);

        decimal? feedPerLitre = milkLiters > 0 ? totalFeedMonthly / milkLiters : null;



        var unweaned = herd.Count(OnMilkDiet);
        var grandHead = $"{(int)totalGoats} goats · {totalMixKg:F1} kg mix + {totalFodder:F0} kg green daily";
        if (unweaned > 0)
            grandHead += $" · {unweaned} unweaned kid{(unweaned == 1 ? "" : "s")} on milk diet";

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

            ActiveRecipe = activeRecipe,

            RecipeOptions = MixRecipeHelper.RecipeOptions.Select(o => new FeedRecipeOptionViewModel { Key = o.Key, Name = o.Name }).ToList(),

            MoneyAccounts = moneyAccounts,

            DirectFeeds = feedCatalog.Where(f => FeedTypes.IsDirectUse(f.UseType)).Select(f => new FeedDirectFeedViewModel
            {
                FeedType = f.FeedType,
                DisplayName = f.DisplayName
            }).ToList(),

            KidMilkDiet = await BuildKidMilkDietViewAsync(herd, cancellationToken)

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

        if (TryParseFeedGroupId(model.StatusKey, out var groupId))
        {
            var group = await _context.CustomFeedingGroups.FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken);
            if (group is null) return;
            group.MixKgPerDay = model.MixKgPerDay;
            group.FodderKgPerDay = model.FodderKgPerDay;
            group.FodderDryKgPerDay = model.FodderDryKgPerDay;
            if (!string.IsNullOrWhiteSpace(model.RecipeFrom))
                group.RecipeFrom = model.RecipeFrom.Trim();
            group.UpdatedDate = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            return;
        }

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
        var feedCatalog = await GetFeedCatalogAsync(cancellationToken);
        var mixFeedKeys = MixRecipeHelper.MixFeedKeys(
            feedCatalog.Where(f => !FeedTypes.IsDirectUse(f.UseType)).Select(f => f.FeedType)).ToList();
        var herd = await LoadHerdSnapshotAsync(cancellationToken);
        var customGroups = await _context.CustomFeedingGroups.AsNoTracking().ToListAsync(cancellationToken);
        var directPlans = await LoadDirectPlansAsync(cancellationToken);

        var dailyUse = BuildDailyUseMap(plans, allRecipes, mixFeedKeys, herd, customGroups, feedCatalog, directPlans)

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

                lines.Add($"{System.Net.WebUtility.HtmlEncode(price.DisplayName)} {(diff > 0 ? "+" : "")}{diff:F1} kg");

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

        await EnsurePurchasableFeedAsync(model.FeedType, cancellationToken);
        var mapped = MapPurchasePayment(model);
        var entity = new FeedPurchase
        {
            Date = model.Date,
            FeedType = model.FeedType,
            Kg = model.Kg,
            RatePerKg = model.RatePerKg,
            Amount = mapped.Amount,
            Comment = string.IsNullOrWhiteSpace(model.Comment) ? null : model.Comment.Trim(),
            UseAs = mapped.UseAs,
            PaymentStatus = mapped.Status,
            PaidAmount = mapped.Paid,
            AccountId = mapped.AccountId,
            Supplier = mapped.Supplier,
            DueDate = model.DueDate,
            AllocationMonths = mapped.AllocMonths
        };

        _context.FeedPurchases.Add(entity);
        await AdjustFeedStockAsync(model.FeedType, model.Kg, setFullLevel: true, cancellationToken);
        await ApplyFeedUseTypeAsync(model.FeedType, mapped.UseAs, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await PostPurchaseMoneyAsync(entity, cancellationToken);
        await RefreshPurchaseRateAsync(model.FeedType, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return await ToPurchaseVmAsync(entity, cancellationToken);

    }



    private async Task EnsurePurchasableFeedAsync(string feedType, CancellationToken cancellationToken)
    {
        if (!FeedTypes.IsPurchasable(feedType) ||
            !await _context.FeedPrices.AnyAsync(p => p.FeedType == feedType, cancellationToken))
            throw new InvalidOperationException("Pick a feed from the list.");
    }

    public async Task<FeedPurchaseViewModel?> UpdateFeedPurchaseAsync(int id, CreateFeedPurchaseViewModel model, CancellationToken cancellationToken = default)

    {

        var entity = await _context.FeedPurchases.FindAsync([id], cancellationToken);

        if (entity is null) return null;
        await EnsurePurchasableFeedAsync(model.FeedType, cancellationToken);



        var oldKg = entity.Kg;

        var oldFeedType = entity.FeedType;
        var mapped = MapPurchasePayment(model);

        entity.Date = model.Date;

        entity.FeedType = model.FeedType;

        entity.Kg = model.Kg;

        entity.RatePerKg = model.RatePerKg;

        entity.Amount = mapped.Amount;

        entity.Comment = string.IsNullOrWhiteSpace(model.Comment) ? null : model.Comment.Trim();
        entity.UseAs = mapped.UseAs;
        entity.PaymentStatus = mapped.Status;
        entity.PaidAmount = mapped.Paid;
        entity.AccountId = mapped.AccountId;
        entity.Supplier = mapped.Supplier;
        entity.DueDate = model.DueDate;
        entity.AllocationMonths = mapped.AllocMonths;

        entity.UpdatedDate = DateTime.UtcNow;



        if (oldFeedType == model.FeedType)

            await AdjustFeedStockAsync(model.FeedType, model.Kg - oldKg, setFullLevel: model.Kg > oldKg, cancellationToken);

        else

        {

            await AdjustFeedStockAsync(oldFeedType, -oldKg, cancellationToken: cancellationToken);

            await AdjustFeedStockAsync(model.FeedType, model.Kg, setFullLevel: true, cancellationToken);

        }



        await ApplyFeedUseTypeAsync(model.FeedType, mapped.UseAs, cancellationToken);
        await ReversePurchaseMoneyAsync(entity.Id, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await PostPurchaseMoneyAsync(entity, cancellationToken);
        await RefreshPurchaseRateAsync(oldFeedType, cancellationToken);
        if (!string.Equals(oldFeedType, model.FeedType, StringComparison.OrdinalIgnoreCase))
            await RefreshPurchaseRateAsync(model.FeedType, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return await ToPurchaseVmAsync(entity, cancellationToken);

    }



    public async Task<bool> DeleteFeedPurchaseAsync(int id, CancellationToken cancellationToken = default)

    {

        var entity = await _context.FeedPurchases.FindAsync([id], cancellationToken);

        if (entity is null) return false;

        var feedType = entity.FeedType;
        await AdjustFeedStockAsync(entity.FeedType, -entity.Kg, cancellationToken: cancellationToken);
        await ReversePurchaseMoneyAsync(entity.Id, cancellationToken);
        _context.FeedPurchases.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        await RefreshPurchaseRateAsync(feedType, cancellationToken);
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



        var useType = FeedTypes.IsDirectUse(model.UseType) ? FeedTypes.UseDirect : FeedTypes.UseRecipe;
        var price = new FeedPrice
        {
            FeedType = feedType,
            DisplayName = displayName,
            PricePerKg = model.PricePerKg,
            StockKg = 0,
            StockKgFull = 0,
            UseType = useType
        };

        _context.FeedPrices.Add(price);



        if (FeedTypes.IsMixIngredient(feedType) && useType != FeedTypes.UseDirect)

        {

            var allRecipes = await GetAllMixRecipesAsync(cancellationToken);

            foreach (var recipe in allRecipes.Values)

                recipe[feedType] = 0;

            await SaveAllMixRecipesAsync(allRecipes, cancellationToken);

        }



        await _context.SaveChangesAsync(cancellationToken);

        return new FeedPriceViewModel { FeedType = feedType, DisplayName = displayName, PricePerKg = model.PricePerKg, StockKg = 0, UseType = useType };

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

        var plan = Enum.TryParse<GoatStatus>(statusKey, ignoreCase: true, out var planStatus)
            ? _context.FeedPlans.AsNoTracking().FirstOrDefault(p => p.StatusKey == planStatus)
            : null;

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
                {
                    var dict = new Dictionary<string, Dictionary<string, decimal>>(parsed, StringComparer.OrdinalIgnoreCase);
                    if (!dict.ContainsKey(FeedTypes.KidStarter))
                        dict[FeedTypes.KidStarter] = new Dictionary<string, decimal>(MixRecipeHelper.DefaultKidStarterRecipe, StringComparer.OrdinalIgnoreCase);
                    return dict;
                }

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

        if (!result.ContainsKey(FeedTypes.KidStarter))
            result[FeedTypes.KidStarter] = new Dictionary<string, decimal>(MixRecipeHelper.DefaultKidStarterRecipe, StringComparer.OrdinalIgnoreCase);

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

            ? $"<span class=\"breed\">Stock reduces automatically each day from your feed plan · up to date to <b>{System.Net.WebUtility.HtmlEncode(dto.LastUsageDate)}</b></span>"

            : "<span class=\"breed\">Stock will start reducing automatically from tomorrow.</span>"

    };



    private MixRecipeStatusViewModel BuildMixRecipeStatusViewModel(

        string statusKey,

        IReadOnlyDictionary<string, Dictionary<string, decimal>> allRecipes,

        IReadOnlyList<string> mixFeedKeys,

        IReadOnlyList<FeedPrice> feedCatalog,

        IReadOnlyDictionary<string, decimal> prices)

    {

        var text = DisplayHelper.FeedRecipeLabel(statusKey);

        var recipe = allRecipes.GetValueOrDefault(statusKey)

            ?? (string.Equals(statusKey, FeedTypes.KidStarter, StringComparison.OrdinalIgnoreCase)
                ? new Dictionary<string, decimal>(MixRecipeHelper.DefaultKidStarterRecipe, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, decimal>(MixRecipeHelper.DefaultRecipe, StringComparer.OrdinalIgnoreCase));

        var items = BuildMixRecipeItems(recipe, mixFeedKeys, feedCatalog, prices);

        var totalKg = MixRecipeHelper.MixTotalKg(recipe, mixFeedKeys);

        var batchCost = MixRecipeHelper.MixBatchCost(recipe, prices, mixFeedKeys);

        var costPerKg = MixRecipeHelper.MixCostPerKg(recipe, prices, mixFeedKeys);



        var compareKeys = MixRecipeHelper.RecipeOptions.Select(o => o.Key).ToList();
        var compare = compareKeys.Select(key =>
        {
            var r = allRecipes.GetValueOrDefault(key) ?? recipe;
            return new MixRecipeCostCompareViewModel
            {
                StatusKey = key,
                StatusDisplay = DisplayHelper.FeedRecipeLabel(key),
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
        IReadOnlyList<string> mixFeedKeys,
        IReadOnlyList<HerdSnap> herd,
        IReadOnlyList<CustomFeedingGroup> customGroups,
        IReadOnlyList<FeedPrice> catalog,
        Dictionary<string, Dictionary<string, decimal>> directPlans)
    {
        var kg = mixFeedKeys.ToDictionary(k => k, _ => 0m, StringComparer.OrdinalIgnoreCase);
        kg[FeedTypes.Fodder] = 0;
        kg[FeedTypes.FodderDry] = 0;
        kg[FeedTypes.MilkPowder] = 0;
        foreach (var f in catalog.Where(x => FeedTypes.IsDirectUse(x.UseType)))
            kg[f.FeedType] = kg.GetValueOrDefault(f.FeedType);

        void AddGroup(string groupKey, string recipeKey, decimal mixKg, decimal fodderKg, decimal dryKg, int n)
        {
            if (n <= 0) return;
            var recipe = allRecipes.GetValueOrDefault(recipeKey) ?? new Dictionary<string, decimal>();
            var mixKgDay = mixKg * n;
            var total = MixRecipeHelper.MixTotalKg(recipe, mixFeedKeys);
            if (total > 0)
            {
                foreach (var k in mixFeedKeys)
                    kg[k] = kg.GetValueOrDefault(k) + mixKgDay * (recipe.GetValueOrDefault(k) / total);
            }
            kg[FeedTypes.Fodder] = kg.GetValueOrDefault(FeedTypes.Fodder) + fodderKg * n;
            kg[FeedTypes.FodderDry] = kg.GetValueOrDefault(FeedTypes.FodderDry) + dryKg * n;
            foreach (var f in catalog.Where(x => FeedTypes.IsDirectUse(x.UseType)))
            {
                var qty = DirectQty(groupKey, f.FeedType, directPlans);
                if (qty > 0)
                    kg[f.FeedType] = kg.GetValueOrDefault(f.FeedType) + qty * n;
            }
        }

        foreach (var st in StatusOrder)
        {
            var n = CountFeedPlan(herd, st);
            var plan = plans.FirstOrDefault(p => p.StatusKey == st);
            if (plan is null || n == 0) continue;
            var stKey = DisplayHelper.StatusKey(st);
            AddGroup(stKey, stKey, plan.MixKgPerDay, plan.FodderKgPerDay, plan.FodderDryKgPerDay, n);
        }

        foreach (var fg in customGroups)
        {
            var n = CountCustomGroup(herd, fg.Id);
            var recipeKey = string.IsNullOrWhiteSpace(fg.RecipeFrom) ? "milking" : fg.RecipeFrom;
            AddGroup(FeedGroupKey(fg.Id), recipeKey, fg.MixKgPerDay, fg.FodderKgPerDay, fg.FodderDryKgPerDay, n);
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

                IsLowStock = isLow,

                AvgCostPerKg = prices.GetValueOrDefault(f.FeedType),

                StatusText = days is null ? "No plan" : days < 7 ? "Order now" : days < 14 ? "Watch" : "Good",

                StatusCssClass = days is null ? "" : days < 7 ? "chip-exp" : days < 14 ? "chip-warn" : "chip-milk"

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
        IReadOnlyList<string> mixFeedKeys,
        IReadOnlyList<HerdSnap> herd,
        IReadOnlyList<CustomFeedingGroup> customGroups,
        IReadOnlyList<FeedPrice> catalog,
        Dictionary<string, Dictionary<string, decimal>> directPlans)
    {
        var daily = BuildDailyUseMap(plans, allRecipes, mixFeedKeys, herd, customGroups, catalog, directPlans);
        var nameByType = catalog.ToDictionary(f => f.FeedType, f => f.DisplayName, StringComparer.OrdinalIgnoreCase);
        var rows = new List<FeedBuyingRowViewModel>();

        void AddRow(string key, string fallback, bool ownLand)
        {
            var d = daily.GetValueOrDefault(key);
            if (d <= 0) return;
            var m = d * 30;
            rows.Add(new FeedBuyingRowViewModel
            {
                DisplayName = nameByType.GetValueOrDefault(key, fallback),
                KgPerDay = d,
                KgPerMonth = m,
                CostPerMonth = ownLand ? 0 : m * prices.GetValueOrDefault(key),
                IsOwnLand = ownLand
            });
        }

        foreach (var k in mixFeedKeys)
            AddRow(k, k, false);
        foreach (var f in catalog.Where(x => FeedTypes.IsDirectUse(x.UseType)))
            AddRow(f.FeedType, f.DisplayName, false);
        AddRow(FeedTypes.FodderDry, "Dry fodder (toori / bhoosa)", false);
        AddRow(FeedTypes.MilkPowder, "Powder milk (kids)", false);
        AddRow(FeedTypes.Fodder, "Green fodder (chaara)", true);
        return rows;
    }

    public async Task<object> AddFeedingGroupAsync(AddFeedingGroupViewModel model, CancellationToken cancellationToken = default)
    {
        var name = (model.Name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Enter a group name.");
        if (await _context.CustomFeedingGroups.AnyAsync(g => g.Name.ToLower() == name.ToLower(), cancellationToken))
            throw new InvalidOperationException("A feeding group called \"" + name + "\" already exists.");

        var recipeFrom = string.IsNullOrWhiteSpace(model.RecipeFrom) ? "milking" : model.RecipeFrom.Trim();
        var copyStatus = DisplayHelper.ParseStatusKey(recipeFrom);
        var src = await _context.FeedPlans.AsNoTracking().FirstOrDefaultAsync(p => p.StatusKey == copyStatus, cancellationToken);
        var group = new CustomFeedingGroup
        {
            Name = name,
            RecipeFrom = recipeFrom,
            MixKgPerDay = src?.MixKgPerDay ?? 0,
            FodderKgPerDay = src?.FodderKgPerDay ?? 0,
            FodderDryKgPerDay = src?.FodderDryKgPerDay ?? 0
        };
        _context.CustomFeedingGroups.Add(group);
        await _context.SaveChangesAsync(cancellationToken);
        return new { id = group.Id, key = FeedGroupKey(group.Id), name = group.Name };
    }

    public async Task<bool> DeleteFeedingGroupAsync(int id, CancellationToken cancellationToken = default)
    {
        var group = await _context.CustomFeedingGroups.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (group is null) return false;
        var goats = await _context.Goats.Where(g => g.CustomFeedingGroupId == id).ToListAsync(cancellationToken);
        foreach (var g in goats)
            g.CustomFeedingGroupId = null;
        var directs = await LoadDirectPlansAsync(cancellationToken);
        directs.Remove(FeedGroupKey(id));
        await SaveDirectPlansAsync(directs, cancellationToken);
        _context.CustomFeedingGroups.Remove(group);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task UpdateDirectPlanAsync(UpdateDirectPlanViewModel model, CancellationToken cancellationToken = default)
    {
        var directs = await LoadDirectPlansAsync(cancellationToken);
        if (!directs.TryGetValue(model.GroupKey, out var map))
        {
            map = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            directs[model.GroupKey] = map;
        }
        map[model.FeedType] = Math.Max(0, model.KgPerGoatPerDay);
        await SaveDirectPlansAsync(directs, cancellationToken);
    }

    public async Task<KidMilkDietViewModel> GetKidMilkDietAsync(CancellationToken cancellationToken = default)
    {
        await CatchUpKidMilkAsync(cancellationToken);
        var herd = await LoadHerdSnapshotAsync(cancellationToken);
        return await BuildKidMilkDietViewAsync(herd, cancellationToken);
    }

    public async Task<KidMilkDietViewModel> SaveKidMilkDietAsync(SaveKidMilkDietViewModel model, CancellationToken cancellationToken = default)
    {
        var state = await LoadKidMilkDietAsync(cancellationToken);
        state.Months = KidMilkDietHelper.Normalize(new KidMilkDietViewModel { Months = model.Months }).Months;
        if (string.IsNullOrWhiteSpace(state.AutoStartDate))
            state.AutoStartDate = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");
        await SaveKidMilkDietStateAsync(state, cancellationToken);
        await CatchUpKidMilkAsync(cancellationToken);
        var herd = await LoadHerdSnapshotAsync(cancellationToken);
        return await BuildKidMilkDietViewAsync(herd, cancellationToken);
    }

    private static string GenerateFeedKey(string name)
    {
        var k = new string(name.ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '_')
            .ToArray());
        k = string.Join('_', k.Split('_', StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(k) ? "feed" : k;
    }

    private sealed record HerdSnap(
        int Id, GoatStatus Status, DateOnly? WeanDate, YoungGoatClass YoungClass,
        int? CustomFeedingGroupId, DateOnly EventDate, DateOnly? DateOfBirth, GoatSource Source);

    private async Task<List<HerdSnap>> LoadHerdSnapshotAsync(CancellationToken cancellationToken) =>
        await _context.Goats.AsNoTracking()
            .Where(g => !g.IsArchived)
            .Select(g => new HerdSnap(g.Id, g.Status, g.WeanDate, g.YoungClass, g.CustomFeedingGroupId, g.EventDate, g.DateOfBirth, g.Source))
            .ToListAsync(cancellationToken);

    private static bool OnMilkDiet(HerdSnap g) => g.Status == GoatStatus.Kid && g.WeanDate is null;
    private static bool InCustomGroup(HerdSnap g) => g.CustomFeedingGroupId is > 0 && !OnMilkDiet(g);
    private static string FeedGroupKey(int id) => "fg:" + id;
    private static bool TryParseFeedGroupId(string? key, out int id)
    {
        id = 0;
        if (string.IsNullOrWhiteSpace(key)) return false;
        if (key.StartsWith("fg:", StringComparison.OrdinalIgnoreCase))
            return int.TryParse(key[3..], out id);
        return false;
    }

    private static int CountFeedPlan(IReadOnlyList<HerdSnap> herd, GoatStatus st)
    {
        return st switch
        {
            GoatStatus.Kid => herd.Count(g => !InCustomGroup(g) && g.Status == GoatStatus.Kid && g.WeanDate is not null && g.YoungClass != YoungGoatClass.Meat),
            GoatStatus.Meat => herd.Count(g => !InCustomGroup(g) && (g.Status == GoatStatus.Meat || (g.Status == GoatStatus.Kid && g.YoungClass == YoungGoatClass.Meat))),
            _ => herd.Count(g => !InCustomGroup(g) && g.Status == st)
        };
    }

    private static int CountCustomGroup(IReadOnlyList<HerdSnap> herd, int groupId) =>
        herd.Count(g => g.CustomFeedingGroupId == groupId && !OnMilkDiet(g));

    private static int FeedPlanHeadcount(IReadOnlyList<HerdSnap> herd, IReadOnlyList<CustomFeedingGroup> groups) =>
        StatusOrder.Sum(st => CountFeedPlan(herd, st)) + groups.Sum(g => CountCustomGroup(herd, g.Id));

    private static decimal DirectQty(string groupKey, string feedType, Dictionary<string, Dictionary<string, decimal>> plans)
    {
        if (!plans.TryGetValue(groupKey, out var map)) return 0;
        return map.GetValueOrDefault(feedType);
    }

    private static IReadOnlyDictionary<string, decimal> DirectKgFor(
        string groupKey, IReadOnlyList<FeedPrice> catalog, Dictionary<string, Dictionary<string, decimal>> plans) =>
        catalog.Where(f => FeedTypes.IsDirectUse(f.UseType))
            .ToDictionary(f => f.FeedType, f => DirectQty(groupKey, f.FeedType, plans), StringComparer.OrdinalIgnoreCase);

    private static decimal DirectCostPerGoat(
        string groupKey, IReadOnlyList<FeedPrice> catalog, IReadOnlyDictionary<string, decimal> prices,
        Dictionary<string, Dictionary<string, decimal>> plans) =>
        catalog.Where(f => FeedTypes.IsDirectUse(f.UseType))
            .Sum(f => DirectQty(groupKey, f.FeedType, plans) * prices.GetValueOrDefault(f.FeedType));

    private async Task<Dictionary<string, Dictionary<string, decimal>>> LoadDirectPlansAsync(CancellationToken cancellationToken)
    {
        var setting = await _context.AppSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == AppSettingKeys.FeedDirectPlans, cancellationToken);
        if (setting is null || string.IsNullOrWhiteSpace(setting.Value))
            return new Dictionary<string, Dictionary<string, decimal>>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, decimal>>>(setting.Value);
            return parsed is null
                ? new Dictionary<string, Dictionary<string, decimal>>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, Dictionary<string, decimal>>(parsed, StringComparer.OrdinalIgnoreCase);
        }
        catch { return new Dictionary<string, Dictionary<string, decimal>>(StringComparer.OrdinalIgnoreCase); }
    }

    private async Task SaveDirectPlansAsync(Dictionary<string, Dictionary<string, decimal>> plans, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(plans);
        var setting = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == AppSettingKeys.FeedDirectPlans, cancellationToken);
        if (setting is null)
            _context.AppSettings.Add(new AppSetting { Key = AppSettingKeys.FeedDirectPlans, Value = json });
        else
        {
            setting.Value = json;
            setting.UpdatedDate = DateTime.UtcNow;
        }
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureMilkPowderAsync(CancellationToken cancellationToken)
    {
        if (await _context.FeedPrices.AnyAsync(p => p.FeedType == FeedTypes.MilkPowder, cancellationToken))
            return;
        _context.FeedPrices.Add(new FeedPrice
        {
            FeedType = FeedTypes.MilkPowder,
            DisplayName = "Powder milk (kids)",
            PricePerKg = 0,
            StockKg = 0,
            StockKgFull = 0,
            UseType = FeedTypes.UseRecipe
        });
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<FeedMoneyAccountViewModel>> LoadMoneyAccountsAsync(CancellationToken cancellationToken)
    {
        var accounts = await _context.MoneyAccounts.AsNoTracking().Where(a => a.IsActive).OrderBy(a => a.Name).ToListAsync(cancellationToken);
        var txs = await _context.LedgerTransactions.AsNoTracking().ToListAsync(cancellationToken);
        var map = accounts.ToDictionary(a => a.Id, a => a.OpeningBalance);
        foreach (var t in txs)
        {
            if (t.AccountId is null || !map.ContainsKey(t.AccountId.Value)) continue;
            map[t.AccountId.Value] += t.Kind switch
            {
                LedgerTransactionKind.Income or LedgerTransactionKind.InvoicePayment => t.Amount,
                LedgerTransactionKind.Expense or LedgerTransactionKind.PayablePayment => -t.Amount,
                LedgerTransactionKind.Transfer => t.Amount,
                _ => 0
            };
        }
        return accounts.Select(a => new FeedMoneyAccountViewModel
        {
            Id = a.Id,
            Name = a.Name,
            Kind = a.Kind.ToString(),
            Balance = map.GetValueOrDefault(a.Id)
        }).ToList();
    }

    private static FeedPurchaseViewModel MapPurchase(
        FeedPurchase p, IReadOnlyDictionary<string, string> nameMap, IReadOnlyDictionary<int, string> acctName) =>
        new()
        {
            Id = p.Id,
            Date = p.Date,
            FeedType = p.FeedType,
            FeedDisplayName = nameMap.GetValueOrDefault(p.FeedType, p.FeedType),
            Kg = p.Kg,
            RatePerKg = p.RatePerKg,
            Amount = p.Amount,
            Comment = p.Comment,
            UseAs = string.IsNullOrWhiteSpace(p.UseAs) ? FeedTypes.UseRecipe : p.UseAs,
            PaymentStatus = string.IsNullOrWhiteSpace(p.PaymentStatus) ? "paid" : p.PaymentStatus,
            PaidAmount = p.PaidAmount,
            AccountId = p.AccountId,
            AccountName = p.AccountId is > 0 && acctName.TryGetValue(p.AccountId.Value, out var n) ? n : ((p.PaidAmount > 0) ? "Account not recorded" : "—"),
            Supplier = p.Supplier,
            DueDate = p.DueDate,
            AllocationMonths = p.AllocationMonths <= 0 ? 1 : p.AllocationMonths
        };

    private async Task<FeedPurchaseViewModel> ToPurchaseVmAsync(FeedPurchase entity, CancellationToken cancellationToken)
    {
        var displayName = await _context.FeedPrices.AsNoTracking()
            .Where(p => p.FeedType == entity.FeedType)
            .Select(p => p.DisplayName)
            .FirstOrDefaultAsync(cancellationToken) ?? entity.FeedType;
        var acctName = entity.AccountId is > 0
            ? await _context.MoneyAccounts.AsNoTracking().Where(a => a.Id == entity.AccountId).Select(a => a.Name).FirstOrDefaultAsync(cancellationToken)
            : null;
        return MapPurchase(entity, new Dictionary<string, string> { [entity.FeedType] = displayName },
            entity.AccountId is > 0 && acctName is not null ? new Dictionary<int, string> { [entity.AccountId.Value] = acctName } : new Dictionary<int, string>());
    }

    private (decimal Amount, decimal Paid, string Status, string UseAs, int? AccountId, string? Supplier, int AllocMonths)
        MapPurchasePayment(CreateFeedPurchaseViewModel model)
    {
        var amount = Math.Round(model.Kg * model.RatePerKg);
        var status = (model.PaymentStatus ?? "paid").Trim().ToLowerInvariant();
        if (status is not "paid" and not "partial" and not "credit") status = "paid";
        var paid = status == "paid" ? amount : status == "credit" ? 0 : Math.Clamp(model.PaidAmount, 0, amount);
        if (paid >= amount) status = "paid";
        else if (paid > 0) status = "partial";
        else status = "credit";
        if (paid > 0 && model.AccountId is not > 0)
            throw new InvalidOperationException("Select Paid from: Cash in Hand or a Bank account.");
        var useAs = FeedTypes.IsDirectUse(model.UseAs) ? FeedTypes.UseDirect : FeedTypes.UseRecipe;
        var supplier = string.IsNullOrWhiteSpace(model.Supplier) ? "Feed supplier" : model.Supplier.Trim();
        var alloc = Math.Clamp(model.AllocationMonths <= 0 ? 1 : model.AllocationMonths, 1, 12);
        return (amount, paid, status, useAs, paid > 0 ? model.AccountId : null, supplier, alloc);
    }

    private async Task ApplyFeedUseTypeAsync(string feedType, string useAs, CancellationToken cancellationToken)
    {
        if (FeedTypes.IsGreenFodder(feedType) || FeedTypes.IsMilkPowder(feedType) ||
            string.Equals(feedType, FeedTypes.FodderDry, StringComparison.OrdinalIgnoreCase))
            return;
        var price = await _context.FeedPrices.FirstOrDefaultAsync(p => p.FeedType == feedType, cancellationToken);
        if (price is null) return;
        var want = FeedTypes.IsDirectUse(useAs) ? FeedTypes.UseDirect : FeedTypes.UseRecipe;
        if (string.Equals(price.UseType, want, StringComparison.OrdinalIgnoreCase)) return;
        price.UseType = want;
        price.UpdatedDate = DateTime.UtcNow;
        if (want == FeedTypes.UseDirect)
        {
            var recipes = await GetAllMixRecipesAsync(cancellationToken);
            foreach (var recipe in recipes.Values)
                recipe.Remove(feedType);
            await SaveAllMixRecipesAsync(recipes, cancellationToken);
        }
        else
        {
            var recipes = await GetAllMixRecipesAsync(cancellationToken);
            foreach (var recipe in recipes.Values)
                if (!recipe.ContainsKey(feedType)) recipe[feedType] = 0;
            await SaveAllMixRecipesAsync(recipes, cancellationToken);
        }
    }

    private async Task RefreshPurchaseRateAsync(string feedType, CancellationToken cancellationToken)
    {
        var rows = await _context.FeedPurchases.AsNoTracking().Where(p => p.FeedType == feedType).ToListAsync(cancellationToken);
        var kg = rows.Sum(p => p.Kg);
        var amt = rows.Sum(p => p.Amount);
        if (kg <= 0) return;
        var price = await _context.FeedPrices.FirstOrDefaultAsync(p => p.FeedType == feedType, cancellationToken);
        if (price is null) return;
        price.PricePerKg = Math.Round(amt / kg, 2);
        price.UpdatedDate = DateTime.UtcNow;
    }

    private async Task ReversePurchaseMoneyAsync(int purchaseId, CancellationToken cancellationToken)
    {
        var key = "feed_purchase:" + purchaseId;
        var txs = await _context.LedgerTransactions.Where(t => t.Reference == key).ToListAsync(cancellationToken);
        _context.LedgerTransactions.RemoveRange(txs);
        var sid = purchaseId.ToString();
        var pays = await _context.SupplierPayables.Where(p => p.SourceType == "feed_purchase" && p.SourceId == sid).ToListAsync(cancellationToken);
        _context.SupplierPayables.RemoveRange(pays);
    }

    private async Task PostPurchaseMoneyAsync(FeedPurchase entity, CancellationToken cancellationToken)
    {
        if (entity.PaidAmount > 0 && entity.AccountId is > 0)
        {
            var acct = await _context.MoneyAccounts.FirstOrDefaultAsync(a => a.Id == entity.AccountId && a.IsActive, cancellationToken)
                ?? throw new InvalidOperationException("That payment account was not found. Set up accounts under Finance → Bank & Cash.");
            _context.LedgerTransactions.Add(new LedgerTransaction
            {
                Date = entity.Date,
                Kind = LedgerTransactionKind.Expense,
                AccountId = acct.Id,
                Amount = entity.PaidAmount,
                Category = "Feed purchase",
                Reference = "feed_purchase:" + entity.Id,
                Notes = $"Feed purchase · {entity.FeedType} · {entity.Supplier}"
            });
        }
        if (entity.PaidAmount < entity.Amount)
        {
            _context.SupplierPayables.Add(new SupplierPayable
            {
                SupplierName = entity.Supplier ?? "Feed supplier",
                Date = entity.Date,
                DueDate = entity.DueDate,
                Description = $"Feed purchase · {entity.FeedType} · {entity.Kg:0.##} kg",
                Amount = entity.Amount,
                PaidAmount = entity.PaidAmount,
                Status = entity.PaidAmount > 0 ? PayableStatus.Partial : PayableStatus.Open,
                SourceType = "feed_purchase",
                SourceId = entity.Id.ToString()
            });
        }
        await Task.CompletedTask;
    }

    private async Task<KidMilkDietViewModel> LoadKidMilkDietAsync(CancellationToken cancellationToken)
    {
        var setting = await _context.AppSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == AppSettingKeys.KidMilkDiet, cancellationToken);
        if (setting is null || string.IsNullOrWhiteSpace(setting.Value))
            return KidMilkDietHelper.Default();
        try
        {
            return KidMilkDietHelper.Normalize(JsonSerializer.Deserialize<KidMilkDietViewModel>(setting.Value));
        }
        catch { return KidMilkDietHelper.Default(); }
    }

    private async Task SaveKidMilkDietStateAsync(KidMilkDietViewModel state, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(state);
        var setting = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == AppSettingKeys.KidMilkDiet, cancellationToken);
        if (setting is null)
            _context.AppSettings.Add(new AppSetting { Key = AppSettingKeys.KidMilkDiet, Value = json });
        else
        {
            setting.Value = json;
            setting.UpdatedDate = DateTime.UtcNow;
        }
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<KidMilkDietViewModel> BuildKidMilkDietViewAsync(IReadOnlyList<HerdSnap> herd, CancellationToken cancellationToken)
    {
        var state = await LoadKidMilkDietAsync(cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var kids = herd.Where(OnMilkDiet).ToList();
        var months = state.Months.ToList();
        var counts = new[] { 0, 0, 0 };
        decimal powder = 0, mother = 0, mix = 0, green = 0, dry = 0;
        foreach (var g in kids)
        {
            var age = AgeDaysOn(g, today);
            var i = KidMilkDietHelper.BandForAge(age);
            var m = months[i];
            counts[i]++;
            powder += m.PowderG / 1000m;
            mother += m.MotherMilkMl / 1000m;
            mix += m.MixG / 1000m;
            green += m.GreenG / 1000m;
            dry += m.DryG / 1000m;
        }
        var powderStock = await _context.FeedPrices.AsNoTracking()
            .Where(p => p.FeedType == FeedTypes.MilkPowder)
            .Select(p => p.StockKg)
            .FirstOrDefaultAsync(cancellationToken);
        var starter = await GetMixRecipeForStatusAsync(FeedTypes.KidStarter, cancellationToken);
        return new KidMilkDietViewModel
        {
            Months = months,
            Logs = state.Logs.Take(15).ToList(),
            AutoStartDate = state.AutoStartDate,
            LastAutoDate = state.LastAutoDate,
            UnweanedKids = kids.Count,
            PowderTodayKg = powder,
            MotherTodayL = mother,
            MixTodayKg = mix,
            GreenTodayKg = green,
            DryTodayKg = dry,
            PowderStockKg = powderStock,
            Counts = counts,
            StarterRecipe = starter
        };
    }

    private static int AgeDaysOn(HerdSnap g, DateOnly date)
    {
        var goat = new Goat { Source = g.Source, EventDate = g.EventDate, DateOfBirth = g.DateOfBirth };
        var born = BreedingRules.BirthDate(goat) ?? g.EventDate;
        return Math.Max(0, date.DayNumber - born.DayNumber);
    }

    private async Task CatchUpKidMilkAsync(CancellationToken cancellationToken)
    {
        var state = await LoadKidMilkDietAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(state.AutoStartDate) || !DateOnly.TryParse(state.AutoStartDate, out var start))
            return;
        var today = DateOnly.FromDateTime(DateTime.Today);
        var last = DateOnly.TryParse(state.LastAutoDate, out var lastDate) ? lastDate : start.AddDays(-1);
        if (last >= today) return;

        var months = state.Months.ToList();
        var logs = state.Logs.ToList();
        var herdAll = await _context.Goats.AsNoTracking()
            .Where(g => g.Status == GoatStatus.Kid || g.WeanDate != null)
            .ToListAsync(cancellationToken);
        var recipes = await GetAllMixRecipesAsync(cancellationToken);
        var catalog = await GetFeedCatalogAsync(cancellationToken);
        var mixKeys = MixRecipeHelper.MixFeedKeys(catalog.Where(f => !FeedTypes.IsDirectUse(f.UseType)).Select(f => f.FeedType)).ToList();
        var starter = recipes.GetValueOrDefault(FeedTypes.KidStarter) ?? new Dictionary<string, decimal>(MixRecipeHelper.DefaultKidStarterRecipe);
        var starterTotal = MixRecipeHelper.MixTotalKg(starter, mixKeys);

        var day = last.AddDays(1);
        var applied = 0;
        while (day <= today && applied < 370)
        {
            var dateStr = day.ToString("yyyy-MM-dd");
            if (logs.Any(l => l.Date == dateStr && l.AutoDaily))
            {
                state.LastAutoDate = dateStr;
                day = day.AddDays(1);
                applied++;
                continue;
            }

            var counts = new[] { 0, 0, 0 };
            decimal powder = 0, mother = 0, mix = 0, green = 0, dry = 0;
            int kids = 0;
            foreach (var g in herdAll)
            {
                if (!EligibleKidOn(g, day)) continue;
                var goatSnap = new HerdSnap(g.Id, g.Status, g.WeanDate, g.YoungClass, g.CustomFeedingGroupId, g.EventDate, g.DateOfBirth, g.Source);
                var age = AgeDaysOn(goatSnap, day);
                var i = KidMilkDietHelper.BandForAge(age);
                var m = months[i];
                kids++;
                counts[i]++;
                powder += m.PowderG / 1000m;
                mother += m.MotherMilkMl / 1000m;
                mix += m.MixG / 1000m;
                green += m.GreenG / 1000m;
                dry += m.DryG / 1000m;
            }

            logs.Insert(0, new KidMilkLogViewModel
            {
                Date = dateStr, Kids = kids, Powder = powder, Mother = mother, Mix = mix, Green = green, Dry = dry,
                Counts = counts, AutoDaily = true
            });

            if (powder > 0)
                await RecordKidUsageAsync(day, FeedTypes.MilkPowder, powder, cancellationToken);
            if (dry > 0)
                await RecordKidUsageAsync(day, FeedTypes.FodderDry, dry, cancellationToken);
            if (mix > 0 && starterTotal > 0)
            {
                foreach (var k in mixKeys)
                {
                    var part = mix * (starter.GetValueOrDefault(k) / starterTotal);
                    if (part > 0) await RecordKidUsageAsync(day, k, part, cancellationToken);
                }
            }

            state.LastAutoDate = dateStr;
            day = day.AddDays(1);
            applied++;
        }

        state.Logs = logs.Take(90).ToList();
        await SaveKidMilkDietStateAsync(state, cancellationToken);
    }

    private static bool EligibleKidOn(Goat g, DateOnly date)
    {
        if (g.IsArchived && g.ArchivedDate is { } ad && ad <= date) return false;
        if (g.WeanDate is { } w && w <= date) return false;
        var born = BreedingRules.BirthDate(g) ?? g.EventDate;
        if (born > date) return false;
        return g.Status == GoatStatus.Kid || g.WeanDate is null;
    }

    private async Task RecordKidUsageAsync(DateOnly date, string feedType, decimal kg, CancellationToken cancellationToken)
    {
        _context.FeedUsageRecords.Add(new FeedUsageRecord
        {
            Date = date,
            FeedType = feedType,
            Kg = Math.Round(kg, 3),
            IsAutomatic = true
        });
        await AdjustFeedStockAsync(feedType, -kg, cancellationToken: cancellationToken);
    }
}




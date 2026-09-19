namespace GoatFarm.Application.ViewModels.Feed;

public class FeedPriceViewModel
{
    public string FeedType { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public decimal PricePerKg { get; set; }
    public decimal StockKg { get; set; }
}

public class MixRecipeItemViewModel
{
    public string FeedType { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public decimal KgInBatch { get; set; }
    public decimal BatchCost { get; set; }
    public int Percent { get; set; }
}

public class MixRecipeStatusViewModel
{
    public string StatusKey { get; set; } = string.Empty;
    public string StatusDisplay { get; set; } = string.Empty;
    public IReadOnlyList<MixRecipeItemViewModel> Items { get; set; } = [];
    public decimal TotalKg { get; set; }
    public decimal BatchCost { get; set; }
    public decimal CostPerKg { get; set; }
    public IReadOnlyList<MixRecipeCostCompareViewModel> AllStatusCosts { get; set; } = [];
}

public class MixRecipeCostCompareViewModel
{
    public string StatusKey { get; set; } = string.Empty;
    public string StatusDisplay { get; set; } = string.Empty;
    public decimal CostPerKg { get; set; }
    public bool IsActive { get; set; }
}

public class FeedGroupPlanRowViewModel
{
    public string StatusKey { get; set; } = string.Empty;
    public string StatusDisplay { get; set; } = string.Empty;
    public string StatusCssClass { get; set; } = string.Empty;
    public int GoatCount { get; set; }
    public decimal MixKgPerDay { get; set; }
    public decimal FodderKgPerDay { get; set; }
    public decimal FodderDryKgPerDay { get; set; }
    public decimal FodderCostPerGoatPerDay { get; set; }
    public decimal MixCostPerKg { get; set; }
    public decimal DailyCostPerGoat { get; set; }
    public decimal MedicineCostPerGoatPerMonth { get; set; }
    public decimal MonthlyTotal { get; set; }
}

public class FeedCategoryCostRowViewModel
{
    public string StatusKey { get; set; } = string.Empty;
    public string StatusDisplay { get; set; } = string.Empty;
    public string StatusCssClass { get; set; } = string.Empty;
    public int GoatCount { get; set; }
    public decimal MixKgPerDay { get; set; }
    public decimal DailyCost { get; set; }
    public decimal MonthlyCost { get; set; }
    public int SharePercent { get; set; }
    public decimal FodderKgPerDay { get; set; }
}

public class FeedPlanViewModel
{
    public string StatusKey { get; set; } = string.Empty;
    public string StatusDisplay { get; set; } = string.Empty;
    public decimal MixKgPerDay { get; set; }
    public decimal FodderKgPerDay { get; set; }
    public decimal MedicineCostPerGoatPerMonth { get; set; }
    public int GoatCount { get; set; }
    public decimal DailyFeedCost { get; set; }
    public decimal DailyTotalCost { get; set; }
    public decimal MonthlyTotalCost { get; set; }
}

public class FeedSummaryRowViewModel
{
    public string StatusKey { get; set; } = string.Empty;
    public string StatusDisplay { get; set; } = string.Empty;
    public string StatusCssClass { get; set; } = string.Empty;
    public int GoatCount { get; set; }
    public decimal FeedMonthly { get; set; }
    public decimal MedicineMonthly { get; set; }
    public decimal TotalMonthly { get; set; }
}

public class FeedBuyingRowViewModel
{
    public string DisplayName { get; set; } = string.Empty;
    public decimal KgPerDay { get; set; }
    public decimal KgPerMonth { get; set; }
    public decimal CostPerMonth { get; set; }
    public bool IsOwnLand { get; set; }
}

public class FeedStoreStatsViewModel
{
    public decimal TotalStockKg { get; set; }
    public decimal DailyUsageKg { get; set; }
    public decimal StockValue { get; set; }
    public int LowStockCount { get; set; }
}

public class FeedLowStockItemViewModel
{
    public string FeedType { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

public class FeedReorderNoteViewModel
{
    public string FeedType { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int DaysToMinimum { get; set; }
    public int DaysUntilEmpty { get; set; }
    public bool IsCritical { get; set; }
}

public class StockCheckRowViewModel
{
    public string FeedType { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public decimal BookKg { get; set; }
}

public class FodderPoolItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class FodderPoolViewModel
{
    public decimal Acres { get; set; }
    public string Mode { get; set; } = "share";
    public IReadOnlyList<FodderPoolItemViewModel> Items { get; set; } = [];
    public decimal YearlyTotal { get; set; }
    public decimal DailyTotal { get; set; }
    public decimal AveragePerGoatDay { get; set; }
    public decimal? CostPerKgGreen { get; set; }
    public decimal? CostPerAcreYear { get; set; }
    public string NoteHtml { get; set; } = string.Empty;
}

public class FeedSettingsViewModel
{
    public bool AutoUsage { get; set; } = true;
    public string? LastUsageDate { get; set; }
    public string AutoInfoHtml { get; set; } = string.Empty;
}

public class FeedPageViewModel
{
    public string ActiveSubTab { get; set; } = "store";
    public IReadOnlyList<FeedPriceViewModel> MixPrices { get; set; } = [];
    public IReadOnlyList<FeedPriceViewModel> AllPrices { get; set; } = [];
    public IReadOnlyList<MixRecipeItemViewModel> MixRecipe { get; set; } = [];
    public decimal MixTotalKg { get; set; }
    public decimal MixBatchCost { get; set; }
    public decimal MixCostPerKg { get; set; }
    public IReadOnlyList<FeedGroupPlanRowViewModel> GroupPlans { get; set; } = [];
    public IReadOnlyList<FeedCategoryCostRowViewModel> CategoryCosts { get; set; } = [];
    public decimal FodderKgPerDayTotal { get; set; }
    public decimal FodderDryKgPerDayTotal { get; set; }
    public decimal MixKgPerDayTotal { get; set; }
    public IReadOnlyList<FeedBuyingRowViewModel> BuyingList { get; set; } = [];
    public decimal BuyingListTotalKg { get; set; }
    public decimal BuyingListTotalCost { get; set; }
    public IReadOnlyList<FeedPurchaseViewModel> FeedPurchases { get; set; } = [];
    public decimal FeedBoughtMonthTotal { get; set; }
    public decimal FeedBoughtKgTotal { get; set; }
    public string FeedMonth { get; set; } = string.Empty;
    public decimal GrandMonthly { get; set; }
    public decimal GrandDaily { get; set; }
    public string GrandHeadText { get; set; } = string.Empty;
    public decimal? FeedCostPerLitre { get; set; }
    public string FeedCostPerLitreText { get; set; } = "— per litre of milk";
    public decimal MilkLitersThisMonth { get; set; }
    public int TotalGoats { get; set; }
    public IReadOnlyList<FeedStockRowViewModel> Stock { get; set; } = [];
    public FeedStoreStatsViewModel StoreStats { get; set; } = new();
    public IReadOnlyList<FeedLowStockItemViewModel> LowStockItems { get; set; } = [];
    public FeedReorderNoteViewModel? ReorderNote { get; set; }
    public IReadOnlyList<StockCheckRowViewModel> StockCheckRows { get; set; } = [];
    public FodderPoolViewModel FodderPool { get; set; } = new();
    public FeedSettingsViewModel FeedSettings { get; set; } = new();
    public string PlanFodderModeHtml { get; set; } = string.Empty;
    public string? ActiveRecipeStatusKey { get; set; }
    public MixRecipeStatusViewModel? ActiveRecipe { get; set; }
}

public class FeedStockRowViewModel
{
    public string FeedType { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public decimal StockKg { get; set; }
    public decimal StockKgFull { get; set; }
    public decimal PurchasedKg { get; set; }
    public decimal UsedKg { get; set; }
    public decimal KgPerDay { get; set; }
    public decimal? DaysLeft { get; set; }
    public string DaysLeftText { get; set; } = "—";
    public string DaysLeftColor { get; set; } = string.Empty;
    public decimal? StockPercent { get; set; }
    public bool IsLowStock { get; set; }
}

public class UpdateFeedStockViewModel
{
    public string FeedType { get; set; } = string.Empty;
    public decimal StockKg { get; set; }
}

public class FeedPurchaseViewModel
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public string DateDisplay => Date.ToString("yyyy-MM-dd");
    public string FeedType { get; set; } = string.Empty;
    public string FeedDisplayName { get; set; } = string.Empty;
    public decimal Kg { get; set; }
    public decimal RatePerKg { get; set; }
    public decimal Amount { get; set; }
    public string? Comment { get; set; }
}

public class CreateFeedPurchaseViewModel
{
    public DateOnly Date { get; set; }
    public string FeedType { get; set; } = string.Empty;
    public decimal Kg { get; set; }
    public decimal RatePerKg { get; set; }
    public string? Comment { get; set; }
}

public class AddFeedTypeViewModel
{
    public string DisplayName { get; set; } = string.Empty;
    public decimal PricePerKg { get; set; }
}

public class UpdateFeedPlanViewModel
{
    public string StatusKey { get; set; } = string.Empty;
    public decimal MixKgPerDay { get; set; }
    public decimal FodderKgPerDay { get; set; }
    public decimal FodderDryKgPerDay { get; set; }
    public decimal MedicineCostPerGoatPerMonth { get; set; }
}

public class UpdateMixRecipeViewModel
{
    public Dictionary<string, decimal> Recipe { get; set; } = new();
}

public class UpdateMixRecipeForStatusViewModel
{
    public string StatusKey { get; set; } = string.Empty;
    public Dictionary<string, decimal> Recipe { get; set; } = new();
}

public class UpdateFodderPoolViewModel
{
    public decimal Acres { get; set; }
    public string Mode { get; set; } = "share";
    public List<FodderPoolItemViewModel> Items { get; set; } = [];
}

public class AddFodderPoolItemViewModel
{
    public string Label { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class UpdateFeedSettingsViewModel
{
    public bool AutoUsage { get; set; } = true;
}

public class SaveStockCheckViewModel
{
    public Dictionary<string, decimal> Counts { get; set; } = new();
}

public class StockCheckResultViewModel
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? DetailHtml { get; set; }
    public decimal UnderageKg { get; set; }
}

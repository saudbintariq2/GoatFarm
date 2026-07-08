namespace GoatFarm.Application.ViewModels.Feed;

public class FeedPriceViewModel
{
    public string FeedType { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public decimal PricePerKg { get; set; }
}

public class FeedPlanItemViewModel
{
    public string FeedType { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int GramsPerDay { get; set; }
    public decimal DailyCost { get; set; }
}

public class FeedPlanViewModel
{
    public string StatusKey { get; set; } = string.Empty;
    public string StatusDisplay { get; set; } = string.Empty;
    public decimal MedicineCostPerGoatPerMonth { get; set; }
    public IReadOnlyList<FeedPlanItemViewModel> Items { get; set; } = [];
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
}

public class FeedPageViewModel
{
    public IReadOnlyList<FeedPriceViewModel> Prices { get; set; } = [];
    public FeedPlanViewModel CurrentPlan { get; set; } = new();
    public IReadOnlyList<FeedSummaryRowViewModel> Summary { get; set; } = [];
    public IReadOnlyList<FeedBuyingRowViewModel> BuyingList { get; set; } = [];
    public decimal GrandMonthly { get; set; }
    public decimal GrandDaily { get; set; }
    public int TotalGoats { get; set; }
    public string SelectedStatusKey { get; set; } = "kid";
    public IReadOnlyList<(string Key, string Label)> StatusOptions { get; set; } = [];
}

public class UpdateFeedPlanViewModel
{
    public string StatusKey { get; set; } = string.Empty;
    public decimal MedicineCostPerGoatPerMonth { get; set; }
    public Dictionary<string, int> Rations { get; set; } = new();
}

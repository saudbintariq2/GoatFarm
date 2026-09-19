namespace GoatFarm.Application.ViewModels.Reports;

public class ReportsPageViewModel
{
    public string Period { get; set; } = "month";
    public string GroupBy { get; set; } = "month";
    public string? CustomFrom { get; set; }
    public string? CustomTo { get; set; }
    public string RangeLabel { get; set; } = string.Empty;
    public int MonthCount { get; set; } = 1;
    public string NoteHtml { get; set; } = string.Empty;
    public bool PreRevenue { get; set; }
    public string TrendHeadLabel { get; set; } = "Month";
    public string TrendSubLabel { get; set; } = "by month";
    public string TrendLastColumnLabel { get; set; } = "Net";

    public ReportDashboardSectionViewModel Dashboard { get; set; } = new();
    public ReportHerdSectionViewModel Herd { get; set; } = new();
    public ReportBreedingSectionViewModel Breeding { get; set; } = new();
    public ReportGrowthSectionViewModel Growth { get; set; } = new();
    public ReportFeedSectionViewModel Feed { get; set; } = new();
    public ReportHealthSectionViewModel Health { get; set; } = new();
    public ReportFinanceSectionViewModel Finance { get; set; } = new();
    public ReportProfitabilitySectionViewModel Profitability { get; set; } = new();
    public ReportInventorySectionViewModel Inventory { get; set; } = new();
    public ReportAlertsSectionViewModel Alerts { get; set; } = new();
    public ReportComparisonSectionViewModel Comparison { get; set; } = new();
}

public class ReportKpiViewModel
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Sub { get; set; }
    public string? Color { get; set; }
    public string? DotColor { get; set; }
}

public class ReportDashboardSectionViewModel
{
    public IReadOnlyList<ReportKpiViewModel> Kpis { get; set; } = [];
    public IReadOnlyList<ReportKpiViewModel> Ratios { get; set; } = [];
    public IReadOnlyList<ReportAlertItemViewModel> Alerts { get; set; } = [];
    public IReadOnlyList<ReportTrendRowViewModel> TrendRows { get; set; } = [];
    public decimal TrendTotalIncome { get; set; }
    public decimal TrendTotalExpense { get; set; }
    public decimal TrendTotalOwner { get; set; }
    public decimal TrendTotalNet { get; set; }
}

public class ReportTrendRowViewModel
{
    public string Key { get; set; } = string.Empty;
    public decimal Income { get; set; }
    public decimal Expense { get; set; }
    public decimal OwnerInvestment { get; set; }
    public decimal Net { get; set; }
}

public class ReportHerdSectionViewModel
{
    public IReadOnlyList<ReportLabelCountRowViewModel> Movement { get; set; } = [];
    public IReadOnlyList<ReportHerdGroupRowViewModel> Composition { get; set; } = [];
    public decimal TotalHerdValue { get; set; }
    public IReadOnlyList<ReportKpiViewModel> AgeSexBreed { get; set; } = [];
    public IReadOnlyList<ReportHerdChangeRowViewModel> Changes { get; set; } = [];
}

public class ReportLabelCountRowViewModel
{
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
    public bool IsTotal { get; set; }
}

public class ReportHerdGroupRowViewModel
{
    public string StatusDisplay { get; set; } = string.Empty;
    public string StatusCssClass { get; set; } = string.Empty;
    public int Count { get; set; }
    public int SharePercent { get; set; }
    public decimal Value { get; set; }
    public bool IsTotal { get; set; }
}

public class ReportHerdChangeRowViewModel
{
    public string Date { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public string Breed { get; set; } = string.Empty;
    public string EventHtml { get; set; } = string.Empty;
    public string ValueDisplay { get; set; } = "—";
}

public class ReportBreedingSectionViewModel
{
    public IReadOnlyList<ReportKpiViewModel> Kpis { get; set; } = [];
    public string NoteHtml { get; set; } = string.Empty;
    public IReadOnlyList<ReportLitterRowViewModel> Litter { get; set; } = [];
    public int LitterTotalDoes { get; set; }
    public int LitterTotalKids { get; set; }
    public IReadOnlyList<ReportKiddingRowViewModel> KiddingCalendar { get; set; } = [];
    public IReadOnlyList<ReportEmptyScanRowViewModel> EmptyScans { get; set; } = [];
}

public class ReportLitterRowViewModel
{
    public string Label { get; set; } = string.Empty;
    public int Does { get; set; }
    public int BarPercent { get; set; }
    public int Kids { get; set; }
}

public class ReportKiddingRowViewModel
{
    public string Tag { get; set; } = string.Empty;
    public string MatedDate { get; set; } = string.Empty;
    public string KidsDisplay { get; set; } = string.Empty;
    public string DueDate { get; set; } = string.Empty;
    public string DueInText { get; set; } = string.Empty;
    public string DueInColor { get; set; } = string.Empty;
}

public class ReportEmptyScanRowViewModel
{
    public string Tag { get; set; } = string.Empty;
    public string MatedDate { get; set; } = string.Empty;
    public string BuckTag { get; set; } = string.Empty;
    public string ScanDate { get; set; } = string.Empty;
    public int Times { get; set; }
    public bool Highlight { get; set; }
}

public class ReportGrowthSectionViewModel
{
    public IReadOnlyList<ReportKpiViewModel> Kpis { get; set; } = [];
    public string NoteHtml { get; set; } = string.Empty;
    public IReadOnlyList<ReportWeightRowViewModel> Weights { get; set; } = [];
    public IReadOnlyList<string> UnderperformingTags { get; set; } = [];
}

public class ReportWeightRowViewModel
{
    public string Tag { get; set; } = string.Empty;
    public string AgeLabel { get; set; } = string.Empty;
    public decimal? FirstKg { get; set; }
    public decimal LatestKg { get; set; }
    public string DailyGainDisplay { get; set; } = "—";
    public bool Underperforming { get; set; }
    public int Readings { get; set; }
}

public class ReportFeedSectionViewModel
{
    public IReadOnlyList<ReportKpiViewModel> Kpis { get; set; } = [];
    public IReadOnlyList<ReportFeedStockRowViewModel> Stock { get; set; } = [];
    public IReadOnlyList<ReportFeedGroupRowViewModel> CostByGroup { get; set; } = [];
    public IReadOnlyList<ReportFeedPurchaseRowViewModel> Purchases { get; set; } = [];
    public decimal PurchasesTotalKg { get; set; }
    public decimal PurchasesTotalAmount { get; set; }
}

public class ReportFeedStockRowViewModel
{
    public string Name { get; set; } = string.Empty;
    public bool IsLow { get; set; }
    public decimal PurchasedKg { get; set; }
    public decimal ConsumedKg { get; set; }
    public decimal StockKg { get; set; }
    public decimal DailyUseKg { get; set; }
    public int? DaysLeft { get; set; }
    public decimal AvgCostPerKg { get; set; }
    public decimal StockValue { get; set; }
}

public class ReportFeedGroupRowViewModel
{
    public string StatusDisplay { get; set; } = string.Empty;
    public string StatusCssClass { get; set; } = string.Empty;
    public int GoatCount { get; set; }
    public decimal CostPerGoatDay { get; set; }
    public decimal CostPerMonth { get; set; }
    public int SharePercent { get; set; }
    public bool IsTotal { get; set; }
}

public class ReportFeedPurchaseRowViewModel
{
    public string FeedName { get; set; } = string.Empty;
    public decimal Kg { get; set; }
    public decimal AvgRate { get; set; }
    public decimal Amount { get; set; }
}

public class ReportHealthSectionViewModel
{
    public IReadOnlyList<ReportKpiViewModel> Kpis { get; set; } = [];
    public IReadOnlyList<ReportVaccineStatusRowViewModel> Vaccination { get; set; } = [];
    public IReadOnlyList<ReportDeathRowViewModel> Deaths { get; set; } = [];
    public decimal DeathsTotalValue { get; set; }
}

public class ReportVaccineStatusRowViewModel
{
    public string Name { get; set; } = string.Empty;
    public string RuleLabel { get; set; } = string.Empty;
    public string ScopeLabel { get; set; } = string.Empty;
    public int Done { get; set; }
    public int DueNow { get; set; }
    public int ComingUp { get; set; }
}

public class ReportDeathRowViewModel
{
    public string Date { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public string AgeLabel { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public decimal? ValueLost { get; set; }
}

public class ReportFinanceSectionViewModel
{
    public decimal TotalExpense { get; set; }
    public decimal TotalOwnerInvestment { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal Profit { get; set; }
    public string ProfitLabel { get; set; } = "Profit";
    public bool ProfitIsNegative { get; set; }
    public IReadOnlyList<ReportOwnerRowViewModel> OwnerRows { get; set; } = [];
    public IReadOnlyList<ReportCategoryRowViewModel> ExpenseCategories { get; set; } = [];
    public IReadOnlyList<ReportCategoryRowViewModel> IncomeSources { get; set; } = [];
    public IReadOnlyList<ReportExpenseEntryViewModel> ExpenseEntries { get; set; } = [];
    public IReadOnlyList<ReportDayRowViewModel> DayRows { get; set; } = [];
    public string DayNote { get; set; } = string.Empty;
}

public class ReportProfitabilitySectionViewModel
{
    public IReadOnlyList<ReportKpiViewModel> UnitEconomics { get; set; } = [];
    public string NoteHtml { get; set; } = string.Empty;
    public IReadOnlyList<ReportKpiViewModel> MilkEconomics { get; set; } = [];
    public IReadOnlyList<ReportMilkMonthRowViewModel> MilkByMonth { get; set; } = [];
}

public class ReportMilkMonthRowViewModel
{
    public string Month { get; set; } = string.Empty;
    public decimal CollectedLiters { get; set; }
    public decimal SoldLiters { get; set; }
    public decimal? AvgRate { get; set; }
    public decimal Income { get; set; }
}

public class ReportInventorySectionViewModel
{
    public IReadOnlyList<ReportKpiViewModel> Kpis { get; set; } = [];
    public IReadOnlyList<ReportInvFeedRowViewModel> FeedStock { get; set; } = [];
    public decimal FeedStockTotalValue { get; set; }
    public IReadOnlyList<ReportInvAssetRowViewModel> Assets { get; set; } = [];
    public decimal AssetsTotalValue { get; set; }
}

public class ReportInvFeedRowViewModel
{
    public string Name { get; set; } = string.Empty;
    public decimal QuantityKg { get; set; }
    public decimal RatePerKg { get; set; }
    public decimal Value { get; set; }
    public string StatusHtml { get; set; } = string.Empty;
}

public class ReportInvAssetRowViewModel
{
    public string Name { get; set; } = string.Empty;
    public string? Note { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal Value { get; set; }
}

public class ReportAlertsSectionViewModel
{
    public IReadOnlyList<ReportAlertItemViewModel> Items { get; set; } = [];
}

public class ReportAlertItemViewModel
{
    public string Color { get; set; } = string.Empty;
    public string Html { get; set; } = string.Empty;
}

public class ReportComparisonSectionViewModel
{
    public IReadOnlyList<ReportCompareRowViewModel> Rows { get; set; } = [];
}

public class ReportCompareRowViewModel
{
    public string Label { get; set; } = string.Empty;
    public string ThisMonth { get; set; } = string.Empty;
    public string LastMonth { get; set; } = string.Empty;
    public string Difference { get; set; } = string.Empty;
    public string DifferenceColor { get; set; } = string.Empty;
    public string? PercentChange { get; set; }
    public string YearToDate { get; set; } = string.Empty;
}

public class ReportOwnerRowViewModel
{
    public string Date { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class ReportCategoryRowViewModel
{
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int Percent { get; set; }
    public string? Extra { get; set; }
    public int BarWidth { get; set; }
}

public class ReportDayRowViewModel
{
    public string Date { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public decimal Income { get; set; }
    public decimal Expense { get; set; }
    public decimal OwnerInvestment { get; set; }
}

public class ReportExpenseEntryViewModel
{
    public string Date { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public decimal Amount { get; set; }
}

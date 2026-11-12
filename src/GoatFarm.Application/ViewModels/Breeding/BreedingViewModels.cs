using GoatFarm.Application.Common;
using GoatFarm.Domain.Enums;

namespace GoatFarm.Application.ViewModels.Breeding;

public class BreedingPrepRowViewModel
{
    public int Id { get; set; }
    public string Tag { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string StatusDisplay { get; set; } = string.Empty;
    public string StatusCssClass { get; set; } = string.Empty;
    public string PrepCrossDate { get; set; } = string.Empty;
    public string DietStartDate { get; set; } = string.Empty;
    public bool DietStartNow { get; set; }
    public string CrossInText { get; set; } = string.Empty;
}

public class BreedingExpectingRowViewModel
{
    public int Id { get; set; }
    public string Tag { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string MatedDate { get; set; } = string.Empty;
    public string? BuckTag { get; set; }
    public int? KidsCount { get; set; }
    public string? UltrasoundDate { get; set; }
    public string KidsDisplay { get; set; } = string.Empty;
    public bool ExtraFeed { get; set; }
    public string ExpectedKidding { get; set; } = string.Empty;
    public string KiddingWindow { get; set; } = string.Empty;
    public string DueText { get; set; } = string.Empty;
    public string DueColor { get; set; } = string.Empty;
}

public class BreedingWaitingRowViewModel
{
    public int Id { get; set; }
    public string Tag { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string MatedDate { get; set; } = string.Empty;
    public string? BuckTag { get; set; }
    public int DaysSinceCross { get; set; }
    public string ScanDueDate { get; set; } = string.Empty;
    public bool ScanDueNow { get; set; }
    public string StatusDisplay { get; set; } = string.Empty;
    public string StatusCssClass { get; set; } = string.Empty;
}

public class BreedingEmptyRowViewModel
{
    public int Id { get; set; }
    public string Tag { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? MatedDate { get; set; }
    public string? BuckTag { get; set; }
    public string ScanDate { get; set; } = string.Empty;
    public int TimesEmpty { get; set; }
    public string StatusDisplay { get; set; } = string.Empty;
    public string StatusCssClass { get; set; } = string.Empty;
}

public class BreedingRationRowViewModel
{
    public int Id { get; set; }
    public string Tag { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string Stage { get; set; } = string.Empty;
    public decimal MixKgPerDay { get; set; }
    public decimal FodderKgPerDay { get; set; }
    public decimal DailyCost { get; set; }
    public string Note { get; set; } = string.Empty;
}

public class BreedingDoeFlowStepViewModel
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
    public string? ArrowLabel { get; set; }
}

public class BreedingPipelineRowViewModel
{
    public int Id { get; set; }
    public string Tag { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Owner { get; set; }
    public string FlowStageKey { get; set; } = string.Empty;
    public string StageKey { get; set; } = string.Empty;
    public string StageLabel { get; set; } = string.Empty;
    public string StageCssClass { get; set; } = string.Empty;
    public string? MatedDate { get; set; }
    public string? BuckTag { get; set; }
    public string KidsHtml { get; set; } = string.Empty;
    public string KeyDateHtml { get; set; } = string.Empty;
    public string DaysText { get; set; } = string.Empty;
    public string DaysStyle { get; set; } = string.Empty;
    public int? KidsCount { get; set; }
}

public class BreedingNowJobViewModel
{
    public string Icon { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Tone { get; set; } = string.Empty;
    public IReadOnlyList<string> Tags { get; set; } = [];
    public string? FilterLink { get; set; }
    public DateOnly SortDate { get; set; }
    public string Subtext { get; set; } = string.Empty;
    /// <summary>no = due/overdue, wt = upcoming, ok = later season step.</summary>
    public string UrgencyKey { get; set; } = "wt";
    public string WhenLabel { get; set; } = string.Empty;
    public string WhenSubtext { get; set; } = string.Empty;
    public string ActionLabel { get; set; } = "Open";
    /// <summary>Breeding sub-view: does, plan, bucks, kidding.</summary>
    public string? BreedView { get; set; }
    public bool OpenScan { get; set; }
}

public class BreedingTodayTimelineMarkerViewModel
{
    public string Icon { get; set; } = string.Empty;
    public string DateText { get; set; } = string.Empty;
    public double LeftPercent { get; set; }
}

public class BreedingTodayTimelineLabelViewModel
{
    public string Icon { get; set; } = string.Empty;
    public string DateText { get; set; } = string.Empty;
    public double LeftPercent { get; set; }
    public bool AlignEnd { get; set; }
}

public class BreedingPlanTimelineSegmentViewModel
{
    public double LeftPercent { get; set; }
    public double WidthPercent { get; set; }
    public string Color { get; set; } = "#ddd";
    public string Title { get; set; } = string.Empty;
}

public class BreedingSeasonStepViewModel
{
    public string Icon { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string RuleHtml { get; set; } = string.Empty;
    public string WindowText { get; set; } = string.Empty;
    public DateOnly WhenDate { get; set; }
    public string DaysText { get; set; } = string.Empty;
    public string DaysStyle { get; set; } = string.Empty;
    public bool Anchor { get; set; }
}

public class BreedingSeasonPanelViewModel
{
    public int BuildUp { get; set; }
    public int ScanAfter { get; set; }
    public int DryOff { get; set; }
    public int WeanDays { get; set; }
    public decimal WeanKg { get; set; }
    public int DryBeforeCross { get; set; }
    public string SeasonSub { get; set; } = string.Empty;
    public string SelectedBatchId { get; set; } = string.Empty;
    public string BatchCountText { get; set; } = string.Empty;
    public IReadOnlyList<BreedingSeasonBatchViewModel> Batches { get; set; } = [];
    public IReadOnlyList<BreedingSeasonStepViewModel> Steps { get; set; } = [];
}

public class BreedingSeasonBatchViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = "⭐";
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public string BirthWindowText { get; set; } = string.Empty;
}

public class BreedingYearRowViewModel
{
    public string BatchId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = "⭐";
    public int DoeCount { get; set; }
    public string BirthWindowText { get; set; } = string.Empty;
    public string CrossText { get; set; } = string.Empty;
    public string ScanText { get; set; } = string.Empty;
    public string DryText { get; set; } = string.Empty;
    public string WeanText { get; set; } = string.Empty;
    public int BuckGroupCount { get; set; }
    public string BirthText { get; set; } = string.Empty;
    public string NextJobText { get; set; } = string.Empty;
    public string NextJobStyle { get; set; } = string.Empty;
    public bool Highlight { get; set; }
}

public class BreedingPlanRowViewModel
{
    public int Id { get; set; }
    public string Tag { get; set; } = string.Empty;
    public string StatusDisplay { get; set; } = string.Empty;
    public string StatusCssClass { get; set; } = string.Empty;
    public string BatchHtml { get; set; } = string.Empty;
    public string BcsHtml { get; set; } = string.Empty;
    public string StandText { get; set; } = string.Empty;
    public string StandColor { get; set; } = string.Empty;
}

public class BreedingHeatRowViewModel
{
    public int Id { get; set; }
    public string Tag { get; set; } = string.Empty;
    public string StatusDisplay { get; set; } = string.Empty;
    public string StatusCssClass { get; set; } = string.Empty;
    public string? CrossedNote { get; set; }
    public string LastSeenHtml { get; set; } = string.Empty;
    public string? NextExpected { get; set; }
    public string InText { get; set; } = string.Empty;
    public string InStyle { get; set; } = string.Empty;
    public int Times { get; set; }
}

public class BreedingGroupCandidateViewModel
{
    public int Id { get; set; }
    public string Tag { get; set; } = string.Empty;
    public string Breed { get; set; } = string.Empty;
    public bool Ready { get; set; }
}

public class BreedingBuckRowViewModel
{
    public int Id { get; set; }
    public string Tag { get; set; } = string.Empty;
    public int Carrying { get; set; }
    public int CrossesThisYear { get; set; }
    public string ConceptionText { get; set; } = "—";
    public string ConceptionStyle { get; set; } = string.Empty;
    public string? LastUsed { get; set; }
    public string LoadText { get; set; } = string.Empty;
    public string LoadColor { get; set; } = string.Empty;
    public int KidsSiredYear { get; set; }
    public int KidsSired { get; set; }
    public string KidsPerCrossText { get; set; } = "—";
    public string KidsPerKiddingText { get; set; } = "—";
    public string AvgBirthKgText { get; set; } = "—";
    public string Avg90DayKgText { get; set; } = "—";
    public int? ConceptionPercent { get; set; }
    public bool BuckCheckOk { get; set; }
    public string? LastCheckDate { get; set; }
    public string BuckCheckLineHtml { get; set; } = string.Empty;
}

public class RecordBuckCheckViewModel
{
    public int BuckId { get; set; }
    public DateOnly CheckDate { get; set; }
    public decimal? WeightKg { get; set; }
    public decimal? Bcs { get; set; }
    public string Feet { get; set; } = "good";
    public string Teeth { get; set; } = "good";
    public string Testicles { get; set; } = "normal";
    public string? Notes { get; set; }
    public bool ConfirmProblem { get; set; }
}

public class BreedingPageViewModel
{
    public int PrepCount { get; set; }
    public int GettingReadyCount { get; set; }
    public int WaitingCount { get; set; }
    public int ExpectingCount { get; set; }
    public int BuckCount { get; set; }
    public int ActiveBreedingGroupCount { get; set; }
    public string WeanedKgPerDoeText { get; set; } = "—";
    public string TodaySeasonName { get; set; } = string.Empty;
    public double TodayTimelinePercent { get; set; }
    public IReadOnlyList<BreedingTodayTimelineMarkerViewModel> TodayTimelineMarkers { get; set; } = [];
    public IReadOnlyList<BreedingSeasonStepViewModel> TodaySeasonNext { get; set; } = [];
    public string NextDueText { get; set; } = "next due —";
    public string BreedWaitText { get; set; } = "0 crossed, waiting for scan";
    public string EmptyRateText { get; set; } = "—";
    public IReadOnlyList<BreedingPrepRowViewModel> PrepRows { get; set; } = [];
    public IReadOnlyList<BreedingWaitingRowViewModel> WaitingRows { get; set; } = [];
    public IReadOnlyList<BreedingExpectingRowViewModel> ExpectingRows { get; set; } = [];
    public IReadOnlyList<BreedingEmptyRowViewModel> EmptyRows { get; set; } = [];
    public IReadOnlyList<BreedingRationRowViewModel> RationRows { get; set; } = [];
    public IReadOnlyList<BreedingPipelineRowViewModel> PipelineRows { get; set; } = [];
    public IReadOnlyList<BreedingDoeFlowStepViewModel> DoeFlowSteps { get; set; } = [];
    public IReadOnlyList<BreedingNowJobViewModel> NowJobs { get; set; } = [];
    public BreedingSeasonPanelViewModel Season { get; set; } = new();
    public IReadOnlyList<BreedingYearRowViewModel> YearRows { get; set; } = [];
    public IReadOnlyList<BreedingPlanRowViewModel> PlanRows { get; set; } = [];
    public IReadOnlyList<BreedingHeatRowViewModel> HeatRows { get; set; } = [];
    public IReadOnlyList<BreedingBuckRowViewModel> BuckRows { get; set; } = [];
    public IReadOnlyList<string> BuckOptions { get; set; } = [];
    public IReadOnlyList<BreedingGroupCandidateViewModel> GroupCandidates { get; set; } = [];
    public double PlanTimelineTodayPercent { get; set; }
    public IReadOnlyList<BreedingPlanTimelineSegmentViewModel> PlanTimelineSegments { get; set; } = [];
    public double TodayPlanTimelineTodayPercent { get; set; }
    public IReadOnlyList<BreedingPlanTimelineSegmentViewModel> TodayPlanTimelineSegments { get; set; } = [];
    public IReadOnlyList<BreedingTodayTimelineLabelViewModel> TodayTimelineLabels { get; set; } = [];
    public int SelectedBatchBuckGroupCount { get; set; }
    public BreedingReportsViewModel Reports { get; set; } = new();
    public BreedingKiddingStatsViewModel KiddingStats { get; set; } = new();
    public IReadOnlyList<BreedingDueToKidRowViewModel> DueToKidRows { get; set; } = [];
    public IReadOnlyList<BreedingMotherCheckRowViewModel> MotherCheckRows { get; set; } = [];
    public IReadOnlyList<BreedingKidGroundRowViewModel> KidGroundRows { get; set; } = [];
    public int SeasonWeanDays { get; set; } = HerdRules.WeanDaysDefault;
    public bool SeasonAutoWean { get; set; } = true;
    public IReadOnlyList<BreedingKidAutoWeanBarViewModel> KidAutoWeanBars { get; set; } = [];
}

public class BreedingReportsViewModel
{
    public bool HasData { get; set; }
    public bool SeasonDone { get; set; }
    public string SelectedBatchId { get; set; } = string.Empty;
    public string SelectedBuck { get; set; } = "all";
    public string HeadlineHtml { get; set; } = string.Empty;
    public IReadOnlyList<BreedingReportSeasonOptionViewModel> Seasons { get; set; } = [];
    public IReadOnlyList<string> BuckOptions { get; set; } = [];
    public IReadOnlyList<BreedingReportScoreTileViewModel> Scorecard { get; set; } = [];
    public IReadOnlyList<BreedingReportFunnelRowViewModel> DoeFunnel { get; set; } = [];
    public IReadOnlyList<BreedingReportFunnelRowViewModel> KidFunnel { get; set; } = [];
    public bool ShowKidFunnel { get; set; }
    public BreedingReportLitterViewModel Litter { get; set; } = new();
    public IReadOnlyList<BreedingReportBuckMetricViewModel> BuckCompare { get; set; } = [];
    public IReadOnlyList<BreedingReportBuckBarViewModel> BuckBars { get; set; } = [];
    public BreedingReportMonthsViewModel Months { get; set; } = new();
    public IReadOnlyList<BreedingReportProblemViewModel> Problems { get; set; } = [];
    public IReadOnlyList<BreedingReportMotherRowViewModel> BestMothers { get; set; } = [];
    public IReadOnlyList<BreedingReportCullRowViewModel> CullWatch { get; set; } = [];
    public bool CanEditTargets { get; set; }
}

public class BreedingReportSeasonOptionViewModel
{
    public string BatchId { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool Done { get; set; }
}

public class BreedingReportScoreTileViewModel
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double? Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string Blurb { get; set; } = string.Empty;
    public decimal Target { get; set; }
    public bool HigherIsBetter { get; set; } = true;
    public string Status { get; set; } = "na";
    public string StatusLabel { get; set; } = "No data yet";
    public string StatusIcon { get; set; } = "⏳";
    public int BarPercent { get; set; }
}

public class BreedingReportFunnelRowViewModel
{
    public string Label { get; set; } = string.Empty;
    public int Value { get; set; }
    public int Lost { get; set; }
    public int BaseValue { get; set; }
    public int BarPercent { get; set; }
    public string BarColor { get; set; } = "#1E4635";
}

public class BreedingReportLitterViewModel
{
    public bool HasKiddings { get; set; }
    public string SummaryHtml { get; set; } = string.Empty;
    public IReadOnlyList<BreedingReportLitterSegmentViewModel> Segments { get; set; } = [];
    public int ExpectedKidsFromScans { get; set; }
}

public class BreedingReportLitterSegmentViewModel
{
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
    public int Percent { get; set; }
    public string Color { get; set; } = "#1E4635";
}

public class BreedingReportBuckMetricViewModel
{
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public IReadOnlyList<BreedingReportBuckBarViewModel> Bars { get; set; } = [];
}

public class BreedingReportBuckBarViewModel
{
    public string BuckTag { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Display { get; set; } = string.Empty;
    public int BarPercent { get; set; }
    public string Color { get; set; } = "#1E4635";
    public bool IsBest { get; set; }
}

public class BreedingReportMonthsViewModel
{
    public bool HasData { get; set; }
    public string SummaryHtml { get; set; } = string.Empty;
    public IReadOnlyList<BreedingReportMonthBarViewModel> Bars { get; set; } = [];
}

public class BreedingReportMonthBarViewModel
{
    public string MonthKey { get; set; } = string.Empty;
    public string ShortLabel { get; set; } = string.Empty;
    public int Value { get; set; }
    public int BarPercent { get; set; }
    public bool ExpectedStyle { get; set; }
}

public class BreedingReportProblemViewModel
{
    public string Icon { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int? Count { get; set; }
    public int BarPercent { get; set; }
}

public class BreedingReportMotherRowViewModel
{
    public int DoeId { get; set; }
    public string Tag { get; set; } = string.Empty;
    public string Breed { get; set; } = string.Empty;
    public int Kiddings { get; set; }
    public string LastBornText { get; set; } = string.Empty;
    public string ScoreText { get; set; } = "—";
}

public class BreedingReportCullRowViewModel
{
    public int DoeId { get; set; }
    public string Tag { get; set; } = string.Empty;
    public int Kiddings { get; set; }
    public bool InSeason { get; set; }
    public IReadOnlyList<string> Reasons { get; set; } = [];
}

public class UpdateBreedingSeasonViewModel
{
    public int? BuildUp { get; set; }
    public int? ScanAfter { get; set; }
    public int? DryOff { get; set; }
    public int? WeanDays { get; set; }
    public decimal? WeanKg { get; set; }
    public int? DryBeforeCross { get; set; }
    public string? SelectedBatchId { get; set; }
    public string? BatchFrom { get; set; }
    public string? BatchTo { get; set; }
    public string? BatchName { get; set; }
    public string? NewBatchName { get; set; }
    public string? NewBatchIcon { get; set; }
    /// <summary>Template batch when creating a new group (V243 “same rules as”).</summary>
    public string? CopyFromBatchId { get; set; }
    public bool? RemoveBatch { get; set; }
    public bool? DuplicateBatch { get; set; }
    public bool? AutoWean { get; set; }
    public string? ReportTargetKey { get; set; }
    public decimal? ReportTargetValue { get; set; }
}

public class UpdateKidFieldViewModel
{
    public int GoatId { get; set; }
    public string Field { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class RecordKidWeanWeightViewModel
{
    public int GoatId { get; set; }
    public decimal Kg { get; set; }
}

public class BulkWeanKidsViewModel
{
    public List<int> GoatIds { get; set; } = [];
    public DateOnly? WeanDate { get; set; }
}

public class UpdateKidWeanRuleViewModel
{
    public int? WeanDays { get; set; }
    public bool? AutoWean { get; set; }
}

public class BreedingAddDoeViewModel
{
    public string Tag { get; set; } = string.Empty;
    public string Stage { get; set; } = "prep";
    public DateOnly? Date { get; set; }
    public string? BuckTag { get; set; }
}

public class BreedingStageActionViewModel
{
    public int GoatId { get; set; }
    public string Action { get; set; } = string.Empty;
    public DateOnly? Date { get; set; }
    public string? BuckTag { get; set; }
    public int? KidsCount { get; set; }
    public string? Notes { get; set; }
    public bool ConfirmWarnings { get; set; }
}

public class CreateBreedingGroupViewModel
{
    public string? BirthBatchId { get; set; }
    public string BuckTag { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public int StayDays { get; set; } = 42;
    /// <summary>Save the group only; record crosses when the buck goes in.</summary>
    public bool DeferCross { get; set; }
    public int Max { get; set; } = 15;
    public List<int> DoeIds { get; set; } = [];
    public bool ConfirmWarnings { get; set; }
    public bool ConfirmSecondActive { get; set; }
}

public class CloseBreedingGroupViewModel
{
    public string GroupId { get; set; } = string.Empty;
    public DateOnly? EndDate { get; set; }
}

public class BreedingGroupMemberActionViewModel
{
    public string GroupId { get; set; } = string.Empty;
    public int GoatId { get; set; }
}

public class BuckInBreedingGroupViewModel
{
    public string GroupId { get; set; } = string.Empty;
    public DateOnly? InDate { get; set; }
    public bool ConfirmWarnings { get; set; }
}

public class BreedingGroupMemberCardViewModel
{
    public int GoatId { get; set; }
    public string Tag { get; set; } = string.Empty;
    public string Breed { get; set; } = string.Empty;
    public string OutcomeKey { get; set; } = string.Empty;
    public string OutcomeText { get; set; } = string.Empty;
    public bool CanRemove { get; set; }
}

public class BreedingGroupCardViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Buck { get; set; } = string.Empty;
    public string Start { get; set; } = string.Empty;
    public string? End { get; set; }
    public int Max { get; set; } = 15;
    public string Status { get; set; } = "active";
    public int MemberCount { get; set; }
    public int WaitingScan { get; set; }
    public int Pregnant { get; set; }
    public int Empty { get; set; }
    public int Kidded { get; set; }
    public int Gone { get; set; }
    public int KidsAlive { get; set; }
    public int? ConceptionPercent { get; set; }
    public int StayDays { get; set; } = 42;
    public bool CrossDeferred { get; set; }
    public bool CanClose { get; set; }
    public bool CanDelete { get; set; }
    public bool CanBuckIn { get; set; }
    public string? BirthBatchId { get; set; }
    public string? BirthBatchName { get; set; }
    public string BirthBatchIcon { get; set; } = "⭐";
    public IReadOnlyList<BreedingGroupMemberCardViewModel> Members { get; set; } = [];
}

public class BreedingScanDoeViewModel
{
    public int Id { get; set; }
    public string Tag { get; set; } = string.Empty;
    public string Breed { get; set; } = string.Empty;
    public string? BuckTag { get; set; }
    public string? MatedDate { get; set; }
    public int DaysSinceCross { get; set; }
    public int Kiddings { get; set; }
    public string KiddingsText { get; set; } = string.Empty;
    public bool AwaitingRescan { get; set; }
}

public class BreedingScanGroupViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Buck { get; set; } = string.Empty;
    public string? BirthBatchIcon { get; set; }
    public bool BuckStillWithDoes { get; set; }
    public IReadOnlyList<BreedingScanDoeViewModel> Does { get; set; } = [];
}

public class BreedingScanSeasonBatchOptionViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = "⭐";
}

public class BreedingScanSheetViewModel
{
    public IReadOnlyList<BreedingScanGroupViewModel> Groups { get; set; } = [];
    public IReadOnlyList<BreedingScanDoeViewModel> Ungrouped { get; set; } = [];
    public IReadOnlyList<BreedingScanSeasonBatchOptionViewModel> BirthBatches { get; set; } = [];
}

public class BulkScanResultItemViewModel
{
    public int GoatId { get; set; }
    /// <summary>0 = empty, 1–6 = kids seen. Ignored when <see cref="NotSure"/> is true.</summary>
    public int KidsCount { get; set; }
    public bool NotSure { get; set; }
    public int RescanInDays { get; set; } = 14;
    /// <summary>Optional; adjusts mated date backward from scan date when pregnant.</summary>
    public int? DaysPregnant { get; set; }
    /// <summary>When empty: next, heat, cull, back.</summary>
    public string? EmptyNextStep { get; set; }
    public string? EmptyBirthBatchId { get; set; }
}

public class BulkScanSaveViewModel
{
    public DateOnly ScanDate { get; set; }
    public List<BulkScanResultItemViewModel> Results { get; set; } = [];
}

public class BulkScanSaveResultViewModel
{
    public int Saved { get; set; }
    public IReadOnlyList<string> Errors { get; set; } = [];
}

public class RecordPrepViewModel
{
    public string Tag { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
}

public class RecordCrossViewModel
{
    public string Tag { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public string? BuckTag { get; set; }
    public bool ConfirmWarnings { get; set; }
}

public class RecordUltrasoundViewModel
{
    public string Tag { get; set; } = string.Empty;
    public int KidsCount { get; set; }
    public DateOnly? Date { get; set; }
    public int? DaysPregnant { get; set; }
}

public class CrossCheckViewModel
{
    public bool CanCross { get; set; }
    public string? DoeTag { get; set; }
    public string? DoeName { get; set; }
    public string? BuckTag { get; set; }
    public bool BuckInHerd { get; set; }
    public IReadOnlyList<string> Errors { get; set; } = [];
    public IReadOnlyList<string> Warnings { get; set; } = [];
    public IReadOnlyList<string> Notes { get; set; } = [];
}

public class BirthKidViewModel
{
    public GoatGender Gender { get; set; }
    /// <summary>Blank = generate a temporary tag.</summary>
    public string? Tag { get; set; }
    public string? Name { get; set; }
    public decimal? WeightKg { get; set; }
}

public class RecordBirthViewModel
{
    public Guid ClientRequestId { get; set; }
    public string DoeTag { get; set; } = string.Empty;
    public DateOnly KiddingDate { get; set; }
    public KiddingEase Ease { get; set; } = KiddingEase.Normal;
    public int Stillborn { get; set; }
    public string? Notes { get; set; }
    public List<BirthKidViewModel> Kids { get; set; } = [];
    public bool ConfirmWarnings { get; set; }
}

public class RecordBirthResultViewModel
{
    public int KiddingRecordId { get; set; }
    public bool AlreadyRecorded { get; set; }
    public IReadOnlyList<RecordedKidViewModel> Kids { get; set; } = [];
}

public class RecordedKidViewModel
{
    public int Id { get; set; }
    public string Tag { get; set; } = string.Empty;
    public bool IsTemporaryTag { get; set; }
    public GoatGender Gender { get; set; }
}

public class RecordAbortionViewModel
{
    public string Tag { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public string? Notes { get; set; }
}

public class RecordReturnedToHeatViewModel
{
    public string Tag { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
}

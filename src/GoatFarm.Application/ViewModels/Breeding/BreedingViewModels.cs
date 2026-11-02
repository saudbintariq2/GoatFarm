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

public class BreedingPipelineRowViewModel
{
    public int Id { get; set; }
    public string Tag { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Owner { get; set; }
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
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
}

public class BreedingYearRowViewModel
{
    public string BatchId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int DoeCount { get; set; }
    public string CrossText { get; set; } = string.Empty;
    public string ScanText { get; set; } = string.Empty;
    public string DryText { get; set; } = string.Empty;
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
}

public class BreedingPageViewModel
{
    public int PrepCount { get; set; }
    public int WaitingCount { get; set; }
    public int ExpectingCount { get; set; }
    public string NextDueText { get; set; } = "next due —";
    public string BreedWaitText { get; set; } = "0 crossed, waiting for scan";
    public string EmptyRateText { get; set; } = "—";
    public IReadOnlyList<BreedingPrepRowViewModel> PrepRows { get; set; } = [];
    public IReadOnlyList<BreedingWaitingRowViewModel> WaitingRows { get; set; } = [];
    public IReadOnlyList<BreedingExpectingRowViewModel> ExpectingRows { get; set; } = [];
    public IReadOnlyList<BreedingEmptyRowViewModel> EmptyRows { get; set; } = [];
    public IReadOnlyList<BreedingRationRowViewModel> RationRows { get; set; } = [];
    public IReadOnlyList<BreedingPipelineRowViewModel> PipelineRows { get; set; } = [];
    public IReadOnlyList<BreedingNowJobViewModel> NowJobs { get; set; } = [];
    public BreedingSeasonPanelViewModel Season { get; set; } = new();
    public IReadOnlyList<BreedingYearRowViewModel> YearRows { get; set; } = [];
    public IReadOnlyList<BreedingPlanRowViewModel> PlanRows { get; set; } = [];
    public IReadOnlyList<BreedingHeatRowViewModel> HeatRows { get; set; } = [];
    public IReadOnlyList<BreedingBuckRowViewModel> BuckRows { get; set; } = [];
    public IReadOnlyList<string> BuckOptions { get; set; } = [];
    public IReadOnlyList<BreedingGroupCandidateViewModel> GroupCandidates { get; set; } = [];
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
    public bool? RemoveBatch { get; set; }
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
    public string BuckTag { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public int Max { get; set; } = 15;
    public List<int> DoeIds { get; set; } = [];
    public bool ConfirmWarnings { get; set; }
    public bool ConfirmSecondActive { get; set; }
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

namespace GoatFarm.Application.ViewModels.Breeding;

public class BreedingKiddingStatsViewModel
{
    public int DueTotal { get; set; }
    public int DueSoon30 { get; set; }
    public int KiddedMothers { get; set; }
    public int KidsOnGround { get; set; }
    public int MothersToWatch { get; set; }
}

public class BreedingDueToKidRowViewModel
{
    public int Id { get; set; }
    public string Tag { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string Breed { get; set; } = string.Empty;
    public int KiddingsCount { get; set; }
    public string NextKiddingOrdinal { get; set; } = string.Empty;
    public string? BuckTag { get; set; }
    public string FlowStageLabel { get; set; } = string.Empty;
    public string FlowStageCssClass { get; set; } = "chip-dry";
    public bool IsDry { get; set; }
    public string DueWindow { get; set; } = string.Empty;
    public int DaysToDue { get; set; }
    public string DaysToDueText { get; set; } = string.Empty;
    public string DaysToDueStyle { get; set; } = string.Empty;
    public string KidsSeenDisplay { get; set; } = "—";
    public int? KidsCount { get; set; }
    public string? MotherScoreHtml { get; set; }
    public double? MotherScoreAverage { get; set; }
}

public class BreedingMotherCheckRowViewModel
{
    public int DoeId { get; set; }
    public int KiddingRecordId { get; set; }
    public string Tag { get; set; } = string.Empty;
    public string Breed { get; set; } = string.Empty;
    public int KiddingsCount { get; set; }
    public string KiddingOrdinalText { get; set; } = string.Empty;
    public string KiddedDate { get; set; } = string.Empty;
    public string KiddedAgoText { get; set; } = string.Empty;
    public int Alive { get; set; }
    public int Born { get; set; }
    public string EaseDisplay { get; set; } = string.Empty;
    public string EaseKey { get; set; } = "Easy";
    public string EaseCssClass { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string RowCssClass { get; set; } = string.Empty;
    public int? Mothering { get; set; }
    public int? MilkProduction { get; set; }
    public int? BodyCondition { get; set; }
    public string DamHealth { get; set; } = "Good";
    public IReadOnlyList<string> DamHealthIssues { get; set; } = [];
    public string KeepDecision { get; set; } = "Keep";
    public string? KeepReason { get; set; }
    public string MotherScoreHtml { get; set; } = "";
}

public class MotherGroupDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#2E6A50";
    public int Order { get; set; }
}

public class BreedingMotherGroupRowViewModel
{
    public int DoeId { get; set; }
    public string Tag { get; set; } = string.Empty;
    public string Breed { get; set; } = string.Empty;
    public int KiddingsCount { get; set; }
    public int? LastLitterBorn { get; set; }
    public string LastLitterText { get; set; } = "—";
    public string MotherScoreHtml { get; set; } = "";
    public string? MotherGroupId { get; set; }
    public string? SuggestedGroupId { get; set; }
}

public class BreedingKidAutoWeanBarViewModel
{
    public string BatchId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = "⭐";
    public int WeanDays { get; set; }
    public bool AutoWean { get; set; }
    public int KidCount { get; set; }
}

public class BreedingKidGroundRowViewModel
{
    public int Id { get; set; }
    public string Tag { get; set; } = string.Empty;
    public bool IsTemporaryTag { get; set; }
    public string? DamTag { get; set; }
    public string SexDisplay { get; set; } = string.Empty;
    public int AgeDays { get; set; }
    public decimal? BirthKg { get; set; }
    public decimal? WeanKg { get; set; }
    public string? Kg90Text { get; set; }
    public string MilkType { get; set; } = "Mother";
    public string HealthTag { get; set; } = "Good";
    public string StatusDisplay { get; set; } = string.Empty;
    public string StatusCssClass { get; set; } = string.Empty;
    public string? WeanDate { get; set; }
    public string? WeanMethod { get; set; }
    public string StatusSubText { get; set; } = string.Empty;
    public string StatusSubStyle { get; set; } = string.Empty;
    public bool IsWeak { get; set; }
    public bool WeaningSoon { get; set; }
    public bool NeedsWeanWeight { get; set; }
    public bool OnMilk { get; set; }
}

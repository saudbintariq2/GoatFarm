using GoatFarm.Application.ViewModels.Feed;
using GoatFarm.Application.ViewModels.Goats;
using GoatFarm.Application.ViewModels.Reminders;

namespace GoatFarm.Application.ViewModels.Search;

public class SearchPageViewModel
{
    public string? InitialTag { get; set; }
    public GoatCardViewModel? Profile { get; set; }
    public string? Error { get; set; }
}

public class GoatCardViewModel : GoatProfileViewModel
{
    public int SubjectGoatId { get; set; }
    public IReadOnlyList<GoatCardPedigreeNodeViewModel> PedigreeNodes { get; set; } = [];
    public IReadOnlyList<GoatCardSiblingViewModel> Siblings { get; set; } = [];
    public IReadOnlyList<GoatCardKiddingRowViewModel> Kiddings { get; set; } = [];
    public int SiredKidsCount { get; set; }
    public int EmptyScanCount { get; set; }
    public int AbortionCount { get; set; }
    public int? ActiveTreatmentId { get; set; }
    public IReadOnlyList<GoatCardKidToTagViewModel> KidsToTag { get; set; } = [];
    public decimal? LatestWeightKg { get; set; }
    public int? LatestBcsScore { get; set; }
    public string? LatestBcsDateDisplay { get; set; }
}

public class GoatCardPedigreeNodeViewModel
{
    public int Id { get; set; }
    public string Tag { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string Breed { get; set; } = string.Empty;
    public int? SireId { get; set; }
    public int? DamId { get; set; }
    public bool IsArchived { get; set; }
}

public class GoatCardSiblingViewModel
{
    public int Id { get; set; }
    public string Tag { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public string BirthDateDisplay { get; set; } = string.Empty;
    public bool Twin { get; set; }
}

public class GoatCardKiddingRowViewModel
{
    public string DateDisplay { get; set; } = string.Empty;
    public string? SireTag { get; set; }
    public int? SireGoatId { get; set; }
    public int LiveKids { get; set; }
    public int Stillborn { get; set; }
}

public class GoatCardKidToTagViewModel
{
    public int Id { get; set; }
    public string Tag { get; set; } = string.Empty;
}

public class GoatProfileViewModel
{
    public GoatViewModel Goat { get; set; } = new();
    public IReadOnlyList<GoatVaccinationRecordViewModel> VaccinationHistory { get; set; } = [];
    public IReadOnlyList<GoatVaccineStatusViewModel> VaccineSchedule { get; set; } = [];
    public IReadOnlyList<GoatTreatmentHistoryViewModel> Treatments { get; set; } = [];
    public IReadOnlyList<GoatWeightRowViewModel> Weights { get; set; } = [];
    public IReadOnlyList<GoatPedigreeRowViewModel> Pedigree { get; set; } = [];
    public IReadOnlyList<GoatBreedingHistoryRowViewModel> BreedingHistory { get; set; } = [];
    public FeedPlanViewModel? FeedPlan { get; set; }
    public IReadOnlyList<ReminderViewModel> Reminders { get; set; } = [];
}

public class GoatWeightRowViewModel
{
    public DateOnly Date { get; set; }
    public decimal Kg { get; set; }
    public string DateDisplay => Date.ToString("yyyy-MM-dd");
}

public class GoatPedigreeRowViewModel
{
    public int Generation { get; set; }
    public string Tag { get; set; } = string.Empty;
    public string? SireTag { get; set; }
    public string? DamTag { get; set; }
    public string? SearchTag { get; set; }
}

public class GoatBreedingHistoryRowViewModel
{
    public DateOnly MatedDate { get; set; }
    public string? BuckTag { get; set; }
    public string Outcome { get; set; } = string.Empty;
    public string MatedDisplay => MatedDate.ToString("yyyy-MM-dd");
}

public class GoatTreatmentHistoryViewModel
{
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string Diagnosis { get; set; } = string.Empty;
    public string Medicine { get; set; } = string.Empty;
    public decimal Cost { get; set; }
    public int? WithdrawalDays { get; set; }
    public string? Notes { get; set; }
    public string StartDateDisplay => StartDate.ToString("yyyy-MM-dd");
    public string EndDateDisplay => EndDate?.ToString("yyyy-MM-dd") ?? "—";
    public bool IsActive => EndDate is null || EndDate >= DateOnly.FromDateTime(DateTime.Today);
}

public class GoatVaccinationRecordViewModel
{
    public string VaccineName { get; set; } = string.Empty;
    public string ScopeDisplay { get; set; } = string.Empty;
    public DateOnly VaccinationDate { get; set; }
    public string DateDisplay => VaccinationDate.ToString("yyyy-MM-dd");
}

public class GoatVaccineStatusViewModel
{
    public string VaccineName { get; set; } = string.Empty;
    public string ScopeDisplay { get; set; } = string.Empty;
    public string RuleDisplay { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StatusCss { get; set; } = string.Empty;
    public string? DueDate { get; set; }
    public string? LastDate { get; set; }
}

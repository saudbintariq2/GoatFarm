using GoatFarm.Application.ViewModels.Feed;
using GoatFarm.Application.ViewModels.Goats;
using GoatFarm.Application.ViewModels.Reminders;

namespace GoatFarm.Application.ViewModels.Search;

public class SearchPageViewModel
{
    public string? InitialTag { get; set; }
    public GoatProfileViewModel? Profile { get; set; }
    public string? Error { get; set; }
}

public class GoatProfileViewModel
{
    public GoatViewModel Goat { get; set; } = new();
    public IReadOnlyList<GoatVaccinationRecordViewModel> VaccinationHistory { get; set; } = [];
    public IReadOnlyList<GoatVaccineStatusViewModel> VaccineSchedule { get; set; } = [];
    public FeedPlanViewModel? FeedPlan { get; set; }
    public IReadOnlyList<ReminderViewModel> Reminders { get; set; } = [];
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

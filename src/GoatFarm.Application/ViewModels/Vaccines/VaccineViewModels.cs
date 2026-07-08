using System.ComponentModel.DataAnnotations;
using GoatFarm.Domain.Enums;

namespace GoatFarm.Application.ViewModels.Vaccines;

public class VaccineViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public VaccineScope Scope { get; set; }
    public string ScopeDisplay { get; set; } = string.Empty;
    public VaccineRuleType RuleType { get; set; }
    public string RuleDisplay { get; set; } = string.Empty;
    public int? Days { get; set; }
    public int? Months { get; set; }
}

public class CreateVaccineViewModel
{
    [Required(ErrorMessage = "Enter a vaccine name")]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public VaccineScope Scope { get; set; } = VaccineScope.All;

    [Required]
    public VaccineRuleType RuleType { get; set; } = VaccineRuleType.Age;

    [Range(1, int.MaxValue, ErrorMessage = "Enter a number")]
    public int Value { get; set; }
}

public class VaccineDueRowViewModel
{
    public int VaccineId { get; set; }
    public string VaccineName { get; set; } = string.Empty;
    public string ScopeDisplay { get; set; } = string.Empty;
    public IReadOnlyList<string> GoatTags { get; set; } = [];
    public int DueCount { get; set; }
}

public class VaccineUpcomingRowViewModel
{
    public string VaccineName { get; set; } = string.Empty;
    public string ScopeDisplay { get; set; } = string.Empty;
    public IReadOnlyList<string> GoatTags { get; set; } = [];
    public int Count { get; set; }
    public int FirstInDays { get; set; }
    public string FirstDate { get; set; } = string.Empty;
}

public class VaccinationLogViewModel
{
    public int VaccineId { get; set; }
    public DateOnly VaccinationDate { get; set; }
    public string Date => VaccinationDate.ToString("yyyy-MM-dd");
    public string VaccineName { get; set; } = string.Empty;
    public int GoatCount { get; set; }
}

public class UpdateVaccinationBatchViewModel
{
    [Required]
    public int VaccineId { get; set; }

    [Required]
    public DateOnly Date { get; set; }

    [Required]
    public DateOnly NewDate { get; set; }
}

public class VaccinePageViewModel
{
    public int VaccineCount { get; set; }
    public int DueNowCount { get; set; }
    public string DueNowNote { get; set; } = string.Empty;
    public int UpcomingCount { get; set; }
    public string UpcomingNote { get; set; } = string.Empty;
    public int RemindDays { get; set; } = 30;
    public IReadOnlyList<VaccineDueRowViewModel> DueNow { get; set; } = [];
    public IReadOnlyList<VaccineUpcomingRowViewModel> Upcoming { get; set; } = [];
    public IReadOnlyList<VaccineViewModel> Schedule { get; set; } = [];
    public IReadOnlyList<VaccinationLogViewModel> History { get; set; } = [];
    public CreateVaccineViewModel NewVaccine { get; set; } = new();
}

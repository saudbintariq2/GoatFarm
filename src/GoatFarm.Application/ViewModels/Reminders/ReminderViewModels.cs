using System.ComponentModel.DataAnnotations;
using GoatFarm.Domain.Enums;

namespace GoatFarm.Application.ViewModels.Reminders;

public class ReminderViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public VaccineScope Scope { get; set; }
    public string ScopeDisplay { get; set; } = string.Empty;
    public DateOnly ReminderDate { get; set; }
    public string DateDisplay => ReminderDate.ToString("yyyy-MM-dd");
    public string WhenDisplay { get; set; } = string.Empty;
    public string WhenColor { get; set; } = string.Empty;
}

public class CreateReminderViewModel
{
    [Required(ErrorMessage = "Enter a reminder")]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    public VaccineScope Scope { get; set; } = VaccineScope.None;

    [Required]
    [Range(1, int.MaxValue)]
    public int Number { get; set; }

    [Required]
    [Range(1, 30)]
    public int UnitDays { get; set; } = 30;
}

public class UpdateReminderViewModel
{
    [Required(ErrorMessage = "Enter a reminder")]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    public VaccineScope Scope { get; set; } = VaccineScope.None;

    [Required]
    public DateOnly ReminderDate { get; set; }
}

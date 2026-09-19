using System.ComponentModel.DataAnnotations;

namespace GoatFarm.Application.ViewModels.Goats;

public class RecordWeightViewModel
{
    [Required]
    public string Tag { get; set; } = string.Empty;

    [Range(0.1, 500)]
    public decimal Kg { get; set; }

    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);
}

public class RecordDeathViewModel
{
    [Required]
    public string Tag { get; set; } = string.Empty;

    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public string? Reason { get; set; }
}

public class WeightDeathResultViewModel
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Tag { get; set; }
}

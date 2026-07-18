using System.ComponentModel.DataAnnotations;
using GoatFarm.Domain.Enums;

namespace GoatFarm.Application.ViewModels.Goats;

public class GoatViewModel
{
    public int Id { get; set; }
    public string Tag { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Comment { get; set; }
    public string Breed { get; set; } = string.Empty;
    public GoatGender Gender { get; set; }
    public GoatStatus Status { get; set; }
    public string StatusKey => Status.ToString().ToLowerInvariant();
    public GoatSource Source { get; set; }
    public decimal PurchasePrice { get; set; }
    public DateOnly EventDate { get; set; }
    public string EventDateDisplay => EventDate.ToString("yyyy-MM-dd");
    public int? GroupId { get; set; }
    public string? GroupName { get; set; }
    public int AgeDays { get; set; }
    public string AgeLabel { get; set; } = string.Empty;
    public string StatusDisplay { get; set; } = string.Empty;
    public string StatusCssClass { get; set; } = string.Empty;
    public string PriceDisplay { get; set; } = string.Empty;
    public DateOnly? PrepCrossDate { get; set; }
    public string? PrepCrossDateDisplay => PrepCrossDate?.ToString("yyyy-MM-dd");
    public DateOnly? MatedDate { get; set; }
    public string? MatedDateDisplay => MatedDate?.ToString("yyyy-MM-dd");
    public string? BuckTag { get; set; }
    public int? KidsCount { get; set; }
    public DateOnly? UltrasoundDate { get; set; }
    public string? UltrasoundDateDisplay => UltrasoundDate?.ToString("yyyy-MM-dd");
    public string? BreedingHint { get; set; }
    public string? BreedingHintColor { get; set; }
}

public class CreateGoatViewModel
{
    [Required(ErrorMessage = "Please enter a Tag / RFID ID")]
    [StringLength(50)]
    public string Tag { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Name { get; set; }

    [StringLength(500)]
    public string? Comment { get; set; }

    [Required]
    public string Breed { get; set; } = "Beetal";

    [Required]
    public GoatGender Gender { get; set; } = GoatGender.Female;

    [Required]
    public GoatSource Source { get; set; } = GoatSource.Bought;

    [Range(0, double.MaxValue)]
    public decimal PurchasePrice { get; set; }

    [Required(ErrorMessage = "Please pick the date")]
    public DateOnly EventDate { get; set; }

    [Required]
    public GoatStatus Status { get; set; } = GoatStatus.Kid;
}

public class BulkMoveViewModel
{
    public IReadOnlyList<int> GoatIds { get; set; } = [];
    public string MoveTarget { get; set; } = string.Empty;
    public DateOnly? PrepCrossDate { get; set; }
    public DateOnly? MatedDate { get; set; }
    public string? BuckTag { get; set; }
}

public class HerdStatsViewModel
{
    public int Total { get; set; }
    public int Kids { get; set; }
    public int Milking { get; set; }
    public int Pregnant { get; set; }
    public int Dry { get; set; }
    public int Bucks { get; set; }
}

public class HerdPageViewModel
{
    public HerdStatsViewModel Stats { get; set; } = new();
    public IReadOnlyList<GoatViewModel> Goats { get; set; } = [];
    public IReadOnlyList<string> Groups { get; set; } = [];
    public string? Filter { get; set; }
    public PaginationViewModel Pagination { get; set; } = new();
    public CreateGoatViewModel NewGoat { get; set; } = new() { EventDate = DateOnly.FromDateTime(DateTime.Today) };
}

public class PaginationViewModel
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalItems { get; set; }
    public int TotalPages => TotalItems == 0 ? 1 : (int)Math.Ceiling(TotalItems / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
    public int StartItem => TotalItems == 0 ? 0 : (Page - 1) * PageSize + 1;
    public int EndItem => TotalItems == 0 ? 0 : Math.Min(Page * PageSize, TotalItems);
}

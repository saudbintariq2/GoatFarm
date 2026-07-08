using System.ComponentModel.DataAnnotations;
using GoatFarm.Application.ViewModels.Goats;

namespace GoatFarm.Application.ViewModels.Milk;

public class MilkProductionViewModel
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public string DateDisplay => Date.ToString("yyyy-MM-dd");
    public string Breed { get; set; } = "Mixed";
    public decimal Liters { get; set; }
}

public class MilkSaleViewModel
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public string DateDisplay => Date.ToString("yyyy-MM-dd");
    public decimal Liters { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
}

public class MilkWasteViewModel
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public string DateDisplay => Date.ToString("yyyy-MM-dd");
    public decimal Liters { get; set; }
    public string? Notes { get; set; }
}

public class CreateMilkProductionViewModel
{
    [Required]
    public DateOnly Date { get; set; }

    [Required]
    public string Breed { get; set; } = "Mixed";

    [Required]
    [Range(0.1, double.MaxValue, ErrorMessage = "Enter litres")]
    public decimal Liters { get; set; }
}

public class CreateMilkSaleViewModel
{
    [Required]
    public DateOnly Date { get; set; }

    [Required]
    [Range(0.1, double.MaxValue)]
    public decimal Liters { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Rate { get; set; }
}

public class CreateMilkWasteViewModel
{
    [Required]
    public DateOnly Date { get; set; }

    [Required]
    [Range(0.1, double.MaxValue, ErrorMessage = "Enter litres")]
    public decimal Liters { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}

public class MilkPageViewModel
{
    public decimal MilkIncome { get; set; }
    public decimal LitersSold { get; set; }
    public decimal LitersProduced { get; set; }
    public decimal LitersWasted { get; set; }
    public decimal LitersLeft { get; set; }
    public decimal LitersPerDayAvg { get; set; }
    public IReadOnlyList<MilkProductionViewModel> Productions { get; set; } = [];
    public IReadOnlyList<MilkSaleViewModel> Sales { get; set; } = [];
    public IReadOnlyList<MilkWasteViewModel> Wastes { get; set; } = [];
    public PaginationViewModel ProductionPagination { get; set; } = new();
    public PaginationViewModel SalePagination { get; set; } = new();
    public PaginationViewModel WastePagination { get; set; } = new();
    public int ProdPage { get; set; } = 1;
    public int SalePage { get; set; } = 1;
    public int WastePage { get; set; } = 1;
    public CreateMilkProductionViewModel NewProduction { get; set; } = new() { Date = DateOnly.FromDateTime(DateTime.Today) };
    public CreateMilkSaleViewModel NewSale { get; set; } = new() { Date = DateOnly.FromDateTime(DateTime.Today) };
    public CreateMilkWasteViewModel NewWaste { get; set; } = new() { Date = DateOnly.FromDateTime(DateTime.Today) };
}

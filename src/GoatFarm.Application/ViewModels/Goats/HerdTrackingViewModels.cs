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

public class RecordSaleViewModel
{
    [Required]
    public string Tag { get; set; } = string.Empty;

    [Range(0.01, 999_999_999)]
    public decimal Price { get; set; }

    public string? Buyer { get; set; }

    public decimal? WeightKg { get; set; }

    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public bool IsCredit { get; set; }

    public int? AccountId { get; set; }

    public int? CustomerId { get; set; }
}

public class GoatSaleFormDataViewModel
{
    public IReadOnlyList<SaleMoneyAccountOptionViewModel> Accounts { get; set; } = [];
    public IReadOnlyList<SaleCustomerOptionViewModel> Customers { get; set; } = [];
}

public class SaleMoneyAccountOptionViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class SaleCustomerOptionViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

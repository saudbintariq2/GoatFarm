using System.ComponentModel.DataAnnotations;

namespace GoatFarm.Application.ViewModels.Finance;

public class AssetViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public decimal Cost { get; set; }
}

public class IncomeViewModel
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateOnly Date { get; set; }
    public string DateDisplay => Date.ToString("yyyy-MM-dd");
}

public class ExpenseViewModel
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateOnly Date { get; set; }
    public string DateDisplay => Date.ToString("yyyy-MM-dd");
}

public class OwnerInvestmentViewModel
{
    public int Id { get; set; }
    public string Note { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateOnly Date { get; set; }
    public string DateDisplay => Date.ToString("yyyy-MM-dd");
}

public class CreateAssetViewModel
{
    [Required(ErrorMessage = "Enter asset name")]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Type { get; set; } = "Machinery";

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Enter cost")]
    public decimal Cost { get; set; }
}

public class CreateIncomeViewModel
{
    [Required]
    public string Type { get; set; } = string.Empty;

    [Required]
    public DateOnly Date { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Enter amount")]
    public decimal Amount { get; set; }
}

public class CreateExpenseViewModel
{
    [Required]
    public string Type { get; set; } = string.Empty;

    [Required]
    public DateOnly Date { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Enter amount")]
    public decimal Amount { get; set; }
}

public class CreateOwnerInvestmentViewModel
{
    [Required(ErrorMessage = "Enter a note")]
    [StringLength(200)]
    public string Note { get; set; } = string.Empty;

    [Required]
    public DateOnly Date { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Enter amount")]
    public decimal Amount { get; set; }
}

public class FinancePageViewModel
{
    public string Month { get; set; } = string.Empty;
    public decimal Profit { get; set; }
    public bool IsLoss { get; set; }
    public string ProfitNote { get; set; } = string.Empty;
    public decimal Capital { get; set; }
    public decimal LivestockValue { get; set; }
    public int BoughtGoatCount { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal TotalExpense { get; set; }
    public decimal FeedMonthly { get; set; }
    public decimal MilkIncome { get; set; }
    public decimal MilkLitersSold { get; set; }
    public decimal OwnerInvestmentMonthTotal { get; set; }
    public decimal OwnerInvestmentTotal { get; set; }
    public IReadOnlyList<AssetViewModel> Assets { get; set; } = [];
    public IReadOnlyList<IncomeViewModel> Incomes { get; set; } = [];
    public IReadOnlyList<ExpenseViewModel> Expenses { get; set; } = [];
    public IReadOnlyList<OwnerInvestmentViewModel> OwnerInvestments { get; set; } = [];
    public CreateAssetViewModel NewAsset { get; set; } = new();
    public CreateIncomeViewModel NewIncome { get; set; } = new() { Date = DateOnly.FromDateTime(DateTime.Today) };
    public CreateExpenseViewModel NewExpense { get; set; } = new() { Date = DateOnly.FromDateTime(DateTime.Today) };
    public CreateOwnerInvestmentViewModel NewOwnerInvestment { get; set; } = new() { Date = DateOnly.FromDateTime(DateTime.Today) };
}

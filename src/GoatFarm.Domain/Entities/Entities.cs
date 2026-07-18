using GoatFarm.Domain.Common;
using GoatFarm.Domain.Enums;

namespace GoatFarm.Domain.Entities;

public class GoatGroup : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ICollection<Goat> Goats { get; set; } = [];
}

public class Goat : BaseEntity
{
    public string Tag { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Comment { get; set; }
    public string Breed { get; set; } = string.Empty;
    public GoatGender Gender { get; set; }
    public GoatStatus Status { get; set; }
    public GoatSource Source { get; set; }
    public decimal PurchasePrice { get; set; }
    public DateOnly EventDate { get; set; }
    public int? GroupId { get; set; }
    public GoatGroup? Group { get; set; }
    public DateOnly? PrepCrossDate { get; set; }
    public DateOnly? MatedDate { get; set; }
    public string? BuckTag { get; set; }
    public int? KidsCount { get; set; }
    public DateOnly? UltrasoundDate { get; set; }
    public ICollection<VaccinationHistory> VaccinationHistories { get; set; } = [];
}

public class FeedPrice : BaseEntity
{
    public string FeedType { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public decimal PricePerKg { get; set; }
    public decimal StockKg { get; set; }
}

public class FeedPlan : BaseEntity
{
    public GoatStatus StatusKey { get; set; }
    public decimal MedicineCostPerGoatPerMonth { get; set; }
    public ICollection<FeedPlanItem> Items { get; set; } = [];
}

public class FeedPlanItem : BaseEntity
{
    public int FeedPlanId { get; set; }
    public FeedPlan FeedPlan { get; set; } = null!;
    public string FeedType { get; set; } = string.Empty;
    public int GramsPerDay { get; set; }
}

public class FeedPurchase : BaseEntity
{
    public DateOnly Date { get; set; }
    public string FeedType { get; set; } = string.Empty;
    public decimal Kg { get; set; }
    public decimal RatePerKg { get; set; }
    public decimal Amount { get; set; }
    public string? Comment { get; set; }
}

public class RecurringCost : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public RecurringCostPeriod Period { get; set; }
}

public class VaccinePurchase : BaseEntity
{
    public DateOnly Date { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Qty { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Comment { get; set; }
}

public class Asset : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public decimal Cost { get; set; }
    public DateOnly? PurchaseDate { get; set; }
    public string? Comment { get; set; }
}

public class Income : BaseEntity
{
    public string Type { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateOnly Date { get; set; }
    public string? Comment { get; set; }
}

public class Expense : BaseEntity
{
    public string Type { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateOnly Date { get; set; }
    public string? Comment { get; set; }
}

public class OwnerInvestment : BaseEntity
{
    public string Note { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateOnly Date { get; set; }
}

public class MilkProduction : BaseEntity
{
    public DateOnly Date { get; set; }
    public string Breed { get; set; } = "Mixed";
    public decimal Liters { get; set; }
    public string? Comment { get; set; }
}

public class MilkSale : BaseEntity
{
    public DateOnly Date { get; set; }
    public decimal Liters { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
    public string? Comment { get; set; }
}

public class MilkWaste : BaseEntity
{
    public DateOnly Date { get; set; }
    public decimal Liters { get; set; }
    public string? Notes { get; set; }
}

public class Vaccine : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public VaccineScope Scope { get; set; }
    public VaccineRuleType RuleType { get; set; }
    public int? Days { get; set; }
    public int? Months { get; set; }
    public ICollection<VaccinationHistory> VaccinationHistories { get; set; } = [];
}

public class VaccinationHistory : BaseEntity
{
    public int GoatId { get; set; }
    public Goat Goat { get; set; } = null!;
    public int VaccineId { get; set; }
    public Vaccine Vaccine { get; set; } = null!;
    public DateOnly VaccinationDate { get; set; }
}

public class Reminder : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public VaccineScope Scope { get; set; }
    public DateOnly ReminderDate { get; set; }
}

public class AppSetting : BaseEntity
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

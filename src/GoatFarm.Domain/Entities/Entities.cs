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
    /// <summary>Optional, bought goats only. Born goats use EventDate as the birth date.</summary>
    public DateOnly? DateOfBirth { get; set; }
    public bool DateOfBirthIsEstimated { get; set; }
    public int? GroupId { get; set; }
    public GoatGroup? Group { get; set; }
    public DateOnly? PrepCrossDate { get; set; }
    public DateOnly? MatedDate { get; set; }
    public string? BuckTag { get; set; }
    public int? KidsCount { get; set; }
    public DateOnly? UltrasoundDate { get; set; }
    public bool IsArchived { get; set; }
    public string? ArchivedReason { get; set; }
    public DateOnly? ArchivedDate { get; set; }
    public string? Owner { get; set; }
    public GoatHealthState HealthState { get; set; }
    public BreedingStage BreedingStage { get; set; }
    public int? SireId { get; set; }
    public Goat? Sire { get; set; }
    public int? DamId { get; set; }
    public Goat? Dam { get; set; }
    /// <summary>Parent tag when the parent is not (or no longer) registered in the herd.</summary>
    public string? SireTagText { get; set; }
    public string? DamTagText { get; set; }
    public bool IsTemporaryTag { get; set; }
    public int? KiddingRecordId { get; set; }
    public KiddingRecord? KiddingRecord { get; set; }
    public ICollection<VaccinationHistory> VaccinationHistories { get; set; } = [];
    public ICollection<GoatWeightRecord> WeightRecords { get; set; } = [];
}

public class BreedingEvent : BaseEntity
{
    public int DoeId { get; set; }
    public Goat Doe { get; set; } = null!;
    public int? BuckId { get; set; }
    public Goat? Buck { get; set; }
    public string? BuckTagText { get; set; }
    public DateOnly MatedDate { get; set; }
    public DateOnly? ScanDate { get; set; }
    public int? ScanKids { get; set; }
    public BreedingOutcome Outcome { get; set; }
    public DateOnly? OutcomeDate { get; set; }
    public string? Notes { get; set; }
}

public class KiddingRecord : BaseEntity
{
    public int DoeId { get; set; }
    public Goat Doe { get; set; } = null!;
    public int? BreedingEventId { get; set; }
    public BreedingEvent? BreedingEvent { get; set; }
    public DateOnly KiddingDate { get; set; }
    public int LiveKids { get; set; }
    public int Stillborn { get; set; }
    public KiddingEase Ease { get; set; }
    public string? Notes { get; set; }
    /// <summary>Client-generated key so a resubmitted birth form cannot create kids twice.</summary>
    public Guid ClientRequestId { get; set; }
    public ICollection<Goat> Kids { get; set; } = [];
}

public class GoatWeightRecord : BaseEntity
{
    public int GoatId { get; set; }
    public Goat Goat { get; set; } = null!;
    public DateOnly Date { get; set; }
    public decimal Kg { get; set; }
}

public class DeathRecord : BaseEntity
{
    public int GoatId { get; set; }
    public string Tag { get; set; } = string.Empty;
    public string? Breed { get; set; }
    public DateOnly Date { get; set; }
    public string? Reason { get; set; }
    public int? AgeDays { get; set; }
    public decimal ValueLost { get; set; }
}

public class FeedUsageRecord : BaseEntity
{
    public DateOnly Date { get; set; }
    public string FeedType { get; set; } = string.Empty;
    public decimal Kg { get; set; }
    public bool IsAutomatic { get; set; }
    public bool IsStockCheck { get; set; }
}

public class FeedPrice : BaseEntity
{
    public string FeedType { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public decimal PricePerKg { get; set; }
    public decimal StockKg { get; set; }
    public decimal StockKgFull { get; set; }
}

public class FeedPlan : BaseEntity
{
    public GoatStatus StatusKey { get; set; }
    public decimal MixKgPerDay { get; set; }
    public decimal FodderKgPerDay { get; set; }
    public decimal FodderDryKgPerDay { get; set; }
    public decimal MedicineCostPerGoatPerMonth { get; set; }
    public ICollection<FeedPlanItem> Items { get; set; } = [];
}

public class Employee : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Role { get; set; }
    public decimal MonthlySalary { get; set; }
}

public class BreedingEmptyLog : BaseEntity
{
    public int GoatId { get; set; }
    public Goat Goat { get; set; } = null!;
    public DateOnly ScanDate { get; set; }
    public DateOnly? MatedDate { get; set; }
    public string? BuckTag { get; set; }
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

public class FarmTask : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public FarmTaskType Type { get; set; }
    public FarmTaskPriority Priority { get; set; }
    public FarmTaskStatus Status { get; set; }
    public DateOnly? DueDate { get; set; }
    /// <summary>AspNetUsers.Id; null means "everyone".</summary>
    public string? AssigneeUserId { get; set; }
    /// <summary>Null once the creating user has been deleted.</summary>
    public string? CreatedByUserId { get; set; }
    public int? GoatId { get; set; }
    public Goat? Goat { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}

public class AppSetting : BaseEntity
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

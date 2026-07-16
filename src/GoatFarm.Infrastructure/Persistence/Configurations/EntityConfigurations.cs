using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoatFarm.Infrastructure.Persistence.Configurations;

public class GoatConfiguration : IEntityTypeConfiguration<Goat>
{
    public void Configure(EntityTypeBuilder<Goat> builder)
    {
        builder.ToTable("Goats");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Tag).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Name).HasMaxLength(100);
        builder.Property(x => x.Comment).HasMaxLength(500);
        builder.Property(x => x.Breed).IsRequired().HasMaxLength(100);
        builder.Property(x => x.PurchasePrice).HasPrecision(18, 2);
        builder.HasIndex(x => x.Tag).IsUnique();
        builder.HasOne(x => x.Group)
            .WithMany(x => x.Goats)
            .HasForeignKey(x => x.GroupId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class GoatGroupConfiguration : IEntityTypeConfiguration<GoatGroup>
{
    public void Configure(EntityTypeBuilder<GoatGroup> builder)
    {
        builder.ToTable("GoatGroups");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.HasIndex(x => x.Name).IsUnique();
    }
}

public class FeedPriceConfiguration : IEntityTypeConfiguration<FeedPrice>
{
    public void Configure(EntityTypeBuilder<FeedPrice> builder)
    {
        builder.ToTable("FeedPrices");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FeedType).IsRequired().HasMaxLength(50);
        builder.Property(x => x.DisplayName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.PricePerKg).HasPrecision(18, 2);
        builder.HasIndex(x => x.FeedType).IsUnique();
    }
}

public class FeedPlanConfiguration : IEntityTypeConfiguration<FeedPlan>
{
    public void Configure(EntityTypeBuilder<FeedPlan> builder)
    {
        builder.ToTable("FeedPlans");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.MedicineCostPerGoatPerMonth).HasPrecision(18, 2);
        builder.HasIndex(x => x.StatusKey).IsUnique();
        builder.HasMany(x => x.Items)
            .WithOne(x => x.FeedPlan)
            .HasForeignKey(x => x.FeedPlanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class FeedPlanItemConfiguration : IEntityTypeConfiguration<FeedPlanItem>
{
    public void Configure(EntityTypeBuilder<FeedPlanItem> builder)
    {
        builder.ToTable("FeedPlanItems");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FeedType).IsRequired().HasMaxLength(50);
        builder.HasIndex(x => new { x.FeedPlanId, x.FeedType }).IsUnique();
    }
}

public class FeedPurchaseConfiguration : IEntityTypeConfiguration<FeedPurchase>
{
    public void Configure(EntityTypeBuilder<FeedPurchase> builder)
    {
        builder.ToTable("FeedPurchases");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FeedType).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Kg).HasPrecision(18, 2);
        builder.Property(x => x.RatePerKg).HasPrecision(18, 2);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Comment).HasMaxLength(500);
    }
}

public class RecurringCostConfiguration : IEntityTypeConfiguration<RecurringCost>
{
    public void Configure(EntityTypeBuilder<RecurringCost> builder)
    {
        builder.ToTable("RecurringCosts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
    }
}

public class VaccinePurchaseConfiguration : IEntityTypeConfiguration<VaccinePurchase>
{
    public void Configure(EntityTypeBuilder<VaccinePurchase> builder)
    {
        builder.ToTable("VaccinePurchases");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Qty).HasPrecision(18, 2);
        builder.Property(x => x.Unit).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Comment).HasMaxLength(500);
    }
}

public class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    public void Configure(EntityTypeBuilder<Asset> builder)
    {
        builder.ToTable("Assets");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Type).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Cost).HasPrecision(18, 2);
        builder.Property(x => x.Comment).HasMaxLength(500);
    }
}

public class IncomeConfiguration : IEntityTypeConfiguration<Income>
{
    public void Configure(EntityTypeBuilder<Income> builder)
    {
        builder.ToTable("Incomes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Type).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Comment).HasMaxLength(500);
    }
}

public class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.ToTable("Expenses");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Type).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Comment).HasMaxLength(500);
    }
}

public class OwnerInvestmentConfiguration : IEntityTypeConfiguration<OwnerInvestment>
{
    public void Configure(EntityTypeBuilder<OwnerInvestment> builder)
    {
        builder.ToTable("OwnerInvestments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Note).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
    }
}

public class MilkProductionConfiguration : IEntityTypeConfiguration<MilkProduction>
{
    public void Configure(EntityTypeBuilder<MilkProduction> builder)
    {
        builder.ToTable("MilkProductions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Breed).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Liters).HasPrecision(18, 2);
        builder.Property(x => x.Comment).HasMaxLength(500);
    }
}

public class MilkSaleConfiguration : IEntityTypeConfiguration<MilkSale>
{
    public void Configure(EntityTypeBuilder<MilkSale> builder)
    {
        builder.ToTable("MilkSales");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Liters).HasPrecision(18, 2);
        builder.Property(x => x.Rate).HasPrecision(18, 2);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Comment).HasMaxLength(500);
    }
}

public class MilkWasteConfiguration : IEntityTypeConfiguration<MilkWaste>
{
    public void Configure(EntityTypeBuilder<MilkWaste> builder)
    {
        builder.ToTable("MilkWastes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Liters).HasPrecision(18, 2);
        builder.Property(x => x.Notes).HasMaxLength(500);
    }
}

public class VaccineConfiguration : IEntityTypeConfiguration<Vaccine>
{
    public void Configure(EntityTypeBuilder<Vaccine> builder)
    {
        builder.ToTable("Vaccines");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
    }
}

public class VaccinationHistoryConfiguration : IEntityTypeConfiguration<VaccinationHistory>
{
    public void Configure(EntityTypeBuilder<VaccinationHistory> builder)
    {
        builder.ToTable("VaccinationHistories");
        builder.HasKey(x => x.Id);
        builder.HasOne(x => x.Goat)
            .WithMany(x => x.VaccinationHistories)
            .HasForeignKey(x => x.GoatId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Vaccine)
            .WithMany(x => x.VaccinationHistories)
            .HasForeignKey(x => x.VaccineId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.GoatId, x.VaccineId, x.VaccinationDate });
    }
}

public class ReminderConfiguration : IEntityTypeConfiguration<Reminder>
{
    public void Configure(EntityTypeBuilder<Reminder> builder)
    {
        builder.ToTable("Reminders");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(200);
    }
}

public class AppSettingConfiguration : IEntityTypeConfiguration<AppSetting>
{
    public void Configure(EntityTypeBuilder<AppSetting> builder)
    {
        builder.ToTable("AppSettings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Key).IsRequired().HasMaxLength(100);
        builder.HasIndex(x => x.Key).IsUnique();
    }
}

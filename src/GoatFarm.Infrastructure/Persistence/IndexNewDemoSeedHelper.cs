using System.Text.Json;
using GoatFarm.Application.Common;
using GoatFarm.Domain.Constants;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Infrastructure.Persistence;

/// <summary>Sample farm rows matching <c>index_new.html</c> <c>seed()</c> (tags 201–311, B-12, etc.).</summary>
public static class IndexNewDemoSeedHelper
{
    public static DateOnly DaysAgo(int n) => DateOnly.FromDateTime(DateTime.Today).AddDays(-n);

    public static DateOnly MonthDate(int day)
    {
        var t = DateTime.Today;
        return new DateOnly(t.Year, t.Month, Math.Clamp(day, 1, DateTime.DaysInMonth(t.Year, t.Month)));
    }

    public static async Task<bool> SeedHerdAsync(GoatFarmDbContext context, CancellationToken cancellationToken = default)
    {
        if (await context.Goats.AnyAsync(cancellationToken))
            return false;

        void Add(
            string tag, string breed, GoatGender gender, GoatStatus status, GoatSource source, int daysAgoEvent,
            decimal price = 0, string? name = null,
            DateOnly? prepCross = null, DateOnly? mated = null, string? buck = null, int? kids = null, DateOnly? us = null)
        {
            context.Goats.Add(new Goat
            {
                Tag = tag,
                Name = name,
                Breed = breed,
                Gender = gender,
                Status = status,
                Source = source,
                EventDate = DaysAgo(daysAgoEvent),
                PurchasePrice = price,
                HealthState = GoatHealthState.Healthy,
                PrepCrossDate = prepCross,
                MatedDate = mated,
                BuckTag = buck,
                KidsCount = kids,
                UltrasoundDate = us,
                BreedingStage = mated.HasValue
                    ? (kids > 0 ? BreedingStage.ConfirmedPregnant : BreedingStage.ScanPending)
                    : prepCross.HasValue ? BreedingStage.Ready : BreedingStage.None
            });
        }

        Add("201", "Beetal", GoatGender.Female, GoatStatus.Milking, GoatSource.Bought, 900, 38000);
        Add("202", "Beetal", GoatGender.Female, GoatStatus.Pregnant, GoatSource.Bought, 720, 42000,
            mated: DaysAgo(100), buck: "B-12", kids: 2, us: DaysAgo(50));
        Add("203", "Makhee Cheeni", GoatGender.Female, GoatStatus.Milking, GoatSource.Bought, 640, 45000);
        Add("204", "Makhee Cheeni", GoatGender.Female, GoatStatus.Pregnant, GoatSource.Bought, 810, 40000,
            mated: DaysAgo(60), buck: "B-12");
        Add("205", "Beetal", GoatGender.Female, GoatStatus.Pregnant, GoatSource.Bought, 690, 39000,
            mated: DaysAgo(125), buck: "Sultan");
        Add("B-12", "Beetal", GoatGender.Male, GoatStatus.Buck, GoatSource.Bought, 1100, 65000, name: "Sultan");
        Add("305", "Beetal", GoatGender.Female, GoatStatus.Kid, GoatSource.Born, 45);
        Add("306", "Beetal", GoatGender.Male, GoatStatus.Kid, GoatSource.Born, 52);
        Add("307", "Makhee Cheeni", GoatGender.Female, GoatStatus.Kid, GoatSource.Born, 68);
        Add("308", "Makhee Cheeni", GoatGender.Female, GoatStatus.Kid, GoatSource.Born, 75);
        Add("309", "Beetal", GoatGender.Male, GoatStatus.Kid, GoatSource.Born, 120);
        Add("310", "Beetal", GoatGender.Female, GoatStatus.Milking, GoatSource.Bought, 500, 41000);
        Add("311", "Makhee Cheeni", GoatGender.Female, GoatStatus.Dry, GoatSource.Bought, 400, 30000,
            prepCross: DaysAgo(24));

        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>Feed stock, fodder land pool, and auto-usage — matches <c>index_new.html</c> <c>seed()</c> tail.</summary>
    public static async Task ApplyIndexNewFeedSettingsAsync(GoatFarmDbContext context, CancellationToken cancellationToken = default)
    {
        var stockByType = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["wanda"] = 150,
            ["binola"] = 80,
            ["sarson"] = 0,
            ["bran"] = 120,
            ["maize"] = 60,
            ["sheera"] = 40,
            ["fodderdry"] = 200
        };

        foreach (var row in await context.FeedPrices.ToListAsync(cancellationToken))
        {
            if (!stockByType.TryGetValue(row.FeedType, out var kg)) continue;
            row.StockKg = kg;
            row.StockKgFull = kg;
        }

        var pool = new FodderPoolDto
        {
            Acres = 3,
            Mode = "share",
            Items =
            [
                new() { Id = "rent", Label = "Land rent", Amount = 240_000 },
                new() { Id = "seeding", Label = "Seeding & ploughing", Amount = 35_000 },
                new() { Id = "cutting", Label = "Cutting & labour", Amount = 60_000 },
                new() { Id = "water", Label = "Irrigation / water", Amount = 24_000 },
                new() { Id = "fert", Label = "Fertiliser (urea/DAP)", Amount = 28_000 }
            ]
        };

        await UpsertAppSettingJsonAsync(context, AppSettingKeys.FodderPool, pool, cancellationToken);

        var feedSettings = new FeedSettingsDto
        {
            AutoUsage = true,
            LastUsageDate = DaysAgo(1).ToString("yyyy-MM-dd")
        };
        await UpsertAppSettingJsonAsync(context, AppSettingKeys.FeedSettings, feedSettings, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task UpsertAppSettingJsonAsync<T>(
        GoatFarmDbContext context, string key, T value, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(value);
        var row = await context.AppSettings.FirstOrDefaultAsync(s => s.Key == key, cancellationToken);
        if (row is null)
            context.AppSettings.Add(new AppSetting { Key = key, Value = json });
        else
            row.Value = json;
    }

    public static async Task SeedMilkAsync(GoatFarmDbContext context, CancellationToken cancellationToken = default)
    {
        if (await context.MilkProductions.AnyAsync(cancellationToken))
            return;

        context.MilkProductions.AddRange(
            new MilkProduction { Date = DaysAgo(0), Breed = "Mixed", Liters = 186, Comment = "Demo" },
            new MilkProduction { Date = DaysAgo(1), Breed = "Mixed", Liters = 190, Comment = "Demo" },
            new MilkProduction { Date = DaysAgo(2), Breed = "Beetal", Liters = 120, Comment = "Demo" },
            new MilkProduction { Date = DaysAgo(2), Breed = "Makhee Cheeni", Liters = 68, Comment = "Demo" },
            new MilkProduction { Date = DaysAgo(3), Breed = "Mixed", Liters = 188, Comment = "Demo" },
            new MilkProduction { Date = DaysAgo(4), Breed = "Mixed", Liters = 182, Comment = "Demo" });

        context.MilkSales.AddRange(
            new MilkSale { Date = MonthDate(6), Liters = 500, Rate = 150, Amount = 75000, Comment = "Demo" },
            new MilkSale { Date = MonthDate(12), Liters = 300, Rate = 150, Amount = 45000, Comment = "Demo" });
    }

    public static async Task SeedFinanceAndOpsAsync(GoatFarmDbContext context, CancellationToken cancellationToken = default)
    {
        if (!await context.Assets.AnyAsync(cancellationToken))
        {
            context.Assets.AddRange(
                new Asset { Name = "Chaff cutter", Type = "Machinery", Cost = 55000, PurchaseDate = MonthDate(1) },
                new Asset { Name = "Milking & cooling", Type = "Machinery", Cost = 40000, PurchaseDate = MonthDate(1) },
                new Asset { Name = "Sheds & fencing", Type = "Buildings/Sheds", Cost = 250000, PurchaseDate = MonthDate(1) });
        }

        if (!await context.Incomes.AnyAsync(cancellationToken))
        {
            context.Incomes.AddRange(
                new Income { Type = "Ghee", Amount = 25000, Date = MonthDate(10), Comment = "Demo" },
                new Income { Type = "Live goat sale", Amount = 30000, Date = MonthDate(15), Comment = "Demo" });
        }

        if (!await context.Expenses.AnyAsync(cancellationToken))
        {
            context.Expenses.AddRange(
                new Expense { Type = "Cultivation (fodder)", Amount = 18000, Date = MonthDate(3), Comment = "Demo" },
                new Expense { Type = "Utilities", Amount = 8000, Date = MonthDate(7), Comment = "Demo" });
        }

        if (!await context.OwnerInvestments.AnyAsync(cancellationToken))
        {
            context.OwnerInvestments.AddRange(
                new OwnerInvestment { Note = "Startup — buying goats & sheds", Amount = 500000, Date = MonthDate(1) },
                new OwnerInvestment { Note = "Monthly top-up", Amount = 60000, Date = MonthDate(4) });
        }

        if (!await context.Employees.AnyAsync(cancellationToken))
        {
            context.Employees.AddRange(
                new Employee { Name = "Aslam", Role = "Herd worker", MonthlySalary = 25000 },
                new Employee { Name = "Bilal", Role = "Milking & feeding", MonthlySalary = 22000 });
        }

        if (!await context.RecurringCosts.AnyAsync(cancellationToken))
        {
            context.RecurringCosts.Add(new RecurringCost
            {
                Name = "Land rent (cultivated land)", Amount = 240000, Period = RecurringCostPeriod.Year
            });
        }

        if (!await context.FeedPurchases.AnyAsync(cancellationToken))
        {
            context.FeedPurchases.AddRange(
                new FeedPurchase
                {
                    Date = MonthDate(2), FeedType = "wanda", Kg = 200, RatePerKg = 90, Amount = 18000,
                    Comment = "monthly stock"
                },
                new FeedPurchase
                {
                    Date = MonthDate(8), FeedType = "binola", Kg = 100, RatePerKg = 95, Amount = 9500, Comment = ""
                });
        }

        if (!await context.VaccinePurchases.AnyAsync(cancellationToken))
        {
            context.VaccinePurchases.Add(new VaccinePurchase
            {
                Date = MonthDate(4), Name = "PPR", Qty = 1, Unit = "Box", Amount = 3500, PaidAmount = 3500,
                PaymentStatus = "paid", Comment = "50 doses"
            });
        }

        if (!await context.Reminders.AnyAsync(cancellationToken))
        {
            context.Reminders.AddRange(
                new Reminder { Title = "Hoof trimming", Scope = VaccineScope.All, ReminderDate = DaysAgo(-20) },
                new Reminder
                {
                    Title = "Pregnancy check (ultrasound)", Scope = VaccineScope.Pregnant, ReminderDate = DaysAgo(-8)
                });
        }

        await SeedSampleVaccinationsAsync(context, cancellationToken);
    }

    private static async Task SeedSampleVaccinationsAsync(GoatFarmDbContext context, CancellationToken cancellationToken)
    {
        if (await context.VaccinationHistories.AnyAsync(cancellationToken))
            return;

        var ppr = await context.Vaccines.FirstOrDefaultAsync(v => v.Name.StartsWith("PPR"), cancellationToken);
        if (ppr is null) return;

        var tags = new[] { "201", "202", "203" };
        foreach (var tag in tags)
        {
            var goat = await context.Goats.FirstOrDefaultAsync(g => g.Tag == tag, cancellationToken);
            if (goat is null) continue;
            context.VaccinationHistories.Add(new VaccinationHistory
            {
                GoatId = goat.Id,
                VaccineId = ppr.Id,
                VaccinationDate = DaysAgo(40)
            });
        }
    }
}

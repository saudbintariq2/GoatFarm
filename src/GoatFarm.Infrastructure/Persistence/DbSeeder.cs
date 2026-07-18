using GoatFarm.Application.Interfaces;
using GoatFarm.Domain.Constants;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Infrastructure.Persistence;

public static class DbSeeder
{
    public static async Task SeedAsync(GoatFarmDbContext context, CancellationToken cancellationToken = default)
    {
        if (await context.Goats.AnyAsync(cancellationToken)) return;

        var groups = new[]
        {
            new GoatGroup { Name = "Lactating kids" },
            new GoatGroup { Name = "Weaned kids" },
            new GoatGroup { Name = "Sale lot" }
        };
        context.GoatGroups.AddRange(groups);
        await context.SaveChangesAsync();

        foreach (var (key, name) in FeedTypes.All)
        {
            context.FeedPrices.Add(new FeedPrice
            {
                FeedType = key,
                DisplayName = name,
                PricePerKg = key switch
                {
                    "wanda" => 90,
                    "binola" => 95,
                    "sarson" => 85,
                    "bran" => 60,
                    "maize" => 75,
                    "sheera" => 50,
                    "fodder" => 15,
                    _ => 0
                },
                StockKg = key switch
                {
                    "wanda" => 150,
                    "binola" => 80,
                    "sarson" => 0,
                    "bran" => 120,
                    "maize" => 60,
                    "sheera" => 40,
                    "fodder" => 0,
                    _ => 0
                }
            });
        }

        var planData = new Dictionary<GoatStatus, (int med, Dictionary<string, int> rations)>
        {
            [GoatStatus.Kid] = (50, new() { ["wanda"] = 150, ["fodder"] = 500 }),
            [GoatStatus.Milking] = (80, new() { ["wanda"] = 500, ["binola"] = 200, ["bran"] = 200, ["maize"] = 100, ["sheera"] = 50, ["fodder"] = 2000 }),
            [GoatStatus.Pregnant] = (150, new() { ["wanda"] = 400, ["binola"] = 150, ["bran"] = 150, ["maize"] = 100, ["sheera"] = 50, ["fodder"] = 1500 }),
            [GoatStatus.Dry] = (40, new() { ["wanda"] = 200, ["bran"] = 100, ["fodder"] = 1500 }),
            [GoatStatus.Buck] = (60, new() { ["wanda"] = 400, ["binola"] = 100, ["bran"] = 100, ["maize"] = 100, ["fodder"] = 1500 }),
            [GoatStatus.Sale] = (30, new() { ["wanda"] = 250, ["bran"] = 100, ["fodder"] = 1500 })
        };

        foreach (var (status, data) in planData)
        {
            var plan = new FeedPlan { StatusKey = status, MedicineCostPerGoatPerMonth = data.med };
            foreach (var f in FeedTypes.All)
            {
                plan.Items.Add(new FeedPlanItem
                {
                    FeedType = f.Key,
                    GramsPerDay = data.rations.GetValueOrDefault(f.Key, 0)
                });
            }
            context.FeedPlans.Add(plan);
        }

        context.AppSettings.Add(new AppSetting { Key = "RemindDays", Value = "30" });

        var goats = new List<Goat>
        {
            NewGoat("201", "Beetal", GoatGender.Female, GoatStatus.Milking, GoatSource.Bought, 38000, 900),
            NewGoat("202", "Beetal", GoatGender.Female, GoatStatus.Pregnant, GoatSource.Bought, 42000, 720),
            NewGoat("203", "Makhee Cheeni", GoatGender.Female, GoatStatus.Milking, GoatSource.Bought, 45000, 640),
            NewGoat("204", "Makhee Cheeni", GoatGender.Female, GoatStatus.Pregnant, GoatSource.Bought, 40000, 810),
            NewGoat("205", "Beetal", GoatGender.Female, GoatStatus.Pregnant, GoatSource.Bought, 39000, 690),
            NewGoat("B-12", "Beetal", GoatGender.Male, GoatStatus.Buck, GoatSource.Bought, 65000, 1100, "Sultan"),
            NewGoat("305", "Beetal", GoatGender.Female, GoatStatus.Kid, GoatSource.Born, 0, 45),
            NewGoat("306", "Beetal", GoatGender.Male, GoatStatus.Kid, GoatSource.Born, 0, 52),
            NewGoat("307", "Makhee Cheeni", GoatGender.Female, GoatStatus.Kid, GoatSource.Born, 0, 68),
            NewGoat("308", "Makhee Cheeni", GoatGender.Female, GoatStatus.Kid, GoatSource.Born, 0, 75),
            NewGoat("309", "Beetal", GoatGender.Male, GoatStatus.Kid, GoatSource.Born, 0, 120),
            NewGoat("310", "Beetal", GoatGender.Female, GoatStatus.Milking, GoatSource.Bought, 41000, 500),
            NewGoat("311", "Makhee Cheeni", GoatGender.Female, GoatStatus.Dry, GoatSource.Bought, 30000, 400)
        };
        context.Goats.AddRange(goats);
        await context.SaveChangesAsync(cancellationToken);

        goats[1].MatedDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-100));
        goats[1].BuckTag = "B-12";
        goats[1].UltrasoundDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-50));
        goats[1].KidsCount = 2;
        goats[3].MatedDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-60));
        goats[3].BuckTag = "B-12";
        goats[4].MatedDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-125));
        goats[4].BuckTag = "Sultan";
        goats[11].PrepCrossDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30));

        context.Assets.AddRange(
            new Asset { Name = "Chaff cutter", Type = "Machinery", Cost = 55000 },
            new Asset { Name = "Milking & cooling", Type = "Machinery", Cost = 40000 },
            new Asset { Name = "Sheds & fencing", Type = "Buildings/Sheds", Cost = 250000 }
        );

        var today = DateTime.Today;
        context.Incomes.AddRange(
            new Income { Type = "Ghee", Amount = 25000, Date = new DateOnly(today.Year, today.Month, 10) },
            new Income { Type = "Live goat sale", Amount = 30000, Date = new DateOnly(today.Year, today.Month, 15) }
        );
        context.Expenses.AddRange(
            new Expense { Type = "Salaries", Amount = 45000, Date = new DateOnly(today.Year, today.Month, 1) },
            new Expense { Type = "Cultivation (fodder)", Amount = 18000, Date = new DateOnly(today.Year, today.Month, 3) },
            new Expense { Type = "Utilities", Amount = 8000, Date = new DateOnly(today.Year, today.Month, 7) }
        );
        context.OwnerInvestments.AddRange(
            new OwnerInvestment { Note = "Startup — buying goats & sheds", Amount = 500000, Date = new DateOnly(today.Year, today.Month, 1) },
            new OwnerInvestment { Note = "Monthly top-up", Amount = 60000, Date = new DateOnly(today.Year, today.Month, 4) }
        );

        var vET = new Vaccine { Name = "Enterotoxaemia (ET)", Scope = VaccineScope.Kid, RuleType = VaccineRuleType.Age, Days = 30 };
        var vPPR = new Vaccine { Name = "PPR", Scope = VaccineScope.All, RuleType = VaccineRuleType.Repeat, Months = 12 };
        var vFMD = new Vaccine { Name = "FMD", Scope = VaccineScope.All, RuleType = VaccineRuleType.Repeat, Months = 6 };
        var vCCPP = new Vaccine { Name = "CCPP", Scope = VaccineScope.All, RuleType = VaccineRuleType.Repeat, Months = 12 };
        var vETB = new Vaccine { Name = "ET booster (pre-kidding)", Scope = VaccineScope.Pregnant, RuleType = VaccineRuleType.Repeat, Months = 6 };
        var vDW = new Vaccine { Name = "Deworming", Scope = VaccineScope.All, RuleType = VaccineRuleType.Repeat, Months = 3 };
        context.Vaccines.AddRange(vET, vPPR, vFMD, vCCPP, vETB, vDW);
        await context.SaveChangesAsync();

        context.VaccinationHistories.AddRange(
            new VaccinationHistory { GoatId = goats[0].Id, VaccineId = vPPR.Id, VaccinationDate = DateOnly.FromDateTime(today.AddDays(-40)) },
            new VaccinationHistory { GoatId = goats[1].Id, VaccineId = vPPR.Id, VaccinationDate = DateOnly.FromDateTime(today.AddDays(-40)) },
            new VaccinationHistory { GoatId = goats[2].Id, VaccineId = vPPR.Id, VaccinationDate = DateOnly.FromDateTime(today.AddDays(-40)) }
        );

        context.Reminders.AddRange(
            new Reminder { Title = "Hoof trimming", Scope = VaccineScope.None, ReminderDate = DateOnly.FromDateTime(today.AddDays(20)) },
            new Reminder { Title = "Pregnancy check (ultrasound)", Scope = VaccineScope.Pregnant, ReminderDate = DateOnly.FromDateTime(today.AddDays(8)) }
        );

        context.MilkProductions.AddRange(
            new MilkProduction { Date = DateOnly.FromDateTime(today), Breed = "Mixed", Liters = 186 },
            new MilkProduction { Date = DateOnly.FromDateTime(today.AddDays(-1)), Breed = "Mixed", Liters = 190 },
            new MilkProduction { Date = DateOnly.FromDateTime(today.AddDays(-2)), Breed = "Beetal", Liters = 120 },
            new MilkProduction { Date = DateOnly.FromDateTime(today.AddDays(-2)), Breed = "Makhee Cheeni", Liters = 68 },
            new MilkProduction { Date = DateOnly.FromDateTime(today.AddDays(-3)), Breed = "Mixed", Liters = 188 },
            new MilkProduction { Date = DateOnly.FromDateTime(today.AddDays(-4)), Breed = "Mixed", Liters = 182 }
        );

        context.MilkSales.AddRange(
            new MilkSale { Date = new DateOnly(today.Year, today.Month, 6), Liters = 500, Rate = 150, Amount = 75000 },
            new MilkSale { Date = new DateOnly(today.Year, today.Month, 12), Liters = 300, Rate = 150, Amount = 45000 }
        );

        context.MilkWastes.AddRange(
            new MilkWaste { Date = new DateOnly(today.Year, today.Month, 10), Liters = 12, Notes = "Spoiled — not cooled in time" },
            new MilkWaste { Date = new DateOnly(today.Year, today.Month, 15), Liters = 8, Notes = "Leftover unsold from morning collection" }
        );

        await context.SaveChangesAsync(cancellationToken);
    }

    private static Goat NewGoat(string tag, string breed, GoatGender gender, GoatStatus status, GoatSource source, decimal price, int daysAgo, string? name = null) =>
        new()
        {
            Tag = tag,
            Name = name,
            Breed = breed,
            Gender = gender,
            Status = status,
            Source = source,
            PurchasePrice = price,
            EventDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-daysAgo))
        };
}

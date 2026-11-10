using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;

namespace GoatFarm.Tests;

public class Phase6DashboardTests
{
    [Fact]
    public async Task Dashboard_HerdComposition_GroupsByRole()
    {
        var (_, stats, db) = Phase5DashboardFinanceTests.Create();
        db.Goats.AddRange(
            new Goat { Tag = "D1", Status = GoatStatus.Milking, Gender = GoatGender.Female, EventDate = TestDb.Today },
            new Goat { Tag = "B1", Status = GoatStatus.Buck, Gender = GoatGender.Male, EventDate = TestDb.Today },
            new Goat { Tag = "K1", Status = GoatStatus.Kid, Gender = GoatGender.Female, EventDate = TestDb.Today },
            new Goat { Tag = "S1", Status = GoatStatus.Sale, Gender = GoatGender.Female, EventDate = TestDb.Today });
        await db.SaveChangesAsync();

        var dash = await stats.GetDashboardAsync(null);
        Assert.Equal(1, dash.HerdComposition.First(s => s.Name == "Does").Count);
        Assert.Equal(1, dash.HerdComposition.First(s => s.Name == "Bucks").Count);
        Assert.Equal(1, dash.HerdComposition.First(s => s.Name == "Kids").Count);
        Assert.Equal(1, dash.HerdComposition.First(s => s.Name == "For sale").Count);
        Assert.Equal(6, dash.SixMonthTrend.Count);
    }

    [Fact]
    public async Task Dashboard_SixMonthTrend_IncludesMilkAndIncome()
    {
        var (_, stats, db) = Phase5DashboardFinanceTests.Create();
        var month = $"{TestDb.Today.Year:0000}-{TestDb.Today.Month:00}";
        db.MilkProductions.Add(new MilkProduction { Date = TestDb.Today, Liters = 40 });
        db.Incomes.Add(new Income { Type = "Goat sale", Amount = 5000, Date = TestDb.Today });
        await db.SaveChangesAsync();

        var dash = await stats.GetDashboardAsync(month);
        var current = dash.SixMonthTrend.Last();
        Assert.Equal(40, current.MilkLiters);
        Assert.Equal(5000, current.MoneyIn);
    }

    [Fact]
    public async Task Dashboard_RecentGoats_OrderedByEventDate()
    {
        var (_, stats, db) = Phase5DashboardFinanceTests.Create();
        db.Goats.AddRange(
            new Goat { Tag = "OLD", EventDate = TestDb.Today.AddDays(-30) },
            new Goat { Tag = "NEW", EventDate = TestDb.Today });
        await db.SaveChangesAsync();

        var dash = await stats.GetDashboardAsync(null);
        Assert.Equal("NEW", dash.RecentGoats[0].Tag);
    }
}

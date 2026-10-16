using GoatFarm.Domain.Entities;
using GoatFarm.Infrastructure.Services;

namespace GoatFarm.Tests.Assistant;

public class MilkRangeQueryTests
{
    [Fact]
    public async Task QueryRange_UsesDateFilter_NotAllHistoryPaging()
    {
        var db = TestDb.Create();
        for (var i = 0; i < 30; i++)
        {
            db.MilkProductions.Add(new MilkProduction
            {
                Date = new DateOnly(2026, 10, 1).AddDays(i % 15),
                Breed = "Beetal",
                Liters = 10
            });
        }

        db.MilkProductions.Add(new MilkProduction { Date = new DateOnly(2026, 9, 5), Breed = "Beetal", Liters = 100 });
        db.MilkProductions.Add(new MilkProduction { Date = new DateOnly(2026, 9, 20), Breed = "Mixed", Liters = 200 });
        db.MilkSales.Add(new MilkSale { Date = new DateOnly(2026, 9, 8), Liters = 50, Rate = 20, Amount = 1000 });
        db.SaveChanges();

        var result = await new MilkService(db).QueryRangeAsync(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30));

        Assert.Equal(300, result.LitersProduced);
        Assert.Equal(50, result.LitersSold);
        Assert.Equal(2, result.ProductionCount);
        Assert.Equal(2, result.Productions.Count);
        Assert.All(result.Productions, p => Assert.Equal(9, p.Date.Month));
    }

    [Fact]
    public async Task QueryRange_IncludesBothBoundaryDays_AndExcludesTheNextDay()
    {
        var db = TestDb.Create();
        db.MilkProductions.AddRange(
            new MilkProduction { Date = new DateOnly(2026, 8, 31), Breed = "Beetal", Liters = 9 },
            new MilkProduction { Date = new DateOnly(2026, 9, 1), Breed = "Beetal", Liters = 10 },
            new MilkProduction { Date = new DateOnly(2026, 9, 30), Breed = "Beetal", Liters = 20 },
            new MilkProduction { Date = new DateOnly(2026, 10, 1), Breed = "Beetal", Liters = 40 });
        db.SaveChanges();

        var result = await new MilkService(db).QueryRangeAsync(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30));

        Assert.Equal(30, result.LitersProduced);
        Assert.Equal(2, result.ProductionCount);
    }
}

using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure.Persistence;
using GoatFarm.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GoatFarm.Tests;

/// <summary>Counts entity instances EF builds, to prove a page query does not load the whole herd.</summary>
internal sealed class GoatMaterializationCounter : IMaterializationInterceptor
{
    private int _count;
    public int Count => _count;
    public void Reset() => _count = 0;

    public object InitializedInstance(MaterializationInterceptionData materializationData, object entity)
    {
        if (entity is Goat) Interlocked.Increment(ref _count);
        return entity;
    }
}

internal static class HerdFixture
{
    public static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Today);

    private static Goat G(string tag, int ageDays, GoatStatus status = GoatStatus.Dry, GoatGender gender = GoatGender.Female) => new()
    {
        Tag = tag, Breed = "Beetal", Gender = gender, Status = status, Source = GoatSource.Born, EventDate = Today.AddDays(-ageDays)
    };

    /// <summary>One goat per filter case, plus an archived goat that must never appear.</summary>
    public static async Task SeedFilterCasesAsync(GoatFarmDbContext db)
    {
        var group = new GoatGroup { Name = "Pen A" };
        db.GoatGroups.Add(group);
        var buck = G("BUCK", 900, GoatStatus.Buck, GoatGender.Male);
        db.Goats.Add(buck);
        await db.SaveChangesAsync();

        db.Goats.AddRange(
            G("KID-10", 10, GoatStatus.Kid),
            G("KID-80", 80, GoatStatus.Kid),
            G("YEARLING", 200),
            G("FUTURE", -3, GoatStatus.Kid),
            new Goat { Tag = "D-PREP", Breed = "Beetal", EventDate = Today.AddDays(-600), Status = GoatStatus.Dry, PrepCrossDate = Today },
            new Goat { Tag = "D-WAIT", Breed = "Beetal", EventDate = Today.AddDays(-600), Status = GoatStatus.Dry, MatedDate = Today.AddDays(-20) },
            new Goat { Tag = "D-EXP", Breed = "Beetal", EventDate = Today.AddDays(-600), Status = GoatStatus.Pregnant, MatedDate = Today.AddDays(-50), KidsCount = 2 },
            new Goat { Tag = "D-DUE", Breed = "Beetal", EventDate = Today.AddDays(-600), Status = GoatStatus.Pregnant, MatedDate = Today.AddDays(-130), KidsCount = 1 },
            new Goat { Tag = "SICK", Name = "Rani", Breed = "Teddy", EventDate = Today.AddDays(-700), Status = GoatStatus.Milking,
                HealthState = GoatHealthState.Sick, Owner = "Partner Ali", GroupId = group.Id },
            new Goat { Tag = "CHILD", Breed = "Beetal", EventDate = Today.AddDays(-400), Status = GoatStatus.Dry, SireId = buck.Id, DamTagText = "OUT-DAM" },
            new Goat { Tag = "GONE", Breed = "Beetal", EventDate = Today.AddDays(-300), Status = GoatStatus.Dry, IsArchived = true, Owner = "Partner Ali" });
        await db.SaveChangesAsync();
    }

    public static readonly (string Filter, string? Search, string[] Tags)[] Cases =
    [
        ("all", null, ["BUCK", "KID-10", "KID-80", "YEARLING", "FUTURE", "D-PREP", "D-WAIT", "D-EXP", "D-DUE", "SICK", "CHILD"]),
        ("age:0:70", null, ["KID-10", "FUTURE"]),
        ("age:70:90", null, ["KID-80"]),
        ("age:180:365", null, ["YEARLING"]),
        ("st:kid", null, ["KID-10", "KID-80", "FUTURE"]),
        ("st:pregnant", null, ["D-EXP", "D-DUE"]),
        ("br:prep", null, ["D-PREP"]),
        ("br:waiting", null, ["D-WAIT"]),
        ("br:expecting", null, ["D-EXP", "D-DUE"]),
        ("br:due30", null, ["D-DUE"]),
        ("health:sick", null, ["SICK"]),
        ("owner:partner ali", null, ["SICK"]),
        ("grp:Pen A", null, ["SICK"]),
        ("sire:buck", null, ["CHILD"]),
        ("dam:out-dam", null, ["CHILD"]),
        ("all", "rani", ["SICK"]),
        ("all", "ALI", ["SICK"]),
        ("all", "teddy", ["SICK"]),
        ("all", "out-dam", ["CHILD"]),
        ("all", "d-", ["D-PREP", "D-WAIT", "D-EXP", "D-DUE", "KID-10", "KID-80"]),
        ("st:pregnant", "due", ["D-DUE"]),
        ("all", "no-such-goat", [])
    ];

    public static async Task SeedManyAsync(GoatFarmDbContext db, int count)
    {
        for (var i = 0; i < count; i++)
            db.Goats.Add(G($"H{i:D4}", 100 + i));
        await db.SaveChangesAsync();
    }
}

public class HerdPagingTests
{
    private static (GoatFarmDbContext Db, GoatMaterializationCounter Counter) Create()
    {
        var counter = new GoatMaterializationCounter();
        var db = new GoatFarmDbContext(new DbContextOptionsBuilder<GoatFarmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(counter)
            .Options);
        return (db, counter);
    }

    [Fact]
    public async Task EveryFilterAndSearch_ReturnsTheSameGoatsAsBefore()
    {
        var (db, _) = Create();
        await HerdFixture.SeedFilterCasesAsync(db);
        var svc = new GoatService(db);

        foreach (var (filter, search, tags) in HerdFixture.Cases)
        {
            var page = await svc.GetHerdPageAsync(filter, 1, 100, search);
            Assert.True(tags.Order().SequenceEqual(page.Goats.Select(g => g.Tag).Order()),
                $"{filter} / {search}: expected [{string.Join(",", tags.Order())}] got [{string.Join(",", page.Goats.Select(g => g.Tag).Order())}]");
            Assert.Equal(tags.Length, page.Pagination.TotalItems);
        }
    }

    [Fact]
    public async Task Page_MaterializesOnlyThatPageAndOrdersByNewestFirst()
    {
        var (db, counter) = Create();
        await HerdFixture.SeedManyAsync(db, 60);
        db.ChangeTracker.Clear();
        var svc = new GoatService(db);
        counter.Reset();

        var page2 = await svc.GetHerdPageAsync("all", 2, 10);

        Assert.Equal(10, counter.Count);
        Assert.Equal(60, page2.Pagination.TotalItems);
        Assert.Equal(60, page2.Stats.Total);
        var ids = page2.Goats.Select(g => g.Id).ToList();
        Assert.Equal(ids.OrderByDescending(i => i), ids);
        var page1 = await svc.GetHerdPageAsync("all", 1, 10);
        Assert.True(page1.Goats.Min(g => g.Id) > ids.Max());
    }

    [Fact]
    public async Task OutOfRangePage_IsClampedAndPageSizeIsCapped()
    {
        var (db, _) = Create();
        await HerdFixture.SeedManyAsync(db, 25);
        var svc = new GoatService(db);

        var last = await svc.GetHerdPageAsync("all", 99, 10);
        Assert.Equal(3, last.Pagination.Page);
        Assert.Equal(5, last.Goats.Count);

        var huge = await svc.GetHerdPageAsync("all", 1, 100_000);
        Assert.Equal(200, huge.Pagination.PageSize);
    }
}

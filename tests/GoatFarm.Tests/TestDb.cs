using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Tests;

internal static class TestDb
{
    public static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Today);

    public static GoatFarmDbContext Create() =>
        new(new DbContextOptionsBuilder<GoatFarmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    public static Goat Doe(string tag, int ageDays = 500, GoatStatus status = GoatStatus.Dry, int? sireId = null, int? damId = null) => new()
    {
        Tag = tag,
        Breed = "Beetal",
        Gender = GoatGender.Female,
        Status = status,
        Source = GoatSource.Born,
        EventDate = Today.AddDays(-ageDays),
        SireId = sireId,
        DamId = damId
    };

    public static Goat Buck(string tag, int? sireId = null, int? damId = null) => new()
    {
        Tag = tag,
        Breed = "Beetal",
        Gender = GoatGender.Male,
        Status = GoatStatus.Buck,
        Source = GoatSource.Bought,
        EventDate = Today.AddDays(-700),
        SireId = sireId,
        DamId = damId
    };
}

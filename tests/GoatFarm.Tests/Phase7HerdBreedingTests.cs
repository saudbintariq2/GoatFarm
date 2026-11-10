using GoatFarm.Application.ViewModels.Goats;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure.Persistence;
using GoatFarm.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Tests;

public class Phase7HerdBreedingTests
{
    private static (GoatService Goats, GoatFarmDbContext Db) Create()
    {
        var db = TestDb.Create();
        return (new GoatService(db), db);
    }

    [Fact]
    public async Task BulkCross_RequiresBuckTag()
    {
        var (goats, db) = Create();
        var doe = TestDb.Doe("D-cross");
        db.Goats.Add(doe);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            goats.BulkMoveAsync(new BulkMoveViewModel
            {
                GoatIds = [doe.Id],
                MoveTarget = "br:cross",
                MatedDate = TestDb.Today
            }));
        Assert.Contains("buck", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Delete_BlockedWhenVaccinationHistoryExists()
    {
        var (goats, db) = Create();
        var kid = TestDb.Doe("K1");
        db.Goats.Add(kid);
        await db.SaveChangesAsync();
        db.VaccinationHistories.Add(new VaccinationHistory
        {
            GoatId = kid.Id,
            VaccineId = 1,
            VaccinationDate = TestDb.Today
        });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => goats.DeleteAsync(kid.Id));
    }
}

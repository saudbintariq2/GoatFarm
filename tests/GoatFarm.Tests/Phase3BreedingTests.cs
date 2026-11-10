using GoatFarm.Application.Common;
using GoatFarm.Application.ViewModels.Breeding;
using GoatFarm.Application.ViewModels.Goats;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure.Persistence;
using GoatFarm.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Tests;

public class Phase3BreedingTests
{
    private static (BreedingService Breeding, GoatService Goats, GoatFarmDbContext Db) Create()
    {
        var db = TestDb.Create();
        var goats = new GoatService(db);
        return (new BreedingService(db, goats, feedService: null!), goats, db);
    }

    [Fact]
    public async Task UpdateSeason_NewBatch_ReturnsBatchId()
    {
        var (svc, _, _) = Create();
        var id = await svc.UpdateSeasonAsync(new UpdateBreedingSeasonViewModel { NewBatchName = "Fall 2026" });
        Assert.False(string.IsNullOrWhiteSpace(id));
        Assert.StartsWith("b", id);
    }

    [Fact]
    public async Task CreateGroup_RequiresBuckTagAndDate()
    {
        var (svc, _, db) = Create();
        await db.Goats.AddRangeAsync(TestDb.Buck("B1"), TestDb.Doe("D1"));
        await db.SaveChangesAsync();
        var doeId = (await db.Goats.SingleAsync(g => g.Tag == "D1")).Id;
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CreateBreedingGroupAsync(new CreateBreedingGroupViewModel { DoeIds = [doeId] }));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CreateBreedingGroupAsync(new CreateBreedingGroupViewModel
            {
                BuckTag = "B1",
                DoeIds = [doeId]
            }));
    }

    [Fact]
    public async Task ReconcilePregnancy_BackfillsMatedDateFromBreedingEvent()
    {
        var (_, goats, db) = Create();
        var doe = TestDb.Doe("D3");
        doe.Status = GoatStatus.Pregnant;
        doe.KidsCount = 2;
        db.Goats.Add(doe);
        await db.SaveChangesAsync();
        var mated = TestDb.Today.AddDays(-40);
        db.BreedingEvents.Add(new BreedingEvent
        {
            DoeId = doe.Id,
            MatedDate = mated,
            ScanKids = 2,
            Outcome = BreedingOutcome.ConfirmedPregnant,
            ScanDate = TestDb.Today.AddDays(-5)
        });
        await db.SaveChangesAsync();

        var breeding = new BreedingService(db, goats, feedService: null!);
        var result = await breeding.ReconcilePregnancyAsync();
        Assert.True(result.Adjusted >= 1);
        var saved = await db.Goats.SingleAsync(g => g.Tag == "D3");
        Assert.Equal(mated, saved.MatedDate);
        Assert.Equal(GoatStatus.Pregnant, saved.Status);
    }

    [Fact]
    public async Task ReconcilePregnancy_FixesOrphanPregnantStatus()
    {
        var (_, goats, db) = Create();
        var doe = TestDb.Doe("D1");
        doe.Status = GoatStatus.Pregnant;
        db.Goats.Add(doe);
        await db.SaveChangesAsync();

        var breeding = new BreedingService(db, goats, feedService: null!);
        var result = await breeding.ReconcilePregnancyAsync();
        Assert.Equal(1, result.Adjusted);
        var saved = await db.Goats.SingleAsync(g => g.Tag == "D1");
        Assert.Equal(GoatStatus.Dry, saved.Status);
    }

    [Fact]
    public async Task HerdUpdate_CannotSetPregnantWithoutBreedingData()
    {
        var (_, goats, db) = Create();
        var doe = await db.Goats.AddAsync(TestDb.Doe("D2"));
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            goats.UpdateAsync(doe.Entity.Id, new CreateGoatViewModel
            {
                Tag = "D2",
                Breed = doe.Entity.Breed,
                Gender = GoatGender.Female,
                Status = GoatStatus.Pregnant,
                Source = GoatSource.Born,
                EventDate = TestDb.Today,
                HealthState = GoatHealthState.Healthy
            }));
    }

    [Fact]
    public void PregnancyRules_MatchesBreedingAndReports()
    {
        var confirmed = new Goat
        {
            Gender = GoatGender.Female,
            MatedDate = TestDb.Today.AddDays(-30),
            KidsCount = 2
        };
        var waiting = new Goat
        {
            Gender = GoatGender.Female,
            MatedDate = TestDb.Today.AddDays(-10),
            KidsCount = null
        };
        Assert.True(PregnancyRules.IsConfirmedPregnant(confirmed));
        Assert.True(PregnancyRules.IsAwaitingScan(waiting));
        Assert.False(PregnancyRules.IsConfirmedPregnant(waiting));
    }
}

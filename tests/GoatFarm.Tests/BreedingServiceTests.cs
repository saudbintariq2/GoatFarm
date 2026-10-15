using GoatFarm.Application.Common;
using GoatFarm.Application.ViewModels.Breeding;
using GoatFarm.Application.ViewModels.Goats;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure.Persistence;
using GoatFarm.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Tests;

public class BreedingServiceTests
{
    private static (BreedingService Breeding, GoatService Goats, GoatFarmDbContext Db) Create()
    {
        var db = TestDb.Create();
        var goats = new GoatService(db);
        return (new BreedingService(db, goats, feedService: null!), goats, db);
    }

    private static async Task<Goat> AddAsync(GoatFarmDbContext db, Goat goat)
    {
        db.Goats.Add(goat);
        await db.SaveChangesAsync();
        return goat;
    }

    [Fact]
    public async Task RecordCross_PutsDoeInScanPendingWithoutMarkingPregnant()
    {
        var (svc, _, db) = Create();
        var doe = await AddAsync(db, TestDb.Doe("D1"));
        var buck = await AddAsync(db, TestDb.Buck("B1"));

        await svc.RecordCrossAsync(new RecordCrossViewModel { Tag = "D1", Date = TestDb.Today.AddDays(-3), BuckTag = "b1" });

        var saved = await db.Goats.SingleAsync(g => g.Id == doe.Id);
        Assert.Equal(BreedingStage.ScanPending, saved.BreedingStage);
        Assert.Equal(GoatStatus.Dry, saved.Status);
        Assert.Equal("B1", saved.BuckTag);
        var ev = await db.BreedingEvents.SingleAsync();
        Assert.Equal(BreedingOutcome.Open, ev.Outcome);
        Assert.Equal(buck.Id, ev.BuckId);
    }

    [Fact]
    public async Task RecordCross_RejectsUnderAgeDoeAndFutureDate()
    {
        var (svc, _, db) = Create();
        await AddAsync(db, TestDb.Doe("YOUNG", ageDays: 120));
        await AddAsync(db, TestDb.Doe("D1"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.RecordCrossAsync(new RecordCrossViewModel { Tag = "YOUNG", Date = TestDb.Today }));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.RecordCrossAsync(new RecordCrossViewModel { Tag = "D1", Date = TestDb.Today.AddDays(2) }));
        Assert.Empty(db.BreedingEvents);
    }

    [Fact]
    public async Task RecordCross_RequiresConfirmationForHalfSiblings()
    {
        var (svc, _, db) = Create();
        var sire = await AddAsync(db, TestDb.Buck("SIRE"));
        await AddAsync(db, TestDb.Doe("D1", sireId: sire.Id));
        await AddAsync(db, TestDb.Buck("B1", sireId: sire.Id));
        var model = new RecordCrossViewModel { Tag = "D1", Date = TestDb.Today, BuckTag = "B1" };

        var ex = await Assert.ThrowsAsync<ConfirmationRequiredException>(() => svc.RecordCrossAsync(model));
        Assert.Contains(ex.Warnings, w => w.Contains("half siblings"));
        Assert.Empty(db.BreedingEvents);

        model.ConfirmWarnings = true;
        await svc.RecordCrossAsync(model);
        Assert.Single(db.BreedingEvents);
    }

    [Fact]
    public async Task PositiveScan_ConfirmsPregnancy()
    {
        var (svc, _, db) = Create();
        var doe = await AddAsync(db, TestDb.Doe("D1"));
        await svc.RecordCrossAsync(new RecordCrossViewModel { Tag = "D1", Date = TestDb.Today.AddDays(-50) });

        await svc.RecordUltrasoundAsync(new RecordUltrasoundViewModel { Tag = "D1", KidsCount = 2, Date = TestDb.Today });

        var saved = await db.Goats.SingleAsync(g => g.Id == doe.Id);
        Assert.Equal(GoatStatus.Pregnant, saved.Status);
        Assert.Equal(BreedingStage.ConfirmedPregnant, saved.BreedingStage);
        var ev = await db.BreedingEvents.SingleAsync();
        Assert.Equal(BreedingOutcome.ConfirmedPregnant, ev.Outcome);
        Assert.Equal(2, ev.ScanKids);
    }

    [Fact]
    public async Task EmptyScan_ClosesCycleAndKeepsMilkingDoeMilking()
    {
        var (svc, _, db) = Create();
        var doe = await AddAsync(db, TestDb.Doe("D1", status: GoatStatus.Milking));
        await svc.RecordCrossAsync(new RecordCrossViewModel { Tag = "D1", Date = TestDb.Today.AddDays(-50) });

        await svc.RecordUltrasoundAsync(new RecordUltrasoundViewModel { Tag = "D1", KidsCount = 0, Date = TestDb.Today });

        var saved = await db.Goats.SingleAsync(g => g.Id == doe.Id);
        Assert.Null(saved.MatedDate);
        Assert.Equal(BreedingStage.None, saved.BreedingStage);
        Assert.Equal(GoatStatus.Milking, saved.Status);
        Assert.Equal(BreedingOutcome.Empty, (await db.BreedingEvents.SingleAsync()).Outcome);
        Assert.Single(db.BreedingEmptyLogs);
    }

    [Fact]
    public async Task RecordBirth_CreatesKidsWithPedigreeAndIsIdempotent()
    {
        var (svc, _, db) = Create();
        var doe = await AddAsync(db, TestDb.Doe("D1"));
        var buck = await AddAsync(db, TestDb.Buck("B1"));
        await svc.RecordCrossAsync(new RecordCrossViewModel { Tag = "D1", Date = TestDb.Today.AddDays(-150), BuckTag = "B1" });
        await svc.RecordUltrasoundAsync(new RecordUltrasoundViewModel { Tag = "D1", KidsCount = 2, Date = TestDb.Today.AddDays(-100) });

        var request = new RecordBirthViewModel
        {
            ClientRequestId = Guid.NewGuid(),
            DoeTag = "D1",
            KiddingDate = TestDb.Today,
            Kids =
            [
                new BirthKidViewModel { Gender = GoatGender.Female, WeightKg = 3.2m },
                new BirthKidViewModel { Gender = GoatGender.Male, Tag = "K-100" }
            ]
        };

        var first = await svc.RecordBirthAsync(request);
        var second = await svc.RecordBirthAsync(request);

        Assert.False(first.AlreadyRecorded);
        Assert.True(second.AlreadyRecorded);
        var kids = await db.Goats.Where(g => g.DamId == doe.Id).ToListAsync();
        Assert.Equal(2, kids.Count);
        Assert.All(kids, k => Assert.Equal(buck.Id, k.SireId));
        Assert.All(kids, k => Assert.Equal(GoatStatus.Kid, k.Status));
        Assert.Contains(kids, k => k.IsTemporaryTag && k.Tag.StartsWith("T-D1-"));
        Assert.Contains(kids, k => k.Tag == "K-100" && !k.IsTemporaryTag);
        Assert.Single(db.GoatWeightRecords);
        Assert.Single(db.KiddingRecords);

        var savedDoe = await db.Goats.SingleAsync(g => g.Id == doe.Id);
        Assert.Equal(GoatStatus.Milking, savedDoe.Status);
        Assert.Null(savedDoe.MatedDate);
        Assert.Equal(BreedingOutcome.Kidded, (await db.BreedingEvents.SingleAsync()).Outcome);
    }

    [Fact]
    public async Task RecordBirth_RejectsTagAlreadyInUseAndSavesNothing()
    {
        var (svc, _, db) = Create();
        await AddAsync(db, TestDb.Doe("D1"));
        await AddAsync(db, TestDb.Doe("TAKEN"));
        await svc.RecordCrossAsync(new RecordCrossViewModel { Tag = "D1", Date = TestDb.Today.AddDays(-150) });

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.RecordBirthAsync(new RecordBirthViewModel
        {
            ClientRequestId = Guid.NewGuid(),
            DoeTag = "D1",
            KiddingDate = TestDb.Today,
            Kids = [new BirthKidViewModel { Gender = GoatGender.Female, Tag = "taken" }]
        }));

        Assert.Equal(2, await db.Goats.CountAsync());
        Assert.Empty(db.KiddingRecords);
    }

    [Fact]
    public async Task RecordBirth_WithoutCrossNeedsConfirmation()
    {
        var (svc, _, db) = Create();
        await AddAsync(db, TestDb.Doe("D1"));
        var request = new RecordBirthViewModel
        {
            ClientRequestId = Guid.NewGuid(),
            DoeTag = "D1",
            KiddingDate = TestDb.Today,
            Kids = [new BirthKidViewModel { Gender = GoatGender.Female }]
        };

        await Assert.ThrowsAsync<ConfirmationRequiredException>(() => svc.RecordBirthAsync(request));
        request.ConfirmWarnings = true;
        var result = await svc.RecordBirthAsync(request);
        Assert.Single(result.Kids);
    }

    [Fact]
    public async Task GoatDelete_IsBlockedWhenOffspringExist()
    {
        var (_, goats, db) = Create();
        var doe = await AddAsync(db, TestDb.Doe("D1"));
        await AddAsync(db, TestDb.Doe("KID", ageDays: 30, status: GoatStatus.Kid, damId: doe.Id));

        await Assert.ThrowsAsync<InvalidOperationException>(() => goats.DeleteAsync(doe.Id));
        Assert.Equal(2, await db.Goats.CountAsync());
    }

    [Fact]
    public async Task BulkCross_IsAllOrNothing()
    {
        var (_, goats, db) = Create();
        var ok = await AddAsync(db, TestDb.Doe("D1"));
        var young = await AddAsync(db, TestDb.Doe("D2", ageDays: 100));

        await Assert.ThrowsAsync<InvalidOperationException>(() => goats.BulkMoveAsync(new BulkMoveViewModel
        {
            GoatIds = [ok.Id, young.Id],
            MoveTarget = "br:cross",
            MatedDate = TestDb.Today
        }));

        Assert.Empty(db.BreedingEvents);
        Assert.Null((await db.Goats.AsNoTracking().SingleAsync(g => g.Id == ok.Id)).MatedDate);
    }

    [Fact]
    public async Task GoatCreate_LinksParentsByTagAndRejectsWrongGender()
    {
        var (_, goats, db) = Create();
        var sire = await AddAsync(db, TestDb.Buck("SIRE"));
        await AddAsync(db, TestDb.Doe("DAM"));

        var created = await goats.CreateAsync(new CreateGoatViewModel
        {
            Tag = "NEW", Breed = "Beetal", Gender = GoatGender.Female, Status = GoatStatus.Kid,
            Source = GoatSource.Born, EventDate = TestDb.Today, SireTag = "sire", DamTag = "OUTSIDE-9"
        });
        var saved = await db.Goats.SingleAsync(g => g.Id == created.Id);
        Assert.Equal(sire.Id, saved.SireId);
        Assert.Null(saved.DamId);
        Assert.Equal("OUTSIDE-9", saved.DamTagText);

        await Assert.ThrowsAsync<InvalidOperationException>(() => goats.CreateAsync(new CreateGoatViewModel
        {
            Tag = "NEW2", Breed = "Beetal", Gender = GoatGender.Female, Status = GoatStatus.Kid,
            Source = GoatSource.Born, EventDate = TestDb.Today, SireTag = "DAM"
        }));
    }
}

using GoatFarm.Application.Common;
using GoatFarm.Application.ViewModels.Breeding;
using GoatFarm.Application.ViewModels.Goats;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure.Persistence;
using GoatFarm.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Tests;

public class DateOfBirthTests
{
    private static readonly DateOnly Today = TestDb.Today;

    private static Goat BoughtDoe(string tag, int? ageDays = null, bool estimated = false, int boughtDaysAgo = 30,
        GoatStatus status = GoatStatus.Dry) => new()
    {
        Tag = tag, Breed = "Beetal", Gender = GoatGender.Female, Status = status, Source = GoatSource.Bought,
        EventDate = Today.AddDays(-boughtDaysAgo),
        DateOfBirth = ageDays is { } a ? Today.AddDays(-a) : null,
        DateOfBirthIsEstimated = estimated
    };

    [Fact]
    public void Age_ComesFromEventDateForBornGoats_AndFromDateOfBirthForBoughtGoats()
    {
        Assert.Equal(500, BreedingRules.AgeDays(TestDb.Doe("BORN", ageDays: 500), Today));
        Assert.Equal(400, BreedingRules.AgeDays(BoughtDoe("B-DOB", ageDays: 400), Today));
        Assert.Null(BreedingRules.AgeDays(BoughtDoe("B-NONE"), Today));

        var legacyBornWithDob = TestDb.Doe("BORN2", ageDays: 500);
        legacyBornWithDob.DateOfBirth = Today.AddDays(-50);
        Assert.Equal(500, BreedingRules.AgeDays(legacyBornWithDob, Today));
    }

    [Fact]
    public void FarmBornDoes_KeepThe300DayRule()
    {
        Assert.Contains("299 days old", BreedingRules.EligibilityError(TestDb.Doe("YOUNG", ageDays: 299), Today));
        Assert.Null(BreedingRules.EligibilityError(TestDb.Doe("ADULT", ageDays: 300), Today));
        Assert.Null(BreedingRules.AgeVerificationWarning(TestDb.Doe("ADULT", ageDays: 300), Today));
    }

    [Fact]
    public void BoughtDoeWithKnownDateOfBirth_IsCheckedLikeAFarmBornDoe()
    {
        Assert.Contains("200 days old", BreedingRules.EligibilityError(BoughtDoe("B200", ageDays: 200), Today));
        Assert.Null(BreedingRules.EligibilityError(BoughtDoe("B400", ageDays: 400), Today));
        Assert.Null(BreedingRules.AgeVerificationWarning(BoughtDoe("B400", ageDays: 400), Today));
    }

    [Fact]
    public void EstimatedAge_UnderTheLimitIsBlocked_OverTheLimitNeedsConfirmation()
    {
        Assert.Contains("estimated", BreedingRules.EligibilityError(BoughtDoe("E200", ageDays: 200, estimated: true), Today));
        Assert.Null(BreedingRules.EligibilityError(BoughtDoe("E400", ageDays: 400, estimated: true), Today));
        Assert.Contains("estimated (about 400 days)", BreedingRules.AgeVerificationWarning(BoughtDoe("E400", ageDays: 400, estimated: true), Today));
    }

    [Fact]
    public void UnknownAge_NeedsConfirmation_AndAnUnknownAgeKidIsStillBlocked()
    {
        Assert.Null(BreedingRules.EligibilityError(BoughtDoe("U"), Today));
        Assert.Contains("age is unknown", BreedingRules.AgeVerificationWarning(BoughtDoe("U"), Today));
        Assert.Contains("age is unknown", BreedingRules.EligibilityError(BoughtDoe("UKID", status: GoatStatus.Kid), Today));
    }

    private static (BreedingService Breeding, GoatService Goats, GoatFarmDbContext Db) Create()
    {
        var db = TestDb.Create();
        var goats = new GoatService(db);
        return (new BreedingService(db, goats, feedService: null!), goats, db);
    }

    [Fact]
    public async Task CrossingAnUnknownAgeDoe_RequiresConfirmation_AndKeepsTheRelatedGoatWarning()
    {
        var (svc, _, db) = Create();
        var buck = TestDb.Buck("SIRE");
        db.Goats.Add(buck);
        await db.SaveChangesAsync();
        var doe = BoughtDoe("DAUGHTER");
        doe.SireId = buck.Id;
        db.Goats.Add(doe);
        await db.SaveChangesAsync();

        var check = await svc.CheckCrossAsync("DAUGHTER", "SIRE", Today);
        Assert.True(check.CanCross);
        Assert.Contains(check.Warnings, w => w.Contains("age is unknown"));
        Assert.Contains(check.Warnings, w => w.Contains("sire (father)"));

        var ex = await Assert.ThrowsAsync<ConfirmationRequiredException>(() =>
            svc.RecordCrossAsync(new RecordCrossViewModel { Tag = "DAUGHTER", BuckTag = "SIRE", Date = Today }));
        Assert.Equal(2, ex.Warnings.Count);
        Assert.Empty(db.BreedingEvents);

        await svc.RecordCrossAsync(new RecordCrossViewModel { Tag = "DAUGHTER", BuckTag = "SIRE", Date = Today, ConfirmWarnings = true });
        Assert.Single(db.BreedingEvents);
    }

    [Fact]
    public async Task CrossingABoughtDoeWithKnownAdultAge_NeedsNoAgeConfirmation()
    {
        var (svc, _, db) = Create();
        db.Goats.AddRange(BoughtDoe("KNOWN", ageDays: 500), TestDb.Buck("B1"));
        await db.SaveChangesAsync();

        await svc.RecordCrossAsync(new RecordCrossViewModel { Tag = "KNOWN", BuckTag = "B1", Date = Today });
        Assert.Single(db.BreedingEvents);
    }

    [Fact]
    public async Task CrossDate_BeforeAKnownDateOfBirth_IsRejected()
    {
        var (svc, _, db) = Create();
        db.Goats.Add(BoughtDoe("KNOWN", ageDays: 400));
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.RecordCrossAsync(new RecordCrossViewModel { Tag = "KNOWN", Date = Today.AddDays(-401), ConfirmWarnings = true }));
        Assert.Contains("before KNOWN was born", ex.Message);
    }

    [Fact]
    public async Task BulkCrossFromHerd_AlsoAsksToConfirmUnknownAge()
    {
        var (_, goats, db) = Create();
        db.Goats.AddRange(BoughtDoe("U1"), TestDb.Doe("BORN1"));
        await db.SaveChangesAsync();
        var ids = await db.Goats.Select(g => g.Id).ToListAsync();

        var ex = await Assert.ThrowsAsync<ConfirmationRequiredException>(() => goats.BulkMoveAsync(
            new BulkMoveViewModel { GoatIds = ids, MoveTarget = "br:cross", MatedDate = Today }));
        Assert.Single(ex.Warnings, w => w.Contains("U1") && w.Contains("age is unknown"));
        Assert.Empty(db.BreedingEvents);
    }

    private static CreateGoatViewModel Model(string tag, GoatSource source = GoatSource.Bought, DateOnly? dob = null,
        bool estimated = false, int boughtDaysAgo = 30) => new()
    {
        Tag = tag, Breed = "Beetal", Gender = GoatGender.Female, Status = GoatStatus.Dry, Source = source,
        EventDate = Today.AddDays(-boughtDaysAgo), DateOfBirth = dob, DateOfBirthIsEstimated = estimated
    };

    [Fact]
    public async Task Create_StoresAnOptionalDateOfBirthForBoughtGoats()
    {
        var (_, goats, db) = Create();
        var known = await goats.CreateAsync(Model("K", dob: Today.AddDays(-400)));
        var est = await goats.CreateAsync(Model("E", dob: Today.AddDays(-700), estimated: true));
        var none = await goats.CreateAsync(Model("N", estimated: true));

        Assert.Equal(400, known.AgeDays);
        Assert.False(known.AgeIsEstimated);
        Assert.True(est.AgeIsEstimated);
        Assert.StartsWith("about ", est.AgeLabel);
        Assert.Null(none.AgeDays);
        Assert.Equal("age unknown", none.AgeLabel);
        var stored = await db.Goats.AsNoTracking().SingleAsync(g => g.Tag == "N");
        Assert.Null(stored.DateOfBirth);
        Assert.False(stored.DateOfBirthIsEstimated);
    }

    [Fact]
    public async Task Create_IgnoresDateOfBirthForFarmBornGoats()
    {
        var (_, goats, db) = Create();
        var born = await goats.CreateAsync(Model("BORN", GoatSource.Born, dob: Today.AddDays(-10), estimated: true, boughtDaysAgo: 100));

        Assert.Equal(100, born.AgeDays);
        var stored = await db.Goats.AsNoTracking().SingleAsync();
        Assert.Null(stored.DateOfBirth);
        Assert.False(stored.DateOfBirthIsEstimated);
    }

    [Theory]
    [InlineData(1, 30, "in the future")]
    [InlineData(-10, 30, "after the purchase date")]
    [InlineData(-(365 * 26), 30, "more than 25 years")]
    public async Task Create_RejectsImpossibleDatesOfBirth(int dobOffsetDays, int boughtDaysAgo, string message)
    {
        var (_, goats, db) = Create();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            goats.CreateAsync(Model("BAD", dob: Today.AddDays(dobOffsetDays), boughtDaysAgo: boughtDaysAgo)));
        Assert.Contains(message, ex.Message);
        Assert.Empty(db.Goats);
    }

    [Fact]
    public async Task Update_CanAddAndClearADateOfBirth()
    {
        var (_, goats, db) = Create();
        var g = await goats.CreateAsync(Model("G"));
        await goats.UpdateAsync(g.Id, Model("G", dob: Today.AddDays(-365), estimated: true));
        Assert.Equal(Today.AddDays(-365), (await db.Goats.AsNoTracking().SingleAsync()).DateOfBirth);

        var cleared = await goats.UpdateAsync(g.Id, Model("G"));
        Assert.Null(cleared!.AgeDays);
        Assert.Null((await db.Goats.AsNoTracking().SingleAsync()).DateOfBirth);
    }

    [Fact]
    public async Task AgeFilters_UseTheBirthDate_AndLeaveOutGoatsOfUnknownAge()
    {
        var (_, goats, db) = Create();
        db.Goats.AddRange(
            TestDb.Doe("BORN-80", ageDays: 80, status: GoatStatus.Kid),
            BoughtDoe("BOUGHT-80", ageDays: 80, boughtDaysAgo: 5),
            BoughtDoe("BOUGHT-NODOB", boughtDaysAgo: 80));
        await db.SaveChangesAsync();

        var page = await goats.GetHerdPageAsync("age:70:90", 1, 50);
        Assert.Equal(["BORN-80", "BOUGHT-80"], page.Goats.Select(g => g.Tag).Order());
        var all = await goats.GetHerdPageAsync("all", 1, 50);
        Assert.Equal(3, all.Goats.Count);
    }

    [Fact]
    public async Task DeathRecord_StoresAgeOnlyWhenTheBirthDateIsKnown()
    {
        var (_, goats, db) = Create();
        db.Goats.AddRange(BoughtDoe("NODOB", boughtDaysAgo: 200), BoughtDoe("DOB", ageDays: 600, boughtDaysAgo: 200));
        await db.SaveChangesAsync();

        await goats.RecordDeathAsync(new RecordDeathViewModel { Tag = "NODOB", Date = Today });
        await goats.RecordDeathAsync(new RecordDeathViewModel { Tag = "DOB", Date = Today.AddDays(-100) });

        var deaths = await db.DeathRecords.AsNoTracking().ToDictionaryAsync(d => d.Tag);
        Assert.Null(deaths["NODOB"].AgeDays);
        Assert.Equal(500, deaths["DOB"].AgeDays);
    }
}

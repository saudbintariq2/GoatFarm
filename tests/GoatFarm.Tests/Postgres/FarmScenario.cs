using GoatFarm.Application.ViewModels.Breeding;
using GoatFarm.Application.ViewModels.Goats;
using GoatFarm.Application.ViewModels.Tasks;
using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure.Identity;
using GoatFarm.Infrastructure.Persistence;
using GoatFarm.Infrastructure.Services;

namespace GoatFarm.Tests.Postgres;

/// <summary>Builds a farm through the real services so stored data has the same shape as production.</summary>
internal static class FarmScenario
{
    public const string StaffEmail = "staff@farm.test";

    public static async Task SeedDefaultsAsync(GoatFarmDbContext db)
    {
        await DbSeeder.SeedAsync(db);
        await FeedPlanMigrator.MigrateAsync(db);
        await SettingsSeedHelper.SeedDefaultsAsync(db);
    }

    public static async Task BuildAsync(GoatFarmDbContext db)
    {
        db.Users.Add(new ApplicationUser
        {
            Id = "user-staff-1", UserName = StaffEmail, NormalizedUserName = StaffEmail.ToUpperInvariant(),
            Email = StaffEmail, NormalizedEmail = StaffEmail.ToUpperInvariant(), FullName = "Staff One",
            PasswordHash = "AQAAAAIAAYagAAAAEXAMPLEHASHNOTEXPORTED", SecurityStamp = "stamp"
        });
        await db.SaveChangesAsync();

        var goats = new GoatService(db);
        var breeding = new BreedingService(db, goats, feedService: null!);
        var tasks = new TaskService(db);
        var today = DateOnly.FromDateTime(DateTime.Today);

        await goats.CreateAsync(Goat("SIRE-1", GoatGender.Male, GoatStatus.Buck, today.AddDays(-900)));
        await goats.CreateAsync(Goat("DAM-1", GoatGender.Female, GoatStatus.Dry, today.AddDays(-800)));
        await goats.CreateAsync(Goat("DOE-A", GoatGender.Female, GoatStatus.Dry, today.AddDays(-500), sire: "SIRE-1", dam: "DAM-1",
            owner: "Partner Ali", health: GoatHealthState.Recovering));
        await goats.CreateAsync(Goat("DOE-B", GoatGender.Female, GoatStatus.Milking, today.AddDays(-600), dam: "OUTSIDE-DAM"));
        await goats.CreateAsync(Goat("DOE-C", GoatGender.Female, GoatStatus.Dry, today.AddDays(-700)));
        await goats.CreateAsync(Goat("BUCK-X", GoatGender.Male, GoatStatus.Buck, today.AddDays(-1000)));

        // DOE-A: crossed, scanned twins, kidded -> history row Kidded + kidding record + 2 kids.
        await breeding.RecordCrossAsync(new RecordCrossViewModel { Tag = "DOE-A", Date = today.AddDays(-152), BuckTag = "BUCK-X" });
        await breeding.RecordUltrasoundAsync(new RecordUltrasoundViewModel { Tag = "DOE-A", KidsCount = 2, Date = today.AddDays(-100) });
        await breeding.RecordBirthAsync(new RecordBirthViewModel
        {
            ClientRequestId = Guid.Parse("11111111-2222-3333-4444-555555555555"),
            DoeTag = "DOE-A",
            KiddingDate = today.AddDays(-2),
            Stillborn = 1,
            Ease = KiddingEase.Assisted,
            Kids =
            [
                new BirthKidViewModel { Gender = GoatGender.Female, WeightKg = 3.1m },
                new BirthKidViewModel { Gender = GoatGender.Male, Tag = "KID-M1", WeightKg = 3.4m }
            ]
        });

        // DOE-B: crossed and confirmed (open, ConfirmedPregnant). DOE-C: crossed, scan pending, then a second empty scan history.
        await breeding.RecordCrossAsync(new RecordCrossViewModel { Tag = "DOE-B", Date = today.AddDays(-60), BuckTag = "BUCK-X" });
        await breeding.RecordUltrasoundAsync(new RecordUltrasoundViewModel { Tag = "DOE-B", KidsCount = 1, Date = today.AddDays(-10) });
        await breeding.RecordCrossAsync(new RecordCrossViewModel { Tag = "DOE-C", Date = today.AddDays(-90), BuckTag = "UNREGISTERED-BUCK" });
        await breeding.RecordUltrasoundAsync(new RecordUltrasoundViewModel { Tag = "DOE-C", KidsCount = 0, Date = today.AddDays(-40) });
        await breeding.RecordCrossAsync(new RecordCrossViewModel { Tag = "DOE-C", Date = today.AddDays(-20), BuckTag = "BUCK-X" });

        await tasks.CreateAsync(new SaveTaskViewModel { Title = "Check DOE-B udder", AssigneeUserId = "user-staff-1",
            Priority = FarmTaskPriority.High, DueDate = today.AddDays(3), GoatTag = "DOE-B", Notes = "اردو نوٹ" }, "user-staff-1");
        var done = await tasks.CreateAsync(new SaveTaskViewModel { Title = "Clean pen", Type = FarmTaskType.Reminder }, "user-staff-1");
        await tasks.SetStatusAsync(done.Id, FarmTaskStatus.Completed, "user-staff-1", canEditAny: true);
    }

    private static CreateGoatViewModel Goat(string tag, GoatGender gender, GoatStatus status, DateOnly date,
        string? sire = null, string? dam = null, string? owner = null, GoatHealthState health = GoatHealthState.Healthy) => new()
    {
        Tag = tag, Name = tag + " name", Breed = "Beetal", Gender = gender, Status = status,
        Source = GoatSource.Born, EventDate = date, SireTag = sire, DamTag = dam, Owner = owner, HealthState = health
    };
}

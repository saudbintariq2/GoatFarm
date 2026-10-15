using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Breeding;
using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GoatFarm.Tests.Postgres;

[Trait("Category", "Postgres")]
public class BirthIdempotencyPgTests
{
    [SkippableFact]
    public async Task ConcurrentSubmitsOfTheSameBirth_CreateOneRecord_AndAllReturnSuccess()
    {
        await using var pg = await PostgresTestDatabase.CreateAsync();
        await using (var db = pg.CreateContext())
        {
            await FarmScenario.SeedDefaultsAsync(db);
            db.Goats.Add(TestDb.Doe("RACE-DOE", ageDays: 700));
            await db.SaveChangesAsync();
        }

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = pg.ConnectionString })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(config);
        await using var provider = services.BuildServiceProvider();

        using (var setup = provider.CreateScope())
        {
            var breeding = setup.ServiceProvider.GetRequiredService<IBreedingService>();
            await breeding.RecordCrossAsync(new RecordCrossViewModel { Tag = "RACE-DOE", Date = TestDb.Today.AddDays(-148), ConfirmWarnings = true });
            await breeding.RecordUltrasoundAsync(new RecordUltrasoundViewModel { Tag = "RACE-DOE", KidsCount = 2, Date = TestDb.Today.AddDays(-100) });
        }

        var request = new RecordBirthViewModel
        {
            ClientRequestId = Guid.NewGuid(),
            DoeTag = "RACE-DOE",
            KiddingDate = TestDb.Today,
            Kids = [new BirthKidViewModel { Gender = GoatGender.Female }, new BirthKidViewModel { Gender = GoatGender.Male }]
        };

        async Task<RecordBirthResultViewModel> Submit()
        {
            using var scope = provider.CreateScope();
            await Task.Yield();
            return await scope.ServiceProvider.GetRequiredService<IBreedingService>().RecordBirthAsync(request);
        }

        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(Submit)));

        Assert.Single(results, r => !r.AlreadyRecorded);
        Assert.All(results, r => Assert.Equal(2, r.Kids.Count));
        Assert.Single(results.Select(r => r.KiddingRecordId).Distinct());
        Assert.Equal(1, await pg.ScalarAsync("select count(*) from \"KiddingRecords\""));
        Assert.Equal(2, await pg.ScalarAsync("select count(*) from \"Goats\" g join \"Goats\" d on d.\"Id\" = g.\"DamId\" where d.\"Tag\" = 'RACE-DOE'"));
    }
}

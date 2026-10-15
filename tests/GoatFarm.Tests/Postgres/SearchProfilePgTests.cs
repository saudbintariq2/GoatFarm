using GoatFarm.Application.Interfaces;
using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GoatFarm.Tests.Postgres;

[Trait("Category", "Postgres")]
public class SearchProfilePgTests
{
    [SkippableTheory]
    [InlineData(GoatStatus.Milking)]
    [InlineData(GoatStatus.Dry)]
    [InlineData(GoatStatus.Kid)]
    public async Task GoatProfile_LoadsOnPostgres_IncludingFeedPlanCost(GoatStatus status)
    {
        await using var pg = await PostgresTestDatabase.CreateAsync();
        await using (var db = pg.CreateContext())
        {
            await FarmScenario.SeedDefaultsAsync(db);
            db.Goats.Add(TestDb.Doe("PROFILE-1", status: status));
            await db.SaveChangesAsync();
        }

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = pg.ConnectionString })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(config);
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var profile = await scope.ServiceProvider.GetRequiredService<ISearchService>().GetProfileByTagAsync("profile-1");

        Assert.NotNull(profile);
        Assert.NotNull(profile!.FeedPlan);
        var feed = scope.ServiceProvider.GetRequiredService<IFeedService>();
        Assert.True(feed.CalculateDailyFeedCost(1m, "milking") >= 0);
        Assert.True(feed.CalculateDailyFeedCost(1m, "not-a-status") >= 0);
    }
}

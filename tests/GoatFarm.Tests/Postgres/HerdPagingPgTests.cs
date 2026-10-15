using GoatFarm.Infrastructure.Services;

namespace GoatFarm.Tests.Postgres;

[Trait("Category", "Postgres")]
public class HerdPagingPgTests
{
    [SkippableFact]
    public async Task EveryFilterAndSearch_TranslatesToSqlAndMatches()
    {
        await using var pg = await PostgresTestDatabase.CreateAsync();
        await using (var seed = pg.CreateContext())
            await HerdFixture.SeedFilterCasesAsync(seed);

        await using var db = pg.CreateContext();
        var svc = new GoatService(db);
        foreach (var (filter, search, tags) in HerdFixture.Cases)
        {
            var page = await svc.GetHerdPageAsync(filter, 1, 100, search);
            Assert.True(tags.Order().SequenceEqual(page.Goats.Select(g => g.Tag).Order()),
                $"{filter} / {search}: expected [{string.Join(",", tags.Order())}] got [{string.Join(",", page.Goats.Select(g => g.Tag).Order())}]");
        }
    }

    [SkippableFact]
    public async Task Page_IsLimitedInSqlAndMaterializesOnlyOnePage()
    {
        await using var pg = await PostgresTestDatabase.CreateAsync();
        await using (var seed = pg.CreateContext())
            await HerdFixture.SeedManyAsync(seed, 250);

        var sql = new List<string>();
        var counter = new GoatMaterializationCounter();
        await using var db = pg.CreateContext(sql.Add, counter);
        var page = await new GoatService(db).GetHerdPageAsync("st:dry", 3, 10, "h0");

        Assert.Equal(10, counter.Count);
        Assert.Equal(10, page.Goats.Count);
        Assert.Equal(250, page.Stats.Total);
        Assert.Equal(250, page.Pagination.TotalItems);
        var goatSelect = Assert.Single(sql, s => s.Contains("FROM \"Goats\"") && s.Contains("ORDER BY") && s.Contains("LIMIT"));
        Assert.Contains("OFFSET", goatSelect);
        Assert.Contains(sql, s => s.Contains("count(*)") && s.Contains("FROM \"Goats\""));
    }
}

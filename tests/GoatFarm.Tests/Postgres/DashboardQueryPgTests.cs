using System.Diagnostics;
using System.Text;
using GoatFarm.Application.Common;
using GoatFarm.Application.Interfaces;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure;
using GoatFarm.Infrastructure.Diagnostics;
using GoatFarm.Infrastructure.Persistence;
using GoatFarm.Infrastructure.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace GoatFarm.Tests.Postgres;

/// <summary>Measures the dashboard build (cache bypassed) on a representative disposable farm.</summary>
[Trait("Category", "Postgres")]
public class DashboardQueryPgTests(ITestOutputHelper output)
{
    internal static ServiceProvider BuildServices(PostgresTestDatabase pg, bool pooled = true)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = pooled ? pg.PooledConnectionString : pg.ConnectionString
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(config);
        return services.BuildServiceProvider();
    }

    /// <summary>~120 goats in every status plus a year of feed, milk, finance and vaccine entries.</summary>
    internal static async Task SeedRepresentativeFarmAsync(GoatFarmDbContext db)
    {
        await FarmScenario.SeedDefaultsAsync(db);
        await FarmScenario.BuildAsync(db);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var statuses = new[] { GoatStatus.Kid, GoatStatus.Milking, GoatStatus.Pregnant, GoatStatus.Dry, GoatStatus.Buck, GoatStatus.Sale, GoatStatus.Meat };
        for (var i = 0; i < 112; i++)
        {
            var status = statuses[i % statuses.Length];
            db.Goats.Add(new Goat
            {
                Tag = $"R{i:000}", Breed = i % 3 == 0 ? "Beetal" : "Teddy",
                Gender = status == GoatStatus.Buck || i % 5 == 0 ? GoatGender.Male : GoatGender.Female,
                Status = status, Source = i % 4 == 0 ? GoatSource.Bought : GoatSource.Born,
                PurchasePrice = i % 4 == 0 ? 40000 + i * 100 : 0,
                EventDate = today.AddDays(-(60 + i * 9)), IsArchived = i % 17 == 0
            });
        }
        for (var d = 0; d < 365; d += 3)
        {
            var date = today.AddDays(-d);
            db.MilkProductions.Add(new MilkProduction { Date = date, Liters = 40 + d % 7 });
            db.MilkSales.Add(new MilkSale { Date = date, Liters = 30, Rate = 180, Amount = 5400 });
            if (d % 15 == 0)
            {
                db.FeedPurchases.Add(new FeedPurchase { Date = date, FeedType = "wanda", Kg = 500, RatePerKg = 95, Amount = 47500 });
                db.Expenses.Add(new Expense { Date = date, Type = "Electricity", Amount = 8000 });
                db.Incomes.Add(new Income { Date = date, Type = "Goat sale", Amount = 65000 });
                db.VaccinePurchases.Add(new VaccinePurchase { Date = date, Name = "PPR", Qty = 50, Unit = "dose", Amount = 3000 });
            }
        }
        db.Employees.Add(new Employee { Name = "Worker A", MonthlySalary = 30000 });
        db.Employees.Add(new Employee { Name = "Worker B", MonthlySalary = 25000 });
        db.OwnerInvestments.Add(new OwnerInvestment { Date = today.AddDays(-30), Note = "Top-up", Amount = 200000 });
        await db.SaveChangesAsync();
    }

    internal sealed record DashboardRun(int Queries, double DbMs, double TotalMs, int StatusCountQueries, string TopRepeated);

    internal static async Task<DashboardRun> MeasureDashboardBuildAsync(ServiceProvider sp)
    {
        var month = MonthHelper.CurrentMonthKey();
        sp.GetRequiredService<IMemoryCache>().Remove($"dashboard:{month}");
        using var scope = sp.CreateScope();
        var metrics = new QueryMetrics(captureCommandText: true);
        var clock = Stopwatch.StartNew();
        using (QueryMetrics.Begin(metrics))
            await scope.ServiceProvider.GetRequiredService<IStatisticsService>().GetDashboardAsync(month);
        clock.Stop();

        var texts = metrics.CommandTexts!;
        var statusCounts = texts.Where(t => t.Key.Contains("count(*)") && t.Key.Contains("\"Status\" = @")).Sum(t => t.Value);
        var top = string.Join(" | ", texts.OrderByDescending(t => t.Value).Take(3)
            .Select(t => $"{t.Value}x {Flatten(t.Key)[..Math.Min(90, Flatten(t.Key).Length)]}"));
        return new DashboardRun(metrics.Commands, metrics.DatabaseTime.TotalMilliseconds, clock.Elapsed.TotalMilliseconds, statusCounts, top);
    }

    private static string Flatten(string sql) => string.Join(' ', sql.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    [SkippableFact]
    public async Task DashboardBuild_QueryCountAndTiming()
    {
        await using var pg = await PostgresTestDatabase.CreateAsync();
        await using (var db = pg.CreateContext())
            await SeedRepresentativeFarmAsync(db);
        var report = new StringBuilder();
        foreach (var pooled in new[] { true, false })
        {
            await using var sp = BuildServices(pg, pooled);
            for (var i = 0; i < 4; i++)
            {
                var r = await MeasureDashboardBuildAsync(sp);
                report.AppendLine($"{(pooled ? "pooled" : "no pooling")} run {i + 1}{(i == 0 ? " (first in process/provider)" : "")}: " +
                    $"{r.Queries} queries, {r.StatusCountQueries} per-status counts, {r.DbMs:F0} ms db, {r.TotalMs:F0} ms total; top: {r.TopRepeated}");
            }
        }
        output.WriteLine(report.ToString());
        if (Environment.GetEnvironmentVariable("GOATFARM_METRICS_OUT") is { Length: > 0 } path)
            await File.AppendAllTextAsync(path, $"[{DateTime.Now:s}] dashboard build\n{report}");
    }

    [SkippableFact]
    public async Task DashboardBuild_RunsNoPerStatusCountQueries()
    {
        await using var pg = await PostgresTestDatabase.CreateAsync();
        await using (var db = pg.CreateContext())
            await SeedRepresentativeFarmAsync(db);
        await using var sp = BuildServices(pg);

        var run = await MeasureDashboardBuildAsync(sp);

        Assert.Equal(0, run.StatusCountQueries);
        Assert.True(run.Queries <= 80, $"Dashboard build ran {run.Queries} queries; top: {run.TopRepeated}");
    }

    /// <summary>The pre-optimisation behaviour: one count query per call.</summary>
    private sealed class PerStatusCountingGoatService(GoatService inner, GoatFarmDbContext db) : IGoatService
    {
        public int CountByStatus(GoatStatus status, IReadOnlyList<Application.ViewModels.Goats.GoatViewModel>? goats = null) =>
            goats?.Count(g => g.Status == status) ?? db.Goats.Count(g => !g.IsArchived && g.Status == status);
        public IReadOnlyDictionary<GoatStatus, int> GetLiveStatusCounts() =>
            Enum.GetValues<GoatStatus>().ToDictionary(s => s, s => CountByStatus(s));
        public Task<Application.ViewModels.Goats.HerdPageViewModel> GetHerdPageAsync(string? filter, int page = 1, int pageSize = 10, string? search = null, CancellationToken ct = default) => inner.GetHerdPageAsync(filter, page, pageSize, search, ct);
        public Task<Application.ViewModels.Goats.GoatViewModel?> GetByIdAsync(int id, CancellationToken ct = default) => inner.GetByIdAsync(id, ct);
        public Task<Application.ViewModels.Goats.GoatViewModel?> GetByTagAsync(string tag, CancellationToken ct = default) => inner.GetByTagAsync(tag, ct);
        public Task<Application.ViewModels.Goats.GoatViewModel> CreateAsync(Application.ViewModels.Goats.CreateGoatViewModel m, CancellationToken ct = default) => inner.CreateAsync(m, ct);
        public Task<Application.ViewModels.Goats.GoatViewModel?> UpdateAsync(int id, Application.ViewModels.Goats.CreateGoatViewModel m, CancellationToken ct = default) => inner.UpdateAsync(id, m, ct);
        public Task<bool> DeleteAsync(int id, CancellationToken ct = default) => inner.DeleteAsync(id, ct);
        public Task BulkMoveAsync(Application.ViewModels.Goats.BulkMoveViewModel m, CancellationToken ct = default) => inner.BulkMoveAsync(m, ct);
        public Task<Application.ViewModels.Goats.ParentOptionsViewModel> GetParentOptionsAsync(CancellationToken ct = default) => inner.GetParentOptionsAsync(ct);
        public Task<IReadOnlyList<string>> GetGroupsAsync(CancellationToken ct = default) => inner.GetGroupsAsync(ct);
        public Task<string> CreateGroupAsync(string name, CancellationToken ct = default) => inner.CreateGroupAsync(name, ct);
        public int GetAgeInDays(DateOnly d) => inner.GetAgeInDays(d);
        public string GetAgeLabel(int days) => inner.GetAgeLabel(days);
        public Task<Application.ViewModels.Goats.WeightDeathResultViewModel> RecordWeightAsync(Application.ViewModels.Goats.RecordWeightViewModel m, CancellationToken ct = default) => inner.RecordWeightAsync(m, ct);
        public Task<Application.ViewModels.Goats.WeightDeathResultViewModel> RecordDeathAsync(Application.ViewModels.Goats.RecordDeathViewModel m, CancellationToken ct = default) => inner.RecordDeathAsync(m, ct);
    }

    [SkippableFact]
    public async Task DashboardAndReports_ShowTheSameFiguresAsPerStatusCounting()
    {
        await using var pg = await PostgresTestDatabase.CreateAsync();
        await using (var db = pg.CreateContext())
            await SeedRepresentativeFarmAsync(db);

        async Task<(string Dashboard, string Reports)> BuildAsync(bool perStatus)
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                { ["ConnectionStrings:DefaultConnection"] = pg.PooledConnectionString }).Build();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddInfrastructure(config);
            if (perStatus)
                services.AddScoped<IGoatService>(s => new PerStatusCountingGoatService(
                    new GoatService(s.GetRequiredService<GoatFarmDbContext>()), s.GetRequiredService<GoatFarmDbContext>()));
            await using var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var dash = await scope.ServiceProvider.GetRequiredService<IStatisticsService>().GetDashboardAsync(MonthHelper.CurrentMonthKey());
            var rep = await scope.ServiceProvider.GetRequiredService<IReportsService>().GetReportsPageAsync("year", null, null);
            return (System.Text.Json.JsonSerializer.Serialize(dash), System.Text.Json.JsonSerializer.Serialize(rep));
        }

        var optimised = await BuildAsync(perStatus: false);
        var original = await BuildAsync(perStatus: true);
        Assert.Equal(original.Dashboard, optimised.Dashboard);
        Assert.Equal(original.Reports, optimised.Reports);
    }

    [SkippableFact]
    public async Task GroupedStatusCounts_MatchPerStatusSqlCounts_AndExcludeArchived()
    {
        await using var pg = await PostgresTestDatabase.CreateAsync();
        await using (var db = pg.CreateContext())
            await SeedRepresentativeFarmAsync(db);
        await using var sp = BuildServices(pg);
        using var scope = sp.CreateScope();
        var goats = scope.ServiceProvider.GetRequiredService<IGoatService>();

        var counts = goats.GetLiveStatusCounts();
        foreach (var status in Enum.GetValues<GoatStatus>())
        {
            var sql = await pg.ScalarAsync($"""SELECT count(*) FROM "Goats" WHERE NOT "IsArchived" AND "Status" = {(int)status}""");
            Assert.Equal(sql, counts.GetValueOrDefault(status));
            Assert.Equal(sql, goats.CountByStatus(status));
        }
        Assert.True(await pg.ScalarAsync("""SELECT count(*) FROM "Goats" WHERE "IsArchived" """) > 0);
    }

    [SkippableFact]
    public async Task StatusCounts_RefreshAfterASaveInTheSameRequest()
    {
        await using var pg = await PostgresTestDatabase.CreateAsync();
        await using (var db = pg.CreateContext())
            await SeedRepresentativeFarmAsync(db);
        await using var sp = BuildServices(pg);
        using var scope = sp.CreateScope();
        var goats = scope.ServiceProvider.GetRequiredService<IGoatService>();
        var db2 = scope.ServiceProvider.GetRequiredService<GoatFarmDbContext>();

        var milkingBefore = goats.CountByStatus(GoatStatus.Milking);
        var dryBefore = goats.CountByStatus(GoatStatus.Dry);
        var doe = db2.Goats.First(g => !g.IsArchived && g.Status == GoatStatus.Milking);
        doe.Status = GoatStatus.Dry;
        await db2.SaveChangesAsync();

        Assert.Equal(milkingBefore - 1, goats.CountByStatus(GoatStatus.Milking));
        Assert.Equal(dryBefore + 1, goats.CountByStatus(GoatStatus.Dry));

        db2.Goats.First(g => !g.IsArchived && g.Status == GoatStatus.Dry).IsArchived = true;
        await db2.SaveChangesAsync();
        Assert.Equal(dryBefore, goats.CountByStatus(GoatStatus.Dry));
    }
}

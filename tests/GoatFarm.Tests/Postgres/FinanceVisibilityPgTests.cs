using System.Text.Json;
using GoatFarm.Application.Common;
using GoatFarm.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace GoatFarm.Tests.Postgres;

[Trait("Category", "Postgres")]
public class FinanceVisibilityPgTests
{
    private static readonly string[] FinancialKpis =
        ["Revenue", "Expenses", "Net profit", "Health cost/goat", "Feed share of costs", "Medicine & vet", "Assets & equipment", "Total capital"];

    [SkippableFact]
    public async Task RealReport_FlagsEveryFinancialFigure_AndRedactionRemovesThem()
    {
        await using var pg = await PostgresTestDatabase.CreateAsync();
        await using (var db = pg.CreateContext())
            await DashboardQueryPgTests.SeedRepresentativeFarmAsync(db);
        await using var sp = DashboardQueryPgTests.BuildServices(pg);
        using var scope = sp.CreateScope();

        var full = await scope.ServiceProvider.GetRequiredService<IReportsService>().GetReportsPageAsync("year", null, null);
        Assert.True(full.Finance.TotalIncome > 0 && full.Finance.TotalExpense > 0 && full.Inventory.AssetsTotalValue >= 0);
        var allKpis = full.Dashboard.Kpis.Concat(full.Dashboard.Ratios).Concat(full.Feed.Kpis).Concat(full.Health.Kpis)
            .Concat(full.Inventory.Kpis).Concat(full.Growth.Kpis).Concat(full.Breeding.Kpis).ToList();
        foreach (var label in FinancialKpis)
            Assert.All(allKpis.Where(k => k.Label == label), k => Assert.True(k.IsFinancial, label + " must be flagged financial"));
        Assert.Contains(allKpis, k => k.IsFinancial);

        var shown = FinanceVisibility.WithoutFinance(full);
        var json = JsonSerializer.Serialize(shown);
        Assert.DoesNotContain("\"IsFinancial\":true", json);
        Assert.Equal(0, shown.Finance.TotalIncome + shown.Finance.TotalExpense + shown.Finance.Profit + shown.Finance.TotalOwnerInvestment);
        Assert.Empty(shown.Dashboard.TrendRows);
        Assert.Empty(shown.Comparison.Rows.Where(r => r.Label is "Revenue" or "Expenses" or "Net profit" or "Money put in"));
        Assert.Contains(shown.Comparison.Rows, r => r.Label == "Births");
        Assert.Equal(full.Herd.Movement.Count, shown.Herd.Movement.Count);
        Assert.Equal(full.Breeding.Kpis.Count, shown.Breeding.Kpis.Count);
        Assert.Equal(full.Alerts.Items.Count, shown.Alerts.Items.Count);

        var dash = await scope.ServiceProvider.GetRequiredService<IStatisticsService>().GetDashboardAsync(null);
        Assert.True(dash.TotalIncome > 0);
        var dashShown = FinanceVisibility.WithoutFinance(dash);
        Assert.Equal(0, dashShown.TotalIncome + dashShown.TotalExpense + dashShown.Capital + dashShown.Profit);
        Assert.Equal(dash.HerdStats.Total, dashShown.HerdStats.Total);
        Assert.True((await scope.ServiceProvider.GetRequiredService<IStatisticsService>().GetDashboardAsync(null)).TotalIncome > 0,
            "the cached dashboard must not be redacted for the next (Finance) user");
    }
}

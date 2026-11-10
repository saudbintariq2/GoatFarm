using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Approval;
using GoatFarm.Application.ViewModels.Finance;
using GoatFarm.Domain.Constants;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure.Persistence;
using GoatFarm.Infrastructure.Services;
using GoatFarm.Infrastructure.Services.Extended;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace GoatFarm.Tests;

public class Phase5DashboardFinanceTests
{
    public static (FinanceService Finance, StatisticsService Stats, GoatFarmDbContext Db) Create()
    {
        var db = TestDb.Create();
        var goats = new GoatService(db);
        var feed = new FeedService(db, goats, new MilkService(db));
        var finance = new FinanceService(db, feed, new MilkService(db), new VaccineService(db));
        var stats = new StatisticsService(
            goats,
            feed,
            finance,
            new MilkService(db),
            new VaccineService(db),
            new ReminderService(db),
            new BreedingService(db, goats, feed),
            new LedgerService(db),
            new StubApprovalService(),
            db,
            new MemoryCache(new MemoryCacheOptions()));
        return (finance, stats, db);
    }

    [Fact]
    public async Task FinanceMonthSnapshot_MatchesOverviewTotals()
    {
        var (finance, _, db) = Create();
        var month = $"{TestDb.Today.Year:0000}-{TestDb.Today.Month:00}";

        db.Incomes.Add(new Income { Type = "Test sale", Amount = 1000, Date = TestDb.Today });
        db.Expenses.Add(new Expense { Type = "Fuel", Amount = 200, Date = TestDb.Today });
        await db.SaveChangesAsync();

        var snapshot = await finance.GetFinanceMonthSnapshotAsync(month);
        var full = await finance.GetFinancePageAsync(month, FinancePageLoadOptions.Overview);

        Assert.Equal(full.DisplayIncome, snapshot.TotalIncome);
        Assert.Equal(full.DisplayExpense, snapshot.TotalExpense);
        Assert.Equal(full.Profit, snapshot.Profit);
        Assert.Contains(snapshot.IncomeLines, l => l.Label == "Test sale" && l.Amount == 1000);
    }

    [Fact]
    public async Task OverviewLoad_SkipsSalesRegister()
    {
        var (finance, _, db) = Create();
        var month = $"{TestDb.Today.Year:0000}-{TestDb.Today.Month:00}";
        db.MilkSales.Add(new MilkSale { Date = TestDb.Today, Amount = 500, Liters = 10 });
        await db.SaveChangesAsync();

        var overview = await finance.GetFinancePageAsync(month, FinancePageLoadOptions.Overview);
        var full = await finance.GetFinancePageAsync(month, FinancePageLoadOptions.Full);

        Assert.Empty(overview.Sales);
        Assert.NotEmpty(full.Sales);
    }

    [Fact]
    public async Task Dashboard_IncludesLowFeedInTodayPanel()
    {
        var (_, stats, db) = Create();
        db.FeedPrices.Add(new FeedPrice
        {
            FeedType = FeedTypes.Bran,
            DisplayName = "Bran",
            PricePerKg = 50,
            StockKg = 20,
            StockKgFull = 200
        });
        for (var i = 0; i < 5; i++)
        {
            db.FeedUsageRecords.Add(new FeedUsageRecord
            {
                FeedType = FeedTypes.Bran,
                Date = TestDb.Today.AddDays(-i),
                Kg = 10,
                IsAutomatic = true
            });
        }
        await db.SaveChangesAsync();

        var dash = await stats.GetDashboardAsync(null);
        Assert.Contains(dash.TodayAtFarm, t => t.Text.Contains("Bran", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class StubApprovalService : IApprovalService
    {
        public Task<int> CountPendingAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<ApprovalPageViewModel> GetPageAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ApprovalPageViewModel());
        public Task<ApprovalRequestViewModel?> SubmitAsync(string module, string actionKey, string payloadJson, string userId, string? userDisplay, CancellationToken cancellationToken = default) =>
            Task.FromResult<ApprovalRequestViewModel?>(null);
        public Task<bool> ReviewAsync(int id, bool approve, string reviewerId, string? reviewerDisplay, string? note, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
        public Task<bool> NeedsApprovalAsync(string userId, string role, string action, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }
}

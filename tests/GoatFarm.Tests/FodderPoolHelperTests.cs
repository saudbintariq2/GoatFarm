using GoatFarm.Application.Common;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Tests;

public class FodderPoolHelperTests
{
    [Fact]
    public void SpreadOverMonths_PutsRemainderOnLastMonth()
    {
        var parts = FodderPoolHelper.SpreadOverMonths(28000, 3);
        Assert.Equal([9333m, 9333m, 9334m], parts);
        Assert.Equal(28000, parts.Sum());
    }

    [Fact]
    public void SpreadOverMonths_DividesEvenly()
    {
        var parts = FodderPoolHelper.SpreadOverMonths(36000, 3);
        Assert.Equal([12000m, 12000m, 12000m], parts);
    }

    [Fact]
    public void FodderMatch_FindsRecurringCost()
    {
        var recurring = new List<RecurringCost>
        {
            new() { Name = "Land rent", Amount = 240000, Period = RecurringCostPeriod.Year }
        };
        var match = FodderFinanceMatchHelper.TryMatch("Land rent", recurring, [], 2026);
        Assert.NotNull(match);
        Assert.Equal("Land rent", match!.Name);
    }

    [Fact]
    public async Task RemoveFodderPoolItem_ClearsFinanceEntries()
    {
        var db = TestDb.Create();
        var acct = new MoneyAccount { Name = "Farm Bank", IsActive = true, OpeningBalance = 500000 };
        db.MoneyAccounts.Add(acct);
        await db.SaveChangesAsync();

        var goats = new GoatService(db);
        var feed = new FeedService(db, goats, new MilkService(db));

        var paid = new DateOnly(2026, 1, 1);
        await feed.UpdateFodderPoolAsync(new Application.ViewModels.Feed.UpdateFodderPoolViewModel
        {
            Items =
            [
                new()
                {
                    Id = "rent",
                    Label = "Land rent",
                    Amount = 36000,
                    PaidDate = paid.ToString("yyyy-MM-dd"),
                    AllocateMonths = 3,
                    AccountId = acct.Id
                }
            ]
        });

        Assert.Equal(3, await db.Expenses.CountAsync(e => e.Comment != null && e.Comment.StartsWith("fodder_pool:rent:pl:")));
        Assert.Equal(1, await db.LedgerTransactions.CountAsync(t => t.Reference == "fodder_pool:rent"));

        await feed.RemoveFodderPoolItemAsync("rent");

        Assert.Equal(0, await db.Expenses.CountAsync(e => e.Comment != null && e.Comment.StartsWith("fodder_pool:rent")));
        Assert.Equal(0, await db.LedgerTransactions.CountAsync(t => t.Reference == "fodder_pool:rent"));
    }
}
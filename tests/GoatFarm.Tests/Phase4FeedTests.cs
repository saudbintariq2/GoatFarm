using GoatFarm.Application.ViewModels.Feed;
using GoatFarm.Domain.Constants;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure.Persistence;
using GoatFarm.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Tests;

public class Phase4FeedTests
{
    private static (FeedService Feed, GoatFarmDbContext Db) Create()
    {
        var db = TestDb.Create();
        var goats = new GoatService(db);
        return (new FeedService(db, goats, new MilkService(db)), db);
    }

    [Fact]
    public async Task RebuildStock_MatchesPurchasesMinusUsage()
    {
        var (feed, db) = Create();
        db.FeedPrices.Add(new FeedPrice
        {
            FeedType = FeedTypes.Bran,
            DisplayName = "Bran",
            PricePerKg = 50,
            StockKg = 999,
            StockKgFull = 100
        });
        db.FeedPurchases.Add(new FeedPurchase
        {
            FeedType = FeedTypes.Bran,
            Date = TestDb.Today,
            Kg = 100,
            RatePerKg = 50,
            Amount = 5000
        });
        db.FeedUsageRecords.Add(new FeedUsageRecord
        {
            FeedType = FeedTypes.Bran,
            Date = TestDb.Today,
            Kg = 30,
            IsAutomatic = true
        });
        await db.SaveChangesAsync();

        var before = (await feed.GetFeedStockRowsAsync()).Single(s => s.FeedType == FeedTypes.Bran);
        Assert.Equal(70, before.StockKg);

        var price = await db.FeedPrices.SingleAsync(p => p.FeedType == FeedTypes.Bran);
        price.StockKg = 10;
        await db.SaveChangesAsync();

        var result = await feed.RebuildFeedStockAsync();
        Assert.True(result.Adjusted >= 1);
        price = await db.FeedPrices.SingleAsync(p => p.FeedType == FeedTypes.Bran);
        Assert.Equal(70, price.StockKg);
    }

    [Fact]
    public async Task GetFeedStockRows_MatchesStoredAfterRebuild()
    {
        var (feed, db) = Create();
        db.FeedPrices.Add(new FeedPrice
        {
            FeedType = FeedTypes.Binola,
            DisplayName = "Binola",
            PricePerKg = 80,
            StockKg = 5,
            StockKgFull = 200
        });
        db.FeedPurchases.Add(new FeedPurchase
        {
            FeedType = FeedTypes.Binola,
            Date = TestDb.Today,
            Kg = 200,
            RatePerKg = 80,
            Amount = 16000
        });
        await db.SaveChangesAsync();
        await feed.RebuildFeedStockAsync();
        var rows = await feed.GetFeedStockRowsAsync();
        var stored = await db.FeedPrices.SingleAsync(p => p.FeedType == FeedTypes.Binola);
        Assert.Equal(stored.StockKg, rows.Single(r => r.FeedType == FeedTypes.Binola).StockKg);
    }

    [Fact]
    public void GetFeedPurchasedMonthly_SpreadsAllocation()
    {
        var (feed, db) = Create();
        var month = TestDb.Today.ToString("yyyy-MM");
        db.FeedPurchases.Add(new FeedPurchase
        {
            FeedType = FeedTypes.Bran,
            Date = TestDb.Today,
            Kg = 60,
            RatePerKg = 10,
            Amount = 12000,
            AllocationMonths = 3
        });
        db.SaveChanges();
        Assert.Equal(4000, feed.GetFeedPurchasedMonthly(month));
    }
}

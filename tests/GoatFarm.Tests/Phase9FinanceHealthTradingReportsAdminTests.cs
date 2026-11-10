using GoatFarm.Application.Common;
using GoatFarm.Application.ViewModels.Finance;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure.Persistence;
using GoatFarm.Infrastructure.Services;
using GoatFarm.Infrastructure.Services.Extended;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace GoatFarm.Tests;

public class Phase9FinanceHealthTradingReportsAdminTests
{
    private static IConfiguration DeviceConfig() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ApplicationSettings:DeviceCode"] = "K7Q"
        }).Build();

    [Fact]
    public void FormatTradingBill_MatchesV242()
    {
        var n = FarmRecordNumberFormat.FormatTradingBill("k7q", new DateOnly(2026, 10, 7), 3);
        Assert.Equal("TR-202610-K7Q-003", n);
    }

    [Fact]
    public async Task SaveTradeSell_AssignsServerBillNumber()
    {
        var db = TestDb.Create();
        await new TradingService(db, DeviceConfig()).EnsureSeededAsync();
        var item = await db.TradeItems.FirstAsync();
        var store = await db.TradeStores.FirstAsync();
        var acct = await db.TradeAccounts.FirstAsync();
        var svc = new TradingService(db, DeviceConfig());
        await svc.SaveEntryAsync(new Application.ViewModels.Trading.SaveTradeEntryViewModel
        {
            Type = "buy",
            Date = TestDb.Today.AddDays(-1),
            ItemId = item.Id,
            StoreId = store.Id,
            AccountId = acct.Id,
            Qty = 100,
            Rate = 50,
            Paid = 5000,
            Payment = "paid"
        });
        await svc.SaveEntryAsync(new Application.ViewModels.Trading.SaveTradeEntryViewModel
        {
            Type = "sell",
            Date = TestDb.Today,
            ItemId = item.Id,
            StoreId = store.Id,
            AccountId = acct.Id,
            Qty = 10,
            Rate = 100,
            Received = 1000,
            Payment = "paid"
        });
        var row = await db.TradeEntries.OrderByDescending(x => x.Id).FirstAsync();
        Assert.StartsWith("TR-", row.BillNo);
        Assert.Contains("K7Q", row.BillNo);
    }

    [Fact]
    public async Task BuildMonthlyClosingRows_CarriesCashForward()
    {
        var db = TestDb.Create();
        var acct = new MoneyAccount { Name = "Cash", Kind = MoneyAccountKind.Cash, OpeningBalance = 1000, IsActive = true };
        db.MoneyAccounts.Add(acct);
        await db.SaveChangesAsync();
        var feed = new FeedService(db, new GoatService(db), new MilkService(db));
        var finance = new FinanceService(db, feed, new MilkService(db), new VaccineService(db));
        var month = $"{TestDb.Today.Year:0000}-{TestDb.Today.Month:00}";
        var rows = await finance.BuildMonthlyClosingRowsAsync(month);
        Assert.NotEmpty(rows);
        Assert.Equal(rows[^1].ClosingCash, rows[^1].OpeningCash + rows[^1].MoneyIn - rows[^1].MoneyOut, 1);
    }

    [Fact]
    public async Task VaccinePage_IncludesTagHistory()
    {
        var db = TestDb.Create();
        var goat = new Goat { Tag = "G-99", Breed = "Tedda", Gender = GoatGender.Female };
        var vaccine = new Vaccine { Name = "PPR", Scope = VaccineScope.All, RuleType = VaccineRuleType.Repeat, Months = 12 };
        db.Goats.Add(goat);
        db.Vaccines.Add(vaccine);
        await db.SaveChangesAsync();
        db.VaccinationHistories.Add(new VaccinationHistory
        {
            GoatId = goat.Id,
            VaccineId = vaccine.Id,
            VaccinationDate = TestDb.Today.AddDays(-1)
        });
        await db.SaveChangesAsync();
        var page = await new VaccineService(db).GetVaccinePageAsync(null);
        Assert.Contains(page.TagHistory, h => h.GoatTag == "G-99" && h.VaccineName == "PPR");
    }
}

using GoatFarm.Application.Common;
using GoatFarm.Application.ViewModels.Ledger;
using GoatFarm.Application.ViewModels.Finance;
using GoatFarm.Application.ViewModels.Goats;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure.Persistence;
using GoatFarm.Infrastructure.Services;
using GoatFarm.Infrastructure.Services.Extended;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Tests;

public class FinancePhaseFnTests
{
    private static FinanceService CreateFinance(GoatFarmDbContext db)
    {
        var goats = TestFarmMetrics.CreateGoatService(db);
        var metrics = TestFarmMetrics.Create(db, goats);
        return new FinanceService(db, new FeedService(db, goats, new MilkService(db), metrics), new MilkService(db), new VaccineService(db));
    }

    [Fact]
    public async Task RecordDeath_WritesLivestockLossExpense()
    {
        var db = TestDb.Create();
        var goat = new Goat { Tag = "D-115", Breed = "Tedda", Gender = GoatGender.Female, PurchasePrice = 38000, Source = GoatSource.Bought };
        db.Goats.Add(goat);
        await db.SaveChangesAsync();
        var svc = TestFarmMetrics.CreateGoatService(db);
        await svc.RecordDeathAsync(new RecordDeathViewModel { Tag = "D-115", Date = TestDb.Today, Reason = "illness" });
        var exp = await db.Expenses.SingleAsync();
        Assert.Equal("Livestock loss – death", exp.Type);
        Assert.Equal(38000, exp.Amount);
    }

    [Fact]
    public async Task SavePayable_PostToExpense_CreatesExpenseWithoutSecondCall()
    {
        var db = TestDb.Create();
        db.MoneyAccounts.Add(new MoneyAccount { Name = "Cash", Kind = MoneyAccountKind.Cash, IsActive = true, OpeningBalance = 10000 });
        await db.SaveChangesAsync();
        var ledger = new LedgerService(db);
        await ledger.SavePayableAsync(new SaveSupplierPayableViewModel
        {
            SupplierName = "TEST Feed Supplier",
            Date = TestDb.Today,
            Description = "Wheat bran",
            Amount = 24000,
            PaidAmount = 0,
            PostToExpense = true,
            ExpenseType = "Feed"
        });
        Assert.Single(await db.SupplierPayables.ToListAsync());
        Assert.Single(await db.Expenses.ToListAsync());
    }

    [Fact]
    public async Task Statement_UsesFeedUsedNotPurchasesForPl()
    {
        var db = TestDb.Create();
        db.FeedPrices.Add(new FeedPrice { FeedType = "wheat", DisplayName = "Wheat", PricePerKg = 10, StockKg = 100, StockKgFull = 100 });
        db.FeedPurchases.Add(new FeedPurchase
        {
            Date = TestDb.Today,
            FeedType = "wheat",
            Kg = 100,
            RatePerKg = 10,
            Amount = 1000,
            PaidAmount = 0
        });
        db.FeedUsageRecords.Add(new FeedUsageRecord { Date = TestDb.Today, FeedType = "wheat", Kg = 50 });
        await db.SaveChangesAsync();
        var goats = TestFarmMetrics.CreateGoatService(db);
        var metrics = TestFarmMetrics.Create(db, goats);
        var finance = CreateFinance(db);
        var stmt = await finance.GetStatementAsync(TestDb.Today, TestDb.Today);
        Assert.Contains(stmt.ExpenseLines, l => l.Label == "Feed used" && l.Amount == 500);
        Assert.DoesNotContain(stmt.ExpenseLines, l => l.Label == "Feed bought");
    }

    [Fact]
    public async Task FundLivestock_PostsOwnerFundingForGap()
    {
        var db = TestDb.Create();
        db.MoneyAccounts.Add(new MoneyAccount { Name = "Cash", Kind = MoneyAccountKind.Cash, IsActive = true, OpeningBalance = 0 });
        db.Goats.Add(new Goat { Tag = "G-1", Breed = "Tedda", Gender = GoatGender.Female, PurchasePrice = 50_000, Source = GoatSource.Bought });
        await db.SaveChangesAsync();
        var svc = CreateFinance(db);
        var cash = await db.MoneyAccounts.SingleAsync();
        var dry = await svc.FundLivestockAsOwnerContributionAsync(new FundLivestockAsOwnerViewModel { AccountId = cash.Id, DryRun = true });
        Assert.Equal(50_000, dry.AmountToPost);
        var posted = await svc.FundLivestockAsOwnerContributionAsync(new FundLivestockAsOwnerViewModel { AccountId = cash.Id, DryRun = false });
        Assert.Equal(50_000, posted.AmountToPost);
        Assert.Single(await db.OwnerInvestments.ToListAsync());
        var integ = await svc.GetIntegrityCheckAsync();
        Assert.Equal(0, integ.UnfundedLivestockGap);
    }

    [Fact]
    public async Task Integrity_ReportStatementAndCumulativeProfit()
    {
        var db = TestDb.Create();
        db.MoneyAccounts.Add(new MoneyAccount { Name = "Cash", Kind = MoneyAccountKind.Cash, IsActive = true });
        await db.SaveChangesAsync();
        var svc = CreateFinance(db);
        var integ = await svc.GetIntegrityCheckAsync();
        Assert.Equal(integ.StatementProfitAllTime, integ.BalanceSheetCumulativeProfit);
    }

    [Fact]
    public async Task PayablesList_IncludesOpenFeedCredit()
    {
        var db = TestDb.Create();
        db.FeedPrices.Add(new FeedPrice { FeedType = "wheat", DisplayName = "Wheat", PricePerKg = 10, StockKg = 0, StockKgFull = 100 });
        db.FeedPurchases.Add(new FeedPurchase
        {
            Date = TestDb.Today,
            FeedType = "wheat",
            Kg = 100,
            RatePerKg = 10,
            Amount = 1000,
            PaidAmount = 200,
            Supplier = "Miller"
        });
        // FN8 — payables come from SupplierPayables only (feed credit posts a bill with SourceType feed_purchase).
        db.SupplierPayables.Add(new SupplierPayable
        {
            SupplierName = "Miller",
            Date = TestDb.Today,
            Description = "Feed – Wheat",
            Amount = 1000,
            PaidAmount = 200,
            Status = PayableStatus.Partial,
            SourceType = "feed_purchase",
            SourceId = "1"
        });
        await db.SaveChangesAsync();
        var ledger = new GoatFarm.Infrastructure.Services.Extended.LedgerService(db);
        var page = await ledger.GetPageAsync(MonthHelper.CurrentMonthKey());
        Assert.Contains(page.PayablesList, p => p.Description.StartsWith("Feed"));
        Assert.DoesNotContain(page.PayablesList, p => p.Description.Contains("Feed purchase ·", StringComparison.OrdinalIgnoreCase));
        Assert.True(page.Payables >= 800);
    }

    [Fact]
    public async Task Statement_CountsMilkFromProductSalesOnly_NotLegacyMilkSales()
    {
        var db = TestDb.Create();
        db.MilkSales.Add(new MilkSale { Date = TestDb.Today, Liters = 1, Rate = 200, Amount = 200, Comment = "legacy" });
        db.ProductSales.Add(new ProductSale
        {
            Date = TestDb.Today,
            ProductName = "Raw milk",
            Qty = 1,
            Rate = 200,
            Amount = 200,
            InvoiceNo = "SF-TEST-0001"
        });
        db.GoatSales.Add(new GoatSale
        {
            Tag = "S-301",
            Date = TestDb.Today,
            Price = 45_000,
            Breed = "Tedda"
        });
        await db.SaveChangesAsync();
        var stmt = await CreateFinance(db).GetStatementAsync(TestDb.Today, TestDb.Today);
        Assert.Equal(45_200, stmt.Income);
        Assert.Equal(200, stmt.IncomeLines.Where(l => l.Label.Contains("milk", StringComparison.OrdinalIgnoreCase)).Sum(l => l.Amount));
        Assert.DoesNotContain(stmt.IncomeLines, l => l.Label == "Milk sales");
        Assert.Contains(stmt.IncomeLines, l => l.Label == "Live goat sale" && l.Amount == 45_000);
    }

    [Fact]
    public async Task GetIntegrity_FlagsBalanceSheetImbalance()
    {
        var db = TestDb.Create();
        db.MoneyAccounts.Add(new MoneyAccount { Name = "Cash", Kind = MoneyAccountKind.Cash, IsActive = true, OpeningBalance = 0 });
        db.Goats.Add(new Goat { Tag = "S-1", Breed = "Tedda", Gender = GoatGender.Female, PurchasePrice = 50000, Source = GoatSource.Bought });
        await db.SaveChangesAsync();
        var integ = await CreateFinance(db).GetIntegrityCheckAsync();
        Assert.Contains(integ.Issues, i => i.Code == "BALANCE_SHEET_IMBALANCE");
        Assert.True(integ.CriticalCount > 0);
    }
}
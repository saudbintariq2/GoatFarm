using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure.Persistence;
using GoatFarm.Infrastructure.Services;

namespace GoatFarm.Tests;

public class DuplicateMoneyCheckTests
{
    [Fact]
    public async Task FindDuplicate_FlagsFeedPurchase_WhenExpenseMatches()
    {
        var db = TestDb.Create();
        db.Expenses.Add(new Expense { Type = "Feed", Amount = 10_000, Date = TestDb.Today });
        await db.SaveChangesAsync();
        var check = new DuplicateMoneyCheck(db);
        var dup = await check.FindDuplicateAsync(TestDb.Today, 10_000, null);
        Assert.NotNull(dup);
        Assert.Equal(MoneyDuplicateTabs.Expense, dup!.Tab);
    }

    [Fact]
    public async Task FindDuplicate_FlagsIncome_WhenMilkSaleMatches()
    {
        var db = TestDb.Create();
        db.MilkSales.Add(new MilkSale { Date = TestDb.Today, Liters = 50, Rate = 200, Amount = 10_000 });
        await db.SaveChangesAsync();
        var check = new DuplicateMoneyCheck(db);
        var dup = await check.FindDuplicateAsync(TestDb.Today, 10_000, null);
        Assert.NotNull(dup);
        Assert.Equal(MoneyDuplicateTabs.MilkSale, dup!.Tab);
    }

    [Fact]
    public async Task FindDuplicate_FlagsOwnerFunding_WhenManualLedgerMatches()
    {
        var db = TestDb.Create();
        db.OwnerInvestments.Add(new OwnerInvestment { Note = "Partner A", Amount = 25_000, Date = TestDb.Today });
        await db.SaveChangesAsync();
        var check = new DuplicateMoneyCheck(db);
        var dup = await check.FindDuplicateAsync(TestDb.Today, 25_000, null);
        Assert.NotNull(dup);
        Assert.Equal(MoneyDuplicateTabs.OwnerFunding, dup!.Tab);
    }

    [Fact]
    public async Task FindDuplicate_IncludesManualLedger_NotLinkedMirror()
    {
        var db = TestDb.Create();
        db.LedgerTransactions.Add(new LedgerTransaction
        {
            Date = TestDb.Today,
            Kind = LedgerTransactionKind.Income,
            Amount = 8_000,
            Category = "sale",
            Reference = null
        });
        db.LedgerTransactions.Add(new LedgerTransaction
        {
            Date = TestDb.Today,
            Kind = LedgerTransactionKind.Income,
            Amount = 8_000,
            Category = "Linked income",
            Reference = "fin-income:99"
        });
        await db.SaveChangesAsync();
        var check = new DuplicateMoneyCheck(db);
        var dup = await check.FindDuplicateAsync(TestDb.Today, 8_000, null);
        Assert.NotNull(dup);
        Assert.Equal(MoneyDuplicateTabs.LedgerManual, dup!.Tab);
    }

    [Fact]
    public async Task FindDuplicate_IgnoresSameRecordOnEdit()
    {
        var db = TestDb.Create();
        db.Incomes.Add(new Income { Type = "Milk", Amount = 5_000, Date = TestDb.Today });
        await db.SaveChangesAsync();
        var id = db.Incomes.Single().Id;
        var check = new DuplicateMoneyCheck(db);
        var dup = await check.FindDuplicateAsync(TestDb.Today, 5_000, null, MoneyDuplicateTabs.Income, id);
        Assert.Null(dup);
    }
}
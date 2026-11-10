using GoatFarm.Application.ViewModels.Finance;
using GoatFarm.Application.ViewModels.Ledger;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure.Persistence;
using GoatFarm.Infrastructure.Services;
using GoatFarm.Infrastructure.Services.Extended;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Tests;

public class FinanceMoneyAccountTests
{
    private static FinanceService CreateService(GoatFarmDbContext db) =>
        new(db, feedService: null!, milkService: null!, vaccineService: null!);

    private static async Task<MoneyAccount> SeedBankAsync(GoatFarmDbContext db)
    {
        var acct = new MoneyAccount { Name = "Farm Bank", Kind = MoneyAccountKind.Bank, OpeningBalance = 0, IsActive = true };
        db.MoneyAccounts.Add(acct);
        await db.SaveChangesAsync();
        return acct;
    }

    [Fact]
    public async Task AddOwnerInvestment_WithoutAccount_Throws()
    {
        var db = TestDb.Create();
        var svc = CreateService(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.AddOwnerInvestmentAsync(new CreateOwnerInvestmentViewModel
            {
                Note = "Owner put in",
                Amount = 1000,
                Date = TestDb.Today
            }));
    }

    [Fact]
    public async Task AddOwnerInvestment_WithAccount_CreatesLedgerRow()
    {
        var db = TestDb.Create();
        var acct = await SeedBankAsync(db);
        var svc = CreateService(db);
        await svc.AddOwnerInvestmentAsync(new CreateOwnerInvestmentViewModel
        {
            Note = "Owner put in",
            Amount = 5000,
            Date = TestDb.Today,
            AccountId = acct.Id
        });

        var tx = await db.LedgerTransactions.SingleAsync();
        Assert.Equal(acct.Id, tx.AccountId);
        Assert.Equal(5000, tx.Amount);
        Assert.Equal("fin-funding:1", tx.Reference);
    }

    [Fact]
    public async Task RelinkUnpostedMoney_PostsHistoricalFunding()
    {
        var db = TestDb.Create();
        var acct = await SeedBankAsync(db);
        db.OwnerInvestments.Add(new OwnerInvestment { Note = "Legacy", Amount = 280_000, Date = TestDb.Today });
        await db.SaveChangesAsync();
        var svc = CreateService(db);

        var result = await svc.RelinkUnpostedMoneyAsync(new RelinkUnpostedMoneyViewModel
        {
            DefaultAccountId = acct.Id,
            DryRun = false
        });

        Assert.Equal(1, result.FundingLinked);
        Assert.Equal(280_000, await db.LedgerTransactions.SumAsync(t => t.Amount));
    }

    [Fact]
    public async Task GetIntegrityCheck_FlagsUnlinkedFunding()
    {
        var db = TestDb.Create();
        await SeedBankAsync(db);
        db.OwnerInvestments.Add(new OwnerInvestment { Note = "No ledger", Amount = 100, Date = TestDb.Today });
        await db.SaveChangesAsync();
        var svc = CreateService(db);

        var check = await svc.GetIntegrityCheckAsync();

        Assert.Equal(1, check.UnlinkedFundingCount);
        Assert.Contains(check.Issues, i => i.Code == "UNLINKED_FUNDING");
    }

    [Fact]
    public async Task SetOpeningBalance_LocksAndBlocksSecondChange()
    {
        var db = TestDb.Create();
        var acct = await SeedBankAsync(db);
        var ledger = new LedgerService(db);

        await ledger.SetOpeningBalanceAsync(new SetMoneyAccountOpeningViewModel { Id = acct.Id, OpeningBalance = 50_000 });
        var row = await db.MoneyAccounts.SingleAsync();
        Assert.Equal(50_000, row.OpeningBalance);
        Assert.True(row.OpeningBalanceLocked);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ledger.SetOpeningBalanceAsync(new SetMoneyAccountOpeningViewModel { Id = acct.Id, OpeningBalance = 1 }));
    }

    [Fact]
    public async Task SaveAccount_CannotChangeOpeningAfterLock()
    {
        var db = TestDb.Create();
        var acct = await SeedBankAsync(db);
        acct.OpeningBalance = 10_000;
        acct.OpeningBalanceLocked = true;
        await db.SaveChangesAsync();
        var ledger = new LedgerService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ledger.SaveAccountAsync(new SaveMoneyAccountViewModel
            {
                Id = acct.Id,
                Name = acct.Name,
                Kind = acct.Kind,
                OpeningBalance = 0,
                IsActive = true
            }));
    }

    [Fact]
    public async Task GetIntegrityCheck_FlagsNegativeAccountBalance()
    {
        var db = TestDb.Create();
        var acct = await SeedBankAsync(db);
        acct.OpeningBalance = 100;
        acct.OpeningBalanceLocked = true;
        db.LedgerTransactions.Add(new LedgerTransaction
        {
            Date = TestDb.Today,
            Kind = LedgerTransactionKind.Expense,
            AccountId = acct.Id,
            Amount = 500,
            Category = "Test"
        });
        await db.SaveChangesAsync();
        var svc = CreateService(db);

        var check = await svc.GetIntegrityCheckAsync();

        Assert.Contains(check.Issues, i => i.Code == "LOW_ACCOUNT_BALANCE" && i.Message.Contains(acct.Name));
    }
}

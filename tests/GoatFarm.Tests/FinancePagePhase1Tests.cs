using GoatFarm.Application.ViewModels.Finance;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure.Persistence;
using GoatFarm.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Tests;

public class FinancePagePhase1Tests
{
    private static FinanceService CreateFull(GoatFarmDbContext db) =>
        new(db,
            new FeedService(db, new GoatService(db), new MilkService(db)),
            new MilkService(db),
            new VaccineService(db));

    [Fact]
    public async Task GetFinancePage_PopulatesBalanceSheetAndIntegrity()
    {
        var db = TestDb.Create();
        db.MoneyAccounts.Add(new MoneyAccount { Name = "Farm Cash", Kind = MoneyAccountKind.Cash, IsActive = true });
        await db.SaveChangesAsync();

        var svc = CreateFull(db);
        var page = await svc.GetFinancePageAsync("2026-11");

        Assert.NotNull(page.BalanceSheet);
        Assert.NotNull(page.Integrity);
        Assert.Equal(0, page.Integrity.CriticalCount);
    }

    [Fact]
    public async Task AddExpense_WithoutAccount_ThrowsClearMessage()
    {
        var svc = CreateFull(TestDb.Create());
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.AddExpenseAsync(new CreateExpenseViewModel { Type = "Feed", Amount = 100, Date = TestDb.Today }));
        Assert.Contains("Cash or Bank", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}

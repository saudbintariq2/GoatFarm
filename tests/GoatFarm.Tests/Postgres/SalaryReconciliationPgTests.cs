using GoatFarm.Application.ViewModels.Finance;
using GoatFarm.Infrastructure.Services;

namespace GoatFarm.Tests.Postgres;

[Trait("Category", "Postgres")]
public class SalaryReconciliationPgTests
{
    private const string ExpensesFingerprintSql = """
        SELECT count(*) * 1000000 + coalesce(sum("Amount"), 0) FROM "Expenses"
        """;

    [SkippableFact]
    public async Task Reconciliation_RunsOnPostgres_AndChangesNoRecords()
    {
        await using var pg = await PostgresTestDatabase.CreateAsync();
        await using (var db = pg.CreateContext())
        {
            SalaryReconciliationTests.SeedLegacyPayroll(db);
            await db.SaveChangesAsync();
        }
        var expensesBefore = await pg.ScalarAsync(ExpensesFingerprintSql);

        SalaryReconciliationViewModel r;
        await using (var db = pg.CreateContext())
            r = await new FinanceService(db, null!, null!, null!).GetSalaryReconciliationAsync();

        Assert.Equal(35000, r.ConfirmedTotal);
        Assert.Equal(7000, r.NeedsReviewTotal);
        Assert.Equal(10000, r.RecurringFlaggedMonthly);
        Assert.Equal(expensesBefore, await pg.ScalarAsync(ExpensesFingerprintSql));
        Assert.Equal(6, await pg.ScalarAsync("""SELECT count(*) FROM "Expenses" """));
        Assert.Equal(2, await pg.ScalarAsync("""SELECT count(*) FROM "RecurringCosts" """));
    }
}

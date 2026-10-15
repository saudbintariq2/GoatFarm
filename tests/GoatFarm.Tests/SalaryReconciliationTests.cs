using System.Text.Json;
using GoatFarm.Application.Common;
using GoatFarm.Application.ViewModels.Finance;
using GoatFarm.Domain.Constants;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure.Persistence;
using GoatFarm.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Tests;

public class SalaryReconciliationTests
{
    [Theory]
    [InlineData("Salary advance", SalaryExpenseKind.SalaryAdvance)]
    [InlineData("Staff ADVANCE", SalaryExpenseKind.SalaryAdvance)]
    [InlineData("Advance", SalaryExpenseKind.SalaryAdvance)]
    [InlineData("Worker advance", SalaryExpenseKind.SalaryAdvance)]
    [InlineData("تنخواہ پیشگی", SalaryExpenseKind.SalaryAdvance)]
    [InlineData("Salary", SalaryExpenseKind.SalaryOrWage)]
    [InlineData("Staff salaries", SalaryExpenseKind.SalaryOrWage)]
    [InlineData("Daily wage", SalaryExpenseKind.SalaryOrWage)]
    [InlineData("Tankhwah", SalaryExpenseKind.SalaryOrWage)]
    [InlineData("تنخواہ", SalaryExpenseKind.SalaryOrWage)]
    [InlineData("Land rent advance", SalaryExpenseKind.None)]
    [InlineData("Staff food", SalaryExpenseKind.None)]
    [InlineData("Electricity", SalaryExpenseKind.None)]
    [InlineData("", SalaryExpenseKind.None)]
    [InlineData(null, SalaryExpenseKind.None)]
    public void Classify_RecognisesSalaryWageAndAdvanceTypes(string? type, SalaryExpenseKind expected) =>
        Assert.Equal(expected, SalaryExpenseRules.Classify(type));

    [Theory]
    [InlineData("Staff salary")]
    [InlineData("Chowkidar wage")]
    [InlineData("Salary advance")]
    public void RecurringSalaryCost_IsBlocked(string name)
    {
        var guard = new DoubleEntryGuardService(TestDb.Create());
        var ex = Assert.Throws<DoubleEntryValidationException>(() =>
            guard.ValidateRecurringCost(new CreateRecurringCostViewModel { Name = name, Amount = 20000, Period = "month" }));
        Assert.Equal(DoubleEntrySeverity.Block, ex.Severity);
    }

    [Fact]
    public void RecurringRent_IsAllowed() =>
        new DoubleEntryGuardService(TestDb.Create()).ValidateRecurringCost(new CreateRecurringCostViewModel { Name = "Land rent", Amount = 5000, Period = "month" });

    private static async Task<GoatFarmDbContext> FarmWithLegacyExpenseTypesAsync()
    {
        var db = TestDb.Create();
        db.AppSettings.Add(new AppSetting
        {
            Key = LookupSettingKeys.ExpenseTypes,
            Value = JsonSerializer.Serialize(new[] { "Seeding", "Salary advance", "Staff salary", "Other" })
        });
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task ExpenseTypeDropdown_HidesLegacySalaryTypes_WithoutRewritingTheSavedList()
    {
        await using var db = await FarmWithLegacyExpenseTypesAsync();
        var lookups = new LookupService(db);

        Assert.Equal(["Seeding", "Other"], await lookups.GetListAsync(LookupSettingKeys.ExpenseTypes));
        var stored = await db.AppSettings.AsNoTracking().SingleAsync(s => s.Key == LookupSettingKeys.ExpenseTypes);
        Assert.Contains("Salary advance", stored.Value);
    }

    [Theory]
    [InlineData("Salary advance")]
    [InlineData("staff wage")]
    public async Task AddingASalaryExpenseType_IsRejected(string value)
    {
        await using var db = await FarmWithLegacyExpenseTypesAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => new LookupService(db).AddOptionAsync(LookupSettingKeys.ExpenseTypes, value));
    }

    [Fact]
    public async Task AddingAnotherExpenseType_KeepsLegacyEntriesInTheSavedList()
    {
        await using var db = await FarmWithLegacyExpenseTypesAsync();
        var shown = await new LookupService(db).AddOptionAsync(LookupSettingKeys.ExpenseTypes, "Fencing");

        Assert.Equal(["Seeding", "Other", "Fencing"], shown);
        var stored = JsonSerializer.Deserialize<string[]>((await db.AppSettings.AsNoTracking().SingleAsync(s => s.Key == LookupSettingKeys.ExpenseTypes)).Value)!;
        Assert.Equal(["Seeding", "Salary advance", "Staff salary", "Other", "Fencing"], stored);
    }

    private static readonly DateOnly March = new(2026, 3, 10);

    internal static void SeedLegacyPayroll(GoatFarmDbContext db, bool withEmployees = true)
    {
        if (withEmployees)
        {
            db.Employees.Add(new Employee { Name = "Aslam", MonthlySalary = 30000 });
            db.Employees.Add(new Employee { Name = "Bilal", MonthlySalary = 25000 });
        }
        db.Expenses.AddRange(
            new Expense { Type = "Salary advance", Amount = 10000, Date = March, Comment = "Aslam March" },
            new Expense { Type = "Staff salary", Amount = 25000, Date = March.AddDays(5), Comment = "bilal" },
            new Expense { Type = "Salary advance", Amount = 4000, Date = March.AddMonths(1), Comment = null },
            new Expense { Type = "Other", Amount = 3000, Date = March.AddMonths(1), Comment = "tankhwah for helper" },
            new Expense { Type = "Electricity", Amount = 8000, Date = March },
            new Expense { Type = "Land rent advance", Amount = 50000, Date = March, Comment = "Aslam arranged" });
        db.RecurringCosts.AddRange(
            new RecurringCost { Name = "Chowkidar salary", Amount = 120000, Period = RecurringCostPeriod.Year },
            new RecurringCost { Name = "Land rent", Amount = 5000, Period = RecurringCostPeriod.Month });
    }

    private static FinanceService Finance(GoatFarmDbContext db) => new(db, null!, null!, null!);

    [Fact]
    public async Task Reconciliation_SeparatesConfirmedDoubleCountsFromRowsNeedingReview()
    {
        await using var db = TestDb.Create();
        SeedLegacyPayroll(db);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var r = await Finance(db).GetSalaryReconciliationAsync();

        Assert.Equal(55000, r.AutoSalaryMonthly);
        Assert.Equal(2, r.EmployeeCount);
        var byType = r.Rows.ToDictionary(x => (x.Type, x.Amount));
        Assert.Equal(SalaryReconciliationOutcome.ConfirmedDoubleCount, byType[("Salary advance", 10000)].Outcome);
        Assert.Equal("Aslam", byType[("Salary advance", 10000)].MatchedEmployee);
        Assert.Equal(SalaryReconciliationOutcome.ConfirmedDoubleCount, byType[("Staff salary", 25000)].Outcome);
        Assert.Equal(SalaryReconciliationOutcome.NeedsReview, byType[("Salary advance", 4000)].Outcome);
        Assert.Equal(SalaryReconciliationOutcome.NeedsReview, byType[("Other", 3000)].Outcome);
        Assert.Equal(SalaryReconciliationOutcome.NeedsReview, byType[("Chowkidar salary", 10000)].Outcome);
        Assert.DoesNotContain(r.Rows, x => x.Type is "Electricity" or "Land rent" or "Land rent advance");

        Assert.Equal(35000, r.ConfirmedTotal);
        Assert.Equal(7000, r.NeedsReviewTotal);
        Assert.Equal(0, r.NotDoubleCountedTotal);
        Assert.Equal(10000, r.RecurringFlaggedMonthly);
        Assert.Equal(["2026-03", "2026-04"], r.Months.Select(m => m.Month));
        Assert.Equal(35000, r.Months[0].ConfirmedAmount);
        Assert.Equal(7000, r.Months[1].NeedsReviewAmount);
        Assert.All(r.Months, m => Assert.Equal(55000, m.AutoSalary));
    }

    [Fact]
    public async Task Reconciliation_WithNoEmployeeSalaries_MarksRowsAsNotDoubleCounted()
    {
        await using var db = TestDb.Create();
        SeedLegacyPayroll(db, withEmployees: false);
        await db.SaveChangesAsync();

        var r = await Finance(db).GetSalaryReconciliationAsync();

        Assert.NotEmpty(r.Rows);
        Assert.All(r.Rows, x => Assert.Equal(SalaryReconciliationOutcome.NotDoubleCounted, x.Outcome));
        Assert.Equal(0, r.ConfirmedTotal + r.NeedsReviewTotal + r.RecurringFlaggedMonthly);
        Assert.Equal(42000, r.NotDoubleCountedTotal);
    }

    [Fact]
    public async Task Reconciliation_IsReadOnly()
    {
        await using var db = TestDb.Create();
        SeedLegacyPayroll(db);
        await db.SaveChangesAsync();
        var before = JsonSerializer.Serialize(await db.Expenses.AsNoTracking().OrderBy(e => e.Id).ToListAsync());
        var saves = 0;
        db.SavingChanges += (_, _) => saves++;

        await Finance(db).GetSalaryReconciliationAsync();

        Assert.Equal(0, saves);
        Assert.Equal(before, JsonSerializer.Serialize(await db.Expenses.AsNoTracking().OrderBy(e => e.Id).ToListAsync()));
        Assert.Equal(2, await db.RecurringCosts.CountAsync());
    }
}

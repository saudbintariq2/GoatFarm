using GoatFarm.Application.Common;
using GoatFarm.Application.ViewModels.Finance;
using GoatFarm.Domain.Entities;
using GoatFarm.Infrastructure.Persistence;
using GoatFarm.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Tests;

public class DoubleEntryGuardTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Today);

    private static DoubleEntryGuardService CreateService(out GoatFarmDbContext context)
    {
        var options = new DbContextOptionsBuilder<GoatFarmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        context = new GoatFarmDbContext(options);
        return new DoubleEntryGuardService(context);
    }

    [Fact]
    public async Task ValidateExpenseAsync_BlocksSalaryTypes()
    {
        var guard = CreateService(out _);
        var ex = await Assert.ThrowsAsync<DoubleEntryValidationException>(() =>
            guard.ValidateExpenseAsync(new CreateExpenseViewModel
            {
                Type = "Salary",
                Amount = 1000,
                Date = Today
            }));
        Assert.Equal(DoubleEntrySeverity.Block, ex.Severity);
    }

    [Fact]
    public async Task ValidateExpenseAsync_SalaryBlockIgnoresConfirmation()
    {
        var guard = CreateService(out _);
        var ex = await Assert.ThrowsAsync<DoubleEntryValidationException>(() =>
            guard.ValidateExpenseAsync(new CreateExpenseViewModel
            {
                Type = "Staff wages",
                Amount = 1000,
                Date = Today,
                ConfirmPossibleDuplicate = true
            }));
        Assert.Equal(DoubleEntrySeverity.Block, ex.Severity);
    }

    [Fact]
    public async Task ValidateExpenseAsync_SkipsWhenConfirmed()
    {
        var guard = CreateService(out _);
        await guard.ValidateExpenseAsync(new CreateExpenseViewModel
        {
            Type = "Feed concentrate",
            Amount = 500,
            Date = Today,
            ConfirmPossibleDuplicate = true
        });
    }

    [Theory]
    [InlineData("Salary advance", false)]
    [InlineData("salary ADVANCE", true)]
    [InlineData("Staff advance", true)]
    [InlineData("Advance", false)]
    [InlineData("Wage advance", true)]
    public async Task ValidateExpenseAsync_BlocksSalaryAdvances_EvenWhenConfirmed(string type, bool confirmed)
    {
        var guard = CreateService(out _);
        var ex = await Assert.ThrowsAsync<DoubleEntryValidationException>(() =>
            guard.ValidateExpenseAsync(new CreateExpenseViewModel
            {
                Type = type, Amount = 5000, Date = Today, ConfirmPossibleDuplicate = confirmed
            }));
        Assert.Equal(DoubleEntrySeverity.Block, ex.Severity);
        Assert.Contains("advance", ex.Message);
    }

    [Fact]
    public void DefaultExpenseTypes_DoNotOfferSalaryAdvance() =>
        Assert.DoesNotContain(GoatFarm.Domain.Constants.LookupConstants.ExpenseTypes,
            t => t.Contains("advance", StringComparison.OrdinalIgnoreCase));

    [Theory]
    [InlineData("Feed concentrate")]
    [InlineData("Vaccines")]
    public async Task ValidateExpenseAsync_WarnsOnAutoCountedCosts(string type)
    {
        var guard = CreateService(out _);
        var ex = await Assert.ThrowsAsync<DoubleEntryValidationException>(() =>
            guard.ValidateExpenseAsync(new CreateExpenseViewModel { Type = type, Amount = 500, Date = Today }));
        Assert.Equal(DoubleEntrySeverity.Warn, ex.Severity);
    }

    [Theory]
    [InlineData("Fodder cutting")]
    [InlineData("Cultivation (fodder)")]
    [InlineData("Seeding")]
    [InlineData("Utilities")]
    [InlineData("Land rent advance")]
    public async Task ValidateExpenseAsync_AllowsOrdinaryCosts(string type)
    {
        var guard = CreateService(out _);
        await guard.ValidateExpenseAsync(new CreateExpenseViewModel { Type = type, Amount = 500, Date = Today });
    }

    [Fact]
    public async Task ValidateExpenseAsync_WarnsOnExactDuplicate_ButNotForSameRecord()
    {
        var guard = CreateService(out var context);
        var existing = new Expense { Type = "Utilities", Amount = 2500, Date = Today };
        context.Expenses.Add(existing);
        await context.SaveChangesAsync();

        var model = new CreateExpenseViewModel { Type = "Utilities", Amount = 2500, Date = Today };
        var ex = await Assert.ThrowsAsync<DoubleEntryValidationException>(() => guard.ValidateExpenseAsync(model));
        Assert.Equal(DoubleEntrySeverity.Warn, ex.Severity);

        await guard.ValidateExpenseAsync(model, existing.Id);
    }
}

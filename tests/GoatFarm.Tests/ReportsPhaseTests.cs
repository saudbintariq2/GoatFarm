using GoatFarm.Application.Common;
using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Reminders;
using GoatFarm.Domain.Entities;
using GoatFarm.Infrastructure.Persistence;
using GoatFarm.Infrastructure.Services;
namespace GoatFarm.Tests;

public class ReportsPhaseTests
{
    [Fact]
    public void StaffSalaryAccrual_RespectsStartAndEndDates()
    {
        var staff = new[]
        {
            new Employee { MonthlySalary = 10_000, StartDate = new DateOnly(2026, 3, 1) },
            new Employee { MonthlySalary = 5_000, EndDate = new DateOnly(2026, 1, 31) }
        };

        Assert.Equal(10_000, StaffSalaryAccrual.ForMonth(staff, new DateOnly(2026, 3, 1)));
        Assert.Equal(0, StaffSalaryAccrual.ForMonth(staff, new DateOnly(2026, 2, 1)));
        Assert.Equal(10_000, StaffSalaryAccrual.ForRange(staff, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 15)));
    }

    [Fact]
    public async Task Reports_RevenueIncludesProductSalesLikeFinance()
    {
        var db = TestDb.Create();
        db.ProductSales.Add(new ProductSale
        {
            Date = TestDb.Today,
            ProductName = "Paneer",
            Amount = 7500,
            Qty = 3
        });
        await db.SaveChangesAsync();

        var goats = TestFarmMetrics.CreateGoatService(db);
        var metrics = TestFarmMetrics.Create(db, goats);
        var feed = new FeedService(db, goats, new MilkService(db), metrics);
        var finance = new FinanceService(db, feed, new MilkService(db), new VaccineService(db));
        var reports = new ReportsService(
            db, feed, finance, new MilkService(db), new VaccineService(db), goats,
            new StubReminders(), metrics);

        var stmt = await finance.GetStatementAsync(
            new DateOnly(TestDb.Today.Year, TestDb.Today.Month, 1), TestDb.Today);
        var page = await reports.GetReportsPageAsync("month", null, null);

        Assert.Equal(stmt.Income, page.Finance.TotalIncome);
        Assert.True(page.Finance.TotalIncome >= 7500);
    }

  private sealed class StubReminders : IReminderService
  {
      public Task<IReadOnlyList<ReminderViewModel>> GetRemindersAsync(CancellationToken cancellationToken = default) =>
          Task.FromResult<IReadOnlyList<ReminderViewModel>>([]);

      public Task<ReminderViewModel> AddReminderAsync(CreateReminderViewModel model, CancellationToken cancellationToken = default) =>
          throw new NotSupportedException();

      public Task<ReminderViewModel?> UpdateReminderAsync(int id, UpdateReminderViewModel model, CancellationToken cancellationToken = default) =>
          throw new NotSupportedException();

      public Task<bool> DeleteReminderAsync(int id, CancellationToken cancellationToken = default) =>
          throw new NotSupportedException();
  }
}
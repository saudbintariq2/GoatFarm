using System.Text.Json;
using GoatFarm.Application.Common;
using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Dashboard;
using GoatFarm.Application.ViewModels.Goats;
using GoatFarm.Application.ViewModels.Reports;
using GoatFarm.Application.ViewModels.Settings;
using GoatFarm.Domain.Constants;
using GoatFarm.Infrastructure.Services;
using GoatFarm.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace GoatFarm.Tests;

public class FinanceVisibilityTests
{
    private static ReportKpiViewModel K(string label, bool fin = false) => new() { Label = label, Value = "Rs 123,456", IsFinancial = fin };

    internal static ReportsPageViewModel FullReport() => new()
    {
        NoteHtml = "<b>No sales yet</b> your own money covers the costs",
        PreRevenue = true,
        TrendLastColumnLabel = "You put in",
        Dashboard = new ReportDashboardSectionViewModel
        {
            Kpis = [K("Total herd"), K("Revenue", true), K("Expenses", true), K("Net profit", true)],
            Ratios = [K("Mortality %"), K("Health cost/goat", true)],
            TrendRows = [new ReportTrendRowViewModel { Key = "2026-10", Income = 900, Expense = 500, Net = 400 }],
            TrendTotalIncome = 900, TrendTotalExpense = 500, TrendTotalNet = 400, TrendTotalOwner = 50
        },
        Herd = new ReportHerdSectionViewModel { Movement = [new ReportLabelCountRowViewModel { Label = "Births", Count = 3 }] },
        Feed = new ReportFeedSectionViewModel { Kpis = [K("Feed cost / day"), K("Feed share of costs", true)], PurchasesTotalKg = 500 },
        Health = new ReportHealthSectionViewModel { Kpis = [K("Vaccines due now"), K("Medicine & vet", true)] },
        Finance = new ReportFinanceSectionViewModel
        {
            TotalIncome = 900, TotalExpense = 500, Profit = 400, TotalOwnerInvestment = 50,
            ExpenseEntries = [new ReportExpenseEntryViewModel { Type = "Electricity", Amount = 500 }],
            OwnerRows = [new ReportOwnerRowViewModel { Amount = 50 }]
        },
        Profitability = new ReportProfitabilitySectionViewModel { UnitEconomics = [K("Break-even", true)], MilkEconomics = [K("Milk income", true)] },
        Inventory = new ReportInventorySectionViewModel
        {
            Kpis = [K("Feed stock value"), K("Total capital", true)],
            Assets = [new ReportInvAssetRowViewModel { Name = "Tractor", Value = 900000 }],
            AssetsTotalValue = 900000
        },
        Comparison = new ReportComparisonSectionViewModel
        {
            Rows = [new ReportCompareRowViewModel { Label = "Revenue", IsFinancial = true }, new ReportCompareRowViewModel { Label = "Births" }]
        }
    };

    internal static DashboardViewModel FullDashboard() => new()
    {
        Month = "2026-10", HerdStats = new HerdStatsViewModel { Total = 12 }, LitersProduced = 40, DueNowCount = 2,
        FeedCostMonthly = 30000, TotalIncome = 900, TotalExpense = 500, Capital = 900000, Profit = 400, IsLoss = false
    };

    [Fact]
    public void Reports_WithoutFinance_RemovesEveryFinancialFigure_AndKeepsFarmData()
    {
        var r = FinanceVisibility.WithoutFinance(FullReport());

        Assert.True(r.FinanceHidden);
        Assert.False(r.PreRevenue);
        Assert.Equal(FinanceVisibility.HiddenNote, r.NoteHtml);
        Assert.DoesNotContain(r.Dashboard.Kpis.Concat(r.Dashboard.Ratios).Concat(r.Feed.Kpis).Concat(r.Health.Kpis).Concat(r.Inventory.Kpis),
            k => k.IsFinancial);
        Assert.Empty(r.Dashboard.TrendRows);
        Assert.Equal(0, r.Dashboard.TrendTotalIncome + r.Dashboard.TrendTotalExpense + r.Dashboard.TrendTotalNet + r.Dashboard.TrendTotalOwner);
        Assert.Equal(0, r.Finance.TotalIncome + r.Finance.TotalExpense + r.Finance.Profit + r.Finance.TotalOwnerInvestment);
        Assert.Empty(r.Finance.ExpenseEntries);
        Assert.Empty(r.Finance.OwnerRows);
        Assert.Empty(r.Profitability.UnitEconomics);
        Assert.Empty(r.Profitability.MilkEconomics);
        Assert.Empty(r.Inventory.Assets);
        Assert.Equal(0, r.Inventory.AssetsTotalValue);
        Assert.Equal(["Births"], r.Comparison.Rows.Select(x => x.Label));

        Assert.Equal(["Total herd"], r.Dashboard.Kpis.Select(k => k.Label));
        Assert.Equal(["Mortality %"], r.Dashboard.Ratios.Select(k => k.Label));
        Assert.Equal(["Feed cost / day"], r.Feed.Kpis.Select(k => k.Label));
        Assert.Equal(500, r.Feed.PurchasesTotalKg);
        Assert.Equal(3, r.Herd.Movement[0].Count);
    }

    [Fact]
    public void Dashboard_WithoutFinance_ReturnsACopy_AndLeavesTheSharedCachedModelIntact()
    {
        var cached = FullDashboard();
        var shown = FinanceVisibility.WithoutFinance(cached);

        Assert.NotSame(cached, shown);
        Assert.True(shown.FinanceHidden);
        Assert.Equal(0, shown.TotalIncome + shown.TotalExpense + shown.Capital + shown.Profit);
        Assert.Equal(12, shown.HerdStats.Total);
        Assert.Equal(40, shown.LitersProduced);
        Assert.Equal(2, shown.DueNowCount);
        Assert.Equal(30000, shown.FeedCostMonthly);

        Assert.False(cached.FinanceHidden);
        Assert.Equal(900, cached.TotalIncome);
        Assert.Equal(900000, cached.Capital);
    }

    private sealed class FakePermissions(bool financeView) : IPermissionService
    {
        public Task<string?> GetCurrentUserRoleAsync(CancellationToken ct = default) => Task.FromResult<string?>(FarmRoles.Staff);
        public Task<IReadOnlyDictionary<string, TabPermissionViewModel>> GetCurrentUserPermissionsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<string, TabPermissionViewModel>>(new Dictionary<string, TabPermissionViewModel>());
        public Task<bool> CanAsync(string tab, string action, CancellationToken ct = default) =>
            Task.FromResult(financeView && tab == FarmTabs.Finance && action == FarmActions.View);
    }

    private sealed class FakeReports : IReportsService
    {
        public Task<ReportsPageViewModel> GetReportsPageAsync(string? p, string? f, string? t, string? g = null, CancellationToken ct = default) =>
            Task.FromResult(FullReport());
    }

    private sealed class FakeStats(DashboardViewModel model) : IStatisticsService
    {
        public Task<DashboardViewModel> GetDashboardAsync(string? month, CancellationToken ct = default) => Task.FromResult(model);
        public Task<HerdStatsViewModel> GetHerdStatsAsync(CancellationToken ct = default) => Task.FromResult(model.HerdStats);
    }

    private static T JsonValue<T>(IActionResult result) => Assert.IsType<T>(Assert.IsType<JsonResult>(result).Value);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReportsGetData_IncludesFinanceOnlyWithFinanceView(bool financeView)
    {
        var controller = new ReportsController(new FakeReports(), new FakePermissions(financeView));
        var data = JsonValue<ReportsPageViewModel>(await controller.GetData("year", null, null, null, default));

        Assert.Equal(!financeView, data.FinanceHidden);
        Assert.Equal(financeView ? 900 : 0, data.Finance.TotalIncome);
        Assert.Equal(financeView, data.Dashboard.Kpis.Any(k => k.Label == "Net profit"));
        Assert.Equal(financeView, data.Inventory.Assets.Count > 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DashboardStats_IncludesFinanceOnlyWithFinanceView(bool financeView)
    {
        var cached = FullDashboard();
        var controller = new DashboardController(new FakeStats(cached), null!, null!, new FakePermissions(financeView),
            NullLogger<DashboardController>.Instance);
        var data = JsonValue<DashboardViewModel>(await controller.Stats(null, default));

        Assert.Equal(!financeView, data.FinanceHidden);
        Assert.Equal(financeView ? 900 : 0, data.TotalIncome);
        Assert.Equal(financeView ? 400 : 0, data.Profit);
        Assert.Equal(12, data.HerdStats.Total);
        Assert.Equal(900, cached.TotalIncome);
    }

    [Fact]
    public void RedactedReportJson_ContainsNoAmountsFromFinanceSections()
    {
        var json = JsonSerializer.Serialize(FinanceVisibility.WithoutFinance(FullReport()));
        Assert.DoesNotContain("\"IsFinancial\":true", json);
        Assert.DoesNotContain("Tractor", json);
        Assert.DoesNotContain("Electricity", json);
        Assert.DoesNotContain("No sales yet", json);
    }

    [Fact]
    public void DefaultRoles_OnlyAdminAndManagerHaveFinanceView()
    {
        var roles = UserSettingsService.DefaultRolePermissions();
        Assert.True(roles[FarmRoles.Admin][FarmTabs.Finance].View);
        Assert.True(roles[FarmRoles.Manager][FarmTabs.Finance].View);
        Assert.False(roles[FarmRoles.Staff].TryGetValue(FarmTabs.Finance, out var staff) && staff.View);
        Assert.True(roles[FarmRoles.Staff][FarmTabs.Reports].View);
        Assert.True(roles[FarmRoles.Staff][FarmTabs.Dashboard].View);
    }
}

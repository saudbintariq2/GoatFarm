using System.Text.Json;
using GoatFarm.Application.Assistant;
using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Assistant;
using GoatFarm.Application.ViewModels.Dashboard;
using GoatFarm.Application.ViewModels.Finance;
using GoatFarm.Application.ViewModels.Goats;
using GoatFarm.Application.ViewModels.Milk;
using GoatFarm.Application.ViewModels.Reports;
using GoatFarm.Application.Common;
using GoatFarm.Domain.Constants;
using GoatFarm.Infrastructure.Services.Assistant.Tools;

namespace GoatFarm.Tests.Assistant;

public class AssistantToolTests
{
    private static readonly DateOnly Today = new(2026, 10, 16);

    private static AssistantToolContext Ctx(IPermissionService perms) => new()
    {
        UserId = "user-1",
        Permissions = perms,
        Today = Today
    };

    private static JsonElement Args(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    [Fact]
    public async Task MilkRecords_ReturnsLastMonthTotals()
    {
        var milk = new FakeMilk();
        var tool = new GetMilkRecordsTool(milk);
        var blocks = await tool.ExecuteAsync(Args("""{"month":"lastmonth"}"""), Ctx(FakePermissions.Staff()), default);

        Assert.Equal("2026-09-01", milk.LastFrom?.ToString("yyyy-MM-dd"));
        Assert.Equal("2026-09-30", milk.LastTo?.ToString("yyyy-MM-dd"));
        Assert.Contains(blocks, b => b.Kind == AssistantBlockKind.Summary && b.Summary!.Items.Any(i => i.Value.Contains("300")));
        Assert.Contains(blocks, b => b.Kind == AssistantBlockKind.Table);
        Assert.DoesNotContain(blocks, b => b.Kind == AssistantBlockKind.Error);
    }

    [Fact]
    public async Task Milk_DeniedWithoutMilkView()
    {
        var tool = new GetMilkSummaryTool(new FakeMilk());
        var blocks = await tool.ExecuteAsync(Args("{}"), Ctx(new FakePermissions(FarmTabs.Assistant)), default);
        Assert.Equal(AssistantErrorCodes.PermissionDenied, blocks[0].ErrorCode);
    }

    [Fact]
    public async Task Finance_DeniedForStaff()
    {
        var finance = new FakeFinance();
        var tool = new GetFinanceSummaryTool(finance);
        var blocks = await tool.ExecuteAsync(Args("""{"month":"lastmonth"}"""), Ctx(FakePermissions.Staff()), default);
        Assert.Equal(AssistantErrorCodes.PermissionDenied, blocks[0].ErrorCode);
        Assert.False(finance.Called);
        Assert.DoesNotContain("900", blocks[0].Text);
    }

    [Fact]
    public async Task Finance_ReturnsProfitAndCompositionWithFinanceView()
    {
        var tool = new GetFinanceSummaryTool(new FakeFinance());
        var blocks = await tool.ExecuteAsync(Args("""{"month":"lastmonth"}"""), Ctx(FakePermissions.Manager()), default);
        var summary = blocks.Single(b => b.Kind == AssistantBlockKind.Summary);
        Assert.Contains(summary.Summary!.Items, i => i.Label == "Expenses" && i.Value.Contains("500"));
        Assert.Contains("automatically", summary.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(blocks, b => b.Kind == AssistantBlockKind.Chart);
    }

    [Fact]
    public async Task Expenses_InvalidMonth_Clarifies()
    {
        var tool = new GetExpensesTool(new FakeFinance());
        var blocks = await tool.ExecuteAsync(Args("""{"month":"September"}"""), Ctx(FakePermissions.Manager()), default);
        Assert.Equal(AssistantBlockKind.Clarify, blocks[0].Kind);
        Assert.Equal(AssistantErrorCodes.InvalidArguments, blocks[0].ErrorCode);
    }

    [Fact]
    public async Task Reports_RedactsFinanceForStaff()
    {
        var tool = new GetReportsTool(new FakeReports());
        var blocks = await tool.ExecuteAsync(Args("""{"period":"lastmonth"}"""), Ctx(FakePermissions.Staff()), default);
        Assert.Contains(blocks, b => b.Text.Contains("Finance access"));
        Assert.DoesNotContain(blocks, b => b.Summary?.Items.Any(i => i.Label == "Net profit") == true);
        Assert.DoesNotContain("<b>", string.Join("", blocks.Select(b => b.Text)));
    }

    [Fact]
    public async Task Dashboard_KeepsHerdAndHidesFinanceForStaff()
    {
        var tool = new GetDashboardTool(new FakeStats());
        var blocks = await tool.ExecuteAsync(Args("""{"month":"2026-10"}"""), Ctx(FakePermissions.Staff()), default);
        var summary = blocks.Single(b => b.Kind == AssistantBlockKind.Summary);
        Assert.Contains(summary.Summary!.Items, i => i.Label == "Herd" && i.Value == "12");
        Assert.DoesNotContain(summary.Summary.Items, i => i.Label is "Income" or "Profit" or "Expenses");
    }

    [Fact]
    public async Task Navigation_RejectsUrlArgument()
    {
        var tool = new ResolveNavigationTool();
        var blocks = await tool.ExecuteAsync(
            Args("""{"destination":"milk","url":"https://evil.example"}"""),
            Ctx(FakePermissions.Staff()),
            default);
        Assert.Equal(AssistantErrorCodes.InvalidArguments, blocks[0].ErrorCode);
        Assert.DoesNotContain("evil", blocks[0].Text);
    }

    [Fact]
    public async Task MilkSummary_MatchesServiceTotals_AndNotesEmptyMonths()
    {
        var milk = new FakeMilk();
        var tool = new GetMilkSummaryTool(milk);
        var filled = await tool.ExecuteAsync(Args("""{"month":"2026-09"}"""), Ctx(FakePermissions.Staff()), default);
        var summary = filled.Single(b => b.Kind == AssistantBlockKind.Summary);
        Assert.Equal("300 L", summary.Summary!.Items.Single(i => i.Label == "Produced").Value);
        Assert.Equal("50 L", summary.Summary.Items.Single(i => i.Label == "Sold").Value);
        Assert.Equal("5 L", summary.Summary.Items.Single(i => i.Label == "Wasted").Value);
        Assert.Equal(DisplayHelper.FormatRs(1000), summary.Summary.Items.Single(i => i.Label == "Milk income").Value);
        Assert.DoesNotContain("No milk records", summary.Text);

        milk.Empty = true;
        var empty = await tool.ExecuteAsync(Args("""{"month":"2026-08"}"""), Ctx(FakePermissions.Staff()), default);
        Assert.Contains("No milk records were found for 2026-08", empty.Single(b => b.Kind == AssistantBlockKind.Summary).Text);
        Assert.Equal("0 L", empty.Single(b => b.Kind == AssistantBlockKind.Summary).Summary!.Items.Single(i => i.Label == "Produced").Value);
    }

    [Fact]
    public async Task Finance_MatchesServicePageExactly()
    {
        var page = new FinancePageViewModel
        {
            Month = "2026-09",
            TotalIncome = 900,
            TotalExpense = 500,
            Profit = 400,
            Capital = 1200,
            ManualExpenseMonthly = 80,
            FeedBoughtMonthly = 200,
            MedicineMonthly = 20,
            StaffSalaryMonthly = 150,
            RecurringMonthly = 30,
            VaccineBoughtMonthly = 20
        };
        var tool = new GetFinanceSummaryTool(new FakeFinance { Page = page });
        var blocks = await tool.ExecuteAsync(Args("""{"month":"2026-09"}"""), Ctx(FakePermissions.Manager()), default);
        var items = blocks.Single(b => b.Kind == AssistantBlockKind.Summary).Summary!.Items;
        Assert.Equal(DisplayHelper.FormatRs(900), items.Single(i => i.Label == "Income").Value);
        Assert.Equal(DisplayHelper.FormatRs(500), items.Single(i => i.Label == "Expenses").Value);
        Assert.Equal(DisplayHelper.FormatRs(400), items.Single(i => i.Label == "Profit").Value);
        Assert.Equal(DisplayHelper.FormatRs(1200), items.Single(i => i.Label == "Capital").Value);
    }

    [Fact]
    public async Task Expenses_EmptyManualRows_AreNotInvented()
    {
        var tool = new GetExpensesTool(new FakeFinance
        {
            Page = new FinancePageViewModel { Month = "2026-09", TotalExpense = 400, ManualExpenseMonthly = 0, Expenses = [] }
        });
        var blocks = await tool.ExecuteAsync(Args("""{"month":"2026-09"}"""), Ctx(FakePermissions.Manager()), default);
        Assert.Contains(blocks, b => b.Kind == AssistantBlockKind.Text && b.Text.Contains("no manual expense", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(blocks, b => b.Title == "Manual expenses");
        Assert.Equal(DisplayHelper.FormatRs(400), blocks.Single(b => b.Kind == AssistantBlockKind.Summary).Summary!.Items.Single(i => i.Label == "Total expenses").Value);
    }

    [Fact]
    public async Task Reports_CustomInvalidDates_DoNotCallService()
    {
        var reports = new FakeReports();
        var tool = new GetReportsTool(reports);
        var blocks = await tool.ExecuteAsync(Args("""{"period":"custom","from":"10/01/2026","to":"2026-10-16"}"""), Ctx(FakePermissions.Staff()), default);
        Assert.Equal(AssistantErrorCodes.InvalidArguments, blocks[0].ErrorCode);
        Assert.Equal(0, reports.Calls);
    }

    [Fact]
    public async Task Reports_DoesNotExposeNoteHtmlOrEventHtml()
    {
        var tool = new GetReportsTool(new FakeReports());
        var blocks = await tool.ExecuteAsync(Args("""{"period":"month"}"""), Ctx(FakePermissions.Manager()), default);
        var blob = string.Join('\n', blocks.Select(b => $"{b.Title}\n{b.Text}\n{string.Join(' ', b.Summary?.Items.Select(i => i.Value + i.Note) ?? [])}"));
        Assert.DoesNotContain("<b>", blob);
        Assert.DoesNotContain("EventHtml", blob);
        Assert.DoesNotContain("your own money covers the costs", blob);
    }

    [Fact]
    public async Task MilkRecords_RejectsAmbiguousDates()
    {
        var milk = new FakeMilk();
        var tool = new GetMilkRecordsTool(milk);
        var blocks = await tool.ExecuteAsync(Args("""{"from":"10/01/2026","to":"2026-10-16"}"""), Ctx(FakePermissions.Staff()), default);
        Assert.Equal(AssistantErrorCodes.InvalidArguments, blocks[0].ErrorCode);
        Assert.Null(milk.LastFrom);
    }

    [Fact]
    public async Task Navigation_DeniedWhenUserCannotViewTarget()
    {
        var tool = new ResolveNavigationTool();
        var blocks = await tool.ExecuteAsync(
            Args("""{"destination":"finance","month":"2026-09"}"""),
            Ctx(FakePermissions.Staff()),
            default);
        Assert.Equal(AssistantErrorCodes.PermissionDenied, blocks[0].ErrorCode);
    }

    private sealed class FakeMilk : IMilkService
    {
        public DateOnly? LastFrom { get; private set; }
        public DateOnly? LastTo { get; private set; }
        public bool Empty { get; set; }

        public Task<MilkPageViewModel> GetMilkPageAsync(int prodPage = 1, int salePage = 1, int wastePage = 1, int pageSize = 10, CancellationToken cancellationToken = default) =>
            Task.FromResult(new MilkPageViewModel());
        public Task<MilkProductionViewModel> AddProductionAsync(CreateMilkProductionViewModel model, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MilkSaleViewModel> AddSaleAsync(CreateMilkSaleViewModel model, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MilkWasteViewModel> AddWasteAsync(CreateMilkWasteViewModel model, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MilkProductionViewModel?> UpdateProductionAsync(int id, CreateMilkProductionViewModel model, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MilkSaleViewModel?> UpdateSaleAsync(int id, CreateMilkSaleViewModel model, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MilkWasteViewModel?> UpdateWasteAsync(int id, CreateMilkWasteViewModel model, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> DeleteProductionAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> DeleteSaleAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> DeleteWasteAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public decimal GetMilkLitersProduced(string month) => month == "2026-09" ? 300 : 10;
        public decimal GetMilkLitersSold(string month) => 50;
        public decimal GetMilkLitersWasted(string month) => 5;
        public decimal GetMilkIncomeMonth(string month) => 1000;
        public Task<MilkRangeViewModel> QueryRangeAsync(DateOnly from, DateOnly to, int take = 50, CancellationToken cancellationToken = default)
        {
            LastFrom = from;
            LastTo = to;
            if (Empty)
            {
                return Task.FromResult(new MilkRangeViewModel { From = from, To = to });
            }

            return Task.FromResult(new MilkRangeViewModel
            {
                From = from,
                To = to,
                LitersProduced = 300,
                LitersSold = 50,
                LitersWasted = 5,
                MilkIncome = 1000,
                ProductionCount = 2,
                Productions =
                [
                    new MilkProductionViewModel { Date = from, Breed = "Beetal", Liters = 100 },
                    new MilkProductionViewModel { Date = to, Breed = "Mixed", Liters = 200 }
                ]
            });
        }
    }

    private sealed class FakeFinance : IFinanceService
    {
        public bool Called { get; private set; }
        public FinancePageViewModel? Page { get; set; }
        public Task<FinancePageViewModel> GetFinancePageAsync(string? month, CancellationToken cancellationToken = default)
        {
            Called = true;
            return Task.FromResult(Page ?? new FinancePageViewModel
            {
                Month = month ?? "2026-10",
                TotalIncome = 900,
                TotalExpense = 500,
                Profit = 400,
                ManualExpenseMonthly = 80,
                FeedBoughtMonthly = 200,
                MedicineMonthly = 20,
                StaffSalaryMonthly = 150,
                RecurringMonthly = 30,
                VaccineBoughtMonthly = 20,
                Expenses = [new ExpenseViewModel { Date = new DateOnly(2026, 9, 4), Type = "Electricity", Amount = 80 }]
            });
        }
        public Task<AssetViewModel> AddAssetAsync(CreateAssetViewModel model, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IncomeViewModel> AddIncomeAsync(CreateIncomeViewModel model, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ExpenseViewModel> AddExpenseAsync(CreateExpenseViewModel model, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<OwnerInvestmentViewModel> AddOwnerInvestmentAsync(CreateOwnerInvestmentViewModel model, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RecurringCostViewModel> AddRecurringCostAsync(CreateRecurringCostViewModel model, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EmployeeViewModel> AddEmployeeAsync(CreateEmployeeViewModel model, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EmployeeViewModel?> UpdateEmployeeAsync(int id, CreateEmployeeViewModel model, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> DeleteEmployeeAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public decimal GetStaffSalaryMonthlyTotal() => 0;
        public Task<AssetViewModel?> UpdateAssetAsync(int id, CreateAssetViewModel model, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IncomeViewModel?> UpdateIncomeAsync(int id, CreateIncomeViewModel model, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ExpenseViewModel?> UpdateExpenseAsync(int id, CreateExpenseViewModel model, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<OwnerInvestmentViewModel?> UpdateOwnerInvestmentAsync(int id, CreateOwnerInvestmentViewModel model, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RecurringCostViewModel?> UpdateRecurringCostAsync(int id, CreateRecurringCostViewModel model, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> DeleteAssetAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> DeleteIncomeAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> DeleteExpenseAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> DeleteOwnerInvestmentAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> DeleteRecurringCostAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public decimal GetLivestockValue() => 0;
        public decimal GetAssetsValue() => 0;
        public decimal GetCapital() => 0;
        public decimal GetRecurringMonthlyTotal() => 0;
        public Task<SalaryReconciliationViewModel> GetSalaryReconciliationAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new SalaryReconciliationViewModel());
    }

    private sealed class FakeReports : IReportsService
    {
        public int Calls { get; private set; }
        public Task<ReportsPageViewModel> GetReportsPageAsync(string? period, string? customFrom, string? customTo, string? groupBy = null, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(FinanceVisibilityTests.FullReport());
        }
    }

    private sealed class FakeStats : IStatisticsService
    {
        public Task<DashboardViewModel> GetDashboardAsync(string? month, CancellationToken cancellationToken = default) =>
            Task.FromResult(FinanceVisibilityTests.FullDashboard());
        public Task<HerdStatsViewModel> GetHerdStatsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new HerdStatsViewModel { Total = 12 });
    }
}

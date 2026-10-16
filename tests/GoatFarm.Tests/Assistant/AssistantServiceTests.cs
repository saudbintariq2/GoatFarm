using GoatFarm.Application.Assistant;
using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Assistant;
using GoatFarm.Application.ViewModels.Milk;
using GoatFarm.Infrastructure.Configuration;
using GoatFarm.Infrastructure.Services.Assistant;
using GoatFarm.Infrastructure.Services.Assistant.Tools;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace GoatFarm.Tests.Assistant;

public class AssistantServiceTests
{
    private static AssistantService Create(
        ScriptedLlmClient llm,
        IPermissionService permissions,
        params IAssistantTool[] extraTools)
    {
        var tools = extraTools.Length > 0
            ? extraTools
            : [new GetMilkRecordsTool(new RecordingMilk())];
        return new AssistantService(
            llm,
            tools,
            permissions,
            new AssistantConversationStore(new MemoryCache(new MemoryCacheOptions())),
            NullLogger<AssistantService>.Instance);
    }

    [Fact]
    public async Task InvoiceQuestion_DoesNotCallLlm_AndDoesNotInventInvoices()
    {
        var llm = new ScriptedLlmClient { FinalText = "should not be used" };
        var svc = Create(llm, FakePermissions.Manager());
        var result = await svc.ChatAsync("u1", new AssistantChatRequest { Message = "Show unpaid invoices." });

        Assert.Empty(llm.Executed);
        Assert.Contains(result.Blocks, b => b.ErrorCode == AssistantErrorCodes.Unsupported);
        Assert.Contains(result.Blocks, b => b.Text.Contains("does not keep invoice", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task MissingOpenAiKey_ReturnsConfigurationError()
    {
        var llm = new ScriptedLlmClient { IsConfigured = false };
        var svc = Create(llm, FakePermissions.Staff());
        var result = await svc.ChatAsync("u1", new AssistantChatRequest { Message = "Show milk for last month." });

        Assert.Equal(AssistantErrorCodes.NotConfigured, result.Blocks[0].ErrorCode);
        Assert.Contains("OpenAI__ApiKey", result.Blocks[0].Text);
    }

    [Fact]
    public async Task OpenAiTimeout_ReturnsTimeoutError()
    {
        var llm = new ScriptedLlmClient { ThrowOnComplete = new TimeoutException("deadline") };
        var svc = Create(llm, FakePermissions.Staff());
        var result = await svc.ChatAsync("u1", new AssistantChatRequest { Message = "Show milk for last month." });

        Assert.Equal(AssistantErrorCodes.Timeout, result.Blocks[0].ErrorCode);
        Assert.DoesNotContain("deadline", result.Blocks[0].Text);
    }

    [Fact]
    public async Task SuccessfulReadOnlyToolExecution_ReturnsMilkTable()
    {
        var llm = new ScriptedLlmClient
        {
            ToolCalls =
            [
                new LlmToolCall { Id = "1", Name = "get_milk_records", ArgumentsJson = """{"month":"lastmonth"}""" }
            ]
        };
        var svc = Create(llm, FakePermissions.Staff());
        var result = await svc.ChatAsync("u1", new AssistantChatRequest
        {
            ConversationId = "conv-staff-1",
            Message = "Show milk production for last month.",
            TimeZone = "UTC"
        });

        Assert.Contains(result.Blocks, b => b.Kind == AssistantBlockKind.Table);
        Assert.Contains(result.Blocks, b => b.Kind == AssistantBlockKind.Summary);
        Assert.DoesNotContain(result.Blocks, b => b.Kind == AssistantBlockKind.Error);
    }

    [Fact]
    public async Task UnknownTool_AndMalformedArguments_AreSafe()
    {
        var llm = new ScriptedLlmClient
        {
            ToolCalls =
            [
                new LlmToolCall { Id = "1", Name = "drop_database", ArgumentsJson = "{}" },
                new LlmToolCall { Id = "2", Name = "get_milk_records", ArgumentsJson = "not-json" }
            ],
            FinalText = ""
        };
        var svc = Create(llm, FakePermissions.Staff());
        var result = await svc.ChatAsync("u1", new AssistantChatRequest { Message = "Show milk." });

        Assert.Contains(result.Blocks, b => b.ErrorCode == AssistantErrorCodes.MalformedModel);
        Assert.Contains(result.Blocks, b => b.ErrorCode == AssistantErrorCodes.InvalidArguments);
    }

    [Fact]
    public async Task ConversationsAreIsolatedPerUser()
    {
        var llm = new ScriptedLlmClient { FinalText = "ok" };
        var cache = new AssistantConversationStore(new MemoryCache(new MemoryCacheOptions()));
        var svc = new AssistantService(
            llm,
            [new GetMilkSummaryTool(new RecordingMilk())],
            FakePermissions.Staff(),
            cache,
            NullLogger<AssistantService>.Instance);

        await svc.ChatAsync("alice", new AssistantChatRequest { ConversationId = "shared-01", Message = "Hello from Alice" });
        llm.FinalText = "bob-ok";
        await svc.ChatAsync("bob", new AssistantChatRequest { ConversationId = "shared-01", Message = "Hello from Bob" });

        Assert.DoesNotContain(cache.Get("bob", "shared-01"), m => m.Content.Contains("Alice"));
        Assert.DoesNotContain(llm.Conversations.Last(), m => m.Content.Contains("Alice"));
        Assert.Contains(cache.Get("alice", "shared-01"), m => m.Content.Contains("Alice"));
    }

    [Fact]
    public async Task FailedTurn_DoesNotAppendConversation_AndRetryIsANewTurn()
    {
        var llm = new ScriptedLlmClient { ThrowOnComplete = new TimeoutException("deadline") };
        var cache = new AssistantConversationStore(new MemoryCache(new MemoryCacheOptions()));
        var svc = new AssistantService(
            llm,
            [new GetMilkSummaryTool(new RecordingMilk())],
            FakePermissions.Staff(),
            cache,
            NullLogger<AssistantService>.Instance);

        var failed = await svc.ChatAsync("u1", new AssistantChatRequest { ConversationId = "retry-01", Message = "Show milk." });
        Assert.Equal(AssistantErrorCodes.Timeout, failed.Blocks[0].ErrorCode);
        Assert.Empty(cache.Get("u1", "retry-01"));

        llm.ThrowOnComplete = null;
        llm.FinalText = "Here is milk.";
        var ok = await svc.ChatAsync("u1", new AssistantChatRequest { ConversationId = "retry-01", Message = "Show milk." });
        Assert.Contains(ok.Blocks, b => b.Text.Contains("Here is milk"));
        Assert.Equal(2, cache.Get("u1", "retry-01").Count);
    }

    [Fact]
    public async Task InventedTotalsWithoutTools_AreRejected()
    {
        var llm = new ScriptedLlmClient { FinalText = "Profit last month was Rs 999,999." };
        var svc = Create(llm, FakePermissions.Manager());
        var result = await svc.ChatAsync("u1", new AssistantChatRequest { Message = "How much profit did I make?" });

        Assert.DoesNotContain("999,999", string.Join(' ', result.Blocks.Select(b => b.Text)));
        Assert.Contains(result.Blocks, b => b.ErrorCode == AssistantErrorCodes.Unsupported);
    }

    [Fact]
    public async Task StaffFollowUp_CannotReadFinanceThroughAToolCall()
    {
        var finance = new UnusedFinance();
        var llm = new ScriptedLlmClient
        {
            ToolCalls = [new LlmToolCall { Id = "1", Name = "get_finance_summary", ArgumentsJson = """{"month":"2026-09"}""" }],
            FinalText = ""
        };
        var svc = Create(llm, FakePermissions.Staff(), new GetFinanceSummaryTool(finance));
        var result = await svc.ChatAsync("staff-1", new AssistantChatRequest { Message = "What was profit last month?" });

        Assert.Contains(result.Blocks, b => b.ErrorCode == AssistantErrorCodes.PermissionDenied);
        Assert.DoesNotContain(result.Blocks, b => b.Summary?.Items.Any(i => i.Label == "Income") == true);
        Assert.Equal(0, finance.Calls);
    }

    [Fact]
    public async Task ToolException_DoesNotLeakInternalDetails()
    {
        var llm = new ScriptedLlmClient
        {
            ToolCalls = [new LlmToolCall { Id = "1", Name = "get_milk_records", ArgumentsJson = """{"month":"lastmonth"}""" }],
            FinalText = ""
        };
        var svc = Create(llm, FakePermissions.Staff(), new GetMilkRecordsTool(new ThrowingMilk()));
        var result = await svc.ChatAsync("u1", new AssistantChatRequest { Message = "Show milk." });

        var text = string.Join(' ', result.Blocks.Select(b => b.Text));
        Assert.Contains(result.Blocks, b => b.ErrorCode == AssistantErrorCodes.ProviderError);
        Assert.DoesNotContain("Host=secret", text);
        Assert.DoesNotContain("Password", text);
        Assert.DoesNotContain("stack", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cancellation_IsPropagated()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var llm = new ScriptedLlmClient { FinalText = "ok" };
        var svc = Create(llm, FakePermissions.Staff());
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            svc.ChatAsync("u1", new AssistantChatRequest { Message = "Show milk." }, cts.Token));
    }

    [Fact]
    public async Task ConversationStore_ConcurrentAppends_DoNotCorruptHistory()
    {
        var store = new AssistantConversationStore(new MemoryCache(new MemoryCacheOptions()));
        await Task.WhenAll(Enumerable.Range(0, 20).Select(i => Task.Run(() =>
            store.Append("u1", "conv-race", "q" + i, "a" + i))));

        var history = store.Get("u1", "conv-race");
        Assert.True(history.Count <= AssistantLimits.MaxCachedTurns * 2);
        Assert.Equal(0, history.Count % 2);
        Assert.Equal(history.Count / 2, history.Count(m => m.Role == "user"));
    }

    [Fact]
    public void OpenAiClient_WithoutKey_IsNotConfigured()
    {
        var client = new OpenAiLlmClient(new OpenAiOptions { ApiKey = "" });
        Assert.False(client.IsConfigured);
    }

    [Fact]
    public void NoWriteToolsAreRegistered()
    {
        IAssistantTool[] tools =
        [
            new GetMilkSummaryTool(new RecordingMilk()),
            new GetMilkRecordsTool(new RecordingMilk()),
            new GetFinanceSummaryTool(new UnusedFinance()),
            new GetExpensesTool(new UnusedFinance()),
            new GetReportsTool(new UnusedReports()),
            new GetDashboardTool(new UnusedStats()),
            new ResolveNavigationTool()
        ];

        var names = tools.Select(t => t.Name).OrderBy(n => n).ToList();
        Assert.Equal(
        [
            "get_dashboard", "get_expenses", "get_finance_summary", "get_milk_records",
            "get_milk_summary", "get_reports", "resolve_navigation"
        ], names);
        Assert.All(names, name =>
        {
            Assert.DoesNotContain("add_", name);
            Assert.DoesNotContain("update_", name);
            Assert.DoesNotContain("delete_", name);
            Assert.DoesNotContain("create_", name);
            Assert.DoesNotContain("import", name);
            Assert.DoesNotContain("export", name);
        });
    }

    private class RecordingMilk : IMilkService
    {
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
        public decimal GetMilkLitersProduced(string month) => 300;
        public decimal GetMilkLitersSold(string month) => 50;
        public decimal GetMilkLitersWasted(string month) => 0;
        public decimal GetMilkIncomeMonth(string month) => 1000;
        public virtual Task<MilkRangeViewModel> QueryRangeAsync(DateOnly from, DateOnly to, int take = 50, CancellationToken cancellationToken = default) =>
            Task.FromResult(new MilkRangeViewModel
            {
                From = from,
                To = to,
                LitersProduced = 300,
                ProductionCount = 1,
                Productions = [new MilkProductionViewModel { Date = from, Breed = "Mixed", Liters = 300 }]
            });
    }

    private sealed class ThrowingMilk : RecordingMilk
    {
        public override Task<MilkRangeViewModel> QueryRangeAsync(DateOnly from, DateOnly to, int take = 50, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Host=secret;Password=super-secret; stack at MilkService.cs:240");
    }

    private sealed class UnusedFinance : Application.Interfaces.IFinanceService
    {
        public int Calls { get; private set; }
        public Task<Application.ViewModels.Finance.FinancePageViewModel> GetFinancePageAsync(string? month, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new Application.ViewModels.Finance.FinancePageViewModel());
        }
        public Task<Application.ViewModels.Finance.AssetViewModel> AddAssetAsync(Application.ViewModels.Finance.CreateAssetViewModel model, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Application.ViewModels.Finance.IncomeViewModel> AddIncomeAsync(Application.ViewModels.Finance.CreateIncomeViewModel model, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Application.ViewModels.Finance.ExpenseViewModel> AddExpenseAsync(Application.ViewModels.Finance.CreateExpenseViewModel model, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Application.ViewModels.Finance.OwnerInvestmentViewModel> AddOwnerInvestmentAsync(Application.ViewModels.Finance.CreateOwnerInvestmentViewModel model, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Application.ViewModels.Finance.RecurringCostViewModel> AddRecurringCostAsync(Application.ViewModels.Finance.CreateRecurringCostViewModel model, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Application.ViewModels.Finance.EmployeeViewModel> AddEmployeeAsync(Application.ViewModels.Finance.CreateEmployeeViewModel model, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Application.ViewModels.Finance.EmployeeViewModel?> UpdateEmployeeAsync(int id, Application.ViewModels.Finance.CreateEmployeeViewModel model, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> DeleteEmployeeAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public decimal GetStaffSalaryMonthlyTotal() => 0;
        public Task<Application.ViewModels.Finance.AssetViewModel?> UpdateAssetAsync(int id, Application.ViewModels.Finance.CreateAssetViewModel model, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Application.ViewModels.Finance.IncomeViewModel?> UpdateIncomeAsync(int id, Application.ViewModels.Finance.CreateIncomeViewModel model, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Application.ViewModels.Finance.ExpenseViewModel?> UpdateExpenseAsync(int id, Application.ViewModels.Finance.CreateExpenseViewModel model, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Application.ViewModels.Finance.OwnerInvestmentViewModel?> UpdateOwnerInvestmentAsync(int id, Application.ViewModels.Finance.CreateOwnerInvestmentViewModel model, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Application.ViewModels.Finance.RecurringCostViewModel?> UpdateRecurringCostAsync(int id, Application.ViewModels.Finance.CreateRecurringCostViewModel model, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> DeleteAssetAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> DeleteIncomeAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> DeleteExpenseAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> DeleteOwnerInvestmentAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> DeleteRecurringCostAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public decimal GetLivestockValue() => 0;
        public decimal GetAssetsValue() => 0;
        public decimal GetCapital() => 0;
        public decimal GetRecurringMonthlyTotal() => 0;
        public Task<Application.ViewModels.Finance.SalaryReconciliationViewModel> GetSalaryReconciliationAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new Application.ViewModels.Finance.SalaryReconciliationViewModel());
    }

    private sealed class UnusedReports : IReportsService
    {
        public Task<Application.ViewModels.Reports.ReportsPageViewModel> GetReportsPageAsync(string? period, string? customFrom, string? customTo, string? groupBy = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Application.ViewModels.Reports.ReportsPageViewModel());
    }

    private sealed class UnusedStats : IStatisticsService
    {
        public Task<Application.ViewModels.Dashboard.DashboardViewModel> GetDashboardAsync(string? month, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Application.ViewModels.Dashboard.DashboardViewModel());
        public Task<Application.ViewModels.Goats.HerdStatsViewModel> GetHerdStatsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new Application.ViewModels.Goats.HerdStatsViewModel());
    }
}

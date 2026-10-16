using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Settings;
using GoatFarm.Domain.Constants;

namespace GoatFarm.Tests.Assistant;

internal sealed class FakePermissions : IPermissionService
{
    private readonly HashSet<string> _viewTabs;

    public FakePermissions(params string[] viewTabs) =>
        _viewTabs = new HashSet<string>(viewTabs, StringComparer.OrdinalIgnoreCase);

    public static FakePermissions Staff() => new(
        FarmTabs.Dashboard, FarmTabs.Tasks, FarmTabs.Herd, FarmTabs.Breeding, FarmTabs.Search,
        FarmTabs.Feed, FarmTabs.Products, FarmTabs.Milk, FarmTabs.Vaccines, FarmTabs.Reports, FarmTabs.Assistant);

    public static FakePermissions Manager() => new(
        FarmTabs.Dashboard, FarmTabs.Tasks, FarmTabs.Herd, FarmTabs.Breeding, FarmTabs.Search,
        FarmTabs.Feed, FarmTabs.Products, FarmTabs.Milk, FarmTabs.Vaccines, FarmTabs.Reports, FarmTabs.Finance, FarmTabs.Assistant);

    public Task<string?> GetCurrentUserRoleAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(_viewTabs.Contains(FarmTabs.Finance) ? FarmRoles.Manager : FarmRoles.Staff);

    public Task<IReadOnlyDictionary<string, TabPermissionViewModel>> GetCurrentUserPermissionsAsync(
        CancellationToken cancellationToken = default)
    {
        var map = FarmTabs.All.ToDictionary(
            t => t.Key,
            t => _viewTabs.Contains(t.Key) ? TabPermissionViewModel.FullAccess : TabPermissionViewModel.NoAccess);
        return Task.FromResult<IReadOnlyDictionary<string, TabPermissionViewModel>>(map);
    }

    public Task<bool> CanAsync(string tab, string action, CancellationToken cancellationToken = default) =>
        Task.FromResult(_viewTabs.Contains(tab) && action == FarmActions.View);
}

internal sealed class ScriptedLlmClient : ILlmClient
{
    public bool IsConfigured { get; set; } = true;
    public Exception? ThrowOnComplete { get; set; }
    public IReadOnlyList<LlmToolCall> ToolCalls { get; set; } = [];
    public string? FinalText { get; set; } = "Here is what I found.";
    public List<LlmToolCall> Executed { get; } = [];
    public List<IReadOnlyList<LlmMessage>> Conversations { get; } = [];

    public async Task<LlmCompletionResult> CompleteTurnAsync(
        IReadOnlyList<LlmMessage> conversation,
        IReadOnlyList<LlmToolSpec> tools,
        Func<LlmToolCall, CancellationToken, Task<string>> executeTool,
        int maxToolExecutions,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Conversations.Add(conversation.ToList());
        if (ThrowOnComplete is not null)
            throw ThrowOnComplete;
        if (!IsConfigured)
            throw new InvalidOperationException("The OpenAI API key is not configured.");

        var count = 0;
        foreach (var call in ToolCalls)
        {
            if (count >= maxToolExecutions)
                break;
            count++;
            Executed.Add(call);
            await executeTool(call, cancellationToken);
        }

        return new LlmCompletionResult { FinalText = FinalText };
    }
}

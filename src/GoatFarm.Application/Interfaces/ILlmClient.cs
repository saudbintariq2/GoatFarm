namespace GoatFarm.Application.Interfaces;

public interface ILlmClient
{
    bool IsConfigured { get; }

    Task<LlmCompletionResult> CompleteTurnAsync(
        IReadOnlyList<LlmMessage> conversation,
        IReadOnlyList<LlmToolSpec> tools,
        Func<LlmToolCall, CancellationToken, Task<string>> executeTool,
        int maxToolExecutions,
        CancellationToken cancellationToken);
}

public sealed class LlmCompletionResult
{
    public string? FinalText { get; init; }
}

public sealed class LlmMessage
{
    public required string Role { get; init; }
    public string Content { get; init; } = "";
}

public sealed class LlmToolSpec
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string ParametersJsonSchema { get; init; }
}

public sealed class LlmToolCall
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string ArgumentsJson { get; init; }
}

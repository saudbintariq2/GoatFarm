using System.ClientModel;
using GoatFarm.Application.Interfaces;
using GoatFarm.Infrastructure.Configuration;
using OpenAI;
using OpenAI.Chat;

namespace GoatFarm.Infrastructure.Services.Assistant;

public sealed class OpenAiLlmClient : ILlmClient
{
    private readonly OpenAiOptions _options;
    private readonly ChatClient? _chat;

    public OpenAiLlmClient(OpenAiOptions options)
    {
        _options = options;
        if (!options.HasApiKey)
            return;

        var clientOptions = new OpenAIClientOptions
        {
            NetworkTimeout = TimeSpan.FromSeconds(options.TimeoutSeconds)
        };
        if (!string.IsNullOrWhiteSpace(options.BaseUrl)
            && Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var endpoint))
            clientOptions.Endpoint = endpoint;

        _chat = new ChatClient(options.Model, new ApiKeyCredential(options.ApiKey), clientOptions);
    }

    public bool IsConfigured => _chat is not null;

    public async Task<LlmCompletionResult> CompleteTurnAsync(
        IReadOnlyList<LlmMessage> conversation,
        IReadOnlyList<LlmToolSpec> tools,
        Func<LlmToolCall, CancellationToken, Task<string>> executeTool,
        int maxToolExecutions,
        CancellationToken cancellationToken)
    {
        if (_chat is null)
            throw new InvalidOperationException("The OpenAI API key is not configured.");

        var messages = conversation.Select(ToChatMessage).ToList();
        var options = new ChatCompletionOptions
        {
            MaxOutputTokenCount = _options.MaxOutputTokens
        };
        foreach (var tool in tools)
        {
            options.Tools.Add(ChatTool.CreateFunctionTool(
                tool.Name,
                tool.Description,
                BinaryData.FromString(tool.ParametersJsonSchema)));
        }

        var executions = 0;
        for (var round = 0; round < maxToolExecutions + 1; round++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await _chat.CompleteChatAsync(messages, options, cancellationToken);
            var completion = result.Value;

            if (completion.FinishReason == ChatFinishReason.ToolCalls)
            {
                messages.Add(new AssistantChatMessage(completion));
                foreach (var call in completion.ToolCalls)
                {
                    if (executions >= maxToolExecutions)
                    {
                        messages.Add(new ToolChatMessage(call.Id, """{"ok":false,"error":"Tool limit reached."}"""));
                        continue;
                    }

                    executions++;
                    var args = call.FunctionArguments?.ToString() ?? "{}";
                    var toolResult = await executeTool(
                        new LlmToolCall { Id = call.Id, Name = call.FunctionName, ArgumentsJson = args },
                        cancellationToken);
                    messages.Add(new ToolChatMessage(call.Id, toolResult));
                }

                continue;
            }

            var text = completion.Content.Count > 0 ? completion.Content[0].Text : null;
            return new LlmCompletionResult { FinalText = text };
        }

        return new LlmCompletionResult { FinalText = null };
    }

    private static ChatMessage ToChatMessage(LlmMessage message) => message.Role switch
    {
        "system" => new SystemChatMessage(message.Content),
        "assistant" => new AssistantChatMessage(message.Content),
        "tool" => throw new InvalidOperationException("Tool messages are only created inside the OpenAI loop."),
        _ => new UserChatMessage(message.Content)
    };
}

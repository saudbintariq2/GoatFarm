using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using GoatFarm.Application.Assistant;
using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Assistant;
using Microsoft.Extensions.Logging;

namespace GoatFarm.Infrastructure.Services.Assistant;

public sealed class AssistantService : IAssistantService
{
    private static readonly Regex InvoiceWord = new(@"\binvoices?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex HtmlTag = new(@"<[^>]+>", RegexOptions.Compiled);

    private readonly ILlmClient _llm;
    private readonly IReadOnlyDictionary<string, IAssistantTool> _tools;
    private readonly IPermissionService _permissions;
    private readonly AssistantConversationStore _conversations;
    private readonly ILogger<AssistantService> _logger;

    public AssistantService(
        ILlmClient llm,
        IEnumerable<IAssistantTool> tools,
        IPermissionService permissions,
        AssistantConversationStore conversations,
        ILogger<AssistantService> logger)
    {
        _llm = llm;
        _tools = tools.ToDictionary(t => t.Name, StringComparer.Ordinal);
        _permissions = permissions;
        _conversations = conversations;
        _logger = logger;
    }

    public async Task<AssistantTurnResponse> ChatAsync(
        string userId,
        AssistantChatRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return Turn("", Error("Sign in to use the assistant.", AssistantErrorCodes.PermissionDenied));

        var conversationId = NormalizeConversationId(request.ConversationId);
        var message = (request.Message ?? "").Trim();
        if (message.Length == 0)
            return Turn(conversationId, Clarify("Enter a question about farm records."));
        if (message.Length > AssistantLimits.MaxMessageLength)
            return Turn(conversationId, Clarify($"Keep questions under {AssistantLimits.MaxMessageLength} characters."));

        if (InvoiceWord.IsMatch(message))
        {
            return Turn(conversationId, new AssistantBlock
            {
                Kind = AssistantBlockKind.Clarify,
                Title = "Invoices are not in this app",
                Text = "Goat Records does not keep invoice records or paid/unpaid invoice status. "
                    + "I can show expenses, milk sales, other income, or a monthly profit and loss summary instead.",
                Clarification = new AssistantClarification
                {
                    Questions =
                    [
                        "Show expenses for last month?",
                        "Show milk sales for last month?",
                        "Show a monthly profit and loss summary?"
                    ]
                },
                ErrorCode = AssistantErrorCodes.Unsupported
            });
        }

        if (!_llm.IsConfigured)
        {
            return Turn(conversationId, Error(
                "The AI assistant is not configured. Set the OpenAI__ApiKey environment variable on the server.",
                AssistantErrorCodes.NotConfigured));
        }

        var today = AssistantDateRange.ResolveToday(request.TimeZone);
        var context = new AssistantToolContext
        {
            UserId = userId,
            Permissions = _permissions,
            Today = today
        };

        var blocks = new List<AssistantBlock>();
        var executions = 0;
        var specs = _tools.Values.Select(t => new LlmToolSpec
        {
            Name = t.Name,
            Description = t.Description,
            ParametersJsonSchema = t.ParametersJsonSchema
        }).ToList();

        var history = _conversations.Get(userId, conversationId);
        var llmMessages = new List<LlmMessage>
        {
            new() { Role = "system", Content = SystemPrompt(today) }
        };
        llmMessages.AddRange(history);
        llmMessages.Add(new LlmMessage { Role = "user", Content = message });

        LlmCompletionResult completion;
        try
        {
            completion = await _llm.CompleteTurnAsync(
                llmMessages,
                specs,
                async (call, ct) =>
                {
                    if (executions >= AssistantLimits.MaxToolExecutionsPerTurn)
                    {
                        blocks.Add(Error(
                            "I reached the limit of data lookups for this question. Ask a more specific follow-up.",
                            AssistantErrorCodes.ToolLimit));
                        return """{"ok":false,"error":"Tool limit reached."}""";
                    }

                    executions++;
                    _logger.LogInformation("Assistant tool {Tool} for user {UserId}", call.Name, userId);

                    if (!_tools.TryGetValue(call.Name, out var tool))
                    {
                        blocks.Add(Error(
                            "The assistant tried a lookup that is not available.",
                            AssistantErrorCodes.MalformedModel));
                        return """{"ok":false,"error":"Unknown tool."}""";
                    }

                    if (!AssistantJson.TryParseArguments(call.ArgumentsJson, out var args, out var parseError))
                    {
                        blocks.AddRange(AssistantToolResults.InvalidArgs(parseError ?? "Invalid tool arguments."));
                        return """{"ok":false,"error":"Invalid arguments."}""";
                    }

                    IReadOnlyList<AssistantBlock> toolBlocks;
                    try
                    {
                        toolBlocks = await tool.ExecuteAsync(args, context, ct);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            "Assistant tool {Tool} failed ({ExceptionType}) for user {UserId}",
                            call.Name, ex.GetType().Name, userId);
                        toolBlocks = AssistantToolResults.Error(
                            "That lookup could not be completed. Try again.",
                            AssistantErrorCodes.ProviderError);
                    }

                    blocks.AddRange(toolBlocks);
                    return CompactToolResult(toolBlocks);
                },
                AssistantLimits.MaxToolExecutionsPerTurn,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return Turn(conversationId, Error("The AI service timed out. Try again in a moment.", AssistantErrorCodes.Timeout));
        }
        catch (TimeoutException)
        {
            return Turn(conversationId, Error("The AI service timed out. Try again in a moment.", AssistantErrorCodes.Timeout));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Assistant provider error for user {UserId}", userId);
            return Turn(conversationId, Error(
                "The AI service could not answer just now. Try again in a moment.",
                AssistantErrorCodes.ProviderError));
        }

        var finalText = Sanitize(completion.FinalText);
        if (!string.IsNullOrWhiteSpace(finalText))
        {
            if (HasUngroundedFigures(finalText, blocks))
            {
                if (!blocks.Any(b => b.Kind is AssistantBlockKind.Summary or AssistantBlockKind.Table or AssistantBlockKind.Chart
                        or AssistantBlockKind.Error or AssistantBlockKind.Clarify))
                {
                    blocks.Add(new AssistantBlock
                    {
                        Kind = AssistantBlockKind.Clarify,
                        Title = "I need a farm-records question",
                        Text = "I can only answer from farm records. Ask about milk, expenses, profit and loss, reports, or the dashboard.",
                        Clarification = new AssistantClarification
                        {
                            Questions =
                            [
                                "Show milk production for last month.",
                                "What were my total expenses last month?",
                                "Show the latest dashboard summary."
                            ]
                        },
                        ErrorCode = AssistantErrorCodes.Unsupported
                    });
                }
            }
            else
                blocks.Insert(0, AssistantToolResults.Text("", finalText));
        }

        if (blocks.Count == 0)
            blocks.Add(AssistantToolResults.Text("", "I could not find farm data for that question."));

        var storedReply = string.Join(" ", blocks.Select(b => b.Text).Where(t => !string.IsNullOrWhiteSpace(t)));
        _conversations.Append(userId, conversationId, message, storedReply);
        return new AssistantTurnResponse { ConversationId = conversationId, Blocks = blocks };
    }

    private static string SystemPrompt(DateOnly today) =>
        "You are the Goat Records farm assistant. Today is "
        + today.ToString("yyyy-MM-dd")
        + ". Answer only from tool results. Never invent numbers, records, invoices, or SQL. "
        + "This application has no invoice module and no paid/unpaid invoice status. "
        + "Use month as yyyy-MM; last calendar month is lastmonth. "
        + "Do not request settings, backup, user admin, or arbitrary URLs. "
        + "Call tools for farm data. Keep any closing sentence short and do not add HTML.";

    private static string NormalizeConversationId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Guid.NewGuid().ToString("N");
        var trimmed = value.Trim();
        if (trimmed.Length is >= 8 and <= AssistantLimits.MaxConversationIdLength
            && trimmed.All(c => char.IsLetterOrDigit(c) || c is '-' or '_'))
            return trimmed;
        return Guid.NewGuid().ToString("N");
    }

    private static string CompactToolResult(IReadOnlyList<AssistantBlock> blocks)
    {
        var payload = blocks.Select(b => new
        {
            kind = b.Kind.ToString().ToLowerInvariant(),
            title = b.Title,
            text = b.Text,
            summary = b.Summary?.Items.Select(i => new { i.Label, i.Value, i.Note }),
            columns = b.Table?.Columns,
            rowCount = b.Table?.Rows.Count,
            path = b.Navigation?.Path,
            error = b.ErrorCode
        });
        var json = JsonSerializer.Serialize(payload);
        return json.Length <= AssistantLimits.MaxToolResultChars
            ? json
            : json[..AssistantLimits.MaxToolResultChars];
    }

    private static string Sanitize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";
        var plain = WebUtility.HtmlDecode(HtmlTag.Replace(text, " "));
        plain = Regex.Replace(plain, @"\s+", " ").Trim();
        return plain.Length <= AssistantLimits.MaxTextChars ? plain : plain[..AssistantLimits.MaxTextChars];
    }

    private static bool HasUngroundedFigures(string text, IReadOnlyList<AssistantBlock> grounded)
    {
        if (!Regex.IsMatch(text, @"\d|Rs\b", RegexOptions.IgnoreCase))
            return false;

        var haystack = string.Join('\n', grounded.SelectMany(GroundedStrings));
        foreach (Match match in Regex.Matches(text, @"\d+(?:[.,]\d+)?"))
        {
            if (!haystack.Contains(match.Value, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static IEnumerable<string> GroundedStrings(AssistantBlock block)
    {
        yield return block.Title;
        yield return block.Text;
        if (block.Summary is not null)
        {
            foreach (var item in block.Summary.Items)
            {
                yield return item.Label;
                yield return item.Value;
                yield return item.Note ?? "";
            }
        }

        if (block.Table is not null)
        {
            foreach (var column in block.Table.Columns)
                yield return column;
            foreach (var row in block.Table.Rows)
            {
                foreach (var cell in row)
                    yield return cell;
            }
        }

        if (block.Chart is not null)
        {
            foreach (var point in block.Chart.Points)
            {
                yield return point.Label;
                yield return point.Value.ToString(CultureInfo.InvariantCulture);
            }
        }
    }

    private static AssistantTurnResponse Turn(string conversationId, AssistantBlock block) =>
        new() { ConversationId = conversationId, Blocks = [block] };

    private static AssistantBlock Error(string text, string code) => new()
    {
        Kind = AssistantBlockKind.Error,
        Title = "Assistant",
        Text = text,
        ErrorCode = code
    };

    private static AssistantBlock Clarify(string text) => new()
    {
        Kind = AssistantBlockKind.Clarify,
        Title = "Need a bit more detail",
        Text = text,
        Clarification = new AssistantClarification { Questions = [text] },
        ErrorCode = AssistantErrorCodes.Validation
    };
}

using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Assistant;
using Microsoft.Extensions.Caching.Memory;

namespace GoatFarm.Infrastructure.Services.Assistant;

public sealed class AssistantConversationStore
{
    private readonly IMemoryCache _cache;
    private readonly object _gate = new();

    public AssistantConversationStore(IMemoryCache cache) => _cache = cache;

    public IReadOnlyList<LlmMessage> Get(string userId, string conversationId)
    {
        lock (_gate)
        {
            return _cache.TryGetValue(Key(userId, conversationId), out List<LlmMessage>? turns) && turns is not null
                ? turns.ToList()
                : [];
        }
    }

    public void Append(string userId, string conversationId, string userText, string assistantText)
    {
        lock (_gate)
        {
            var key = Key(userId, conversationId);
            var turns = _cache.TryGetValue(key, out List<LlmMessage>? existing) && existing is not null
                ? existing.ToList()
                : [];

            turns.Add(new LlmMessage { Role = "user", Content = Truncate(userText) });
            turns.Add(new LlmMessage { Role = "assistant", Content = Truncate(assistantText) });

            var overflow = turns.Count - AssistantLimits.MaxCachedTurns * 2;
            if (overflow > 0)
                turns.RemoveRange(0, overflow);

            _cache.Set(key, turns, new MemoryCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromMinutes(AssistantLimits.ConversationTtlMinutes)
            });
        }
    }

    private static string Key(string userId, string conversationId) =>
        $"assistant:conv:{userId}:{conversationId}";

    private static string Truncate(string text) =>
        text.Length <= AssistantLimits.MaxTextChars ? text : text[..AssistantLimits.MaxTextChars];
}

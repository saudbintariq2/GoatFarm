using GoatFarm.Application.ViewModels.Assistant;

namespace GoatFarm.Application.Interfaces;

public interface IAssistantService
{
    Task<AssistantTurnResponse> ChatAsync(
        string userId,
        AssistantChatRequest request,
        CancellationToken cancellationToken = default);
}

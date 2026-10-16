using System.Text.Json;
using GoatFarm.Application.ViewModels.Assistant;

namespace GoatFarm.Application.Assistant;

public interface IAssistantTool
{
    string Name { get; }
    string Description { get; }
    string ParametersJsonSchema { get; }
    Task<IReadOnlyList<AssistantBlock>> ExecuteAsync(
        JsonElement arguments,
        AssistantToolContext context,
        CancellationToken cancellationToken);
}

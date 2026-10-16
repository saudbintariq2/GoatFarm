using GoatFarm.Application.Interfaces;

namespace GoatFarm.Application.Assistant;

public sealed class AssistantToolContext
{
    public required string UserId { get; init; }
    public required IPermissionService Permissions { get; init; }
    public required DateOnly Today { get; init; }
}

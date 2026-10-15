using GoatFarm.Application.ViewModels.Tasks;
using GoatFarm.Domain.Enums;

namespace GoatFarm.Application.Interfaces;

public interface ITaskService
{
    Task<TasksPageViewModel> GetPageAsync(string currentUserId, bool canEdit, string? filter, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TaskViewModel>> GetOpenForUserAsync(string currentUserId, int take, CancellationToken cancellationToken = default);
    Task<TaskViewModel> CreateAsync(SaveTaskViewModel model, string currentUserId, CancellationToken cancellationToken = default);
    Task<TaskViewModel?> UpdateAsync(int id, SaveTaskViewModel model, string currentUserId, CancellationToken cancellationToken = default);

    /// <summary>Users with tasks edit permission can move any task; others only tasks assigned to them or to everyone.</summary>
    Task<TaskViewModel?> SetStatusAsync(int id, FarmTaskStatus status, string currentUserId, bool canEditAny, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}

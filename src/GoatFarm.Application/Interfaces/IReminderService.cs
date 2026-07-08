using GoatFarm.Application.ViewModels.Reminders;

namespace GoatFarm.Application.Interfaces;

public interface IReminderService
{
    Task<ReminderViewModel> AddReminderAsync(CreateReminderViewModel model, CancellationToken cancellationToken = default);
    Task<ReminderViewModel?> UpdateReminderAsync(int id, UpdateReminderViewModel model, CancellationToken cancellationToken = default);
    Task<bool> DeleteReminderAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReminderViewModel>> GetRemindersAsync(CancellationToken cancellationToken = default);
}

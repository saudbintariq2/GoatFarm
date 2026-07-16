using GoatFarm.Application.ViewModels.Goats;
using GoatFarm.Domain.Enums;

namespace GoatFarm.Application.Interfaces;

public interface IGoatService
{
    Task<HerdPageViewModel> GetHerdPageAsync(string? filter, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default);
    Task<GoatViewModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<GoatViewModel?> GetByTagAsync(string tag, CancellationToken cancellationToken = default);
    Task<GoatViewModel> CreateAsync(CreateGoatViewModel model, CancellationToken cancellationToken = default);
    Task<GoatViewModel?> UpdateAsync(int id, CreateGoatViewModel model, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task BulkMoveAsync(BulkMoveViewModel model, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetGroupsAsync(CancellationToken cancellationToken = default);
    Task<string> CreateGroupAsync(string name, CancellationToken cancellationToken = default);
    int CountByStatus(GoatStatus status, IReadOnlyList<GoatViewModel>? goats = null);
    int GetAgeInDays(DateOnly eventDate);
    string GetAgeLabel(int days);
}

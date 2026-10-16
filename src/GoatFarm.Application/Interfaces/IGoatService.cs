using GoatFarm.Application.ViewModels.Goats;
using GoatFarm.Domain.Enums;

namespace GoatFarm.Application.Interfaces;

public interface IGoatService
{
    Task<HerdPageViewModel> GetHerdPageAsync(string? filter, int page = 1, int pageSize = 10, string? search = null, CancellationToken cancellationToken = default);
    Task<GoatViewModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<GoatViewModel?> GetByTagAsync(string tag, CancellationToken cancellationToken = default);
    Task<GoatViewModel> CreateAsync(CreateGoatViewModel model, CancellationToken cancellationToken = default);
    Task<GoatViewModel?> UpdateAsync(int id, CreateGoatViewModel model, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task BulkMoveAsync(BulkMoveViewModel model, CancellationToken cancellationToken = default);
    Task<ParentOptionsViewModel> GetParentOptionsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetGroupsAsync(CancellationToken cancellationToken = default);
    Task<string> CreateGroupAsync(string name, CancellationToken cancellationToken = default);
    int CountByStatus(GoatStatus status, IReadOnlyList<GoatViewModel>? goats = null);
    IReadOnlyDictionary<GoatStatus, int> GetLiveStatusCounts();
    int GetAgeInDays(DateOnly eventDate);
    string GetAgeLabel(int days);
    Task<WeightDeathResultViewModel> RecordWeightAsync(RecordWeightViewModel model, CancellationToken cancellationToken = default);
    Task<WeightDeathResultViewModel> RecordDeathAsync(RecordDeathViewModel model, CancellationToken cancellationToken = default);
    Task<WeightDeathResultViewModel> RecordSaleAsync(RecordSaleViewModel model, CancellationToken cancellationToken = default);
    Task<GoatSaleFormDataViewModel> GetSaleFormDataAsync(CancellationToken cancellationToken = default);
}

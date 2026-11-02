using GoatFarm.Application.Common;
using GoatFarm.Application.ViewModels.Breeding;

namespace GoatFarm.Application.Interfaces;

public interface IBreedingService
{
    Task<BreedingPageViewModel> GetBreedingPageAsync(string? selectedBatchId = null, CancellationToken cancellationToken = default);
    Task RecordPrepAsync(RecordPrepViewModel model, CancellationToken cancellationToken = default);
    Task<CrossCheckViewModel> CheckCrossAsync(string doeTag, string? buckTag, DateOnly? date, CancellationToken cancellationToken = default);
    Task RecordCrossAsync(RecordCrossViewModel model, CancellationToken cancellationToken = default);
    Task RecordUltrasoundAsync(RecordUltrasoundViewModel model, CancellationToken cancellationToken = default);
    Task<RecordBirthResultViewModel> RecordBirthAsync(RecordBirthViewModel model, CancellationToken cancellationToken = default);
    Task RecordAbortionAsync(RecordAbortionViewModel model, CancellationToken cancellationToken = default);
    Task MarkKiddedAsync(int goatId, CancellationToken cancellationToken = default);
    Task RemovePrepAsync(int goatId, CancellationToken cancellationToken = default);
    Task RemoveCrossAsync(int goatId, CancellationToken cancellationToken = default);
    Task RecordCrossFromPrepAsync(int goatId, DateOnly date, string? buckTag, bool confirmWarnings, CancellationToken cancellationToken = default);
    Task RecordReturnedToHeatAsync(RecordReturnedToHeatViewModel model, CancellationToken cancellationToken = default);
    Task UpdateSeasonAsync(UpdateBreedingSeasonViewModel model, CancellationToken cancellationToken = default);
    Task AddDoeToPipelineAsync(BreedingAddDoeViewModel model, CancellationToken cancellationToken = default);
    Task ApplyStageActionAsync(BreedingStageActionViewModel model, CancellationToken cancellationToken = default);
    Task ClearBreedingMistakeAsync(int goatId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BreedingGroupDto>> GetBreedingGroupsAsync(CancellationToken cancellationToken = default);
    Task CreateBreedingGroupAsync(CreateBreedingGroupViewModel model, CancellationToken cancellationToken = default);
}

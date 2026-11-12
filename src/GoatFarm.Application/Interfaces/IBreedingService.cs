using GoatFarm.Application.Common;
using GoatFarm.Application.ViewModels.Breeding;

namespace GoatFarm.Application.Interfaces;

public interface IBreedingService
{
    Task<BreedingPageViewModel> GetBreedingPageAsync(
        string? selectedBatchId = null,
        string? reportBuck = null,
        CancellationToken cancellationToken = default);
    Task<BreedingDoeProfileViewModel?> GetDoeProfileAsync(int goatId, CancellationToken cancellationToken = default);
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
    Task<string?> UpdateSeasonAsync(UpdateBreedingSeasonViewModel model, CancellationToken cancellationToken = default);
    Task<PregnancyReconcileResultViewModel> ReconcilePregnancyAsync(CancellationToken cancellationToken = default);
    Task AddDoeToPipelineAsync(BreedingAddDoeViewModel model, CancellationToken cancellationToken = default);
    Task ApplyStageActionAsync(BreedingStageActionViewModel model, CancellationToken cancellationToken = default);
    Task ClearBreedingMistakeAsync(int goatId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BreedingGroupDto>> GetBreedingGroupsAsync(CancellationToken cancellationToken = default);
    Task CreateBreedingGroupAsync(CreateBreedingGroupViewModel model, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BreedingGroupCardViewModel>> GetBreedingGroupCardsAsync(CancellationToken cancellationToken = default);
    Task<BreedingScanSheetViewModel> GetScanSheetAsync(CancellationToken cancellationToken = default);
    Task<BulkScanSaveResultViewModel> SaveScanBatchAsync(BulkScanSaveViewModel model, CancellationToken cancellationToken = default);
    Task CloseBreedingGroupAsync(CloseBreedingGroupViewModel model, CancellationToken cancellationToken = default);
    Task DeleteBreedingGroupAsync(string groupId, CancellationToken cancellationToken = default);
    Task RemoveBreedingGroupMemberAsync(BreedingGroupMemberActionViewModel model, CancellationToken cancellationToken = default);
    Task PutBuckInGroupAsync(BuckInBreedingGroupViewModel model, CancellationToken cancellationToken = default);
    Task TakeBuckOutGroupAsync(CloseBreedingGroupViewModel model, CancellationToken cancellationToken = default);
    Task RecordBuckCheckAsync(RecordBuckCheckViewModel model, CancellationToken cancellationToken = default);
    Task UpdateKidFieldAsync(UpdateKidFieldViewModel model, CancellationToken cancellationToken = default);
    Task RecordKidWeanWeightAsync(RecordKidWeanWeightViewModel model, CancellationToken cancellationToken = default);
    Task BulkWeanKidsAsync(BulkWeanKidsViewModel model, CancellationToken cancellationToken = default);
    Task UpdateKidWeanRuleAsync(UpdateKidWeanRuleViewModel model, CancellationToken cancellationToken = default);
}

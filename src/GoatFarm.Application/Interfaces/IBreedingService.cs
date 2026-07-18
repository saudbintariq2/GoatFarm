using GoatFarm.Application.ViewModels.Breeding;

namespace GoatFarm.Application.Interfaces;

public interface IBreedingService
{
    Task<BreedingPageViewModel> GetBreedingPageAsync(CancellationToken cancellationToken = default);
    Task RecordPrepAsync(RecordPrepViewModel model, CancellationToken cancellationToken = default);
    Task RecordCrossAsync(RecordCrossViewModel model, CancellationToken cancellationToken = default);
    Task RecordUltrasoundAsync(RecordUltrasoundViewModel model, CancellationToken cancellationToken = default);
    Task MarkKiddedAsync(int goatId, CancellationToken cancellationToken = default);
    Task RemovePrepAsync(int goatId, CancellationToken cancellationToken = default);
    Task RemoveCrossAsync(int goatId, CancellationToken cancellationToken = default);
    Task RecordCrossFromPrepAsync(int goatId, DateOnly date, string? buckTag, CancellationToken cancellationToken = default);
}

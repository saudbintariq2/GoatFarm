using GoatFarm.Application.ViewModels.Search;

namespace GoatFarm.Application.Interfaces;

public interface ISearchService
{
    Task<GoatProfileViewModel?> GetProfileByTagAsync(string tag, CancellationToken cancellationToken = default);
    Task<GoatCardViewModel?> GetGoatCardByTagAsync(string tag, CancellationToken cancellationToken = default);
    Task<GoatCardViewModel?> GetGoatCardByIdAsync(int id, CancellationToken cancellationToken = default);
}

using GoatFarm.Application.ViewModels.Feed;

namespace GoatFarm.Application.Interfaces;

public interface IFeedService
{
    Task<FeedPageViewModel> GetFeedPageAsync(string? statusKey, CancellationToken cancellationToken = default);
    Task UpdateFeedPriceAsync(string feedType, decimal price, CancellationToken cancellationToken = default);
    Task UpdateFeedPlanAsync(UpdateFeedPlanViewModel model, CancellationToken cancellationToken = default);
    decimal CalculateDailyFeedCost(FeedPlanViewModel plan, IReadOnlyDictionary<string, decimal> prices);
    decimal CalculateFarmFeedMonthly();
}

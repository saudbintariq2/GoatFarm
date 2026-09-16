using GoatFarm.Application.ViewModels.Feed;

namespace GoatFarm.Application.Interfaces;

public interface IFeedService
{
    Task<FeedPageViewModel> GetFeedPageAsync(string? statusKey, string? month = null, CancellationToken cancellationToken = default);
    Task UpdateFeedPriceAsync(string feedType, decimal price, CancellationToken cancellationToken = default);
    Task UpdateFeedPlanAsync(UpdateFeedPlanViewModel model, CancellationToken cancellationToken = default);
    Task UpdateMixRecipeAsync(UpdateMixRecipeViewModel model, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, decimal>> GetMixRecipeAsync(CancellationToken cancellationToken = default);
    decimal GetMixCostPerKg();
    decimal GetFarmFodderKgPerDay();
    Task<FeedPurchaseViewModel> AddFeedPurchaseAsync(CreateFeedPurchaseViewModel model, CancellationToken cancellationToken = default);
    Task<FeedPurchaseViewModel?> UpdateFeedPurchaseAsync(int id, CreateFeedPurchaseViewModel model, CancellationToken cancellationToken = default);
    Task<bool> DeleteFeedPurchaseAsync(int id, CancellationToken cancellationToken = default);
    Task<FeedPriceViewModel> AddFeedTypeAsync(AddFeedTypeViewModel model, CancellationToken cancellationToken = default);
    Task<bool> DeleteFeedTypeAsync(string feedType, CancellationToken cancellationToken = default);
    Task UpdateFeedStockAsync(UpdateFeedStockViewModel model, CancellationToken cancellationToken = default);
    decimal CalculateDailyFeedCost(decimal mixKgPerDay);
    decimal CalculateFarmFeedMonthly();
    decimal CalculateFarmMedicineMonthly();
    decimal GetFeedPurchasedMonthly(string month);
    decimal GetFeedPurchasedKg(string month);
}

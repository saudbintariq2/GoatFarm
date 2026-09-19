using GoatFarm.Application.ViewModels.Feed;

namespace GoatFarm.Application.Interfaces;

public interface IFeedService
{
    Task<FeedPageViewModel> GetFeedPageAsync(string? statusKey, string? month = null, string? subTab = null, CancellationToken cancellationToken = default);
    Task UpdateFeedPriceAsync(string feedType, decimal price, CancellationToken cancellationToken = default);
    Task UpdateFeedPlanAsync(UpdateFeedPlanViewModel model, CancellationToken cancellationToken = default);
    Task UpdateMixRecipeAsync(UpdateMixRecipeViewModel model, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, decimal>> GetMixRecipeAsync(CancellationToken cancellationToken = default);
    Task<MixRecipeStatusViewModel> GetMixRecipeForStatusAsync(string statusKey, CancellationToken cancellationToken = default);
    Task UpdateMixRecipeForStatusAsync(UpdateMixRecipeForStatusViewModel model, CancellationToken cancellationToken = default);
    Task<FodderPoolViewModel> GetFodderPoolAsync(CancellationToken cancellationToken = default);
    Task<FodderPoolViewModel> UpdateFodderPoolAsync(UpdateFodderPoolViewModel model, CancellationToken cancellationToken = default);
    Task<FodderPoolViewModel> AddFodderPoolItemAsync(AddFodderPoolItemViewModel model, CancellationToken cancellationToken = default);
    Task<FodderPoolViewModel> RemoveFodderPoolItemAsync(string itemId, CancellationToken cancellationToken = default);
    Task<FeedSettingsViewModel> GetFeedSettingsAsync(CancellationToken cancellationToken = default);
    Task<FeedSettingsViewModel> UpdateFeedSettingsAsync(UpdateFeedSettingsViewModel model, CancellationToken cancellationToken = default);
    Task<int> RunAutoUsageAsync(CancellationToken cancellationToken = default);
    Task<StockCheckResultViewModel> SaveStockCheckAsync(SaveStockCheckViewModel model, CancellationToken cancellationToken = default);
    decimal GetMixCostPerKg();
    decimal GetMixCostPerKg(string statusKey);
    decimal GetFarmFodderKgPerDay();
    Task<FeedPurchaseViewModel> AddFeedPurchaseAsync(CreateFeedPurchaseViewModel model, CancellationToken cancellationToken = default);
    Task<FeedPurchaseViewModel?> UpdateFeedPurchaseAsync(int id, CreateFeedPurchaseViewModel model, CancellationToken cancellationToken = default);
    Task<bool> DeleteFeedPurchaseAsync(int id, CancellationToken cancellationToken = default);
    Task<FeedPriceViewModel> AddFeedTypeAsync(AddFeedTypeViewModel model, CancellationToken cancellationToken = default);
    Task<bool> DeleteFeedTypeAsync(string feedType, CancellationToken cancellationToken = default);
    Task UpdateFeedStockAsync(UpdateFeedStockViewModel model, CancellationToken cancellationToken = default);
    decimal CalculateDailyFeedCost(decimal mixKgPerDay, string? statusKey = null);
    decimal CalculateFarmFeedMonthly();
    decimal CalculateFarmMedicineMonthly();
    decimal GetFeedPurchasedMonthly(string month);
    decimal GetFeedPurchasedKg(string month);
}

using GoatFarm.Domain.Constants;

namespace GoatFarm.Application.Common;

public static class MixRecipeHelper
{
    public static readonly IReadOnlyDictionary<string, decimal> DefaultRecipe =
        new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            [FeedTypes.Wanda] = 25,
            [FeedTypes.Binola] = 40,
            [FeedTypes.Sarson] = 0,
            [FeedTypes.Bran] = 20,
            [FeedTypes.Maize] = 10,
            [FeedTypes.Sheera] = 5
        };

    public static readonly IReadOnlyDictionary<Domain.Enums.GoatStatus, (decimal Mix, decimal Fodder, decimal Med)> DefaultPlans =
        new Dictionary<Domain.Enums.GoatStatus, (decimal, decimal, decimal)>
        {
            [Domain.Enums.GoatStatus.Kid] = (0.15m, 0.5m, 50),
            [Domain.Enums.GoatStatus.Milking] = (1.0m, 2.0m, 80),
            [Domain.Enums.GoatStatus.Pregnant] = (0.8m, 1.5m, 150),
            [Domain.Enums.GoatStatus.Dry] = (0.3m, 1.5m, 40),
            [Domain.Enums.GoatStatus.Buck] = (0.7m, 1.5m, 60),
            [Domain.Enums.GoatStatus.Sale] = (0.35m, 1.5m, 30)
        };

    public static IEnumerable<string> MixFeedKeys(IEnumerable<string> allFeedTypes) =>
        allFeedTypes.Where(k => !string.Equals(k, FeedTypes.Fodder, StringComparison.OrdinalIgnoreCase));

    public static decimal MixTotalKg(IReadOnlyDictionary<string, decimal> recipe, IEnumerable<string> mixFeedKeys) =>
        mixFeedKeys.Sum(k => recipe.GetValueOrDefault(k));

    public static decimal MixBatchCost(
        IReadOnlyDictionary<string, decimal> recipe,
        IReadOnlyDictionary<string, decimal> prices,
        IEnumerable<string> mixFeedKeys) =>
        mixFeedKeys.Sum(k => recipe.GetValueOrDefault(k) * prices.GetValueOrDefault(k));

    public static decimal MixCostPerKg(
        IReadOnlyDictionary<string, decimal> recipe,
        IReadOnlyDictionary<string, decimal> prices,
        IEnumerable<string> mixFeedKeys)
    {
        var total = MixTotalKg(recipe, mixFeedKeys);
        return total > 0 ? MixBatchCost(recipe, prices, mixFeedKeys) / total : 0;
    }

    public static decimal PlanDailyFeedCost(decimal mixKgPerDay, decimal mixCostPerKg) =>
        mixKgPerDay * mixCostPerKg;
}

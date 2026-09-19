using GoatFarm.Domain.Constants;
using GoatFarm.Domain.Enums;

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

    public static readonly IReadOnlyDictionary<GoatStatus, Dictionary<string, decimal>> DefaultRecipesPerStatus =
        new Dictionary<GoatStatus, Dictionary<string, decimal>>
        {
            [GoatStatus.Kid] = new(StringComparer.OrdinalIgnoreCase)
            {
                [FeedTypes.Wanda] = 40, [FeedTypes.Binola] = 20, [FeedTypes.Sarson] = 0,
                [FeedTypes.Bran] = 25, [FeedTypes.Maize] = 12, [FeedTypes.Sheera] = 3
            },
            [GoatStatus.Milking] = new(StringComparer.OrdinalIgnoreCase)
            {
                [FeedTypes.Wanda] = 25, [FeedTypes.Binola] = 40, [FeedTypes.Sarson] = 0,
                [FeedTypes.Bran] = 20, [FeedTypes.Maize] = 10, [FeedTypes.Sheera] = 5
            },
            [GoatStatus.Pregnant] = new(StringComparer.OrdinalIgnoreCase)
            {
                [FeedTypes.Wanda] = 28, [FeedTypes.Binola] = 35, [FeedTypes.Sarson] = 0,
                [FeedTypes.Bran] = 22, [FeedTypes.Maize] = 10, [FeedTypes.Sheera] = 5
            },
            [GoatStatus.Dry] = new(StringComparer.OrdinalIgnoreCase)
            {
                [FeedTypes.Wanda] = 15, [FeedTypes.Binola] = 25, [FeedTypes.Sarson] = 0,
                [FeedTypes.Bran] = 45, [FeedTypes.Maize] = 10, [FeedTypes.Sheera] = 5
            },
            [GoatStatus.Buck] = new(StringComparer.OrdinalIgnoreCase)
            {
                [FeedTypes.Wanda] = 25, [FeedTypes.Binola] = 30, [FeedTypes.Sarson] = 0,
                [FeedTypes.Bran] = 30, [FeedTypes.Maize] = 12, [FeedTypes.Sheera] = 3
            },
            [GoatStatus.Sale] = new(StringComparer.OrdinalIgnoreCase)
            {
                [FeedTypes.Wanda] = 30, [FeedTypes.Binola] = 35, [FeedTypes.Sarson] = 0,
                [FeedTypes.Bran] = 20, [FeedTypes.Maize] = 12, [FeedTypes.Sheera] = 3
            }
        };

    public static readonly IReadOnlyDictionary<GoatStatus, (decimal Mix, decimal Fodder, decimal FodderDry, decimal Med)> DefaultPlans =
        new Dictionary<GoatStatus, (decimal, decimal, decimal, decimal)>
        {
            [GoatStatus.Kid] = (0.15m, 0.5m, 0.1m, 50),
            [GoatStatus.Milking] = (1.0m, 2.0m, 0.5m, 80),
            [GoatStatus.Pregnant] = (0.8m, 1.5m, 0.5m, 150),
            [GoatStatus.Dry] = (0.3m, 1.5m, 0.5m, 40),
            [GoatStatus.Buck] = (0.7m, 1.5m, 0.5m, 60),
            [GoatStatus.Sale] = (0.35m, 1.5m, 0.4m, 30)
        };

    public static IEnumerable<string> MixFeedKeys(IEnumerable<string> allFeedTypes) =>
        allFeedTypes.Where(FeedTypes.IsMixIngredient);

    public static decimal PlanDailyFeedCostV41(
        decimal mixKgPerDay,
        decimal mixCostPerKg,
        decimal fodderDryKgPerDay,
        decimal dryFodderPricePerKg,
        decimal fodderLandCostPerGoatPerDay) =>
        PlanDailyFeedCost(mixKgPerDay, mixCostPerKg)
        + fodderDryKgPerDay * dryFodderPricePerKg
        + fodderLandCostPerGoatPerDay;


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

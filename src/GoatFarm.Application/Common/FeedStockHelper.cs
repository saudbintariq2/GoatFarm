namespace GoatFarm.Application.Common;

public static class FeedStockHelper
{
    public static decimal BookKg(decimal openingKg, decimal purchasedKg, decimal usedKg) =>
        Math.Max(0, openingKg + purchasedKg - usedKg);

    public static decimal InferOpeningKg(decimal currentBookKg, decimal purchasedKg, decimal usedKg) =>
        currentBookKg - purchasedKg + usedKg;
}

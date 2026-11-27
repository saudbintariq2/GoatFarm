using GoatFarm.Domain.Entities;

namespace GoatFarm.Application.Common;

public static class FeedStockHelper
{
    public static decimal BookKg(decimal openingKg, decimal purchasedKg, decimal usedKg) =>
        Math.Max(0, openingKg + purchasedKg - usedKg);

    public static decimal InferOpeningKg(decimal currentBookKg, decimal purchasedKg, decimal usedKg) =>
        currentBookKg - purchasedKg + usedKg;

    /// <summary>
    /// Walks purchases and usage in date order (purchases first on the same day) and caps every usage
    /// record at what was in the store, moving the rest into <see cref="FeedUsageRecord.ShortfallKg"/>.
    /// Feed that was never in the store cannot have been eaten. Returns the closing stock.
    /// </summary>
    public static decimal ReplayUsage(
        decimal openingKg,
        IEnumerable<(DateOnly Date, decimal Kg)> purchases,
        IEnumerable<FeedUsageRecord> usage)
    {
        var events = purchases.Select(p => (p.Date, Order: 0, Id: 0, Kg: p.Kg, Record: (FeedUsageRecord?)null))
            .Concat(usage.Select(u => (u.Date, Order: 1, u.Id, Kg: 0m, Record: (FeedUsageRecord?)u)))
            .OrderBy(e => e.Date).ThenBy(e => e.Order).ThenBy(e => e.Id);

        var stock = Math.Max(0, openingKg);
        foreach (var e in events)
        {
            if (e.Record is null)
            {
                stock += e.Kg;
                continue;
            }

            var need = Math.Max(0, e.Record.Kg) + Math.Max(0, e.Record.ShortfallKg);
            var take = Math.Min(need, Math.Max(0, stock));
            e.Record.Kg = take;
            e.Record.ShortfallKg = need - take;
            stock -= take;
        }
        return Math.Max(0, stock);
    }
}

using GoatFarm.Domain.Entities;

namespace GoatFarm.Application.Common;

/// <summary>Season KPIs aligned with prototype <c>NB.kpis</c> (kg weaned per doe crossed).</summary>
public static class BreedingKpiHelper
{
    public static double? KgPerDoeCrossed(
        IReadOnlyList<KiddingRecord> kiddings,
        BreedingSeasonSettings season,
        DateOnly today)
    {
        var eligible = kiddings
            .Where(k => k.BreedingEventId.HasValue && k.KiddingDate.AddDays(season.WeanDays) <= today)
            .ToList();
        if (eligible.Count == 0)
            return null;

        double total = 0;
        var does = 0;
        foreach (var k in eligible)
        {
            var kidKg = k.Kids
                .Where(kid => !kid.IsArchived && kid.WeanDate.HasValue)
                .Select(kid =>
                {
                    var w = kid.WeightRecords.OrderByDescending(r => r.Date).FirstOrDefault();
                    return w?.Kg;
                })
                .Where(kg => kg is > 0)
                .Sum(kg => (double)kg!.Value);
            if (kidKg <= 0)
                continue;
            total += kidKg;
            does++;
        }

        return does > 0 ? total / does : null;
    }
}

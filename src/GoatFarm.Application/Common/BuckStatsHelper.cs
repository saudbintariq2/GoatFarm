using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;

namespace GoatFarm.Application.Common;

/// <summary>V243 <c>NB.buckStats</c> kid weight metrics for buck ranking.</summary>
public static class BuckStatsHelper
{
    public static bool KidSiredBy(Goat kid, Goat buck) =>
        kid.Source == GoatSource.Born && (
            kid.SireId == buck.Id
            || string.Equals(kid.SireTagText, buck.Tag, StringComparison.OrdinalIgnoreCase)
            || string.Equals(kid.KiddingRecord?.BreedingEvent?.Buck?.Tag, buck.Tag, StringComparison.OrdinalIgnoreCase)
            || string.Equals(kid.KiddingRecord?.BreedingEvent?.BuckTagText, buck.Tag, StringComparison.OrdinalIgnoreCase));

    public static decimal? BirthKg(Goat kid)
    {
        var birth = HerdRules.BirthDate(kid);
        if (birth is null) return null;
        var weights = kid.WeightRecords;
        if (weights.Count == 0) return null;
        var onBirth = weights.FirstOrDefault(w => w.Date == birth.Value)?.Kg;
        if (onBirth > 0) return onBirth;
        return weights.OrderBy(w => w.Date).FirstOrDefault()?.Kg;
    }

    public static decimal? WeanKg(Goat kid)
    {
        if (!kid.WeanDate.HasValue) return null;
        var weights = kid.WeightRecords;
        if (weights.Count == 0) return null;
        var onWean = weights.FirstOrDefault(w => w.Date == kid.WeanDate.Value)?.Kg;
        if (onWean > 0) return onWean;
        return weights.Where(w => w.Date <= kid.WeanDate.Value).OrderByDescending(w => w.Date).FirstOrDefault()?.Kg;
    }

    public static decimal? Adjusted90DayKg(Goat kid)
    {
        var birth = HerdRules.BirthDate(kid);
        var b = BirthKg(kid);
        var w = WeanKg(kid);
        if (birth is null || b is null || w is null || !kid.WeanDate.HasValue) return null;
        var age = Math.Max(1, kid.WeanDate.Value.DayNumber - birth.Value.DayNumber);
        return b.Value + (w.Value - b.Value) / age * 90m;
    }

    public static double? Average(IEnumerable<decimal?> values)
    {
        var list = values.Where(v => v is > 0).Select(v => (double)v!.Value).ToList();
        return list.Count == 0 ? null : list.Average();
    }

    public static string FormatAvg(double? v) => v is null ? "—" : v.Value.ToString("0.0");
}

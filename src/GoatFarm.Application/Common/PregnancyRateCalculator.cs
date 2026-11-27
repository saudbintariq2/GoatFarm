using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;

namespace GoatFarm.Application.Common;

public readonly record struct PregnancyRate(int Scanned, int Pregnant, int Waiting, decimal? Rate);

/// <summary>
/// The one pregnancy rate shown on Breeding › Reports, Bucks compared, Bucks workload and Reports › Dashboard:
/// pregnant ÷ scanned, using each doe's latest cross. A doe that kidded counts as pregnant; does still waiting
/// for a scan are not in the denominator.
/// </summary>
public static class PregnancyRateCalculator
{
    public static bool IsPregnant(BreedingEvent e) =>
        e.Outcome is BreedingOutcome.ConfirmedPregnant or BreedingOutcome.Kidded or BreedingOutcome.Aborted
        || (e.Outcome == BreedingOutcome.Open && e.ScanKids is > 0);

    public static PregnancyRate Compute(IEnumerable<BreedingEvent> events)
    {
        var latest = events
            .Where(e => e.Outcome != BreedingOutcome.Cancelled)
            .GroupBy(e => e.DoeId)
            .Select(g => g.OrderByDescending(e => e.MatedDate).ThenByDescending(e => e.Id).First())
            .ToList();

        var pregnant = latest.Count(IsPregnant);
        var empty = latest.Count(e => e.Outcome == BreedingOutcome.Empty);
        var waiting = latest.Count(e => e.Outcome == BreedingOutcome.Open && !IsPregnant(e));
        var scanned = pregnant + empty;
        decimal? rate = scanned == 0 ? null : Math.Round(100m * pregnant / scanned, 0);
        return new PregnancyRate(scanned, pregnant, waiting, rate);
    }
}

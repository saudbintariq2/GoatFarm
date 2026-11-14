using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;

namespace GoatFarm.Application.Common;

public static class BreedingGroupPickerHelper
{
  /// <summary>Returns why this doe cannot be added to the buck group, or null if she can.</summary>
  public static string? BlockReason(
      Goat doe,
      DateOnly today,
      decimal minYoungDoeKg,
      decimal? latestWeightKg,
      Goat? buck,
      IReadOnlyDictionary<int, PedigreeNode> pedigree,
      IReadOnlyDictionary<int, string> doeIdToActiveBuckTag)
  {
    if (doe.IsArchived)
      return "Archived";

    if (doe.Gender != GoatGender.Female)
      return "Not a doe";

    if (doe.Status == GoatStatus.Kid)
      return "Still marked as a kid";

    if (doeIdToActiveBuckTag.TryGetValue(doe.Id, out var inGroupBuck))
      return $"In {inGroupBuck}'s group";

    if (doe.MatedDate.HasValue)
      return "Already crossed";

    var elig = BreedingRules.EligibilityError(doe, today);
    if (elig is not null)
    {
      var age = BreedingRules.AgeDays(doe, today);
      if (age.HasValue && age < BreedingHelper.MinBreedingAgeDays)
        return $"Too young ({age} days)";
      if (elig.Contains("scan", StringComparison.OrdinalIgnoreCase) || elig.Contains("crossed", StringComparison.OrdinalIgnoreCase))
        return elig;
      return elig.Length > 80 ? elig[..77] + "…" : elig;
    }

    if (latestWeightKg is null or <= 0)
      return "Weigh first — no weight";
    if (latestWeightKg < minYoungDoeKg)
      return $"{latestWeightKg:0.#} kg — needs {minYoungDoeKg:0.#} kg";

    if (buck is not null &&
        pedigree.TryGetValue(doe.Id, out var doeNode) &&
        pedigree.TryGetValue(buck.Id, out var buckNode))
    {
      if (doe.SireId == buck.Id)
        return $"Related to {buck.Tag} — he is her sire";
      foreach (var w in BreedingRules.InbreedingWarnings(doeNode, buckNode, pedigree))
        return $"Related to {buck.Tag} — {w}";
    }

    return null;
  }
}
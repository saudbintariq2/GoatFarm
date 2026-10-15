using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;

namespace GoatFarm.Application.Common;

public sealed record PedigreeNode(int Id, string Tag, int? SireId, int? DamId);

public sealed class CrossCheckResult
{
    public List<string> Errors { get; } = [];
    /// <summary>Must be confirmed by the user before the cross is saved.</summary>
    public List<string> Warnings { get; } = [];
    /// <summary>Informational only.</summary>
    public List<string> Notes { get; } = [];
}

public static class BreedingRules
{
    /// <summary>Birth date when one is recorded: EventDate for home-born goats, the optional DateOfBirth for bought goats.</summary>
    public static DateOnly? BirthDate(Goat goat) =>
        goat.Source == GoatSource.Born ? goat.EventDate : goat.DateOfBirth;

    public static bool AgeIsEstimated(Goat goat) =>
        goat.Source != GoatSource.Born && goat.DateOfBirth.HasValue && goat.DateOfBirthIsEstimated;

    /// <summary>Age in days when a birth date is recorded (exact or estimated); null when unknown.</summary>
    public static int? AgeDays(Goat goat, DateOnly today) =>
        BirthDate(goat) is { } born ? today.DayNumber - born.DayNumber : null;

    /// <summary>
    /// Breeding-age confirmation for bought does whose age is estimated or unknown. Under-age by a known or
    /// estimated birth date is an error instead (see EligibilityError).
    /// </summary>
    public static string? AgeVerificationWarning(Goat doe, DateOnly today)
    {
        if (doe.Source == GoatSource.Born) return null;
        var age = AgeDays(doe, today);
        if (age is null)
            return $"{doe.Tag} was bought and her age is unknown. Confirm she is at least {BreedingHelper.MinBreedingAgeDays} days old.";
        if (AgeIsEstimated(doe))
            return $"{doe.Tag}'s age is estimated (about {age} days). Confirm she is at least {BreedingHelper.MinBreedingAgeDays} days old.";
        return null;
    }

    /// <summary>Returns why the doe cannot be prepped or crossed, or null when she can.</summary>
    public static string? EligibilityError(Goat doe, DateOnly today)
    {
        if (doe.IsArchived)
            return $"{doe.Tag} is archived ({doe.ArchivedReason ?? "no longer in the herd"}).";
        if (doe.Gender != GoatGender.Female)
            return $"{doe.Tag} is not a doe.";
        if (doe.Status is GoatStatus.Buck or GoatStatus.Meat or GoatStatus.Sale)
            return $"{doe.Tag} is marked {DisplayHelper.GetStatusDisplay(doe.Status).Text} — not for breeding.";
        if (doe.MatedDate.HasValue)
            return $"{doe.Tag} was already crossed on {doe.MatedDate:yyyy-MM-dd}. Record her scan or remove that cross first.";

        var age = AgeDays(doe, today);
        if (age.HasValue && age < BreedingHelper.MinBreedingAgeDays)
            return AgeIsEstimated(doe)
                ? $"{doe.Tag} is only about {age} days old by her estimated birth date — does are bred from {BreedingHelper.MinBreedingAgeDays} days. Correct the estimate if it is wrong."
                : $"{doe.Tag} is only {age} days old — does are bred from {BreedingHelper.MinBreedingAgeDays} days.";
        if (!age.HasValue && doe.Status == GoatStatus.Kid)
            return $"{doe.Tag} is still marked as a kid and her age is unknown. Change her status first.";
        return null;
    }

    public static string? BuckError(Goat buck)
    {
        if (buck.IsArchived)
            return $"Buck {buck.Tag} is archived ({buck.ArchivedReason ?? "no longer in the herd"}).";
        if (buck.Gender != GoatGender.Male)
            return $"{buck.Tag} is not a male goat.";
        return null;
    }

    /// <summary>Relationship warnings between a doe and buck using registered sire/dam links.</summary>
    public static IEnumerable<string> InbreedingWarnings(
        PedigreeNode doe, PedigreeNode buck, IReadOnlyDictionary<int, PedigreeNode> herd)
    {
        if (doe.SireId == buck.Id)
            yield return $"{buck.Tag} is the sire (father) of {doe.Tag}.";
        if (buck.DamId == doe.Id)
            yield return $"{doe.Tag} is the dam (mother) of {buck.Tag}.";

        var sameSire = doe.SireId.HasValue && doe.SireId == buck.SireId;
        var sameDam = doe.DamId.HasValue && doe.DamId == buck.DamId;
        if (sameSire && sameDam)
        {
            yield return $"{doe.Tag} and {buck.Tag} are full siblings.";
            yield break;
        }
        if (sameSire || sameDam)
        {
            yield return $"{doe.Tag} and {buck.Tag} are half siblings (same {(sameSire ? "sire" : "dam")}).";
            yield break;
        }

        var doeGrand = Grandparents(doe, herd);
        var buckGrand = Grandparents(buck, herd);
        if (doeGrand.Overlaps(buckGrand))
            yield return $"{doe.Tag} and {buck.Tag} share a grandparent.";
        if (doeGrand.Contains(buck.Id))
            yield return $"{buck.Tag} is a grandparent of {doe.Tag}.";
        if (buckGrand.Contains(doe.Id))
            yield return $"{doe.Tag} is a grandparent of {buck.Tag}.";
    }

    public static bool HasKnownParents(PedigreeNode node) => node.SireId.HasValue || node.DamId.HasValue;

    private static HashSet<int> Grandparents(PedigreeNode node, IReadOnlyDictionary<int, PedigreeNode> herd)
    {
        var result = new HashSet<int>();
        foreach (var parentId in new[] { node.SireId, node.DamId })
        {
            if (parentId is null || !herd.TryGetValue(parentId.Value, out var parent)) continue;
            if (parent.SireId.HasValue) result.Add(parent.SireId.Value);
            if (parent.DamId.HasValue) result.Add(parent.DamId.Value);
        }
        return result;
    }
}

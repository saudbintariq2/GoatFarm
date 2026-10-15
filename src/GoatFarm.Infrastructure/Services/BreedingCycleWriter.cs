using GoatFarm.Application.Common;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Infrastructure.Services;

/// <summary>
/// Single place that changes a doe's breeding cycle so Breeding and Herd bulk actions apply the same rules.
/// Callers save; nothing here calls SaveChanges.
/// </summary>
internal static class BreedingCycleWriter
{
    public sealed record CrossPlan(CrossCheckResult Check, Goat? Buck, string? BuckTag);

    public static async Task<Dictionary<int, PedigreeNode>> LoadPedigreeAsync(GoatFarmDbContext context, CancellationToken ct) =>
        await context.Goats.AsNoTracking()
            .Select(g => new PedigreeNode(g.Id, g.Tag, g.SireId, g.DamId))
            .ToDictionaryAsync(n => n.Id, ct);

    public static async Task<Goat?> FindBuckAsync(GoatFarmDbContext context, string? buckTag, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(buckTag)) return null;
        var tag = buckTag.Trim().ToLower();
        return await context.Goats.AsNoTracking()
            .Where(g => g.Tag.ToLower() == tag)
            .OrderBy(g => g.IsArchived)
            .FirstOrDefaultAsync(ct);
    }

    public static CrossPlan PlanCross(
        Goat doe, Goat? buck, string? buckTag, DateOnly date, DateOnly today,
        IReadOnlyDictionary<int, PedigreeNode> pedigree)
    {
        var check = new CrossCheckResult();
        var eligibility = BreedingRules.EligibilityError(doe, today);
        if (eligibility is not null) check.Errors.Add(eligibility);

        if (date > today)
            check.Errors.Add("The cross date cannot be in the future.");
        else if (BreedingRules.BirthDate(doe) is { } born && !BreedingRules.AgeIsEstimated(doe) && date < born)
            check.Errors.Add($"The cross date is before {doe.Tag} was born.");

        if (check.Errors.Count == 0 && BreedingRules.AgeVerificationWarning(doe, today) is { } ageWarning)
            check.Warnings.Add(ageWarning);

        var trimmedBuckTag = string.IsNullOrWhiteSpace(buckTag) ? null : buckTag.Trim();
        if (buck is not null)
        {
            var buckError = BreedingRules.BuckError(buck);
            if (buckError is not null) check.Errors.Add(buckError);
            if (buck.Id == doe.Id) check.Errors.Add("The doe and buck are the same goat.");
            trimmedBuckTag = buck.Tag;

            if (check.Errors.Count == 0 &&
                pedigree.TryGetValue(doe.Id, out var doeNode) &&
                pedigree.TryGetValue(buck.Id, out var buckNode))
            {
                check.Warnings.AddRange(BreedingRules.InbreedingWarnings(doeNode, buckNode, pedigree));
                if (!BreedingRules.HasKnownParents(doeNode) || !BreedingRules.HasKnownParents(buckNode))
                    check.Notes.Add("Pedigree is incomplete — inbreeding can't be fully ruled out.");
            }
        }
        else if (trimmedBuckTag is not null)
        {
            check.Notes.Add($"Buck {trimmedBuckTag} is not registered in the herd — pedigree can't be checked.");
        }
        else
        {
            check.Notes.Add("No buck recorded for this cross.");
        }

        return new CrossPlan(check, buck, trimmedBuckTag);
    }

    public static void ApplyCross(GoatFarmDbContext context, Goat doe, DateOnly date, CrossPlan plan, IEnumerable<BreedingEvent> openEvents)
    {
        var now = DateTime.UtcNow;
        foreach (var stale in openEvents)
        {
            stale.Outcome = BreedingOutcome.Cancelled;
            stale.OutcomeDate = date;
            stale.Notes = AppendNote(stale.Notes, "superseded by a new cross");
            stale.UpdatedDate = now;
        }

        context.BreedingEvents.Add(new BreedingEvent
        {
            DoeId = doe.Id,
            BuckId = plan.Buck?.Id,
            BuckTagText = plan.BuckTag,
            MatedDate = date,
            Outcome = BreedingOutcome.Open
        });

        doe.MatedDate = date;
        doe.BuckTag = plan.BuckTag;
        doe.PrepCrossDate = null;
        doe.KidsCount = null;
        doe.UltrasoundDate = null;
        doe.BreedingStage = BreedingStage.ScanPending;
        doe.UpdatedDate = now;
    }

    public static Task<List<BreedingEvent>> OpenEventsAsync(GoatFarmDbContext context, int doeId, CancellationToken ct) =>
        context.BreedingEvents
            .Where(e => e.DoeId == doeId && (e.Outcome == BreedingOutcome.Open || e.Outcome == BreedingOutcome.ConfirmedPregnant))
            .OrderByDescending(e => e.MatedDate)
            .ToListAsync(ct);

    /// <summary>Closes the doe's open cycle with the given outcome and clears her current-cycle fields.</summary>
    public static void CloseCycle(Goat doe, IReadOnlyList<BreedingEvent> openEvents, BreedingOutcome outcome, DateOnly date, string? note = null)
    {
        var now = DateTime.UtcNow;
        foreach (var e in openEvents)
        {
            e.Outcome = outcome;
            e.OutcomeDate = date;
            if (note is not null) e.Notes = AppendNote(e.Notes, note);
            e.UpdatedDate = now;
        }

        doe.MatedDate = null;
        doe.BuckTag = null;
        doe.KidsCount = null;
        doe.UltrasoundDate = null;
        doe.PrepCrossDate = null;
        doe.BreedingStage = BreedingStage.None;
        doe.UpdatedDate = now;
    }

    private static string AppendNote(string? existing, string note) =>
        string.IsNullOrWhiteSpace(existing) ? note : $"{existing}; {note}";
}

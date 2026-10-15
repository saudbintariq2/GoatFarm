using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Infrastructure.Persistence;

internal static class GoatGraphCleaner
{
    /// <summary>
    /// Pedigree, breeding and kidding rows reference goats with RESTRICT foreign keys, so a full farm wipe
    /// must remove them and unlink parents before the goats themselves are deleted. Tasks are kept and only unlinked.
    /// </summary>
    public static async Task DetachBreedingGraphAsync(GoatFarmDbContext context, CancellationToken cancellationToken)
    {
        foreach (var task in await context.FarmTasks.Where(t => t.GoatId != null).ToListAsync(cancellationToken))
            task.GoatId = null;

        foreach (var goat in await context.Goats
                     .Where(g => g.SireId != null || g.DamId != null || g.KiddingRecordId != null)
                     .ToListAsync(cancellationToken))
        {
            goat.SireId = null;
            goat.DamId = null;
            goat.KiddingRecordId = null;
        }

        await context.SaveChangesAsync(cancellationToken);

        context.KiddingRecords.RemoveRange(await context.KiddingRecords.ToListAsync(cancellationToken));
        await context.SaveChangesAsync(cancellationToken);
        context.BreedingEvents.RemoveRange(await context.BreedingEvents.ToListAsync(cancellationToken));
        await context.SaveChangesAsync(cancellationToken);
    }
}

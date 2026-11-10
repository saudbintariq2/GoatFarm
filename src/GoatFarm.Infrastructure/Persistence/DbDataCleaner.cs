using GoatFarm.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Infrastructure.Persistence;

public static class DbDataCleaner
{
    public static async Task ClearAllFarmDataAsync(GoatFarmDbContext context, CancellationToken cancellationToken = default)
    {
        await BackupImportHelper.ClearFarmDataAsync(context, cancellationToken);

        var demoMarker = await context.AppSettings.FirstOrDefaultAsync(s => s.Key == DemoDataSeedHelper.MarkerKey, cancellationToken);
        if (demoMarker is not null)
        {
            context.AppSettings.Remove(demoMarker);
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}

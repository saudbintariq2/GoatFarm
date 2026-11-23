using GoatFarm.Application.Interfaces;
using GoatFarm.Infrastructure.Persistence;
using GoatFarm.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GoatFarm.Tests;

internal static class TestFarmMetrics
{
    private sealed class GoatServiceHolder
    {
        public GoatService Instance = null!;
    }

    public static GoatService CreateGoatService(GoatFarmDbContext db)
    {
        var holder = new GoatServiceHolder();
        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton<IGoatService>(_ => holder.Instance);
        services.AddSingleton<IMilkService>(new MilkService(db));
        services.AddSingleton<IFarmMetricsService, FarmMetricsService>();
        services.AddSingleton<IFeedService>(sp =>
            new FeedService(
                db,
                holder.Instance,
                sp.GetRequiredService<IMilkService>(),
                sp.GetRequiredService<IFarmMetricsService>()));
        var sp = services.BuildServiceProvider();
        holder.Instance = new GoatService(db, sp);
        return holder.Instance;
    }

    public static IFarmMetricsService Create(GoatFarmDbContext db, IGoatService goats)
    {
        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton(goats);
        services.AddSingleton<IMilkService>(new MilkService(db));
        services.AddSingleton<IFarmMetricsService, FarmMetricsService>();
        services.AddSingleton<IFeedService>(sp =>
            new FeedService(
                db,
                goats,
                sp.GetRequiredService<IMilkService>(),
                sp.GetRequiredService<IFarmMetricsService>()));
        return services.BuildServiceProvider().GetRequiredService<IFarmMetricsService>();
    }
}
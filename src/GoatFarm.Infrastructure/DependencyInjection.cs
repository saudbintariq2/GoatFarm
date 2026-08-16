using GoatFarm.Application.Interfaces;
using GoatFarm.Domain.Common;
using GoatFarm.Infrastructure.Configuration;
using GoatFarm.Infrastructure.Persistence;
using GoatFarm.Infrastructure.Repositories;
using GoatFarm.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GoatFarm.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = ConnectionStringResolver.Resolve(configuration);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'DefaultConnection' is missing or empty. " +
                "Set ONE of these, then redeploy:\n" +
                "  • Railway: link Postgres and set ConnectionStrings__DefaultConnection=${{Postgres.DATABASE_URL}}\n" +
                "  • Railway: or rely on DATABASE_URL when Postgres is in the same project\n" +
                "  • Azure: ConnectionStrings__DefaultConnection (two underscores)\n" +
                "  • Local: appsettings.Development.json");
        }

        services.AddMemoryCache();

        services.AddDbContext<GoatFarmDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorCodesToAdd: null);
                npgsql.CommandTimeout(60);
            }));

        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IGoatService, GoatService>();
        services.AddScoped<IBreedingService, BreedingService>();
        services.AddScoped<ISearchService, SearchService>();
        services.AddScoped<IFeedService, FeedService>();
        services.AddScoped<IMilkService, MilkService>();
        services.AddScoped<IFinanceService, FinanceService>();
        services.AddScoped<IVaccineService, VaccineService>();
        services.AddScoped<IReminderService, ReminderService>();
        services.AddScoped<IStatisticsService, StatisticsService>();
        services.AddScoped<IBackupService, BackupService>();
        services.AddScoped<IUserSettingsService, UserSettingsService>();
        services.AddScoped<ILookupService, LookupService>();

        return services;
    }
}

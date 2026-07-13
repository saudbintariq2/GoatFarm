using GoatFarm.Application.Interfaces;
using GoatFarm.Domain.Common;
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
        services.AddDbContext<GoatFarmDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IGoatService, GoatService>();
        services.AddScoped<IFeedService, FeedService>();
        services.AddScoped<IMilkService, MilkService>();
        services.AddScoped<IFinanceService, FinanceService>();
        services.AddScoped<IVaccineService, VaccineService>();
        services.AddScoped<IReminderService, ReminderService>();
        services.AddScoped<IStatisticsService, StatisticsService>();
        services.AddScoped<IBackupService, BackupService>();
        services.AddScoped<IUserSettingsService, UserSettingsService>();

        return services;
    }
}

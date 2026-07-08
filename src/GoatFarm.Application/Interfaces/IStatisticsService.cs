using GoatFarm.Application.ViewModels.Dashboard;
using GoatFarm.Application.ViewModels.Goats;

namespace GoatFarm.Application.Interfaces;

public interface IStatisticsService
{
    Task<DashboardViewModel> GetDashboardAsync(string? month, CancellationToken cancellationToken = default);
    Task<HerdStatsViewModel> GetHerdStatsAsync(CancellationToken cancellationToken = default);
}

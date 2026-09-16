using GoatFarm.Application.ViewModels.Reports;

namespace GoatFarm.Application.Interfaces;

public interface IReportsService
{
    Task<ReportsPageViewModel> GetReportsPageAsync(
        string? period,
        string? customFrom,
        string? customTo,
        CancellationToken cancellationToken = default);
}

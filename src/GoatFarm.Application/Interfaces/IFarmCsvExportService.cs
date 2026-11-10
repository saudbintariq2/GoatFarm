namespace GoatFarm.Application.Interfaces;

public interface IFarmCsvExportService
{
    Task<byte[]> ExportHerdAsync(CancellationToken cancellationToken = default);
    Task<byte[]> ExportFinanceAsync(string? month, CancellationToken cancellationToken = default);
    Task<byte[]> ExportFeedAsync(CancellationToken cancellationToken = default);
    Task<byte[]> ExportHealthAsync(CancellationToken cancellationToken = default);
    Task<byte[]> ExportBreedingAsync(CancellationToken cancellationToken = default);
    Task<byte[]> ExportTasksAsync(CancellationToken cancellationToken = default);
}

namespace GoatFarm.Application.Interfaces;

public interface IBackupService
{
    Task<object> ExportAsync(CancellationToken cancellationToken = default);
    Task ImportAsync(string json, CancellationToken cancellationToken = default);
}

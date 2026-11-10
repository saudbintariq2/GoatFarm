using GoatFarm.Application.ViewModels.Backup;

namespace GoatFarm.Application.Interfaces;

public interface IBackupService
{
    Task<object> ExportAsync(CancellationToken cancellationToken = default);
    Task ImportAsync(string json, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StoredBackupFileViewModel>> ListStoredBackupsAsync(CancellationToken cancellationToken = default);
    Task<StoredBackupFileViewModel> SaveStoredBackupAsync(object payload, string label, CancellationToken cancellationToken = default);
    Task RunAutomaticBackupIfDueAsync(CancellationToken cancellationToken = default);
    Task<byte[]?> ReadStoredBackupAsync(string fileName, CancellationToken cancellationToken = default);
}

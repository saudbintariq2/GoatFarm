using GoatFarm.Application.ViewModels.Backup;

namespace GoatFarm.Application.Interfaces;

public interface IBackupService
{
    Task<object> ExportAsync(CancellationToken cancellationToken = default);
    Task ImportAsync(string json, CancellationToken cancellationToken = default);
    /// <summary>Removes all farm operational data. Identity users, roles, and app settings are kept.</summary>
    Task ClearAllFarmDataAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StoredBackupFileViewModel>> ListStoredBackupsAsync(CancellationToken cancellationToken = default);
    Task<StoredBackupFileViewModel> SaveStoredBackupAsync(object payload, string label, CancellationToken cancellationToken = default);
    /// <summary>
    /// The daily backup: refuses an export with no goats (critical alert, nothing saved), otherwise saves on the server,
    /// uploads off-site and deletes copies older than the retention period.
    /// </summary>
    Task<BackupRunResult> RunScheduledBackupAsync(CancellationToken cancellationToken = default);
    Task<BackupStatusViewModel> GetStatusAsync(CancellationToken cancellationToken = default);
    Task<byte[]?> ReadStoredBackupAsync(string fileName, CancellationToken cancellationToken = default);
}

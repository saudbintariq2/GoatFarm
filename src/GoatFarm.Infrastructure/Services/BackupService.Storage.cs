using System.Text.Json;
using GoatFarm.Application.ViewModels.Backup;

namespace GoatFarm.Infrastructure.Services;

public partial class BackupService
{
    private string ResolveStorageDirectory()
    {
        if (!string.IsNullOrWhiteSpace(_backupOptions.StoragePath))
            return Path.GetFullPath(_backupOptions.StoragePath);
        var root = _hostEnvironment?.ContentRootPath ?? Directory.GetCurrentDirectory();
        return Path.Combine(root, "App_Data", "backups");
    }

    public async Task<IReadOnlyList<StoredBackupFileViewModel>> ListStoredBackupsAsync(CancellationToken cancellationToken = default)
    {
        var dir = ResolveStorageDirectory();
        if (!Directory.Exists(dir))
            return [];

        return await Task.Run(() =>
        {
            return Directory.EnumerateFiles(dir, "goat-records-*.json")
                .Select(path =>
                {
                    var info = new FileInfo(path);
                    var name = info.Name;
                    var label = name.Contains("pre-restore", StringComparison.OrdinalIgnoreCase) ? "pre-restore"
                        : name.Contains("-auto-", StringComparison.OrdinalIgnoreCase) ? "automatic"
                        : "stored";
                    return new StoredBackupFileViewModel
                    {
                        FileName = name,
                        Label = label,
                        CreatedUtc = info.LastWriteTimeUtc,
                        SizeBytes = info.Length
                    };
                })
                .OrderByDescending(f => f.CreatedUtc)
                .ToList();
        }, cancellationToken);
    }

    public async Task<StoredBackupFileViewModel> SaveStoredBackupAsync(object payload, string label, CancellationToken cancellationToken = default)
    {
        var dir = ResolveStorageDirectory();
        Directory.CreateDirectory(dir);
        var safeLabel = string.IsNullOrWhiteSpace(label) ? "stored" : label.Replace(' ', '-').ToLowerInvariant();
        var fileName = $"goat-records-{safeLabel}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";
        var path = Path.Combine(dir, fileName);
        var json = JsonSerializer.Serialize(payload, SerializerOptions);
        await File.WriteAllTextAsync(path, json, cancellationToken);
        await TrimStoredBackupsAsync(dir, cancellationToken);
        var info = new FileInfo(path);
        return new StoredBackupFileViewModel
        {
            FileName = fileName,
            Label = safeLabel,
            CreatedUtc = info.LastWriteTimeUtc,
            SizeBytes = info.Length
        };
    }

    public async Task RunAutomaticBackupIfDueAsync(CancellationToken cancellationToken = default)
    {
        if (!_backupOptions.AutomaticEnabled)
            return;

        var dir = ResolveStorageDirectory();
        Directory.CreateDirectory(dir);
        var interval = TimeSpan.FromHours(Math.Max(1, _backupOptions.IntervalHours));
        var latest = Directory.Exists(dir)
            ? Directory.EnumerateFiles(dir, "goat-records-auto-*.json")
                .Select(p => new FileInfo(p))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .FirstOrDefault()
            : null;

        if (latest is not null && DateTime.UtcNow - latest.LastWriteTimeUtc < interval)
            return;

        var payload = await ExportAsync(cancellationToken);
        await SaveStoredBackupAsync(payload, "auto", cancellationToken);
    }

    public async Task<byte[]?> ReadStoredBackupAsync(string fileName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Contains("..", StringComparison.Ordinal)
            || fileName.Contains('/') || fileName.Contains('\\'))
            return null;

        var path = Path.Combine(ResolveStorageDirectory(), fileName);
        if (!File.Exists(path))
            return null;

        return await File.ReadAllBytesAsync(path, cancellationToken);
    }

    private async Task TrimStoredBackupsAsync(string dir, CancellationToken cancellationToken)
    {
        var retain = Math.Max(3, _backupOptions.RetainCount);
        var files = Directory.EnumerateFiles(dir, "goat-records-*.json")
            .Select(p => new FileInfo(p))
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .Skip(retain)
            .ToList();
        foreach (var f in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            f.Delete();
        }

        await Task.CompletedTask;
    }
}

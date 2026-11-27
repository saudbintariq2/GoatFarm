using System.Text.Json;
using GoatFarm.Application.ViewModels.Backup;
using GoatFarm.Domain.Constants;
using GoatFarm.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace GoatFarm.Infrastructure.Services;

public partial class BackupService
{
    public const string BackupFailedAlertKey = "backup-failed";
    public const string BackupOffsiteAlertKey = "backup-offsite";
    private const int AlwaysKeepNewest = 3;

    private sealed class BackupStatusRecord
    {
        public DateTime? LastSuccessUtc { get; set; }
        public string? LastFileName { get; set; }
        public int LastGoats { get; set; }
        public bool LastUploadedOffsite { get; set; }
        public DateTime? LastFailureUtc { get; set; }
        public string? LastFailureMessage { get; set; }
    }

    private string ResolveStorageDirectory()
    {
        if (!string.IsNullOrWhiteSpace(_backupOptions.StoragePath))
            return Path.GetFullPath(_backupOptions.StoragePath);
        var root = _hostEnvironment?.ContentRootPath ?? Directory.GetCurrentDirectory();
        return Path.Combine(root, "App_Data", "backups");
    }

    public static TimeZoneInfo ResolveTimeZone(string? id)
    {
        foreach (var candidate in new[] { id, "Asia/Karachi", "Pakistan Standard Time" })
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            try { return TimeZoneInfo.FindSystemTimeZoneById(candidate); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.CreateCustomTimeZone("PKT", TimeSpan.FromHours(5), "PKT", "PKT");
    }

    private string FormatLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), ResolveTimeZone(_backupOptions.TimeZone))
            .ToString("yyyy-MM-dd HH:mm");

    public async Task<IReadOnlyList<StoredBackupFileViewModel>> ListStoredBackupsAsync(CancellationToken cancellationToken = default)
    {
        var rows = new Dictionary<string, StoredBackupFileViewModel>(StringComparer.OrdinalIgnoreCase);
        var dir = ResolveStorageDirectory();
        if (Directory.Exists(dir))
        {
            foreach (var path in Directory.EnumerateFiles(dir, BackupFileNames.Prefix + "*.json"))
            {
                var info = new FileInfo(path);
                rows[info.Name] = new StoredBackupFileViewModel
                {
                    FileName = info.Name,
                    Label = BackupFileNames.LabelOf(info.Name),
                    CreatedUtc = BackupFileNames.TryParseSavedUtc(info.Name) ?? await ReadSavedAtAsync(path, cancellationToken) ?? info.LastWriteTimeUtc,
                    SizeBytes = info.Length,
                    OnServer = true
                };
            }
        }

        if (_offsite.IsConfigured)
        {
            try
            {
                foreach (var o in await _offsite.ListAsync(cancellationToken))
                {
                    if (rows.TryGetValue(o.Name, out var local)) { local.Offsite = true; continue; }
                    rows[o.Name] = new StoredBackupFileViewModel
                    {
                        FileName = o.Name,
                        Label = BackupFileNames.LabelOf(o.Name),
                        CreatedUtc = BackupFileNames.TryParseSavedUtc(o.Name) ?? o.LastModifiedUtc,
                        SizeBytes = o.SizeBytes,
                        Offsite = true
                    };
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await RaiseAlertAsync(BackupOffsiteAlertKey, "critical",
                    $"Could not read the off-site backup bucket: {ex.Message}", cancellationToken);
            }
        }

        foreach (var row in rows.Values)
            row.CreatedDisplay = FormatLocal(row.CreatedUtc);
        return rows.Values.OrderByDescending(f => f.CreatedUtc).ToList();
    }

    private static async Task<DateTime?> ReadSavedAtAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = File.OpenRead(path);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            return doc.RootElement.TryGetProperty("_savedAt", out var s) && s.TryGetDateTime(out var dt)
                ? dt.ToUniversalTime()
                : null;
        }
        catch (JsonException) { return null; }
        catch (IOException) { return null; }
    }

    public async Task<StoredBackupFileViewModel> SaveStoredBackupAsync(object payload, string label, CancellationToken cancellationToken = default)
    {
        var safeLabel = string.IsNullOrWhiteSpace(label) ? "stored" : label.Replace(' ', '-').ToLowerInvariant();
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, SerializerOptions);
        var (row, _) = await StoreAsync(bytes, safeLabel, throwOnOffsiteError: false, cancellationToken);
        return row;
    }

    private async Task<(StoredBackupFileViewModel Row, bool Uploaded)> StoreAsync(
        byte[] bytes, string label, bool throwOnOffsiteError, CancellationToken cancellationToken)
    {
        var savedUtc = DateTime.UtcNow;
        var fileName = BackupFileNames.Build(label, savedUtc);
        var dir = ResolveStorageDirectory();
        Directory.CreateDirectory(dir);
        await File.WriteAllBytesAsync(Path.Combine(dir, fileName), bytes, cancellationToken);
        TrimLocalBackups(dir, cancellationToken);

        var uploaded = false;
        if (_offsite.IsConfigured)
        {
            try
            {
                await _offsite.UploadAsync(fileName, bytes, cancellationToken);
                uploaded = true;
                await _offsite.DeleteOlderThanAsync(TimeSpan.FromDays(RetentionDays), cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException && !throwOnOffsiteError)
            {
                await RaiseAlertAsync(BackupOffsiteAlertKey, "critical",
                    $"Backup {fileName} was saved on the server but the off-site upload failed: {ex.Message}", cancellationToken);
            }
        }

        return (new StoredBackupFileViewModel
        {
            FileName = fileName,
            Label = BackupFileNames.LabelOf(fileName),
            CreatedUtc = savedUtc,
            CreatedDisplay = FormatLocal(savedUtc),
            SizeBytes = bytes.LongLength,
            OnServer = true,
            Offsite = uploaded
        }, uploaded);
    }

    private int RetentionDays => Math.Max(1, _backupOptions.RetentionDays);

    public async Task<BackupRunResult> RunScheduledBackupAsync(CancellationToken cancellationToken = default)
    {
        var status = await LoadStatusAsync(cancellationToken);
        try
        {
            _context.ChangeTracker.Clear();
            var bytes = JsonSerializer.SerializeToUtf8Bytes(await ExportAsync(cancellationToken), SerializerOptions);
            var goats = CountGoats(bytes);
            var minimum = Math.Max(1, _backupOptions.MinimumGoats);
            if (goats < minimum)
            {
                var message = $"Daily backup refused: the export had {goats} goats, so nothing was saved. "
                    + "The backup job may be reading an empty or wrong database — check the connection string.";
                await RecordFailureAsync(status, message, cancellationToken);
                return new BackupRunResult { Success = false, Goats = goats, Message = message };
            }

            var (row, uploaded) = await StoreAsync(bytes, "auto", throwOnOffsiteError: true, cancellationToken);

            status.LastSuccessUtc = row.CreatedUtc;
            status.LastFileName = row.FileName;
            status.LastGoats = goats;
            status.LastUploadedOffsite = uploaded;
            await SaveStatusAsync(status, cancellationToken);
            await FarmAlertStore.ClearAsync(_context, BackupFailedAlertKey, cancellationToken);

            if (uploaded)
                await FarmAlertStore.ClearAsync(_context, BackupOffsiteAlertKey, cancellationToken);
            else if (_hostEnvironment?.IsDevelopment() != true)
                await RaiseAlertAsync(BackupOffsiteAlertKey, "critical",
                    "Backups are only on the app server, which is wiped on every redeploy. Set BACKUP_BUCKET, BACKUP_KEY, BACKUP_SECRET and BACKUP_ENDPOINT.",
                    cancellationToken);

            return new BackupRunResult
            {
                Success = true,
                FileName = row.FileName,
                Goats = goats,
                UploadedOffsite = uploaded,
                Message = $"Backup {row.FileName} saved with {goats} goats" + (uploaded ? " (server + off-site)." : " (server only).")
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var message = $"Daily backup failed: {ex.Message}";
            _context.ChangeTracker.Clear();
            await RecordFailureAsync(status, message, cancellationToken);
            return new BackupRunResult { Success = false, Message = message };
        }
    }

    public async Task<BackupStatusViewModel> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var s = await LoadStatusAsync(cancellationToken);
        return new BackupStatusViewModel
        {
            AutomaticEnabled = _backupOptions.AutomaticEnabled,
            OffsiteConfigured = _offsite.IsConfigured,
            ScheduleDisplay = $"daily at {_backupOptions.DailyAt} ({_backupOptions.TimeZone}), kept {RetentionDays} days",
            LastSuccessUtc = s.LastSuccessUtc,
            LastSuccessDisplay = s.LastSuccessUtc is { } ok ? FormatLocal(ok) : "never",
            LastFileName = s.LastFileName,
            LastGoats = s.LastGoats,
            LastFailureUtc = s.LastFailureUtc,
            LastFailureMessage = s.LastFailureMessage
        };
    }

    private static int CountGoats(byte[] bytes)
    {
        using var doc = JsonDocument.Parse(bytes);
        return doc.RootElement.TryGetProperty("goats", out var goats) && goats.ValueKind == JsonValueKind.Array
            ? goats.GetArrayLength()
            : 0;
    }

    private async Task RecordFailureAsync(BackupStatusRecord status, string message, CancellationToken cancellationToken)
    {
        status.LastFailureUtc = DateTime.UtcNow;
        status.LastFailureMessage = message;
        await SaveStatusAsync(status, cancellationToken);
        await RaiseAlertAsync(BackupFailedAlertKey, "critical", message, cancellationToken);
    }

    private Task RaiseAlertAsync(string key, string severity, string message, CancellationToken cancellationToken) =>
        FarmAlertStore.RaiseAsync(_context, new FarmAlert { Key = key, Severity = severity, Message = message, Href = "/BackupCenter" }, cancellationToken);

    private async Task<BackupStatusRecord> LoadStatusAsync(CancellationToken cancellationToken)
    {
        var row = await _context.AppSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == AppSettingKeys.BackupStatus, cancellationToken);
        if (string.IsNullOrWhiteSpace(row?.Value)) return new BackupStatusRecord();
        try { return JsonSerializer.Deserialize<BackupStatusRecord>(row.Value) ?? new BackupStatusRecord(); }
        catch (JsonException) { return new BackupStatusRecord(); }
    }

    private async Task SaveStatusAsync(BackupStatusRecord status, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(status);
        var row = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == AppSettingKeys.BackupStatus, cancellationToken);
        if (row is null) _context.AppSettings.Add(new AppSetting { Key = AppSettingKeys.BackupStatus, Value = json });
        else row.Value = json;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<byte[]?> ReadStoredBackupAsync(string fileName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Contains("..", StringComparison.Ordinal)
            || fileName.Contains('/') || fileName.Contains('\\')
            || !fileName.StartsWith(BackupFileNames.Prefix, StringComparison.OrdinalIgnoreCase))
            return null;

        var path = Path.Combine(ResolveStorageDirectory(), fileName);
        if (File.Exists(path))
            return await File.ReadAllBytesAsync(path, cancellationToken);

        return _offsite.IsConfigured ? await _offsite.DownloadAsync(fileName, cancellationToken) : null;
    }

    /// <summary>Deletes server copies older than the retention period, always keeping the newest few.</summary>
    private void TrimLocalBackups(string dir, CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddDays(-RetentionDays);
        var files = Directory.EnumerateFiles(dir, BackupFileNames.Prefix + "*.json")
            .Select(p => new FileInfo(p))
            .Select(f => (File: f, Saved: BackupFileNames.TryParseSavedUtc(f.Name) ?? f.LastWriteTimeUtc))
            .OrderByDescending(f => f.Saved)
            .Skip(AlwaysKeepNewest)
            .Where(f => f.Saved < cutoff)
            .ToList();
        foreach (var (file, _) in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            file.Delete();
        }
    }
}

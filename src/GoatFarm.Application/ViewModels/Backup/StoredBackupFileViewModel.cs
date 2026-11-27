namespace GoatFarm.Application.ViewModels.Backup;

public class StoredBackupFileViewModel
{
    public string FileName { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    /// <summary>Save time from the file name (or the backup's <c>_savedAt</c>), not the file-system date.</summary>
    public DateTime CreatedUtc { get; set; }
    public string CreatedDisplay { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public bool OnServer { get; set; }
    public bool Offsite { get; set; }
    public string LocationDisplay => OnServer && Offsite ? "Server + off-site" : Offsite ? "Off-site" : "Server only";
}

public class BackupRunResult
{
    public bool Success { get; set; }
    public string? FileName { get; set; }
    public int Goats { get; set; }
    public bool UploadedOffsite { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class BackupStatusViewModel
{
    public bool AutomaticEnabled { get; set; }
    public bool OffsiteConfigured { get; set; }
    public string ScheduleDisplay { get; set; } = string.Empty;
    public DateTime? LastSuccessUtc { get; set; }
    public string LastSuccessDisplay { get; set; } = "never";
    public string? LastFileName { get; set; }
    public int LastGoats { get; set; }
    public DateTime? LastFailureUtc { get; set; }
    public string? LastFailureMessage { get; set; }
}

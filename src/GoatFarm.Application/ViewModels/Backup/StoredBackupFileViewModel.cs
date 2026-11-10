namespace GoatFarm.Application.ViewModels.Backup;

public class StoredBackupFileViewModel
{
    public string FileName { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public string CreatedDisplay => CreatedUtc.ToLocalTime().ToString("g");
    public long SizeBytes { get; set; }
}

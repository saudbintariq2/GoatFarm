namespace GoatFarm.Infrastructure.Configuration;

public class BackupOptions
{
    public const string SectionName = "Backup";

    public bool AutomaticEnabled { get; set; }
    public int IntervalHours { get; set; } = 168;
    public string StoragePath { get; set; } = "";
    public int RetainCount { get; set; } = 12;
}

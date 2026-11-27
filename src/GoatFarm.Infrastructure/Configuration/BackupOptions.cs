namespace GoatFarm.Infrastructure.Configuration;

public class BackupOptions
{
    public const string SectionName = "Backup";

    public bool AutomaticEnabled { get; set; }
    /// <summary>Local time of the daily backup, "HH:mm" in <see cref="TimeZone"/>.</summary>
    public string DailyAt { get; set; } = "02:00";
    public string TimeZone { get; set; } = "Asia/Karachi";
    public string StoragePath { get; set; } = "";
    /// <summary>Server and off-site copies older than this are deleted; the newest few local files are always kept.</summary>
    public int RetentionDays { get; set; } = 30;
    /// <summary>An automatic backup with fewer goats than this is refused and raises a critical alert.</summary>
    public int MinimumGoats { get; set; } = 1;
}

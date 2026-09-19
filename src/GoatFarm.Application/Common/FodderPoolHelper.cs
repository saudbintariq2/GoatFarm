namespace GoatFarm.Application.Common;

public class FodderPoolItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class FodderPoolDto
{
    public decimal Acres { get; set; }
    public string Mode { get; set; } = "share";
    public List<FodderPoolItemDto> Items { get; set; } = [];
}

public class FeedSettingsDto
{
    public bool AutoUsage { get; set; } = true;
    public string? LastUsageDate { get; set; }
}

public static class FodderPoolHelper
{
    public static FodderPoolDto DefaultPool() => new()
    {
        Items =
        [
            new() { Id = "rent", Label = "Land rent", Amount = 0 },
            new() { Id = "seeding", Label = "Seeding & ploughing", Amount = 0 },
            new() { Id = "cutting", Label = "Cutting & labour", Amount = 0 },
            new() { Id = "water", Label = "Irrigation / water", Amount = 0 }
        ]
    };

    public static decimal AnnualTotal(FodderPoolDto pool) =>
        pool.Items.Sum(i => i.Amount);

    public static decimal DailyTotal(FodderPoolDto pool) =>
        AnnualTotal(pool) / 365m;
}

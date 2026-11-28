namespace GoatFarm.Application.Common;

/// <summary>B7 — Flushing custom feed group for Second group build-up.</summary>
public static class FlushingFeedHelper
{
    public const string GroupName = "Flushing";
    public const string SecondBatchName = "Second group";
    public const decimal ExtraMixKg = 0.3m;
    public const string PlanActionKey = "flushing-plan";
    public const string RestoreSettingKey = "FlushingRestoreGroups";

    public static bool IsSecondGroup(BreedingSeasonBatch batch) =>
        string.Equals(batch.Name, SecondBatchName, StringComparison.OrdinalIgnoreCase);

    public static BreedingSeasonBatch? FindSecondGroup(BreedingSeasonSettings season) =>
        season.Batches.FirstOrDefault(IsSecondGroup);
}

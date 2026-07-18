namespace GoatFarm.Application.Common;

public static class BreedingHelper
{
    public const int GestationDays = 150;
    public const int KiddingWindowStart = 145;
    public const int KiddingWindowEnd = 155;
    public const int PrepDietLeadDays = 60;

    public static string KidsLabel(int count) => count switch
    {
        1 => "1 kid (single)",
        2 => "Twins",
        3 => "Triplets",
        _ => $"{count} kids"
    };

    public static DateOnly ExpectedKidding(DateOnly matedDate) =>
        matedDate.AddDays(GestationDays);

    public static int DaysUntil(DateOnly date)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        return (date.ToDateTime(TimeOnly.MinValue) - today.ToDateTime(TimeOnly.MinValue)).Days;
    }

    public static string DueText(int daysUntil) => daysUntil switch
    {
        < 0 => $"overdue {-daysUntil}d",
        0 => "today",
        _ => $"in {daysUntil} days"
    };
}

using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;

namespace GoatFarm.Application.Common;

public static class KidLabels
{
    public static string MilkTypeLabel(KidMilkType type) => type switch
    {
        KidMilkType.Mother => "Mother",
        KidMilkType.Powder => "Powder",
        KidMilkType.Mixed => "Mixed",
        KidMilkType.Foster => "Foster",
        _ => type.ToString()
    };

    public static string KidMilkLine(Goat kid, DateOnly today, int dietDays = HerdRules.WeanDaysDefault)
    {
        if (kid.Status != GoatStatus.Kid || kid.WeanDate.HasValue)
            return string.Empty;

        var age = HerdRules.AgeDays(kid, today);
        var dayOfDiet = age is > 0 ? age.Value : 1;

        return kid.KidMilkType switch
        {
            KidMilkType.Mother => $"Mother milk · day {dayOfDiet} of {dietDays}",
            KidMilkType.Powder => $"Powder milk · day {dayOfDiet} of {dietDays}",
            KidMilkType.Mixed => $"Mixed milk · day {dayOfDiet} of {dietDays}",
            KidMilkType.Foster => $"Foster milk · day {dayOfDiet} of {dietDays}",
            _ => $"On milk · day {dayOfDiet} of {dietDays}"
        };
    }
}
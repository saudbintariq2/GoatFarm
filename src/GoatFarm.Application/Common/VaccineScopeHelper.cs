using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;

namespace GoatFarm.Application.Common;

public static class VaccineScopeHelper
{
    public static bool AppliesToGoat(Vaccine vaccine, Goat goat) =>
        vaccine.Scope == VaccineScope.All || MatchesScope(goat, vaccine.Scope);

    public static bool MatchesScope(Goat goat, VaccineScope scope) =>
        scope switch
        {
            VaccineScope.Kid => goat.Status == GoatStatus.Kid,
            VaccineScope.Milking => goat.Status == GoatStatus.Milking,
            VaccineScope.Pregnant => PregnancyRules.IsConfirmedPregnant(goat),
            VaccineScope.Dry => goat.Status == GoatStatus.Dry,
            VaccineScope.Buck => goat.Status == GoatStatus.Buck,
            VaccineScope.Sale => goat.Status == GoatStatus.Sale,
            _ => false
        };
}
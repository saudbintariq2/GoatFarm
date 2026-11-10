using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;
using System.Linq.Expressions;

namespace GoatFarm.Application.Common;

/// <summary>Single definition of pregnancy used by herd, breeding, and reports (cross + positive scan).</summary>
public static class PregnancyRules
{
    public static bool IsConfirmedPregnant(Goat g) =>
        !g.IsArchived &&
        g.Gender == GoatGender.Female &&
        g.MatedDate.HasValue &&
        g.KidsCount is > 0;

    public static bool IsAwaitingScan(Goat g) =>
        !g.IsArchived &&
        g.Gender == GoatGender.Female &&
        g.MatedDate.HasValue &&
        !(g.KidsCount > 0);

    public static Expression<Func<Goat, bool>> ConfirmedPregnantExpression { get; } = g =>
        g.Gender == GoatGender.Female && g.MatedDate != null && g.KidsCount > 0;
}

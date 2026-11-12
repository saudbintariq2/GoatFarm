using GoatFarm.Domain.Entities;

namespace GoatFarm.Application.Common;

public static class BuckCheckHelper
{
    public const int ValidDays = 60;

    public static BuckHealthCheck? LatestCheck(IEnumerable<BuckHealthCheck> checks, int buckId) =>
        checks.Where(c => c.BuckId == buckId).OrderByDescending(c => c.CheckDate).FirstOrDefault();

    public static bool IsOk(BuckHealthCheck? check, DateOnly onOrBefore) =>
        check is not null && onOrBefore.DayNumber - check.CheckDate.DayNumber <= ValidDays;
}

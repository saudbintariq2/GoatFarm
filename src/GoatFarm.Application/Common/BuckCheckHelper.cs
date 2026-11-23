using GoatFarm.Domain.Entities;

namespace GoatFarm.Application.Common;

public static class BuckCheckHelper
{
    public const int ValidDays = 60;

    public static BuckHealthCheck? LatestCheck(IEnumerable<BuckHealthCheck> checks, int buckId) =>
        checks.Where(c => c.BuckId == buckId).OrderByDescending(c => c.CheckDate).FirstOrDefault();

    public static bool IsOk(BuckHealthCheck? check, DateOnly onOrBefore) =>
        check is not null && onOrBefore.DayNumber - check.CheckDate.DayNumber <= ValidDays;

    /// <summary>B6 — valid check on or after 30 days before buck-in, and not stale for today's work.</summary>
    public static bool IsOkForBuckIn(BuckHealthCheck? check, DateOnly buckInDate, DateOnly today)
    {
        if (check is null) return false;
        var earliest = buckInDate.AddDays(-30);
        return check.CheckDate >= earliest && today.DayNumber - check.CheckDate.DayNumber <= ValidDays;
    }
}

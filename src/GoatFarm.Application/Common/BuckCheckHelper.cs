using GoatFarm.Domain.Entities;

namespace GoatFarm.Application.Common;

public enum BuckCheckState
{
    Due,
    Missed,
    Done
}

/// <summary>B6 — single buck-check status for Breeding › Today and Bucks.</summary>
public sealed class BuckCheckStatus
{
    public BuckCheckState State { get; init; }
    public DateOnly? DueDate { get; init; }
    public BuckHealthCheck? Check { get; init; }
    public bool IsOk => State == BuckCheckState.Done;

    public string BucksLineHtml
    {
        get
        {
            if (State == BuckCheckState.Done && Check is not null)
            {
                return "Buck check done " + Check.CheckDate.ToString("d MMM yyyy") +
                       (Check.Bcs is not null ? " · BCS " + Check.Bcs.Value.ToString("0.#") : "") +
                       (Check.WeightKg is not null ? " · " + Check.WeightKg.Value.ToString("0.#") + " kg" : "");
            }

            if (Check is null)
                return State == BuckCheckState.Missed ? "Buck check missed" : "Buck check needed";

            return State == BuckCheckState.Missed
                ? "Last check " + Check.CheckDate.ToString("d MMM yyyy") + " — missed"
                : "Last check " + Check.CheckDate.ToString("d MMM yyyy") + " — check again";
        }
    }

    public string TodayTitle(string buckTag) =>
        State == BuckCheckState.Missed
            ? "Buck check missed for " + buckTag
            : "Buck check for " + buckTag;

    public string TodaySubtext(string groupName) =>
        State == BuckCheckState.Missed
            ? "Buck already with does — check now · " + groupName
            : "Needed before he goes in · " + groupName;
}

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

    /// <summary>
    /// Single status for Today jobs and Bucks rows.
    /// When <paramref name="buckInDate"/> is set, uses the buck-in window rules; otherwise general 60-day currency.
    /// </summary>
    public static BuckCheckStatus Evaluate(BuckHealthCheck? check, DateOnly today, DateOnly? buckInDate = null)
    {
        if (buckInDate is { } inDate)
        {
            if (IsOkForBuckIn(check, inDate, today))
                return new BuckCheckStatus { State = BuckCheckState.Done, Check = check, DueDate = null };

            if (today >= inDate)
                return new BuckCheckStatus { State = BuckCheckState.Missed, Check = check, DueDate = today };

            var due = inDate.AddDays(-14);
            return new BuckCheckStatus { State = BuckCheckState.Due, Check = check, DueDate = due };
        }

        if (IsOk(check, today))
            return new BuckCheckStatus { State = BuckCheckState.Done, Check = check, DueDate = null };

        if (check is null)
            return new BuckCheckStatus { State = BuckCheckState.Due, Check = null, DueDate = today };

        var renew = check.CheckDate.AddDays(ValidDays);
        if (today > renew)
            return new BuckCheckStatus { State = BuckCheckState.Missed, Check = check, DueDate = today };

        return new BuckCheckStatus { State = BuckCheckState.Due, Check = check, DueDate = renew };
    }
}

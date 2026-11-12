using GoatFarm.Domain.Entities;

namespace GoatFarm.Application.Common;

public sealed class BreedingGroupMemberOutcome
{
    public string Key { get; init; } = "other";
    public string Text { get; init; } = string.Empty;
    public int KidsAlive { get; init; }
}

public static class BreedingGroupHelper
{
    public static bool TryParseStart(string? start, out DateOnly date)
    {
        date = default;
        return !string.IsNullOrWhiteSpace(start) && DateOnly.TryParse(start, out date);
    }

    public static BreedingGroupMemberOutcome ResolveOutcome(
        BreedingGroupDto group,
        BreedingGroupMemberDto member,
        Goat? goat,
        IReadOnlyList<BreedingEmptyLog> emptyLogs,
        IReadOnlyList<KiddingRecord> kiddings)
    {
        if (goat is null || goat.IsArchived)
            return new BreedingGroupMemberOutcome { Key = "gone", Text = "left herd" };

        if (group.CrossDeferred && string.IsNullOrWhiteSpace(group.Start) && !goat.MatedDate.HasValue)
            return new BreedingGroupMemberOutcome { Key = "pending", Text = "assigned · buck not in yet" };

        if (!TryParseStart(group.Start, out var crossStart))
            crossStart = goat.MatedDate ?? Today();

        var buck = group.Buck ?? "";
        var sameCross = goat.MatedDate == crossStart &&
                        string.Equals(goat.BuckTag ?? "", buck, StringComparison.OrdinalIgnoreCase);

        var kid = kiddings
            .Where(k => k.DoeId == member.GoatId && k.KiddingDate >= crossStart)
            .OrderBy(k => k.KiddingDate)
            .FirstOrDefault();
        if (kid is not null && (sameCross || goat.MatedDate is null || goat.MatedDate <= kid.KiddingDate))
        {
            var alive = kid.LiveKids;
            return new BreedingGroupMemberOutcome
            {
                Key = "kidded",
                Text = $"kidded {kid.KiddingDate:yyyy-MM-dd} · {alive} alive" +
                       (kid.Stillborn > 0 ? $" · {kid.Stillborn} stillborn" : ""),
                KidsAlive = alive
            };
        }

        var emptied = emptyLogs.Any(l =>
            l.GoatId == member.GoatId &&
            l.MatedDate == crossStart &&
            (string.IsNullOrEmpty(l.BuckTag) ||
             string.Equals(l.BuckTag, buck, StringComparison.OrdinalIgnoreCase)));
        if (emptied)
            return new BreedingGroupMemberOutcome { Key = "empty", Text = "empty / not pregnant" };

        if (sameCross || (goat.MatedDate == crossStart && goat.BuckTag is not null))
        {
            if (PregnancyRules.IsConfirmedPregnant(goat))
            {
                return new BreedingGroupMemberOutcome
                {
                    Key = "preg",
                    Text = $"pregnant · {BreedingHelper.KidsLabel(goat.KidsCount ?? 0)} · due ~{BreedingHelper.ExpectedKidding(goat.MatedDate!.Value):yyyy-MM-dd}"
                };
            }

            if (PregnancyRules.IsAwaitingScan(goat))
            {
                var days = Today().DayNumber - goat.MatedDate!.Value.DayNumber;
                return new BreedingGroupMemberOutcome
                {
                    Key = "wait",
                    Text = $"crossed · awaiting scan ({days} days)"
                };
            }
        }

        return new BreedingGroupMemberOutcome { Key = "other", Text = "moved out of this group" };
    }

    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.Today);
}

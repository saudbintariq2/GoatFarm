using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;

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
        IReadOnlyList<KiddingRecord> kiddings,
        bool buckStillWithDoes = false)
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
                if (buckStillWithDoes && group.Status == "active")
                {
                    return new BreedingGroupMemberOutcome
                    {
                        Key = "with_buck",
                        Text = $"with buck ({days}d)"
                    };
                }

                DateOnly? scanWhen = null;
                if (TryParseStart(group.End, out var buckOut))
                    scanWhen = buckOut.AddDays(BreedingHelper.ScanAfterDays);
                var scanText = scanWhen.HasValue
                    ? $"scan on {scanWhen:dd MMM}"
                    : $"awaiting scan ({days} days)";
                return new BreedingGroupMemberOutcome
                {
                    Key = "wait",
                    Text = $"crossed · {scanText}"
                };
            }
        }

        return new BreedingGroupMemberOutcome { Key = "other", Text = "moved out of this group" };
    }

    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.Today);

    public static bool BuckStillWithDoes(BreedingGroupDto grp, DateOnly today)
    {
        if (grp.CrossDeferred || string.IsNullOrWhiteSpace(grp.Start)) return false;
        if (!TryParseStart(grp.Start, out var buckIn) || buckIn > today) return false;
        if (string.IsNullOrWhiteSpace(grp.End)) return true;
        return TryParseStart(grp.End, out var buckOut) && buckOut >= today;
    }

    public static HashSet<int> DoesWithBuckIn(
        IReadOnlyList<BreedingGroupDto> groups,
        IReadOnlyDictionary<int, Goat> goatsById,
        DateOnly today)
    {
        var set = new HashSet<int>();
        foreach (var grp in groups.Where(g => string.Equals(g.Status, "active", StringComparison.OrdinalIgnoreCase)))
        {
            if (!BuckStillWithDoes(grp, today)) continue;
            foreach (var m in grp.Members)
            {
                if (!goatsById.TryGetValue(m.GoatId, out var g)) continue;
                if (PregnancyRules.IsAwaitingScan(g))
                    set.Add(m.GoatId);
            }
        }

        return set;
    }
}

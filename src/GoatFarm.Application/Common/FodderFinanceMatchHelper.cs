using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;

namespace GoatFarm.Application.Common;

public sealed record FodderFinanceMatch(string Name, string Where, decimal PerYear);

public static class FodderFinanceMatchHelper
{
    public static FodderFinanceMatch? TryMatch(
        string label,
        IReadOnlyList<RecurringCost> recurringCosts,
        IReadOnlyList<Expense> expenses,
        int calendarYear)
    {
        var l = (label ?? "").Trim().ToLowerInvariant();
        if (l.Length == 0) return null;

        var words = l.Split([' ', '-', '/', '&', ',', '.'], StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3)
            .ToList();

        bool Hit(string? name)
        {
            var n = (name ?? "").Trim().ToLowerInvariant();
            if (n.Length < 3) return false;
            return n.Contains(l, StringComparison.Ordinal) || l.Contains(n, StringComparison.Ordinal)
                || words.Any(w => n.Contains(w, StringComparison.Ordinal));
        }

        var rc = recurringCosts.FirstOrDefault(r => Hit(r.Name));
        if (rc is not null)
        {
            var monthly = rc.Period == RecurringCostPeriod.Year ? rc.Amount / 12m : rc.Amount;
            return new FodderFinanceMatch(rc.Name, "recurring cost", monthly * 12m);
        }

        var matched = expenses
            .Where(e => e.Comment is null || !e.Comment.StartsWith("fodder_pool:", StringComparison.OrdinalIgnoreCase))
            .Where(e => e.Date.Year == calendarYear && (Hit(e.Type) || Hit(e.Comment)))
            .ToList();
        if (matched.Count == 0) return null;

        var perYear = matched.Sum(e => e.Amount);
        return new FodderFinanceMatch(matched[0].Type, "running cost", perYear);
    }
}
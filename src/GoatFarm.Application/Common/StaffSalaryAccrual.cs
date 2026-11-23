using GoatFarm.Domain.Entities;

namespace GoatFarm.Application.Common;

public static class StaffSalaryAccrual
{
    public static decimal ForRange(IEnumerable<Employee> staff, DateOnly from, DateOnly toInclusive)
    {
        if (toInclusive < from)
            (from, toInclusive) = (toInclusive, from);

        decimal sum = 0;
        var cursor = new DateOnly(from.Year, from.Month, 1);
        var endMonth = new DateOnly(toInclusive.Year, toInclusive.Month, 1);
        while (cursor <= endMonth)
        {
            sum += ForMonth(staff, cursor);
            cursor = cursor.AddMonths(1);
        }

        return sum;
    }

    public static decimal ForMonth(IEnumerable<Employee> staff, DateOnly monthStart)
    {
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        return staff
            .Where(e => e.MonthlySalary > 0 && IsEmployedInMonth(e, monthStart, monthEnd))
            .Sum(e => e.MonthlySalary);
    }

    public static decimal ForPeriodPrefix(IEnumerable<Employee> staff, string prefix, DateOnly today)
    {
        if (prefix.Length == 4 && int.TryParse(prefix, out var year))
        {
            var lastMonth = year == today.Year ? today.Month : 12;
            decimal sum = 0;
            for (var m = 1; m <= lastMonth; m++)
                sum += ForMonth(staff, new DateOnly(year, m, 1));
            return sum;
        }

        if (prefix.Length == 7 && DateOnly.TryParse(prefix + "-01", out var monthStart))
            return ForMonth(staff, monthStart);

        return 0;
    }

    private static bool IsEmployedInMonth(Employee employee, DateOnly monthStart, DateOnly monthEnd)
    {
        if (employee.StartDate is { } start && start > monthEnd)
            return false;
        if (employee.EndDate is { } end && end < monthStart)
            return false;
        return true;
    }
}
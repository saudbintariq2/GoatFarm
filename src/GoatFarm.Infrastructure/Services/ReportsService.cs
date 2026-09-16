using GoatFarm.Application.Common;
using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Reports;
using GoatFarm.Domain.Entities;
using GoatFarm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Infrastructure.Services;

public class ReportsService : IReportsService
{
    private readonly GoatFarmDbContext _context;
    private readonly IFeedService _feedService;
    private readonly IFinanceService _financeService;
    private readonly IMilkService _milkService;
    private readonly IVaccineService _vaccineService;

    public ReportsService(
        GoatFarmDbContext context,
        IFeedService feedService,
        IFinanceService financeService,
        IMilkService milkService,
        IVaccineService vaccineService)
    {
        _context = context;
        _feedService = feedService;
        _financeService = financeService;
        _milkService = milkService;
        _vaccineService = vaccineService;
    }

    public async Task<ReportsPageViewModel> GetReportsPageAsync(
        string? period,
        string? customFrom,
        string? customTo,
        CancellationToken cancellationToken = default)
    {
        period ??= "month";
        var range = BuildRange(period, customFrom, customTo);

        var expenses = await _context.Expenses.AsNoTracking().ToListAsync(cancellationToken);
        var incomes = await _context.Incomes.AsNoTracking().ToListAsync(cancellationToken);
        var feedBuys = await _context.FeedPurchases.AsNoTracking().ToListAsync(cancellationToken);
        var vaccineBuys = await _context.VaccinePurchases.AsNoTracking().ToListAsync(cancellationToken);
        var milkSales = await _context.MilkSales.AsNoTracking().ToListAsync(cancellationToken);
        var ownerInv = await _context.OwnerInvestments.AsNoTracking().ToListAsync(cancellationToken);
        var recurring = await _context.RecurringCosts.AsNoTracking().ToListAsync(cancellationToken);

        var monthlyFixed = _financeService.GetStaffSalaryMonthlyTotal()
            + _feedService.CalculateFarmMedicineMonthly()
            + _financeService.GetRecurringMonthlyTotal();

        var incMap = BuildIncomeMap(incomes, milkSales, range);
        var incTot = incMap.Values.Sum();
        var expMap = BuildExpenseMap(expenses, feedBuys, vaccineBuys, recurring, range);
        var expTot = expMap.Values.Sum();
        var profit = incTot - expTot;

        var ownList = ownerInv.Where(o => range.Test(o.Date)).OrderByDescending(o => o.Date).ToList();
        var ownTot = ownList.Sum(o => o.Amount);

        var preRevenue = incTot == 0;
        string profitLabel;
        string noteHtml;
        decimal displayProfit;
        if (preRevenue)
        {
            var gap = ownTot - expTot;
            profitLabel = gap >= 0 ? "Funding left over" : "Still to fund";
            displayProfit = Math.Abs(gap);
            noteHtml = "<b>No sales yet — that is normal for a new farm.</b> " +
                "While the farm is not earning, your own money covers the costs, so there is no profit to report. " +
                "What matters now is <b>total spent</b> and <b>money you put in</b>. Once milk and goat sales start, " +
                "the profit figure becomes meaningful and will appear here automatically.";
        }
        else
        {
            profitLabel = "Profit";
            displayProfit = Math.Abs(profit);
            noteHtml = "<b>Report:</b> actual dated entries (feed bought, vaccines, milk, and costs you added) are summed for the period. " +
                "Monthly costs — staff salaries, medicine, rent — are counted at the current rate for each month in the period.";
        }

        var feedKgT = feedBuys.Where(x => range.Test(x.Date)).Sum(x => x.Kg);
        var milkL = milkSales.Where(x => range.Test(x.Date)).Sum(x => x.Liters);

        return new ReportsPageViewModel
        {
            Period = period,
            CustomFrom = customFrom,
            CustomTo = customTo,
            RangeLabel = range.Label,
            MonthCount = range.MonthCount,
            TotalIncome = incTot,
            TotalExpense = expTot,
            TotalOwnerInvestment = ownTot,
            Profit = displayProfit,
            PreRevenue = preRevenue,
            ProfitLabel = profitLabel,
            NoteHtml = noteHtml,
            OwnerRows = ownList.Select(o => new ReportOwnerRowViewModel
            {
                Date = o.Date.ToString("yyyy-MM-dd"),
                Note = o.Note,
                Amount = o.Amount
            }).ToList(),
            ExpenseCategories = ToCategoryRows(expMap, expTot, feedKgT),
            IncomeSources = ToIncomeRows(incMap, incTot, milkL),
            MonthRows = BuildMonthRows(incomes, expenses, feedBuys, vaccineBuys, milkSales, ownerInv, range, monthlyFixed),
            DayRows = BuildDayRows(incomes, expenses, feedBuys, vaccineBuys, milkSales, ownerInv, range),
            ExpenseEntries = expenses.Where(e => range.Test(e.Date)).OrderByDescending(e => e.Date)
                .Select(e => new ReportExpenseEntryViewModel
                {
                    Date = e.Date.ToString("yyyy-MM-dd"),
                    Type = e.Type,
                    Comment = e.Comment,
                    Amount = e.Amount
                }).ToList()
        };
    }

    private Dictionary<string, decimal> BuildIncomeMap(
        IReadOnlyList<Income> incomes,
        IReadOnlyList<MilkSale> milkSales,
        ReportRange range)
    {
        var map = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var milkInc = milkSales.Where(x => range.Test(x.Date)).Sum(x => x.Amount);
        if (milkInc > 0) map["Milk sales"] = milkInc;
        foreach (var i in incomes.Where(x => range.Test(x.Date)))
            map[i.Type] = map.GetValueOrDefault(i.Type) + i.Amount;
        return map;
    }

    private Dictionary<string, decimal> BuildExpenseMap(
        IReadOnlyList<Expense> expenses,
        IReadOnlyList<FeedPurchase> feedBuys,
        IReadOnlyList<VaccinePurchase> vaccineBuys,
        IReadOnlyList<RecurringCost> recurring,
        ReportRange range)
    {
        var map = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var feedT = feedBuys.Where(x => range.Test(x.Date)).Sum(x => x.Amount);
        if (feedT > 0) map["Feed bought"] = feedT;
        var vT = vaccineBuys.Where(x => range.Test(x.Date)).Sum(x => x.Amount);
        if (vT > 0) map["Vaccines bought"] = vT;
        var salT = _financeService.GetStaffSalaryMonthlyTotal() * range.MonthCount;
        if (salT > 0) map["Staff salaries"] = salT;
        var medT = _feedService.CalculateFarmMedicineMonthly() * range.MonthCount;
        if (medT > 0) map["Medicine (estimate)"] = medT;
        foreach (var rc in recurring)
        {
            var v = (rc.Period == Domain.Enums.RecurringCostPeriod.Year ? rc.Amount / 12m : rc.Amount) * range.MonthCount;
            if (v > 0) map[rc.Name] = map.GetValueOrDefault(rc.Name) + v;
        }
        foreach (var e in expenses.Where(x => range.Test(x.Date)))
            map[e.Type] = map.GetValueOrDefault(e.Type) + e.Amount;
        return map;
    }

    private static List<ReportCategoryRowViewModel> ToCategoryRows(
        Dictionary<string, decimal> map, decimal total, decimal feedKg)
    {
        var max = map.Values.DefaultIfEmpty(0).Max();
        return map.OrderByDescending(x => x.Value).Select(kv =>
        {
            var pct = total > 0 ? (int)Math.Round(kv.Value / total * 100) : 0;
            var extra = kv.Key == "Feed bought" && feedKg > 0 ? $" · {feedKg:N0} kg" : null;
            return new ReportCategoryRowViewModel
            {
                Name = kv.Key,
                Amount = kv.Value,
                Percent = pct,
                Extra = extra,
                BarWidth = max > 0 ? Math.Max(4, (int)Math.Round(kv.Value / max * 100)) : 0
            };
        }).ToList();
    }

    private static List<ReportCategoryRowViewModel> ToIncomeRows(
        Dictionary<string, decimal> map, decimal total, decimal milkL)
    {
        var max = map.Values.DefaultIfEmpty(0).Max();
        return map.OrderByDescending(x => x.Value).Select(kv =>
        {
            var pct = total > 0 ? (int)Math.Round(kv.Value / total * 100) : 0;
            var extra = kv.Key == "Milk sales" && milkL > 0 ? $" · {milkL:N0} L" : null;
            return new ReportCategoryRowViewModel
            {
                Name = kv.Key,
                Amount = kv.Value,
                Percent = pct,
                Extra = extra,
                BarWidth = max > 0 ? Math.Max(4, (int)Math.Round(kv.Value / max * 100)) : 0
            };
        }).ToList();
    }

    private static List<ReportMonthRowViewModel> BuildMonthRows(
        IReadOnlyList<Income> incomes,
        IReadOnlyList<Expense> expenses,
        IReadOnlyList<FeedPurchase> feedBuys,
        IReadOnlyList<VaccinePurchase> vaccineBuys,
        IReadOnlyList<MilkSale> milkSales,
        IReadOnlyList<OwnerInvestment> ownerInv,
        ReportRange range,
        decimal monthlyFixed)
    {
        var months = new HashSet<string>(StringComparer.Ordinal);
        foreach (var d in AllDates(incomes, expenses, feedBuys, vaccineBuys, milkSales))
        {
            if (range.Test(d)) months.Add(d.ToString("yyyy-MM"));
        }
        if (range.MonthCount == 1)
            months.Add(MonthHelper.CurrentMonthKey());
        else
        {
            var now = DateTime.Today;
            for (var i = 0; i < range.MonthCount; i++)
            {
                var t = new DateTime(now.Year, now.Month, 1).AddMonths(-i);
                var key = t.ToString("yyyy-MM");
                if (range.Test(DateOnly.FromDateTime(t))) months.Add(key);
            }
        }

        return months.OrderByDescending(m => m).Select(mm =>
        {
            var inc = milkSales.Where(x => x.Date.ToString("yyyy-MM") == mm).Sum(x => x.Amount)
                + incomes.Where(x => x.Date.ToString("yyyy-MM") == mm).Sum(x => x.Amount);
            var outAmt = feedBuys.Where(x => x.Date.ToString("yyyy-MM") == mm).Sum(x => x.Amount)
                + vaccineBuys.Where(x => x.Date.ToString("yyyy-MM") == mm).Sum(x => x.Amount)
                + expenses.Where(x => x.Date.ToString("yyyy-MM") == mm).Sum(x => x.Amount)
                + monthlyFixed;
            return new ReportMonthRowViewModel
            {
                Month = mm,
                Income = inc,
                Expense = outAmt,
                Profit = inc - outAmt
            };
        }).ToList();
    }

    private static List<ReportDayRowViewModel> BuildDayRows(
        IReadOnlyList<Income> incomes,
        IReadOnlyList<Expense> expenses,
        IReadOnlyList<FeedPurchase> feedBuys,
        IReadOnlyList<VaccinePurchase> vaccineBuys,
        IReadOnlyList<MilkSale> milkSales,
        IReadOnlyList<OwnerInvestment> ownerInv,
        ReportRange range)
    {
        var days = new HashSet<DateOnly>();
        foreach (var d in AllDates(incomes, expenses, feedBuys, vaccineBuys, milkSales, ownerInv))
        {
            if (range.Test(d)) days.Add(d);
        }

        return days.OrderByDescending(d => d).Select(d =>
        {
            var inc = milkSales.Where(x => x.Date == d).Sum(x => x.Amount)
                + incomes.Where(x => x.Date == d).Sum(x => x.Amount);
            var outAmt = feedBuys.Where(x => x.Date == d).Sum(x => x.Amount)
                + vaccineBuys.Where(x => x.Date == d).Sum(x => x.Amount)
                + expenses.Where(x => x.Date == d).Sum(x => x.Amount);
            var own = ownerInv.Where(x => x.Date == d).Sum(x => x.Amount);
            var parts = new List<string>();
            if (inc > 0) parts.Add("income");
            if (outAmt > 0) parts.Add("costs");
            if (own > 0) parts.Add("you put in");
            return new ReportDayRowViewModel
            {
                Date = d.ToString("yyyy-MM-dd"),
                Summary = parts.Count > 0 ? string.Join(", ", parts) : "—",
                Income = inc,
                Expense = outAmt,
                OwnerInvestment = own
            };
        }).ToList();
    }

    private static IEnumerable<DateOnly> AllDates(
        IReadOnlyList<Income> incomes,
        IReadOnlyList<Expense> expenses,
        IReadOnlyList<FeedPurchase> feedBuys,
        IReadOnlyList<VaccinePurchase> vaccineBuys,
        IReadOnlyList<MilkSale> milkSales,
        IReadOnlyList<OwnerInvestment>? ownerInv = null)
    {
        foreach (var x in incomes) yield return x.Date;
        foreach (var x in expenses) yield return x.Date;
        foreach (var x in feedBuys) yield return x.Date;
        foreach (var x in vaccineBuys) yield return x.Date;
        foreach (var x in milkSales) yield return x.Date;
        if (ownerInv is not null)
            foreach (var x in ownerInv) yield return x.Date;
    }

    private static ReportRange BuildRange(string period, string? customFrom, string? customTo)
    {
        var now = DateTime.Today;
        if (period == "custom" && !string.IsNullOrEmpty(customFrom) && !string.IsNullOrEmpty(customTo)
            && DateOnly.TryParse(customFrom, out var from) && DateOnly.TryParse(customTo, out var to))
        {
            var months = Math.Max(1, (to.Year - from.Year) * 12 + (to.Month - from.Month) + 1);
            return new ReportRange(d => d >= from && d <= to, months, $"{customFrom} → {customTo}");
        }
        if (period == "year")
        {
            var y = now.Year;
            return new ReportRange(d => d.Year == y, now.Month, y.ToString());
        }
        if (period == "all")
        {
            var first = MonthHelper.CurrentMonthKey() + "-01";
            return new ReportRange(_ => true, Math.Max(1, now.Month), "all time");
        }
        var m = MonthHelper.CurrentMonthKey();
        return new ReportRange(d => d.ToString("yyyy-MM") == m, 1, m);
    }

    private sealed record ReportRange(Func<DateOnly, bool> Test, int MonthCount, string Label);
}

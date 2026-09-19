using GoatFarm.Application.Common;
using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Reports;
using GoatFarm.Domain.Constants;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Infrastructure.Services;

public class ReportsService : IReportsService
{
    private const string RemindDaysKey = "vaccine_remind_days";
    private static readonly GoatStatus[] StatusOrder =
    [
        GoatStatus.Kid, GoatStatus.Milking, GoatStatus.Pregnant,
        GoatStatus.Dry, GoatStatus.Buck, GoatStatus.Sale
    ];

    private static readonly string[] MedExpenseTypes =
    [
        "Vet / extra medicine", "Doctor / vet visit", "Vaccines"
    ];

    private readonly GoatFarmDbContext _context;
    private readonly IFeedService _feedService;
    private readonly IFinanceService _financeService;
    private readonly IMilkService _milkService;
    private readonly IVaccineService _vaccineService;
    private readonly IGoatService _goatService;
    private readonly IReminderService _reminderService;

    public ReportsService(
        GoatFarmDbContext context,
        IFeedService feedService,
        IFinanceService financeService,
        IMilkService milkService,
        IVaccineService vaccineService,
        IGoatService goatService,
        IReminderService reminderService)
    {
        _context = context;
        _feedService = feedService;
        _financeService = financeService;
        _milkService = milkService;
        _vaccineService = vaccineService;
        _goatService = goatService;
        _reminderService = reminderService;
    }

    public async Task<ReportsPageViewModel> GetReportsPageAsync(
        string? period,
        string? customFrom,
        string? customTo,
        string? groupBy = null,
        CancellationToken cancellationToken = default)
    {
        period ??= "month";
        groupBy = NormalizeGroupBy(groupBy);

        var expenses = await _context.Expenses.AsNoTracking().ToListAsync(cancellationToken);
        var incomes = await _context.Incomes.AsNoTracking().ToListAsync(cancellationToken);
        var feedBuys = await _context.FeedPurchases.AsNoTracking().ToListAsync(cancellationToken);
        var vaccineBuys = await _context.VaccinePurchases.AsNoTracking().ToListAsync(cancellationToken);
        var milkSales = await _context.MilkSales.AsNoTracking().ToListAsync(cancellationToken);
        var milkProd = await _context.MilkProductions.AsNoTracking().ToListAsync(cancellationToken);
        var ownerInv = await _context.OwnerInvestments.AsNoTracking().ToListAsync(cancellationToken);
        var recurring = await _context.RecurringCosts.AsNoTracking().ToListAsync(cancellationToken);
        var goats = await _context.Goats.AsNoTracking()
            .Where(g => !g.IsArchived)
            .ToListAsync(cancellationToken);
        var allGoats = await _context.Goats.AsNoTracking().ToListAsync(cancellationToken);
        var deaths = await _context.DeathRecords.AsNoTracking().ToListAsync(cancellationToken);
        var weightRecords = await _context.GoatWeightRecords.AsNoTracking().ToListAsync(cancellationToken);
        var emptyLogs = await _context.BreedingEmptyLogs.AsNoTracking().ToListAsync(cancellationToken);
        var vaccines = await _context.Vaccines.AsNoTracking().ToListAsync(cancellationToken);
        var vaccLog = await _context.VaccinationHistories.AsNoTracking().ToListAsync(cancellationToken);
        var feedPrices = await _context.FeedPrices.AsNoTracking().ToListAsync(cancellationToken);
        var feedUsage = await _context.FeedUsageRecords.AsNoTracking().ToListAsync(cancellationToken);
        var assets = await _context.Assets.AsNoTracking().ToListAsync(cancellationToken);
        var reminders = await _reminderService.GetRemindersAsync(cancellationToken);
        var remindDays = await GetRemindDaysAsync(cancellationToken);

        var range = BuildRange(period, customFrom, customTo, expenses, incomes, feedBuys, vaccineBuys, milkSales, ownerInv);
        var monthlyFixed = _financeService.GetStaffSalaryMonthlyTotal()
            + _feedService.CalculateFarmMedicineMonthly()
            + _financeService.GetRecurringMonthlyTotal();
        var feedMonth = _feedService.CalculateFarmFeedMonthly();

        var incMap = BuildIncomeMap(incomes, milkSales, range);
        var incTot = incMap.Values.Sum();
        var expMap = BuildExpenseMap(expenses, feedBuys, vaccineBuys, recurring, range);
        var expTot = expMap.Values.Sum();
        var profit = incTot - expTot;
        var ownList = ownerInv.Where(o => range.Test(o.Date)).OrderByDescending(o => o.Date).ToList();
        var ownTot = ownList.Sum(o => o.Amount);
        var feedKgT = feedBuys.Where(x => range.Test(x.Date)).Sum(x => x.Kg);
        var milkL = milkSales.Where(x => range.Test(x.Date)).Sum(x => x.Liters);
        var milkLp = milkProd.Where(x => range.Test(x.Date)).Sum(x => x.Liters);
        var milkInc = milkSales.Where(x => range.Test(x.Date)).Sum(x => x.Amount);
        var milkLs = milkL;
        var preRevenue = incTot == 0;

        string profitLabel;
        string noteHtml;
        decimal displayProfit;
        bool profitIsNegative;
        if (preRevenue)
        {
            var gap = ownTot - expTot;
            profitLabel = gap >= 0 ? "Funding left over" : "Still to fund";
            displayProfit = Math.Abs(gap);
            profitIsNegative = gap < 0;
            noteHtml = "<b>No sales yet — that is normal for a new farm.</b> " +
                "While the farm is not earning, your own money covers the costs, so there is no profit to report. " +
                "What matters now is <b>total spent</b> and <b>money you put in</b>. Once milk and goat sales start, " +
                "the profit figure becomes meaningful and will appear here automatically.";
        }
        else
        {
            profitLabel = "Net profit";
            displayProfit = Math.Abs(profit);
            profitIsNegative = profit < 0;
            noteHtml = "<b>Report:</b> actual dated entries (feed bought, vaccines, milk, and costs you added) are summed for the period. " +
                "Monthly costs — staff salaries, medicine, rent — are counted at the current rate for each month in the period.";
        }

        var totalGoats = goats.Count;
        var confirmed = goats.Where(g => g.MatedDate.HasValue && g.KidsCount.HasValue && g.KidsCount > 0).ToList();
        var waiting = goats.Where(g => g.MatedDate.HasValue && (!g.KidsCount.HasValue || g.KidsCount == 0)).ToList();
        var emptyCount = emptyLogs.Count;
        var scans = confirmed.Count + emptyCount;
        var concRate = scans > 0 ? (int)Math.Round(confirmed.Count / (double)scans * 100) : (int?)null;
        var kidsExpected = confirmed.Sum(g => g.KidsCount ?? 0);
        var deathsP = deaths.Where(d => range.Test(d.Date)).ToList();
        var births = goats.Where(g => g.Source == GoatSource.Born && range.Test(g.EventDate)).ToList();
        var bought = goats.Where(g => g.Source != GoatSource.Born && range.Test(g.EventDate)).ToList();
        var openHerd = Math.Max(0, totalGoats - births.Count - bought.Count + deathsP.Count);
        var mortality = openHerd > 0 ? deathsP.Count / (double)openHerd * 100 : 0;
        var vaccCost = vaccineBuys.Where(x => range.Test(x.Date)).Sum(x => x.Amount);
        var medCost = expenses.Where(e => range.Test(e.Date) && MedExpenseTypes.Contains(e.Type)).Sum(e => e.Amount);
        var days = Math.Max(1, range.MonthCount * 30);
        var costPerL = milkLp > 0 ? feedMonth * range.MonthCount / milkLp : (decimal?)null;
        var avgRate = milkLs > 0 ? milkInc / milkLs : 0;
        var feedPeriod = feedBuys.Where(x => range.Test(x.Date)).Sum(x => x.Amount);
        var milkingN = _goatService.CountByStatus(GoatStatus.Milking);
        var livestockValue = _financeService.GetLivestockValue();
        var assetsValue = _financeService.GetAssetsValue();

        var mixFeedKeys = feedPrices
            .Where(f => !string.Equals(f.FeedType, FeedTypes.Fodder, StringComparison.OrdinalIgnoreCase))
            .Select(f => f.FeedType)
            .ToList();
        var dailyUse = await BuildDailyUseMapAsync(cancellationToken);
        var stockVal = feedPrices
            .Where(f => !string.Equals(f.FeedType, FeedTypes.Fodder, StringComparison.OrdinalIgnoreCase))
            .Sum(f => f.StockKg * f.PricePerKg);
        int? minDays = null;
        foreach (var f in feedPrices.Where(f => !string.Equals(f.FeedType, FeedTypes.Fodder, StringComparison.OrdinalIgnoreCase)))
        {
            var du = dailyUse.GetValueOrDefault(f.FeedType);
            if (du <= 0) continue;
            var d = (int)Math.Floor(f.StockKg / du);
            if (minDays is null || d < minDays) minDays = d;
        }

        var vaccDueTotal = vaccines.Sum(v => GetDueGoats(v, goats, vaccLog).Count);
        var trend = BuildTrendRows(incomes, expenses, feedBuys, vaccineBuys, milkSales, ownerInv, range, monthlyFixed, groupBy, preRevenue);

        var finance = new ReportFinanceSectionViewModel
        {
            TotalExpense = expTot,
            TotalOwnerInvestment = ownTot,
            TotalIncome = incTot,
            Profit = displayProfit,
            ProfitLabel = profitLabel,
            ProfitIsNegative = profitIsNegative && !preRevenue,
            OwnerRows = ownList.Select(o => new ReportOwnerRowViewModel
            {
                Date = o.Date.ToString("yyyy-MM-dd"),
                Note = o.Note,
                Amount = o.Amount
            }).ToList(),
            ExpenseCategories = ToCategoryRows(expMap, expTot, feedKgT),
            IncomeSources = ToIncomeRows(incMap, incTot, milkL),
            ExpenseEntries = expenses.Where(e => range.Test(e.Date)).OrderByDescending(e => e.Date)
                .Select(e => new ReportExpenseEntryViewModel
                {
                    Date = e.Date.ToString("yyyy-MM-dd"),
                    Type = e.Type,
                    Comment = e.Comment,
                    Amount = e.Amount
                }).ToList(),
            DayRows = BuildDayRows(incomes, expenses, feedBuys, vaccineBuys, milkSales, milkProd, ownerInv, range),
            DayNote = BuildDayNote(range, incomes, expenses, feedBuys, vaccineBuys, milkSales, milkProd, ownerInv)
        };

        var dashboard = BuildDashboard(totalGoats, confirmed, deathsP, minDays, feedMonth, range, incTot, expTot,
            vaccDueTotal, mortality, openHerd, concRate, scans, confirmed.Count, kidsExpected, totalGoats, feedMonth,
            costPerL, vaccCost, medCost, trend, preRevenue);

        var herd = BuildHerdSection(goats, totalGoats, openHerd, births, bought, deathsP, livestockValue, range);
        var breeding = BuildBreedingSection(confirmed, waiting, emptyLogs, goats, emptyCount, concRate, scans, kidsExpected);
        var growth = BuildGrowthSectionWithAdg(goats, weightRecords, totalGoats, feedMonth);

        var feedSection = BuildFeedSection(feedPrices, feedBuys, feedUsage, dailyUse, feedMonth, feedPeriod, expTot,
            costPerL, stockVal, range, mixFeedKeys);
        var health = BuildHealthSection(vaccines, vaccLog, goats, vaccDueTotal, remindDays, range, vaccCost, medCost, totalGoats, deathsP);
        var profitability = BuildProfitabilitySection(totalGoats, expTot, feedMonth, vaccCost, medCost, avgRate, costPerL,
            incTot, incomes, range, milkLp, milkLs, milkInc, milkProd, milkSales, days, milkingN);
        var inventory = BuildInventorySection(feedPrices, dailyUse, stockVal, livestockValue, assetsValue, assets);
        var alerts = BuildAlerts(feedPrices, dailyUse, vaccines, goats, vaccLog, confirmed, remindDays, reminders,
            weightRecords, growth.UnderperformingTags);
        dashboard.Alerts = alerts.Items.Take(10).ToList();
        var comparison = BuildComparison(incomes, expenses, feedBuys, vaccineBuys, milkSales, milkProd, ownerInv, deaths, allGoats);

        return new ReportsPageViewModel
        {
            Period = period,
            GroupBy = groupBy,
            CustomFrom = customFrom,
            CustomTo = customTo,
            RangeLabel = range.Label,
            MonthCount = range.MonthCount,
            NoteHtml = noteHtml,
            PreRevenue = preRevenue,
            TrendHeadLabel = groupBy switch { "day" => "Day", "week" => "Week of", _ => "Month" },
            TrendSubLabel = "by " + groupBy,
            TrendLastColumnLabel = preRevenue ? "You put in" : "Net",
            Dashboard = dashboard,
            Herd = herd,
            Breeding = breeding,
            Growth = growth,
            Feed = feedSection,
            Health = health,
            Finance = finance,
            Profitability = profitability,
            Inventory = inventory,
            Alerts = alerts,
            Comparison = comparison
        };
    }

    private ReportGrowthSectionViewModel BuildGrowthSectionWithAdg(
        IReadOnlyList<Goat> goats,
        IReadOnlyList<GoatWeightRecord> weightRecords,
        int totalGoats,
        decimal feedMonth)
    {
        var weightsByGoat = weightRecords.GroupBy(w => w.GoatId).ToDictionary(g => g.Key, g => g.OrderBy(w => w.Date).ToList());
        var rows = new List<(Goat Goat, GoatWeightRecord First, GoatWeightRecord Last, decimal? Adg, int N)>();
        foreach (var goat in goats)
        {
            if (!weightsByGoat.TryGetValue(goat.Id, out var ws) || ws.Count == 0) continue;
            var first = ws[0];
            var last = ws[^1];
            decimal? adg = null;
            if (ws.Count > 1)
            {
                var dd = Math.Max(1, (last.Date.ToDateTime(TimeOnly.MinValue) - first.Date.ToDateTime(TimeOnly.MinValue)).Days);
                adg = (last.Kg - first.Kg) / dd;
            }
            rows.Add((goat, first, last, adg, ws.Count));
        }

        var withAdg = rows.Where(x => x.Adg.HasValue).ToList();
        var avgAdg = withAdg.Count > 0 ? withAdg.Average(x => x.Adg!.Value) : (decimal?)null;
        var avgW = rows.Count > 0 ? rows.Average(x => (double)x.Last.Kg) : (double?)null;
        var underTags = avgAdg.HasValue
            ? withAdg.Where(x => x.Adg < avgAdg * 0.7m).Select(x => x.Goat.Tag).ToHashSet()
            : [];

        var weightRows = rows.OrderByDescending(x => x.Adg ?? 0).Select(x => new ReportWeightRowViewModel
        {
            Tag = x.Goat.Tag,
            AgeLabel = _goatService.GetAgeLabel(_goatService.GetAgeInDays(x.Goat.EventDate)),
            FirstKg = x.N > 1 ? x.First.Kg : null,
            LatestKg = x.Last.Kg,
            DailyGainDisplay = x.Adg.HasValue ? $"{x.Adg.Value * 1000:F0} g" : "—",
            Underperforming = underTags.Contains(x.Goat.Tag),
            Readings = x.N
        }).ToList();

        return new ReportGrowthSectionViewModel
        {
            Kpis =
            [
                Kpi("Animals weighed", rows.Count.ToString(), $"of {totalGoats} in herd"),
                Kpi("Average weight", avgW.HasValue ? $"{avgW:F1} kg" : "—", "latest reading"),
                Kpi("Avg daily gain", avgAdg.HasValue ? $"{avgAdg * 1000:F0} g/day" : "—", "across weighed animals"),
                Kpi("Cost per kg gain", avgAdg.HasValue && totalGoats > 0
                    ? DisplayHelper.FormatRs((feedMonth / 30 / totalGoats) / avgAdg.Value) : "—", "feed cost ÷ daily gain"),
                Kpi("Underperforming", withAdg.Count(x => avgAdg.HasValue && x.Adg < avgAdg * 0.7m).ToString(),
                    "below 70% of average gain", withAdg.Any(x => avgAdg.HasValue && x.Adg < avgAdg * 0.7m) ? "#8a261c" : null),
                Kpi("Readings taken", rows.Sum(x => x.N).ToString(), "total weigh-ins")
            ],
            NoteHtml = rows.Count > 0
                ? "<span class=\"breed\">Daily gain needs at least two weighings per animal. Weigh monthly for reliable figures.</span>"
                : "<span style=\"color:var(--amber)\">No weights recorded yet. Use <b>Record weight or death</b> on the Herd tab — weigh kids monthly to track growth.</span>",
            Weights = weightRows,
            UnderperformingTags = underTags.ToList()
        };
    }

    private ReportDashboardSectionViewModel BuildDashboard(
        int totalGoats, List<Goat> confirmed, List<DeathRecord> deathsP, int? minDays,
        decimal feedMonth, ReportRange range, decimal incTot, decimal expTot, int vaccDueTotal,
        double mortality, int openHerd, int? concRate, int scans, int confirmedCount, int kidsExpected,
        int herdSize, decimal feedMonthly, decimal? costPerL, decimal vaccCost, decimal medCost,
        (List<ReportTrendRowViewModel> Rows, decimal TIn, decimal TOut, decimal TPut, decimal TNet) trend,
        bool preRevenue)
    {
        var net = incTot - expTot;
        var kpis = new List<ReportKpiViewModel>
        {
            KpiDot("Total herd", totalGoats.ToString(), "var(--green)"),
            KpiDot("Kids", _goatService.CountByStatus(GoatStatus.Kid).ToString(), "var(--amber)"),
            KpiDot("Pregnant", confirmed.Count.ToString(), "var(--pink)"),
            KpiDot("Deaths", deathsP.Count.ToString(), "var(--red)"),
            KpiDot("Feed stock", minDays.HasValue ? $"{minDays} d" : "—", "var(--blue)"),
            KpiDot("Feed cost", DisplayHelper.FormatRs(feedMonth * range.MonthCount), "var(--amber)"),
            KpiDot("Revenue", DisplayHelper.FormatRs(incTot), "var(--green)"),
            KpiDot("Expenses", DisplayHelper.FormatRs(expTot), "var(--red)"),
            KpiDot("Net profit", (net < 0 ? "– " : "") + DisplayHelper.FormatRs(Math.Abs(net)), net < 0 ? "var(--red)" : "var(--green)"),
            KpiDot("Vaccines due", vaccDueTotal.ToString(), vaccDueTotal > 0 ? "var(--amber)" : "var(--green)")
        };

        var ratios = new List<ReportKpiViewModel>
        {
            Kpi("Mortality %", mortality.ToString("F1") + "%", $"{deathsP.Count} of {openHerd}", mortality > 5 ? "#8a261c" : "var(--green-dark)"),
            Kpi("Pregnancy %", concRate.HasValue ? concRate + "%" : "—", scans > 0 ? $"{confirmedCount} of {scans} scans" : "no scans",
                concRate is null ? null : concRate >= 70 ? "var(--green-dark)" : "#8a261c"),
            Kpi("Kids per doe", confirmedCount > 0 ? (kidsExpected / (double)confirmedCount).ToString("F2") : "—", "from scans"),
            Kpi("Feed cost/goat/day", herdSize > 0 ? DisplayHelper.FormatRs(feedMonthly / 30 / herdSize) : "—", "all groups"),
            Kpi("Feed cost/litre", costPerL.HasValue ? "Rs " + costPerL.Value.ToString("F1") : "—", "milk collected",
                costPerL is > 60 ? "#8a261c" : null),
            Kpi("Health cost/goat", herdSize > 0 ? DisplayHelper.FormatRs((vaccCost + medCost) / herdSize) : "—", "vaccines + medicine")
        };

        return new ReportDashboardSectionViewModel
        {
            Kpis = kpis,
            Ratios = ratios,
            Alerts = [],
            TrendRows = trend.Rows,
            TrendTotalIncome = trend.TIn,
            TrendTotalExpense = trend.TOut,
            TrendTotalOwner = trend.TPut,
            TrendTotalNet = trend.TNet
        };
    }

    private static ReportHerdSectionViewModel BuildHerdSection(
        IReadOnlyList<Goat> goats, int totalGoats, int openHerd,
        List<Goat> births, List<Goat> bought, List<DeathRecord> deathsP,
        decimal livestockValue, ReportRange range)
    {
        var movement = new List<ReportLabelCountRowViewModel>
        {
            new() { Label = "Opening herd", Count = openHerd },
            new() { Label = "Purchases", Count = bought.Count },
            new() { Label = "Births", Count = births.Count },
            new() { Label = "Deaths", Count = deathsP.Count },
            new() { Label = "Current herd", Count = totalGoats, IsTotal = true }
        };

        var composition = StatusOrder.Select(st =>
        {
            var n = goats.Count(g => g.Status == st);
            var val = goats.Where(g => g.Status == st).Sum(g => g.PurchasePrice);
            return new ReportHerdGroupRowViewModel
            {
                StatusDisplay = DisplayHelper.GetStatusDisplay(st).Text,
                StatusCssClass = DisplayHelper.GetStatusDisplay(st).Css,
                Count = n,
                SharePercent = totalGoats > 0 ? (int)Math.Round(n / (double)totalGoats * 100) : 0,
                Value = val
            };
        }).ToList();
        composition.Add(new ReportHerdGroupRowViewModel
        {
            StatusDisplay = "TOTAL",
            Count = totalGoats,
            Value = livestockValue,
            IsTotal = true
        });

        var bands = new (string Label, int Lo, int Hi)[]
        {
            ("0–70 days", 0, 70), ("70–90 days", 70, 90), ("3–6 months", 90, 180),
            ("6–12 months", 180, 365), ("Over 1 year", 365, 99999)
        };
        var ageSexBreed = bands.Select(b => Kpi(b.Label,
            goats.Count(g =>
            {
                var d = (DateOnly.FromDateTime(DateTime.Today).ToDateTime(TimeOnly.MinValue) -
                         g.EventDate.ToDateTime(TimeOnly.MinValue)).Days;
                return d >= b.Lo && d < b.Hi;
            }).ToString())).ToList();
        foreach (var grp in goats.GroupBy(g => g.Breed).OrderByDescending(g => g.Count()))
            ageSexBreed.Add(Kpi(grp.Key, grp.Count().ToString(), "breed"));
        ageSexBreed.Add(Kpi("Does (female)", goats.Count(g => g.Gender == GoatGender.Female).ToString()));
        ageSexBreed.Add(Kpi("Bucks (male)", goats.Count(g => g.Gender == GoatGender.Male).ToString()));

        var changes = bought.Select(g => new ReportHerdChangeRowViewModel
            {
                Date = g.EventDate.ToString("yyyy-MM-dd"),
                Tag = g.Tag,
                Breed = g.Breed,
                EventHtml = "<span class=\"chip chip-cap\">Bought</span>",
                ValueDisplay = DisplayHelper.FormatRs(g.PurchasePrice)
            })
            .Concat(births.Select(g => new ReportHerdChangeRowViewModel
            {
                Date = g.EventDate.ToString("yyyy-MM-dd"),
                Tag = g.Tag,
                Breed = g.Breed,
                EventHtml = "<span class=\"chip chip-kid\">Born on farm</span>",
                ValueDisplay = "—"
            }))
            .Concat(deathsP.Select(d => new ReportHerdChangeRowViewModel
            {
                Date = d.Date.ToString("yyyy-MM-dd"),
                Tag = d.Tag,
                Breed = d.Breed ?? "",
                EventHtml = "<span class=\"chip chip-exp\">Died</span>",
                ValueDisplay = d.ValueLost > 0 ? "– " + DisplayHelper.FormatRs(d.ValueLost) : "—"
            }))
            .OrderByDescending(c => c.Date)
            .ToList();

        return new ReportHerdSectionViewModel
        {
            Movement = movement,
            Composition = composition,
            TotalHerdValue = livestockValue,
            AgeSexBreed = ageSexBreed,
            Changes = changes
        };
    }

    private static ReportBreedingSectionViewModel BuildBreedingSection(
        List<Goat> confirmed, List<Goat> waiting, List<BreedingEmptyLog> emptyLogs,
        IReadOnlyList<Goat> goats, int emptyCount, int? concRate, int scans, int kidsExpected)
    {
        var kpis = new List<ReportKpiViewModel>
        {
            Kpi("Does bred", (confirmed.Count + waiting.Count).ToString(), "crossed"),
            Kpi("Awaiting scan", waiting.Count.ToString(), "unconfirmed"),
            Kpi("Confirmed pregnant", confirmed.Count.ToString(), $"{kidsExpected} kids expected", "var(--pink)"),
            Kpi("Empty scans", emptyCount.ToString(), "did not take", emptyCount > 0 ? "#8a261c" : null),
            Kpi("Pregnancy %", concRate.HasValue ? concRate + "%" : "—", scans > 0 ? $"{confirmed.Count} of {scans}" : "—",
                concRate is null ? null : concRate >= 70 ? "var(--green-dark)" : "#8a261c"),
            Kpi("Kids per doe", confirmed.Count > 0 ? (kidsExpected / (double)confirmed.Count).ToString("F2") : "—", "average")
        };

        string noteHtml = concRate is null
            ? "<span class=\"breed\">Record ultrasound results to build a pregnancy rate.</span>"
            : concRate >= 70
                ? "<span style=\"color:var(--green-dark)\">70% or better is healthy.</span>"
                : "<span style=\"color:#8a261c\">Below 70% — check buck fertility, body condition at crossing, and cross timing.</span>";

        var lit = new Dictionary<int, int> { [1] = 0, [2] = 0, [3] = 0, [4] = 0 };
        foreach (var g in confirmed)
        {
            var k = Math.Min(4, g.KidsCount ?? 0);
            if (k > 0) lit[k]++;
        }
        var litNames = new Dictionary<int, string> { [1] = "Single", [2] = "Twins", [3] = "Triplets", [4] = "Four or more" };
        var litter = lit.Select(kv => new ReportLitterRowViewModel
        {
            Label = litNames[kv.Key],
            Does = kv.Value,
            BarPercent = confirmed.Count > 0 ? Math.Max(2, (int)Math.Round(kv.Value / (double)confirmed.Count * 100)) : 0,
            Kids = kv.Value * kv.Key
        }).ToList();

        var kidding = confirmed.OrderBy(g => BreedingHelper.ExpectedKidding(g.MatedDate!.Value)).Select(g =>
        {
            var kd = BreedingHelper.ExpectedKidding(g.MatedDate!.Value);
            var d = BreedingHelper.DaysUntil(kd);
            return new ReportKiddingRowViewModel
            {
                Tag = g.Tag,
                MatedDate = g.MatedDate!.Value.ToString("yyyy-MM-dd"),
                KidsDisplay = BreedingHelper.KidsLabel(g.KidsCount ?? 0),
                DueDate = kd.ToString("yyyy-MM-dd"),
                DueInText = d < 0 ? $"overdue {-d}d" : $"in {d} d",
                DueInColor = d < 0 ? "#8a261c" : d <= 14 ? "var(--amber)" : "var(--green-dark)"
            };
        }).ToList();

        var emptyByGoat = emptyLogs.GroupBy(e => e.GoatId).ToDictionary(g => g.Key, g => g.ToList());
        var emptyRows = emptyLogs.OrderByDescending(e => e.ScanDate).Select(e =>
        {
            var goat = goats.FirstOrDefault(g => g.Id == e.GoatId);
            var times = emptyByGoat.GetValueOrDefault(e.GoatId)?.Count ?? 1;
            return new ReportEmptyScanRowViewModel
            {
                Tag = goat?.Tag ?? "—",
                MatedDate = e.MatedDate?.ToString("yyyy-MM-dd") ?? "—",
                BuckTag = e.BuckTag ?? "—",
                ScanDate = e.ScanDate.ToString("yyyy-MM-dd"),
                Times = times,
                Highlight = times > 1
            };
        }).ToList();

        return new ReportBreedingSectionViewModel
        {
            Kpis = kpis,
            NoteHtml = noteHtml,
            Litter = litter,
            LitterTotalDoes = confirmed.Count,
            LitterTotalKids = kidsExpected,
            KiddingCalendar = kidding,
            EmptyScans = emptyRows
        };
    }

    private ReportFeedSectionViewModel BuildFeedSection(
        IReadOnlyList<FeedPrice> feedPrices,
        IReadOnlyList<FeedPurchase> feedBuys,
        IReadOnlyList<FeedUsageRecord> feedUsage,
        IReadOnlyDictionary<string, decimal> dailyUse,
        decimal feedMonth, decimal feedPeriod, decimal expTot, decimal? costPerL,
        decimal stockVal, ReportRange range, IReadOnlyList<string> mixFeedKeys)
    {
        var feedShare = expTot > 0 ? (int)Math.Round(feedPeriod / expTot * 100) : 0;
        var kpis = new List<ReportKpiViewModel>
        {
            Kpi("Feed cost / day", DisplayHelper.FormatRs(feedMonth / 30), "from your plan"),
            Kpi("Feed cost / month", DisplayHelper.FormatRs(feedMonth), "mix + dry fodder + land"),
            Kpi("Cost per litre", costPerL.HasValue ? "Rs " + costPerL.Value.ToString("F1") : "—", "feed ÷ milk collected",
                costPerL is > 60 ? "#8a261c" : "var(--green-dark)"),
            Kpi("Feed bought", DisplayHelper.FormatRs(feedPeriod), "actual spend"),
            Kpi("Feed share of costs", feedShare + "%", "of running costs", feedShare > 75 ? "#8a261c" : null),
            Kpi("Stock value", DisplayHelper.FormatRs(stockVal), "in the store")
        };

        var buyAgg = feedBuys.GroupBy(b => b.FeedType).ToDictionary(g => g.Key, g => new { Kg = g.Sum(x => x.Kg), Amt = g.Sum(x => x.Amount) });
        var stockRows = feedPrices
            .Where(f => !string.Equals(f.FeedType, FeedTypes.Fodder, StringComparison.OrdinalIgnoreCase))
            .Select(f =>
            {
                var du = dailyUse.GetValueOrDefault(f.FeedType);
                var days = du > 0 ? (int?)Math.Floor(f.StockKg / du) : null;
                var pur = feedBuys.Where(b => b.FeedType == f.FeedType).Sum(b => b.Kg);
                var used = feedUsage.Where(u => u.FeedType == f.FeedType).Sum(u => u.Kg);
                buyAgg.TryGetValue(f.FeedType, out var ag);
                var avg = ag is { Kg: > 0 } ? ag.Amt / ag.Kg : f.PricePerKg;
                var pct = f.StockKgFull > 0 ? f.StockKg / f.StockKgFull : (decimal?)null;
                return new ReportFeedStockRowViewModel
                {
                    Name = f.DisplayName,
                    IsLow = pct is <= 0.2m,
                    PurchasedKg = pur,
                    ConsumedKg = used,
                    StockKg = f.StockKg,
                    DailyUseKg = du,
                    DaysLeft = days,
                    AvgCostPerKg = avg,
                    StockValue = f.StockKg * f.PricePerKg
                };
            }).ToList();

        var plans = _context.FeedPlans.AsNoTracking().ToList();
        var mixCost = _feedService.GetMixCostPerKg();
        var grpRows = new List<ReportFeedGroupRowViewModel>();
        decimal tFeedMon = 0;
        foreach (var st in StatusOrder)
        {
            var n = _goatService.CountByStatus(st);
            var plan = plans.FirstOrDefault(p => p.StatusKey == st);
            if (plan is null) continue;
            var d = MixRecipeHelper.PlanDailyFeedCost(plan.MixKgPerDay, mixCost);
            var m = d * 30 * n;
            tFeedMon += m;
            var (text, css) = DisplayHelper.GetStatusDisplay(st);
            grpRows.Add(new ReportFeedGroupRowViewModel
            {
                StatusDisplay = text,
                StatusCssClass = css,
                GoatCount = n,
                CostPerGoatDay = n > 0 ? d : 0,
                CostPerMonth = m,
                SharePercent = 0
            });
        }
        foreach (var row in grpRows)
            row.SharePercent = tFeedMon > 0 ? (int)Math.Round(row.CostPerMonth / tFeedMon * 100) : 0;
        grpRows.Add(new ReportFeedGroupRowViewModel
        {
            StatusDisplay = "TOTAL",
            GoatCount = StatusOrder.Sum(st => _goatService.CountByStatus(st)),
            CostPerMonth = tFeedMon,
            IsTotal = true
        });

        var fb = feedBuys.Where(x => range.Test(x.Date))
            .GroupBy(b => b.FeedType)
            .Select(g => new ReportFeedPurchaseRowViewModel
            {
                FeedName = feedPrices.FirstOrDefault(f => f.FeedType == g.Key)?.DisplayName ?? g.Key,
                Kg = g.Sum(x => x.Kg),
                AvgRate = g.Sum(x => x.Kg) > 0 ? g.Sum(x => x.Amount) / g.Sum(x => x.Kg) : 0,
                Amount = g.Sum(x => x.Amount)
            })
            .OrderByDescending(x => x.Amount)
            .ToList();

        return new ReportFeedSectionViewModel
        {
            Kpis = kpis,
            Stock = stockRows,
            CostByGroup = grpRows,
            Purchases = fb,
            PurchasesTotalKg = fb.Sum(x => x.Kg),
            PurchasesTotalAmount = fb.Sum(x => x.Amount)
        };
    }

    private ReportHealthSectionViewModel BuildHealthSection(
        IReadOnlyList<Vaccine> vaccines, IReadOnlyList<VaccinationHistory> log,
        IReadOnlyList<Goat> goats, int vaccDueTotal, int remindDays, ReportRange range,
        decimal vaccCost, decimal medCost, int totalGoats, List<DeathRecord> deathsP)
    {
        var vaccUp = vaccines.Sum(v => GetUpcomingGoats(v, goats, log, remindDays).Count);
        var vaccDone = log.Count(l => range.Test(l.VaccinationDate));

        var kpis = new List<ReportKpiViewModel>
        {
            Kpi("Vaccines due now", vaccDueTotal.ToString(), "across the herd", vaccDueTotal > 0 ? "#8a261c" : "var(--green-dark)"),
            Kpi("Coming up", vaccUp.ToString(), $"within {remindDays} days", "var(--amber)"),
            Kpi("Doses given", vaccDone.ToString(), "in this period", "var(--green-dark)"),
            Kpi("Vaccine cost", DisplayHelper.FormatRs(vaccCost), "purchases this period"),
            Kpi("Medicine & vet", DisplayHelper.FormatRs(medCost), medCost > 0 ? "actually spent" : "nothing spent this period"),
            Kpi("Health cost/goat", totalGoats > 0 ? DisplayHelper.FormatRs((vaccCost + medCost) / totalGoats) : "—", "actual spend ÷ herd")
        };

        var vaccRows = vaccines.Select(v => new ReportVaccineStatusRowViewModel
        {
            Name = v.Name,
            RuleLabel = v.RuleType == VaccineRuleType.Age ? $"At {v.Days} days old" : $"Every {v.Months} months",
            ScopeLabel = DisplayHelper.GetScopeLabel(v.Scope),
            Done = log.Count(l => l.VaccineId == v.Id && range.Test(l.VaccinationDate)),
            DueNow = GetDueGoats(v, goats, log).Count,
            ComingUp = GetUpcomingGoats(v, goats, log, remindDays).Count
        }).ToList();

        var deathRows = deathsP.OrderByDescending(d => d.Date).Select(d => new ReportDeathRowViewModel
        {
            Date = d.Date.ToString("yyyy-MM-dd"),
            Tag = d.Tag,
            AgeLabel = d.AgeDays.HasValue ? _goatService.GetAgeLabel(d.AgeDays.Value) : "—",
            Reason = string.IsNullOrWhiteSpace(d.Reason) ? "not stated" : d.Reason,
            ValueLost = d.ValueLost > 0 ? d.ValueLost : null
        }).ToList();

        return new ReportHealthSectionViewModel
        {
            Kpis = kpis,
            Vaccination = vaccRows,
            Deaths = deathRows,
            DeathsTotalValue = deathsP.Sum(d => d.ValueLost)
        };
    }

    private static ReportProfitabilitySectionViewModel BuildProfitabilitySection(
        int totalGoats, decimal expTot, decimal feedMonth, decimal vaccCost, decimal medCost,
        decimal avgRate, decimal? costPerL, decimal incTot, IReadOnlyList<Income> incomes,
        ReportRange range, decimal milkLp, decimal milkLs, decimal milkInc,
        IReadOnlyList<MilkProduction> milkProd, IReadOnlyList<MilkSale> milkSales, int days, int milkingN)
    {
        var goatSales = incomes.Where(i => range.Test(i.Date) && (i.Type.Contains("goat", StringComparison.OrdinalIgnoreCase) ||
            i.Type.Contains("meat", StringComparison.OrdinalIgnoreCase))).ToList();
        var goatSaleTot = goatSales.Sum(i => i.Amount);
        var costPerGoat = totalGoats > 0 ? expTot / totalGoats : (decimal?)null;
        var margin = avgRate > 0 && costPerL.HasValue ? avgRate - costPerL.Value : (decimal?)null;

        var unit = new List<ReportKpiViewModel>
        {
            Kpi("Cost per goat", costPerGoat.HasValue ? DisplayHelper.FormatRs(costPerGoat.Value) : "—", "all costs ÷ herd, this period"),
            Kpi("Feed cost/goat/day", totalGoats > 0 ? DisplayHelper.FormatRs(feedMonth / 30 / totalGoats) : "—"),
            Kpi("Health cost/goat", totalGoats > 0 ? DisplayHelper.FormatRs((vaccCost + medCost) / totalGoats) : "—"),
            Kpi("Milk margin/litre", margin.HasValue ? "Rs " + margin.Value.ToString("F1") : "—", "sale price − feed cost",
                margin is > 0 ? "var(--green-dark)" : margin is < 0 ? "#8a261c" : null),
            Kpi("Goat sales", DisplayHelper.FormatRs(goatSaleTot), $"{goatSales.Count} sale entries"),
            Kpi("Break-even", incTot > 0 ? Math.Round(expTot / incTot * 100) + "%" : "—", "costs as % of revenue",
                incTot > 0 && expTot > incTot ? "#8a261c" : "var(--green-dark)")
        };

        string noteHtml = incTot == 0
            ? "<span style=\"color:var(--amber)\">No sales yet — cost per goat shows what the farm is spending to grow. Profit figures start once selling begins.</span>"
            : expTot > incTot
                ? "<span style=\"color:#8a261c\">Costs are above revenue for this period.</span>"
                : "<span style=\"color:var(--green-dark)\">Revenue is covering costs.</span>";

        var milkKpis = new List<ReportKpiViewModel>
        {
            Kpi("Collected", milkLp.ToString("N0") + " L", (milkLp / days).ToString("F1") + " L per day"),
            Kpi("Sold", milkLs.ToString("N0") + " L", milkLp > 0 ? Math.Round(milkLs / milkLp * 100) + "% of collection" : "—"),
            Kpi("Kept / processed", Math.Max(0, milkLp - milkLs).ToString("N0") + " L", "ghee, paneer, home"),
            Kpi("Milk income", DisplayHelper.FormatRs(milkInc), avgRate > 0 ? "Rs " + avgRate.ToString("F0") + "/litre average" : "—", "var(--green-dark)"),
            Kpi("Per milking doe", milkingN > 0 ? (milkLp / days / milkingN).ToString("F2") + " L" : "—", "per day"),
            Kpi("Cost per litre", costPerL.HasValue ? "Rs " + costPerL.Value.ToString("F1") : "—", "feed cost")
        };

        var mm = new Dictionary<string, (decimal P, decimal S, decimal Inc)>();
        foreach (var x in milkProd.Where(x => range.Test(x.Date)))
        {
            var k = x.Date.ToString("yyyy-MM");
            if (!mm.TryGetValue(k, out var v)) v = (0, 0, 0);
            v.P += x.Liters;
            mm[k] = v;
        }
        foreach (var x in milkSales.Where(x => range.Test(x.Date)))
        {
            var k = x.Date.ToString("yyyy-MM");
            if (!mm.TryGetValue(k, out var v)) v = (0, 0, 0);
            v.S += x.Liters;
            v.Inc += x.Amount;
            mm[k] = v;
        }

        var milkRows = mm.OrderByDescending(kv => kv.Key).Select(kv => new ReportMilkMonthRowViewModel
        {
            Month = kv.Key,
            CollectedLiters = kv.Value.P,
            SoldLiters = kv.Value.S,
            AvgRate = kv.Value.S > 0 ? kv.Value.Inc / kv.Value.S : null,
            Income = kv.Value.Inc
        }).ToList();

        return new ReportProfitabilitySectionViewModel
        {
            UnitEconomics = unit,
            NoteHtml = noteHtml,
            MilkEconomics = milkKpis,
            MilkByMonth = milkRows
        };
    }

    private static ReportInventorySectionViewModel BuildInventorySection(
        IReadOnlyList<FeedPrice> feedPrices,
        IReadOnlyDictionary<string, decimal> dailyUse,
        decimal stockVal, decimal livestockValue, decimal assetsValue,
        IReadOnlyList<Asset> assets)
    {
        int LowCount(IReadOnlyList<FeedPrice> fp) => fp.Count(f =>
            !string.Equals(f.FeedType, FeedTypes.Fodder, StringComparison.OrdinalIgnoreCase) &&
            f.StockKgFull > 0 && f.StockKg / f.StockKgFull <= 0.2m);
        int OutCount(IReadOnlyList<FeedPrice> fp) => fp.Count(f =>
            !string.Equals(f.FeedType, FeedTypes.Fodder, StringComparison.OrdinalIgnoreCase) && f.StockKg <= 0);

        var kpis = new List<ReportKpiViewModel>
        {
            Kpi("Feed stock value", DisplayHelper.FormatRs(stockVal), "in the store"),
            Kpi("Livestock value", DisplayHelper.FormatRs(livestockValue), "bought goats at cost"),
            Kpi("Assets & equipment", DisplayHelper.FormatRs(assetsValue), assets.Count + " items"),
            Kpi("Total capital", DisplayHelper.FormatRs(livestockValue + assetsValue), "livestock + assets", "var(--blue)"),
            Kpi("Low stock items", LowCount(feedPrices).ToString(), "at or under 20%", LowCount(feedPrices) > 0 ? "#8a261c" : "var(--green-dark)"),
            Kpi("Out of stock", OutCount(feedPrices).ToString(), "nothing left", "#8a261c")
        };

        var feedRows = feedPrices
            .Where(f => !string.Equals(f.FeedType, FeedTypes.Fodder, StringComparison.OrdinalIgnoreCase))
            .Select(f =>
            {
                var pct = f.StockKgFull > 0 ? f.StockKg / f.StockKgFull : (decimal?)null;
                var status = f.StockKg <= 0
                    ? "<span class=\"chip chip-exp\">OUT</span>"
                    : pct is <= 0.2m
                        ? "<span class=\"chip chip-exp\">LOW</span>"
                        : "<span class=\"chip chip-inc\">OK</span>";
                return new ReportInvFeedRowViewModel
                {
                    Name = f.DisplayName,
                    QuantityKg = f.StockKg,
                    RatePerKg = f.PricePerKg,
                    Value = f.StockKg * f.PricePerKg,
                    StatusHtml = status
                };
            }).ToList();

        var assetRows = assets.Select(a => new ReportInvAssetRowViewModel
        {
            Name = a.Name,
            Note = a.Comment,
            Type = a.Type,
            Value = a.Cost
        }).ToList();

        return new ReportInventorySectionViewModel
        {
            Kpis = kpis,
            FeedStock = feedRows,
            FeedStockTotalValue = stockVal,
            Assets = assetRows,
            AssetsTotalValue = assetsValue
        };
    }

    private ReportAlertsSectionViewModel BuildAlerts(
        IReadOnlyList<FeedPrice> feedPrices,
        IReadOnlyDictionary<string, decimal> dailyUse,
        IReadOnlyList<Vaccine> vaccines,
        IReadOnlyList<Goat> goats,
        IReadOnlyList<VaccinationHistory> log,
        List<Goat> confirmed,
        int remindDays,
        IReadOnlyList<Application.ViewModels.Reminders.ReminderViewModel> reminders,
        IReadOnlyList<GoatWeightRecord> weightRecords,
        IReadOnlyList<string> underperformingTags)
    {
        var al = new List<ReportAlertItemViewModel>();

        foreach (var f in feedPrices.Where(f => !string.Equals(f.FeedType, FeedTypes.Fodder, StringComparison.OrdinalIgnoreCase)))
        {
            if (f.StockKgFull > 0 && f.StockKg / f.StockKgFull <= 0.2m)
                al.Add(Alert("#8a261c", $"Low feed stock: <b>{f.DisplayName}</b> — {Math.Round(f.StockKg / f.StockKgFull * 100)}% left"));
            if (f.StockKg <= 0)
                al.Add(Alert("#8a261c", $"Out of stock: <b>{f.DisplayName}</b>"));
        }

        foreach (var v in vaccines)
        {
            var d = GetDueGoats(v, goats, log);
            if (d.Count > 0)
                al.Add(Alert("var(--amber)", $"<b>{v.Name}</b> due for {d.Count} goat(s)"));
        }

        foreach (var g in goats.Where(g => g.MatedDate.HasValue && (!g.KidsCount.HasValue || g.KidsCount == 0)))
        {
            var days = _goatService.GetAgeInDays(g.MatedDate!.Value);
            if (days >= 45)
                al.Add(Alert("var(--blue)", $"Scan due: <b>{g.Tag}</b> crossed {days} days ago"));
        }

        foreach (var g in confirmed)
        {
            var kd = BreedingHelper.ExpectedKidding(g.MatedDate!.Value);
            var d = BreedingHelper.DaysUntil(kd);
            if (d <= 14)
                al.Add(Alert("var(--pink)", $"Expected kidding: <b>{g.Tag}</b> in {d} days"));
        }

        foreach (var g in goats.Where(g => g.PrepCrossDate.HasValue && !g.MatedDate.HasValue))
        {
            var start = g.PrepCrossDate!.Value.AddDays(-BreedingHelper.PrepDietLeadDays);
            if (BreedingHelper.DaysUntil(start) <= 0)
                al.Add(Alert("var(--amber)", $"Build-up feeding now: <b>{g.Tag}</b> — cross on {g.PrepCrossDate:yyyy-MM-dd}"));
        }

        foreach (var tag in underperformingTags)
        {
            var recs = weightRecords.Where(w => goats.Any(g => g.Tag == tag && g.Id == w.GoatId)).OrderBy(w => w.Date).ToList();
            if (recs.Count >= 2)
            {
                var adg = (recs[^1].Kg - recs[0].Kg) / Math.Max(1,
                    (recs[^1].Date.ToDateTime(TimeOnly.MinValue) - recs[0].Date.ToDateTime(TimeOnly.MinValue)).Days);
                al.Add(Alert("var(--amber)", $"Low weight gain: <b>{tag}</b> at {adg * 1000:F0} g/day"));
            }
        }

        foreach (var r in reminders)
        {
            var d = BreedingHelper.DaysUntil(r.ReminderDate);
            if (d <= remindDays)
                al.Add(Alert(d < 0 ? "#8a261c" : "var(--amber)",
                    $"{r.Title} — {(d < 0 ? "overdue" : "in " + d + " days")}"));
        }

        return new ReportAlertsSectionViewModel { Items = al };
    }

    private ReportComparisonSectionViewModel BuildComparison(
        IReadOnlyList<Income> incomes, IReadOnlyList<Expense> expenses,
        IReadOnlyList<FeedPurchase> feedBuys, IReadOnlyList<VaccinePurchase> vaccineBuys,
        IReadOnlyList<MilkSale> milkSales, IReadOnlyList<MilkProduction> milkProd,
        IReadOnlyList<OwnerInvestment> ownerInv, IReadOnlyList<DeathRecord> deaths,
        IReadOnlyList<Goat> allGoats)
    {
        var nowM = MonthHelper.CurrentMonthKey();
        var lastM = DateOnly.FromDateTime(DateTime.Today).AddMonths(-1).ToString("yyyy-MM");
        var yr = DateTime.Today.Year.ToString();
        var monthlyFixed = _financeService.GetStaffSalaryMonthlyTotal()
            + _feedService.CalculateFarmMedicineMonthly()
            + _financeService.GetRecurringMonthlyTotal();

        PeriodSum SumFor(string prefix) => SumPeriod(prefix, incomes, expenses, feedBuys, vaccineBuys, milkSales, milkProd, ownerInv, deaths, allGoats, monthlyFixed);

        var a = SumFor(nowM);
        var b = SumFor(lastM);
        var y = SumFor(yr);

        var rows = new (string Label, Func<PeriodSum, decimal> Get, bool Money, bool LowerIsGood)[]
        {
            ("Revenue", s => s.Inc, true, false),
            ("Expenses", s => s.Exp, true, true),
            ("Net profit", s => s.Net, true, false),
            ("Money put in", s => s.Put, true, false),
            ("Milk collected (L)", s => s.Mp, false, false),
            ("Births", s => s.Brn, false, false),
            ("Deaths", s => s.Dth, false, true)
        };

        var compareRows = rows.Select(row =>
        {
            var av = row.Get(a);
            var bv = row.Get(b);
            var yv = row.Get(y);
            var d = av - bv;
            var pct = bv != 0 ? (int?)Math.Round(d / Math.Abs(bv) * 100) : null;
            var good = row.LowerIsGood ? d < 0 : d > 0;
            return new ReportCompareRowViewModel
            {
                Label = row.Label,
                ThisMonth = FormatCompare(av, row.Money),
                LastMonth = FormatCompare(bv, row.Money),
                Difference = (d > 0 ? "+" : "") + FormatCompare(d, row.Money),
                DifferenceColor = d == 0 ? "var(--ink-soft)" : good ? "var(--green-dark)" : "#8a261c",
                PercentChange = pct.HasValue ? (pct > 0 ? "+" : "") + pct + "%" : "—",
                YearToDate = FormatCompare(yv, row.Money)
            };
        }).ToList();

        return new ReportComparisonSectionViewModel { Rows = compareRows };
    }

    private sealed record PeriodSum(decimal Inc, decimal Exp, decimal Net, decimal Mp, decimal Put, decimal Dth, decimal Brn);

    private static PeriodSum SumPeriod(
        string prefix, IReadOnlyList<Income> incomes, IReadOnlyList<Expense> expenses,
        IReadOnlyList<FeedPurchase> feedBuys, IReadOnlyList<VaccinePurchase> vaccineBuys,
        IReadOnlyList<MilkSale> milkSales, IReadOnlyList<MilkProduction> milkProd,
        IReadOnlyList<OwnerInvestment> ownerInv, IReadOnlyList<DeathRecord> deaths,
        IReadOnlyList<Goat> allGoats, decimal monthlyFixed)
    {
        bool Match(DateOnly d) => prefix.Length == 4 ? d.Year.ToString() == prefix : d.ToString("yyyy-MM") == prefix;
        var inc = milkSales.Where(x => Match(x.Date)).Sum(x => x.Amount)
            + incomes.Where(x => Match(x.Date)).Sum(x => x.Amount);
        var months = prefix.Length == 4 ? DateTime.Today.Month : 1;
        var exp = feedBuys.Where(x => Match(x.Date)).Sum(x => x.Amount)
            + vaccineBuys.Where(x => Match(x.Date)).Sum(x => x.Amount)
            + expenses.Where(x => Match(x.Date)).Sum(x => x.Amount)
            + monthlyFixed * months;
        var mp = milkProd.Where(x => Match(x.Date)).Sum(x => x.Liters);
        var put = ownerInv.Where(x => Match(x.Date)).Sum(x => x.Amount);
        var dth = deaths.Count(x => Match(x.Date));
        var brn = allGoats.Count(g => g.Source == GoatSource.Born && Match(g.EventDate));
        return new PeriodSum(inc, exp, inc - exp, mp, put, dth, brn);
    }

    private static string FormatCompare(decimal v, bool money) =>
        money ? (v < 0 ? "– " : "") + DisplayHelper.FormatRs(Math.Abs(v)) : Math.Round(v).ToString("N0");

    private (List<ReportTrendRowViewModel> Rows, decimal TIn, decimal TOut, decimal TPut, decimal TNet) BuildTrendRows(
        IReadOnlyList<Income> incomes, IReadOnlyList<Expense> expenses,
        IReadOnlyList<FeedPurchase> feedBuys, IReadOnlyList<VaccinePurchase> vaccineBuys,
        IReadOnlyList<MilkSale> milkSales, IReadOnlyList<OwnerInvestment> ownerInv,
        ReportRange range, decimal monthlyFixed, string groupBy, bool preRevenue)
    {
        var bucket = new Dictionary<string, (decimal Inc, decimal Out, decimal Put)>(StringComparer.Ordinal);
        void AddTo(DateOnly? d, string field, decimal v)
        {
            if (!d.HasValue || !range.Test(d.Value)) return;
            var key = KeyOf(d.Value.ToString("yyyy-MM-dd"), groupBy);
            if (!bucket.TryGetValue(key, out var b)) b = (0, 0, 0);
            bucket[key] = field switch
            {
                "inc" => (b.Inc + v, b.Out, b.Put),
                "out" => (b.Inc, b.Out + v, b.Put),
                _ => (b.Inc, b.Out, b.Put + v)
            };
        }

        foreach (var x in milkSales) AddTo(x.Date, "inc", x.Amount);
        foreach (var x in incomes) AddTo(x.Date, "inc", x.Amount);
        foreach (var x in feedBuys) AddTo(x.Date, "out", x.Amount);
        foreach (var x in vaccineBuys) AddTo(x.Date, "out", x.Amount);
        foreach (var x in expenses) AddTo(x.Date, "out", x.Amount);
        foreach (var x in ownerInv) AddTo(x.Date, "put", x.Amount);

        var share = groupBy == "day" ? monthlyFixed / 30 : groupBy == "week" ? monthlyFixed / 30 * 7 : monthlyFixed;
        if (groupBy == "month")
        {
            var mset = bucket.Keys.Select(k => k.Length >= 7 ? k[..7] : k).ToHashSet();
            if (range.MonthCount == 1) mset.Add(MonthHelper.CurrentMonthKey());
            else
            {
                for (var i = 0; i < range.MonthCount; i++)
                {
                    var t = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-i);
                    var key = t.ToString("yyyy-MM");
                    if (range.Test(DateOnly.FromDateTime(t))) mset.Add(key);
                }
            }
            foreach (var k in mset)
            {
                if (!bucket.ContainsKey(k)) bucket[k] = (0, 0, 0);
                var b = bucket[k];
                bucket[k] = (b.Inc, b.Out + monthlyFixed, b.Put);
            }
        }
        else
        {
            foreach (var k in bucket.Keys.ToList())
            {
                var b = bucket[k];
                bucket[k] = (b.Inc, b.Out + share, b.Put);
            }
        }

        var limit = groupBy switch { "day" => 90, "week" => 53, _ => 36 };
        var keys = bucket.Keys.OrderByDescending(k => k).Take(limit).ToList();
        decimal tIn = 0, tOut = 0, tPut = 0;
        var rows = keys.Select(k =>
        {
            var b = bucket[k];
            tIn += b.Inc;
            tOut += b.Out;
            tPut += b.Put;
            return new ReportTrendRowViewModel
            {
                Key = k,
                Income = b.Inc,
                Expense = b.Out,
                OwnerInvestment = b.Put,
                Net = b.Inc - b.Out
            };
        }).ToList();

        return (rows, tIn, tOut, tPut, tIn - tOut);
    }

    private async Task<IReadOnlyDictionary<string, decimal>> BuildDailyUseMapAsync(CancellationToken cancellationToken)
    {
        var plans = await _context.FeedPlans.AsNoTracking().ToListAsync(cancellationToken);
        var recipe = await _feedService.GetMixRecipeAsync(cancellationToken);
        var catalog = await _context.FeedPrices.AsNoTracking().ToListAsync(cancellationToken);
        var mixKeys = MixRecipeHelper.MixFeedKeys(catalog.Select(f => f.FeedType)).ToList();
        var mixKgDay = StatusOrder.Sum(st =>
        {
            var n = _goatService.CountByStatus(st);
            var plan = plans.FirstOrDefault(p => p.StatusKey == st);
            return plan is null ? 0 : plan.MixKgPerDay * n;
        });
        var total = MixRecipeHelper.MixTotalKg(recipe, mixKeys);
        var kg = mixKeys.ToDictionary(k => k, _ => 0m);
        if (total <= 0) return kg;
        foreach (var k in mixKeys)
            kg[k] = mixKgDay * (recipe.GetValueOrDefault(k) / total);
        return kg;
    }

    private async Task<int> GetRemindDaysAsync(CancellationToken cancellationToken)
    {
        var setting = await _context.AppSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == RemindDaysKey, cancellationToken);
        return setting is not null && int.TryParse(setting.Value, out var d) ? d : 30;
    }

    private static List<Goat> GetDueGoats(Vaccine v, IReadOnlyList<Goat> goats, IReadOnlyList<VaccinationHistory> log) =>
        GoatsInScope(v, goats).Where(g =>
        {
            var next = NextDue(g, v, log);
            return next is not null && next.Value.Days <= 0;
        }).ToList();

    private static List<Goat> GetUpcomingGoats(Vaccine v, IReadOnlyList<Goat> goats, IReadOnlyList<VaccinationHistory> log, int window) =>
        GoatsInScope(v, goats)
            .Select(g => (Goat: g, Next: NextDue(g, v, log)))
            .Where(x => x.Next is not null && x.Next.Value.Days > 0 && x.Next.Value.Days <= window)
            .Select(x => x.Goat)
            .ToList();

    private static IEnumerable<Goat> GoatsInScope(Vaccine v, IReadOnlyList<Goat> goats) =>
        goats.Where(g => v.Scope == VaccineScope.All || StatusMatchesScope(g.Status, v.Scope));

    private static bool StatusMatchesScope(GoatStatus status, VaccineScope scope) => scope switch
    {
        VaccineScope.Kid => status == GoatStatus.Kid,
        VaccineScope.Milking => status == GoatStatus.Milking,
        VaccineScope.Pregnant => status == GoatStatus.Pregnant,
        VaccineScope.Dry => status == GoatStatus.Dry,
        VaccineScope.Buck => status == GoatStatus.Buck,
        VaccineScope.Sale => status == GoatStatus.Sale,
        _ => false
    };

    private static (DateOnly Date, int Days)? NextDue(Goat g, Vaccine v, IReadOnlyList<VaccinationHistory> log)
    {
        if (v.RuleType == VaccineRuleType.Age)
        {
            if (log.Any(l => l.GoatId == g.Id && l.VaccineId == v.Id)) return null;
            var dueDate = g.EventDate.AddDays(v.Days ?? 0);
            return (dueDate, BreedingHelper.DaysUntil(dueDate));
        }
        var last = log.Where(l => l.GoatId == g.Id && l.VaccineId == v.Id)
            .Select(l => l.VaccinationDate).OrderByDescending(d => d).FirstOrDefault();
        var baseDate = last == default ? DateOnly.FromDateTime(DateTime.Today) : last;
        var nextDate = baseDate.AddDays((v.Months ?? 0) * 30);
        return (nextDate, BreedingHelper.DaysUntil(nextDate));
    }

    private static string KeyOf(string date, string groupBy)
    {
        if (groupBy == "day") return date;
        if (groupBy == "week")
        {
            var dt = DateOnly.Parse(date).ToDateTime(TimeOnly.MinValue);
            var dow = ((int)dt.DayOfWeek + 6) % 7;
            return dt.AddDays(-dow).ToString("yyyy-MM-dd");
        }
        return date[..7];
    }

    private static string NormalizeGroupBy(string? groupBy) =>
        groupBy is "day" or "week" or "month" ? groupBy : "month";

    private static ReportKpiViewModel Kpi(string label, string value, string? sub = null, string? color = null) =>
        new() { Label = label, Value = value, Sub = sub, Color = color };

    private static ReportKpiViewModel KpiDot(string label, string value, string dotColor) =>
        new() { Label = label, Value = value, DotColor = dotColor };

    private static ReportAlertItemViewModel Alert(string color, string html) =>
        new() { Color = color, Html = html };

    private Dictionary<string, decimal> BuildIncomeMap(
        IReadOnlyList<Income> incomes, IReadOnlyList<MilkSale> milkSales, ReportRange range)
    {
        var map = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var milkInc = milkSales.Where(x => range.Test(x.Date)).Sum(x => x.Amount);
        if (milkInc > 0) map["Milk sales"] = milkInc;
        foreach (var i in incomes.Where(x => range.Test(x.Date)))
            map[i.Type] = map.GetValueOrDefault(i.Type) + i.Amount;
        return map;
    }

    private Dictionary<string, decimal> BuildExpenseMap(
        IReadOnlyList<Expense> expenses, IReadOnlyList<FeedPurchase> feedBuys,
        IReadOnlyList<VaccinePurchase> vaccineBuys, IReadOnlyList<RecurringCost> recurring, ReportRange range)
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
            var v = (rc.Period == RecurringCostPeriod.Year ? rc.Amount / 12m : rc.Amount) * range.MonthCount;
            if (v > 0) map[rc.Name] = map.GetValueOrDefault(rc.Name) + v;
        }
        foreach (var e in expenses.Where(x => range.Test(x.Date)))
            map[e.Type] = map.GetValueOrDefault(e.Type) + e.Amount;
        return map;
    }

    private static List<ReportCategoryRowViewModel> ToCategoryRows(Dictionary<string, decimal> map, decimal total, decimal feedKg)
    {
        var max = map.Values.DefaultIfEmpty(0).Max();
        return map.OrderByDescending(x => x.Value).Select(kv =>
        {
            var pct = total > 0 ? (int)Math.Round(kv.Value / total * 100) : 0;
            var extra = kv.Key == "Feed bought" && feedKg > 0 ? $" · {feedKg:N0} kg" : null;
            return new ReportCategoryRowViewModel
            {
                Name = kv.Key, Amount = kv.Value, Percent = pct, Extra = extra,
                BarWidth = max > 0 ? Math.Max(4, (int)Math.Round(kv.Value / max * 100)) : 0
            };
        }).ToList();
    }

    private static List<ReportCategoryRowViewModel> ToIncomeRows(Dictionary<string, decimal> map, decimal total, decimal milkL)
    {
        var max = map.Values.DefaultIfEmpty(0).Max();
        return map.OrderByDescending(x => x.Value).Select(kv =>
        {
            var pct = total > 0 ? (int)Math.Round(kv.Value / total * 100) : 0;
            var extra = kv.Key == "Milk sales" && milkL > 0 ? $" · {milkL:N0} L" : null;
            return new ReportCategoryRowViewModel
            {
                Name = kv.Key, Amount = kv.Value, Percent = pct, Extra = extra,
                BarWidth = max > 0 ? Math.Max(4, (int)Math.Round(kv.Value / max * 100)) : 0
            };
        }).ToList();
    }

    private static List<ReportDayRowViewModel> BuildDayRows(
        IReadOnlyList<Income> incomes, IReadOnlyList<Expense> expenses,
        IReadOnlyList<FeedPurchase> feedBuys, IReadOnlyList<VaccinePurchase> vaccineBuys,
        IReadOnlyList<MilkSale> milkSales, IReadOnlyList<MilkProduction> milkProd,
        IReadOnlyList<OwnerInvestment> ownerInv, ReportRange range)
    {
        var dayMap = new Dictionary<DateOnly, (decimal In, decimal Out, decimal Put, List<string> Items)>();
        void Bump(DateOnly d, string k, decimal v, string? txt)
        {
            if (!range.Test(d)) return;
            if (!dayMap.TryGetValue(d, out var r)) r = (0, 0, 0, []);
            if (k == "in") r.In += v;
            else if (k == "out") r.Out += v;
            else r.Put += v;
            if (!string.IsNullOrEmpty(txt)) r.Items.Add(txt);
            dayMap[d] = r;
        }

        foreach (var x in feedBuys) Bump(x.Date, "out", x.Amount, $"Feed: {x.FeedType} {x.Kg}kg");
        foreach (var x in vaccineBuys) Bump(x.Date, "out", x.Amount, $"Vaccine: {x.Name}");
        foreach (var x in expenses) Bump(x.Date, "out", x.Amount, x.Type);
        foreach (var x in milkSales) Bump(x.Date, "in", x.Amount, $"Milk sold {x.Liters}L");
        foreach (var x in incomes) Bump(x.Date, "in", x.Amount, x.Type);
        foreach (var x in ownerInv) Bump(x.Date, "put", x.Amount, "Money put in");
        foreach (var x in milkProd) Bump(x.Date, "in", 0, $"Milk collected {x.Liters}L");

        return dayMap.OrderByDescending(kv => kv.Key).Take(60).Select(kv =>
        {
            var items = kv.Value.Items;
            var summary = items.Count > 0
                ? string.Join(" · ", items.Take(3)) + (items.Count > 3 ? $" +{items.Count - 3} more" : "")
                : "—";
            return new ReportDayRowViewModel
            {
                Date = kv.Key.ToString("yyyy-MM-dd"),
                Summary = summary,
                Income = kv.Value.In,
                Expense = kv.Value.Out,
                OwnerInvestment = kv.Value.Put
            };
        }).ToList();
    }

    private static string BuildDayNote(
        ReportRange range, IReadOnlyList<Income> incomes, IReadOnlyList<Expense> expenses,
        IReadOnlyList<FeedPurchase> feedBuys, IReadOnlyList<VaccinePurchase> vaccineBuys,
        IReadOnlyList<MilkSale> milkSales, IReadOnlyList<MilkProduction> milkProd,
        IReadOnlyList<OwnerInvestment> ownerInv)
    {
        var days = new HashSet<DateOnly>();
        foreach (var d in AllDates(incomes, expenses, feedBuys, vaccineBuys, milkSales, milkProd, ownerInv))
            if (range.Test(d)) days.Add(d);
        if (days.Count > 60)
            return $"Showing the 60 most recent days of {days.Count}. Use \"Custom…\" above to look at an exact period.";
        return days.Count > 0
            ? $"{days.Count} day(s) with records. Monthly costs like salaries and rent are not split per day."
            : "";
    }

    private static IEnumerable<DateOnly> AllDates(
        IReadOnlyList<Income> incomes, IReadOnlyList<Expense> expenses,
        IReadOnlyList<FeedPurchase> feedBuys, IReadOnlyList<VaccinePurchase> vaccineBuys,
        IReadOnlyList<MilkSale> milkSales, IReadOnlyList<MilkProduction>? milkProd = null,
        IReadOnlyList<OwnerInvestment>? ownerInv = null)
    {
        foreach (var x in incomes) yield return x.Date;
        foreach (var x in expenses) yield return x.Date;
        foreach (var x in feedBuys) yield return x.Date;
        foreach (var x in vaccineBuys) yield return x.Date;
        foreach (var x in milkSales) yield return x.Date;
        if (milkProd is not null) foreach (var x in milkProd) yield return x.Date;
        if (ownerInv is not null) foreach (var x in ownerInv) yield return x.Date;
    }

    private static ReportRange BuildRange(
        string period, string? customFrom, string? customTo,
        IReadOnlyList<Expense> expenses, IReadOnlyList<Income> incomes,
        IReadOnlyList<FeedPurchase> feedBuys, IReadOnlyList<VaccinePurchase> vaccineBuys,
        IReadOnlyList<MilkSale> milkSales, IReadOnlyList<OwnerInvestment> ownerInv)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var todayStr = today.ToString("yyyy-MM-dd");

        if (period == "custom" && !string.IsNullOrEmpty(customFrom) && !string.IsNullOrEmpty(customTo)
            && DateOnly.TryParse(customFrom, out var from) && DateOnly.TryParse(customTo, out var to))
        {
            var months = Math.Max(1, (to.Year - from.Year) * 12 + (to.Month - from.Month) + 1);
            return new ReportRange(d => d >= from && d <= to, months, $"{customFrom} → {customTo}");
        }

        if (period == "today")
            return new ReportRange(d => d == today, 1, todayStr);

        if (period == "week")
        {
            var dow = ((int)today.DayOfWeek + 6) % 7;
            var weekFrom = today.AddDays(-dow);
            return new ReportRange(d => d >= weekFrom && d <= today, 1, $"{weekFrom:yyyy-MM-dd} → {todayStr}");
        }

        if (period == "month")
        {
            var m = MonthHelper.CurrentMonthKey();
            return new ReportRange(d => d.ToString("yyyy-MM") == m, 1, m);
        }

        if (period == "lastmonth")
        {
            var lm = today.AddMonths(-1);
            var key = lm.ToString("yyyy-MM");
            var lastDay = new DateOnly(lm.Year, lm.Month, DateTime.DaysInMonth(lm.Year, lm.Month));
            return new ReportRange(d => d.ToString("yyyy-MM") == key, 1, key);
        }

        if (period == "3m")
        {
            var rangeFrom = today.AddDays(-90);
            return new ReportRange(d => d >= rangeFrom && d <= today, 3, $"{rangeFrom:yyyy-MM-dd} → {todayStr}");
        }

        if (period == "6m")
        {
            var rangeFrom = today.AddDays(-180);
            return new ReportRange(d => d >= rangeFrom && d <= today, 6, $"{rangeFrom:yyyy-MM-dd} → {todayStr}");
        }

        if (period == "year")
        {
            var y = today.Year;
            return new ReportRange(d => d.Year == y, today.Month, y.ToString());
        }

        if (period == "all")
        {
            var all = AllDates(incomes, expenses, feedBuys, vaccineBuys, milkSales, ownerInv: ownerInv)
                .Where(d => d <= today).OrderBy(d => d).ToList();
            var first = all.FirstOrDefault();
            if (first == default) first = today;
            var months = Math.Max(1, (today.Year - first.Year) * 12 + (today.Month - first.Month) + 1);
            return new ReportRange(_ => true, months, "all time (from " + first.ToString("yyyy-MM-dd") + ")");
        }

        var mDefault = MonthHelper.CurrentMonthKey();
        return new ReportRange(d => d.ToString("yyyy-MM") == mDefault, 1, mDefault);
    }

    private sealed record ReportRange(Func<DateOnly, bool> Test, int MonthCount, string Label);
}

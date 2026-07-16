using GoatFarm.Application.Common;
using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Feed;
using GoatFarm.Application.ViewModels.Reminders;
using GoatFarm.Application.ViewModels.Search;
using GoatFarm.Domain.Constants;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Infrastructure.Services;

public class SearchService : ISearchService
{
    private const string RemindDaysKey = "RemindDays";
    private readonly GoatFarmDbContext _context;
    private readonly IGoatService _goatService;

    public SearchService(GoatFarmDbContext context, IGoatService goatService)
    {
        _context = context;
        _goatService = goatService;
    }

    public async Task<GoatProfileViewModel?> GetProfileByTagAsync(string tag, CancellationToken cancellationToken = default)
    {
        var goatEntity = await FindGoatByTagAsync(tag, cancellationToken);
        if (goatEntity is null)
            return null;

        var goat = await _goatService.GetByIdAsync(goatEntity.Id, cancellationToken);
        if (goat is null)
            return null;

        var vaccines = await _context.Vaccines.AsNoTracking().ToListAsync(cancellationToken);
        var history = await _context.VaccinationHistories.AsNoTracking()
            .Where(h => h.GoatId == goatEntity.Id)
            .ToListAsync(cancellationToken);
        var window = await GetRemindDaysAsync(cancellationToken);

        var vaccinationHistory = history
            .Select(h =>
            {
                var vaccine = vaccines.FirstOrDefault(v => v.Id == h.VaccineId);
                return new GoatVaccinationRecordViewModel
                {
                    VaccineName = vaccine?.Name ?? "(removed)",
                    ScopeDisplay = vaccine is not null ? DisplayHelper.GetScopeLabel(vaccine.Scope) : string.Empty,
                    VaccinationDate = h.VaccinationDate
                };
            })
            .OrderByDescending(h => h.VaccinationDate)
            .ToList();

        var vaccineSchedule = vaccines
            .Where(v => GoatInScope(goatEntity, v))
            .Select(v => BuildVaccineStatus(goatEntity, v, history, window))
            .OrderBy(v => v.Status == "Due now" ? 0 : v.Status.StartsWith("Due in", StringComparison.Ordinal) ? 1 : 2)
            .ThenBy(v => v.VaccineName)
            .ToList();

        var feedPlan = await BuildFeedPlanForStatusAsync(goatEntity.Status, cancellationToken);

        var reminders = await _context.Reminders.AsNoTracking()
            .OrderBy(r => r.ReminderDate)
            .ToListAsync(cancellationToken);
        var reminderVms = reminders
            .Where(r => ReminderAppliesToGoat(r, goatEntity.Status))
            .Select(r => MapReminder(r, window))
            .ToList();

        return new GoatProfileViewModel
        {
            Goat = goat,
            VaccinationHistory = vaccinationHistory,
            VaccineSchedule = vaccineSchedule,
            FeedPlan = feedPlan,
            Reminders = reminderVms
        };
    }

    private async Task<FeedPlanViewModel?> BuildFeedPlanForStatusAsync(GoatStatus status, CancellationToken cancellationToken)
    {
        var prices = await _context.FeedPrices.AsNoTracking()
            .ToDictionaryAsync(p => p.FeedType, p => p.PricePerKg, cancellationToken);
        var plan = await _context.FeedPlans.Include(p => p.Items).AsNoTracking()
            .FirstOrDefaultAsync(p => p.StatusKey == status, cancellationToken);
        if (plan is null)
            return null;

        var items = FeedTypes.All.Select(f =>
        {
            var grams = plan.Items.FirstOrDefault(i => i.FeedType == f.Key)?.GramsPerDay ?? 0;
            var price = prices.GetValueOrDefault(f.Key, 0);
            return new FeedPlanItemViewModel
            {
                FeedType = f.Key,
                DisplayName = f.Name,
                GramsPerDay = grams,
                DailyCost = grams / 1000m * price
            };
        }).ToList();

        var dailyFeed = items.Sum(i => i.DailyCost);
        var (text, _) = DisplayHelper.GetStatusDisplay(status);

        return new FeedPlanViewModel
        {
            StatusKey = DisplayHelper.StatusKey(status),
            StatusDisplay = text,
            MedicineCostPerGoatPerMonth = plan.MedicineCostPerGoatPerMonth,
            Items = items,
            GoatCount = 1,
            DailyFeedCost = dailyFeed,
            DailyTotalCost = dailyFeed + plan.MedicineCostPerGoatPerMonth / 30m,
            MonthlyTotalCost = dailyFeed * 30 + plan.MedicineCostPerGoatPerMonth
        };
    }

    private static GoatVaccineStatusViewModel BuildVaccineStatus(
        Goat goat,
        Vaccine vaccine,
        IReadOnlyList<VaccinationHistory> history,
        int window)
    {
        var last = history.Where(h => h.VaccineId == vaccine.Id)
            .Select(h => h.VaccinationDate)
            .OrderByDescending(d => d)
            .FirstOrDefault();
        var lastDate = last == default ? null : last.ToString("yyyy-MM-dd");

        var next = NextDue(goat, vaccine, history);
        string status;
        string statusCss;
        string? dueDate = null;

        if (vaccine.RuleType == VaccineRuleType.Age && last != default)
        {
            status = "Up to date";
            statusCss = "chip-milk";
        }
        else if (next is null)
        {
            status = "Up to date";
            statusCss = "chip-milk";
        }
        else
        {
            dueDate = next.Value.Date.ToString("yyyy-MM-dd");
            if (next.Value.Days <= 0)
            {
                status = "Due now";
                statusCss = "chip-exp";
            }
            else if (next.Value.Days <= window)
            {
                status = $"Due in {next.Value.Days} day{(next.Value.Days == 1 ? "" : "s")}";
                statusCss = "chip-kid";
            }
            else
            {
                status = $"Due {dueDate}";
                statusCss = "chip-dry";
            }
        }

        return new GoatVaccineStatusViewModel
        {
            VaccineName = vaccine.Name,
            ScopeDisplay = DisplayHelper.GetScopeLabel(vaccine.Scope),
            RuleDisplay = RuleLabel(vaccine),
            Status = status,
            StatusCss = statusCss,
            DueDate = dueDate,
            LastDate = lastDate
        };
    }

    private static bool GoatInScope(Goat goat, Vaccine vaccine) =>
        vaccine.Scope == VaccineScope.All || StatusMatchesScope(goat.Status, vaccine.Scope);

    private static bool ReminderAppliesToGoat(Reminder reminder, GoatStatus status) =>
        reminder.Scope == VaccineScope.None ||
        reminder.Scope == VaccineScope.All ||
        StatusMatchesScope(status, reminder.Scope);

    private static bool StatusMatchesScope(GoatStatus status, VaccineScope scope) =>
        scope switch
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
            if (log.Any(l => l.GoatId == g.Id && l.VaccineId == v.Id))
                return null;

            var dueDate = g.EventDate.AddDays(v.Days ?? 0);
            return (dueDate, DaysUntil(dueDate));
        }

        var last = log.Where(l => l.GoatId == g.Id && l.VaccineId == v.Id)
            .Select(l => l.VaccinationDate)
            .OrderByDescending(d => d)
            .FirstOrDefault();
        var baseDate = last == default ? DateOnly.FromDateTime(DateTime.Today) : last;
        var nextDate = baseDate.AddDays((v.Months ?? 0) * 30);
        return (nextDate, DaysUntil(nextDate));
    }

    private static int DaysUntil(DateOnly date)
    {
        var target = date.ToDateTime(TimeOnly.MinValue);
        return (int)Math.Ceiling((target - DateTime.Today).TotalDays);
    }

    private static string RuleLabel(Vaccine v) =>
        v.RuleType == VaccineRuleType.Age ? $"At {v.Days} days old" : $"Every {v.Months} months";

    private async Task<int> GetRemindDaysAsync(CancellationToken cancellationToken)
    {
        var setting = await _context.AppSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == RemindDaysKey, cancellationToken);
        return setting is not null && int.TryParse(setting.Value, out var d) ? d : 30;
    }

    private static ReminderViewModel MapReminder(Reminder r, int window)
    {
        var du = DaysUntil(r.ReminderDate);
        var when = du < 0 ? $"overdue {-du}d" : du == 0 ? "today" : $"in {du} day{(du == 1 ? "" : "s")}";
        var col = du <= 0 ? "#8a261c" : du <= window ? "var(--amber)" : "var(--ink-soft)";
        return new ReminderViewModel
        {
            Id = r.Id,
            Title = r.Title,
            Scope = r.Scope,
            ScopeDisplay = r.Scope != VaccineScope.None ? DisplayHelper.GetScopeLabel(r.Scope) : string.Empty,
            ReminderDate = r.ReminderDate,
            WhenDisplay = when,
            WhenColor = col
        };
    }

    private async Task<Goat?> FindGoatByTagAsync(string tag, CancellationToken cancellationToken)
    {
        var normalized = NormalizeTag(tag);
        if (string.IsNullOrEmpty(normalized))
            return null;

        var goat = await MatchTagAsync(normalized, cancellationToken);
        if (goat is not null)
            return goat;

        if (long.TryParse(normalized, out _))
        {
            var trimmed = normalized.TrimStart('0');
            if (!string.IsNullOrEmpty(trimmed) && !string.Equals(trimmed, normalized, StringComparison.Ordinal))
            {
                goat = await MatchTagAsync(trimmed, cancellationToken);
                if (goat is not null)
                    return goat;
            }

            foreach (var candidate in new[] { normalized.PadLeft(3, '0'), normalized.PadLeft(4, '0') })
            {
                if (string.Equals(candidate, normalized, StringComparison.Ordinal))
                    continue;

                goat = await MatchTagAsync(candidate, cancellationToken);
                if (goat is not null)
                    return goat;
            }
        }

        return null;
    }

    private Task<Goat?> MatchTagAsync(string tag, CancellationToken cancellationToken) =>
        _context.Goats.AsNoTracking()
            .FirstOrDefaultAsync(g => g.Tag.ToLower() == tag.ToLower(), cancellationToken);

    private static string NormalizeTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return string.Empty;

        return new string(tag.Where(c => !char.IsControl(c)).ToArray()).Trim();
    }
}

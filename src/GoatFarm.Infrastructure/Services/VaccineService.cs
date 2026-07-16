using GoatFarm.Application.Common;
using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Vaccines;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Infrastructure.Services;

public class VaccineService : IVaccineService
{
    private const string RemindDaysKey = "RemindDays";
    private readonly GoatFarmDbContext _context;

    public VaccineService(GoatFarmDbContext context) => _context = context;

    public async Task<VaccinePageViewModel> GetVaccinePageAsync(int? remindDays, string? month = null, CancellationToken cancellationToken = default)
    {
        month ??= MonthHelper.CurrentMonthKey();
        var (monthStart, monthEnd) = MonthHelper.GetMonthRange(month);
        var window = remindDays ?? await GetRemindDaysAsync(cancellationToken);
        var goats = await _context.Goats.AsNoTracking().ToListAsync(cancellationToken);
        var vaccines = await _context.Vaccines.AsNoTracking().ToListAsync(cancellationToken);
        var log = await _context.VaccinationHistories.AsNoTracking().ToListAsync(cancellationToken);
        var reminders = await _context.Reminders.AsNoTracking().OrderBy(r => r.ReminderDate).ToListAsync(cancellationToken);

        var dueNow = new List<VaccineDueRowViewModel>();
        var upcoming = new List<VaccineUpcomingRowViewModel>();
        var totalDue = 0;
        var upCount = 0;

        foreach (var v in vaccines)
        {
            var dueGoats = GetDueGoats(v, goats, log);
            if (dueGoats.Count > 0)
            {
                totalDue += dueGoats.Count;
                dueNow.Add(new VaccineDueRowViewModel
                {
                    VaccineId = v.Id,
                    VaccineName = v.Name,
                    ScopeDisplay = DisplayHelper.GetScopeLabel(v.Scope),
                    GoatTags = dueGoats.Select(g => g.Tag).ToList(),
                    DueCount = dueGoats.Count
                });
            }

            var upGoats = GetUpcomingGoats(v, goats, log, window);
            if (upGoats.Count > 0)
            {
                upCount += upGoats.Count;
                upGoats.Sort((a, b) => a.Days.CompareTo(b.Days));
                upcoming.Add(new VaccineUpcomingRowViewModel
                {
                    VaccineName = v.Name,
                    ScopeDisplay = DisplayHelper.GetScopeLabel(v.Scope),
                    GoatTags = upGoats.Select(x => x.Goat.Tag).ToList(),
                    Count = upGoats.Count,
                    FirstInDays = upGoats[0].Days,
                    FirstDate = upGoats[0].Date.ToString("yyyy-MM-dd")
                });
            }
        }

        var schedule = vaccines.Select(v => new VaccineViewModel
        {
            Id = v.Id,
            Name = v.Name,
            Scope = v.Scope,
            ScopeDisplay = DisplayHelper.GetScopeLabel(v.Scope),
            RuleType = v.RuleType,
            RuleDisplay = RuleLabel(v),
            Days = v.Days,
            Months = v.Months
        }).ToList();

        var history = log
            .GroupBy(l => new { l.VaccinationDate, l.VaccineId })
            .Select(g =>
            {
                var v = vaccines.FirstOrDefault(x => x.Id == g.Key.VaccineId);
                return new VaccinationLogViewModel
                {
                    VaccineId = g.Key.VaccineId,
                    VaccinationDate = g.Key.VaccinationDate,
                    VaccineName = v?.Name ?? "(removed)",
                    GoatCount = g.Count()
                };
            })
            .OrderByDescending(h => h.VaccinationDate)
            .Take(15)
            .ToList();

        var purchases = await _context.VaccinePurchases.AsNoTracking()
            .Where(p => p.Date >= monthStart && p.Date < monthEnd)
            .OrderByDescending(p => p.Date)
            .Select(p => new VaccinePurchaseViewModel
            {
                Id = p.Id,
                Date = p.Date,
                Name = p.Name,
                Qty = p.Qty,
                Unit = p.Unit,
                Amount = p.Amount,
                Comment = p.Comment
            })
            .ToListAsync(cancellationToken);

        return new VaccinePageViewModel
        {
            VaccineCount = vaccines.Count,
            DueNowCount = totalDue,
            DueNowNote = totalDue > 0 ? "goats waiting for a shot" : "all up to date",
            UpcomingCount = upCount,
            UpcomingNote = $"due within {window} days",
            RemindDays = window,
            DueNow = dueNow,
            Upcoming = upcoming,
            Schedule = schedule,
            History = history,
            VaccinePurchases = purchases,
            VaccineBoughtMonthTotal = purchases.Sum(p => p.Amount),
            PurchaseMonth = month
        };
    }

    public async Task<VaccineViewModel> AddVaccineAsync(CreateVaccineViewModel model, CancellationToken cancellationToken = default)
    {
        var entity = new Vaccine
        {
            Name = model.Name.Trim(),
            Scope = model.Scope,
            RuleType = model.RuleType,
            Days = model.RuleType == VaccineRuleType.Age ? model.Value : null,
            Months = model.RuleType == VaccineRuleType.Repeat ? model.Value : null
        };
        _context.Vaccines.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return new VaccineViewModel
        {
            Id = entity.Id,
            Name = entity.Name,
            Scope = entity.Scope,
            ScopeDisplay = DisplayHelper.GetScopeLabel(entity.Scope),
            RuleType = entity.RuleType,
            RuleDisplay = RuleLabel(entity),
            Days = entity.Days,
            Months = entity.Months
        };
    }

    public async Task<VaccineViewModel?> UpdateVaccineAsync(int id, CreateVaccineViewModel model, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Vaccines.FindAsync([id], cancellationToken);
        if (entity is null) return null;
        entity.Name = model.Name.Trim();
        entity.Scope = model.Scope;
        entity.RuleType = model.RuleType;
        entity.Days = model.RuleType == VaccineRuleType.Age ? model.Value : null;
        entity.Months = model.RuleType == VaccineRuleType.Repeat ? model.Value : null;
        await _context.SaveChangesAsync(cancellationToken);
        return new VaccineViewModel
        {
            Id = entity.Id,
            Name = entity.Name,
            Scope = entity.Scope,
            ScopeDisplay = DisplayHelper.GetScopeLabel(entity.Scope),
            RuleType = entity.RuleType,
            RuleDisplay = RuleLabel(entity),
            Days = entity.Days,
            Months = entity.Months
        };
    }

    public async Task<bool> DeleteVaccineAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Vaccines.FindAsync([id], cancellationToken);
        if (entity is null) return false;
        _context.Vaccines.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task MarkVaccineDoneAsync(int vaccineId, CancellationToken cancellationToken = default)
    {
        var vaccine = await _context.Vaccines.FindAsync([vaccineId], cancellationToken);
        if (vaccine is null) return;

        var goats = await _context.Goats.AsNoTracking().ToListAsync(cancellationToken);
        var log = await _context.VaccinationHistories.AsNoTracking().ToListAsync(cancellationToken);
        var due = GetDueGoats(vaccine, goats, log);
        var today = DateOnly.FromDateTime(DateTime.Today);

        foreach (var g in due)
        {
            _context.VaccinationHistories.Add(new VaccinationHistory
            {
                GoatId = g.Id,
                VaccineId = vaccineId,
                VaccinationDate = today
            });
        }
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteVaccinationBatchAsync(int vaccineId, DateOnly date, CancellationToken cancellationToken = default)
    {
        var entries = await _context.VaccinationHistories
            .Where(h => h.VaccineId == vaccineId && h.VaccinationDate == date)
            .ToListAsync(cancellationToken);
        if (entries.Count == 0) return false;
        _context.VaccinationHistories.RemoveRange(entries);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> UpdateVaccinationBatchAsync(UpdateVaccinationBatchViewModel model, CancellationToken cancellationToken = default)
    {
        var entries = await _context.VaccinationHistories
            .Where(h => h.VaccineId == model.VaccineId && h.VaccinationDate == model.Date)
            .ToListAsync(cancellationToken);
        if (entries.Count == 0) return false;
        foreach (var entry in entries)
            entry.VaccinationDate = model.NewDate;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task SetReminderWindowAsync(int days, CancellationToken cancellationToken = default)
    {
        var setting = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == RemindDaysKey, cancellationToken);
        if (setting is null)
        {
            _context.AppSettings.Add(new AppSetting { Key = RemindDaysKey, Value = days.ToString() });
        }
        else
        {
            setting.Value = days.ToString();
            setting.UpdatedDate = DateTime.UtcNow;
        }
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<VaccinePurchaseViewModel> AddVaccinePurchaseAsync(CreateVaccinePurchaseViewModel model, CancellationToken cancellationToken = default)
    {
        var entity = new VaccinePurchase
        {
            Date = model.Date,
            Name = model.Name.Trim(),
            Qty = model.Qty,
            Unit = model.Unit,
            Amount = model.Amount,
            Comment = string.IsNullOrWhiteSpace(model.Comment) ? null : model.Comment.Trim()
        };
        _context.VaccinePurchases.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return MapPurchase(entity);
    }

    public async Task<VaccinePurchaseViewModel?> UpdateVaccinePurchaseAsync(int id, CreateVaccinePurchaseViewModel model, CancellationToken cancellationToken = default)
    {
        var entity = await _context.VaccinePurchases.FindAsync([id], cancellationToken);
        if (entity is null) return null;
        entity.Date = model.Date;
        entity.Name = model.Name.Trim();
        entity.Qty = model.Qty;
        entity.Unit = model.Unit;
        entity.Amount = model.Amount;
        entity.Comment = string.IsNullOrWhiteSpace(model.Comment) ? null : model.Comment.Trim();
        entity.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return MapPurchase(entity);
    }

    public async Task<bool> DeleteVaccinePurchaseAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.VaccinePurchases.FindAsync([id], cancellationToken);
        if (entity is null) return false;
        _context.VaccinePurchases.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public decimal GetVaccinePurchasedMonthly(string month)
    {
        var (monthStart, monthEnd) = MonthHelper.GetMonthRange(month);
        return _context.VaccinePurchases.AsNoTracking()
            .Where(p => p.Date >= monthStart && p.Date < monthEnd)
            .Sum(p => p.Amount);
    }

    private static VaccinePurchaseViewModel MapPurchase(VaccinePurchase entity) => new()
    {
        Id = entity.Id,
        Date = entity.Date,
        Name = entity.Name,
        Qty = entity.Qty,
        Unit = entity.Unit,
        Amount = entity.Amount,
        Comment = entity.Comment
    };

    private async Task<int> GetRemindDaysAsync(CancellationToken cancellationToken)
    {
        var setting = await _context.AppSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == RemindDaysKey, cancellationToken);
        return setting is not null && int.TryParse(setting.Value, out var d) ? d : 30;
    }

    private static string RuleLabel(Vaccine v) =>
        v.RuleType == VaccineRuleType.Age ? $"At {v.Days} days old" : $"Every {v.Months} months";

    private static List<Goat> GetDueGoats(Vaccine v, IReadOnlyList<Goat> goats, IReadOnlyList<VaccinationHistory> log) =>
        GoatsInScope(v, goats).Where(g =>
        {
            var next = NextDue(g, v, log);
            return next is not null && next.Value.Days <= 0;
        }).ToList();

    private static List<(Goat Goat, int Days, DateOnly Date)> GetUpcomingGoats(
        Vaccine v, IReadOnlyList<Goat> goats, IReadOnlyList<VaccinationHistory> log, int window) =>
        GoatsInScope(v, goats)
            .Select(g => (Goat: g, Next: NextDue(g, v, log)))
            .Where(x => x.Next is not null && x.Next.Value.Days > 0 && x.Next.Value.Days <= window)
            .Select(x => (x.Goat, x.Next!.Value.Days, x.Next.Value.Date))
            .ToList();

    private static IEnumerable<Goat> GoatsInScope(Vaccine v, IReadOnlyList<Goat> goats) =>
        goats.Where(g => v.Scope == VaccineScope.All || StatusMatchesScope(g.Status, v.Scope));

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
            if (log.Any(l => l.GoatId == g.Id && l.VaccineId == v.Id)) return null;
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
        var now = DateTime.Today;
        return (int)Math.Ceiling((target - now).TotalDays);
    }
}

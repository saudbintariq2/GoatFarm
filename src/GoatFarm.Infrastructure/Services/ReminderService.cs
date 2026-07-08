using GoatFarm.Application.Common;
using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Reminders;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Infrastructure.Services;

public class ReminderService : IReminderService
{
    private readonly GoatFarmDbContext _context;

    public ReminderService(GoatFarmDbContext context) => _context = context;

    public async Task<IReadOnlyList<ReminderViewModel>> GetRemindersAsync(CancellationToken cancellationToken = default)
    {
        var window = await GetRemindDaysAsync(cancellationToken);
        var reminders = await _context.Reminders.AsNoTracking()
            .OrderBy(r => r.ReminderDate)
            .ToListAsync(cancellationToken);

        return reminders.Select(r => MapReminder(r, window)).ToList();
    }

    public async Task<ReminderViewModel> AddReminderAsync(CreateReminderViewModel model, CancellationToken cancellationToken = default)
    {
        var date = DateOnly.FromDateTime(DateTime.Today).AddDays(model.Number * model.UnitDays);
        var entity = new Reminder
        {
            Title = model.Title.Trim(),
            Scope = model.Scope,
            ReminderDate = date
        };
        _context.Reminders.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        var window = await GetRemindDaysAsync(cancellationToken);
        return MapReminder(entity, window);
    }

    public async Task<ReminderViewModel?> UpdateReminderAsync(int id, UpdateReminderViewModel model, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Reminders.FindAsync([id], cancellationToken);
        if (entity is null) return null;
        entity.Title = model.Title.Trim();
        entity.Scope = model.Scope;
        entity.ReminderDate = model.ReminderDate;
        await _context.SaveChangesAsync(cancellationToken);
        var window = await GetRemindDaysAsync(cancellationToken);
        return MapReminder(entity, window);
    }

    public async Task<bool> DeleteReminderAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Reminders.FindAsync([id], cancellationToken);
        if (entity is null) return false;
        _context.Reminders.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<int> GetRemindDaysAsync(CancellationToken cancellationToken)
    {
        var setting = await _context.AppSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == "RemindDays", cancellationToken);
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

    private static int DaysUntil(DateOnly date)
    {
        var target = date.ToDateTime(TimeOnly.MinValue);
        return (int)Math.Ceiling((target - DateTime.Today).TotalDays);
    }
}

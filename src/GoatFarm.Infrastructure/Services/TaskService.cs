using GoatFarm.Application.Common;
using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Tasks;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Infrastructure.Services;

public class TaskService : ITaskService
{
    private const int CompletedWindowDays = 30;
    private const int CompletedListLimit = 50;

    private readonly GoatFarmDbContext _context;

    public TaskService(GoatFarmDbContext context) => _context = context;

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

    public async Task<TasksPageViewModel> GetPageAsync(
        string currentUserId, bool canEdit, string? filter, CancellationToken cancellationToken = default)
    {
        filter = string.IsNullOrWhiteSpace(filter) ? "all" : filter.Trim();
        var users = await GetUserOptionsAsync(cancellationToken);
        var names = users.ToDictionary(u => u.Id, u => u.Name);
        var today = Today;
        var completedSince = DateTime.UtcNow.AddDays(-CompletedWindowDays);

        var open = await _context.FarmTasks.AsNoTracking()
            .Include(t => t.Goat)
            .Where(t => t.Status != FarmTaskStatus.Completed)
            .ToListAsync(cancellationToken);

        var completed = await _context.FarmTasks.AsNoTracking()
            .Include(t => t.Goat)
            .Where(t => t.Status == FarmTaskStatus.Completed && t.CompletedAtUtc >= completedSince)
            .OrderByDescending(t => t.CompletedAtUtc)
            .ToListAsync(cancellationToken);

        var kpis = new TaskKpisViewModel
        {
            Open = open.Count,
            Mine = open.Count(t => t.AssigneeUserId == currentUserId),
            Urgent = open.Count(t => t.Priority == FarmTaskPriority.Urgent),
            Overdue = open.Count(t => t.DueDate < today),
            CompletedLast30Days = completed.Count
        };

        var filteredOpen = ApplyFilter(open, filter, currentUserId, today)
            .OrderByDescending(t => t.Priority)
            .ThenBy(t => t.DueDate ?? DateOnly.MaxValue)
            .ThenBy(t => t.Id)
            .Select(t => Map(t, names, currentUserId, canEdit))
            .ToList();

        var reminders = await _context.Reminders.AsNoTracking()
            .Where(r => r.ReminderDate >= today.AddDays(-7))
            .OrderBy(r => r.ReminderDate)
            .Take(10)
            .ToListAsync(cancellationToken);

        return new TasksPageViewModel
        {
            Filter = filter,
            Kpis = kpis,
            ToDo = filteredOpen.Where(t => t.Status == FarmTaskStatus.ToDo).ToList(),
            InProgress = filteredOpen.Where(t => t.Status == FarmTaskStatus.InProgress).ToList(),
            Completed = ApplyFilter(completed, filter, currentUserId, today)
                .Take(CompletedListLimit)
                .Select(t => Map(t, names, currentUserId, canEdit))
                .ToList(),
            Users = users,
            FarmReminders = reminders.Select(r => new FarmReminderRowViewModel
            {
                Title = r.Title,
                DateDisplay = r.ReminderDate.ToString("yyyy-MM-dd"),
                ScopeDisplay = r.Scope == VaccineScope.None ? null : DisplayHelper.GetScopeLabel(r.Scope)
            }).ToList()
        };
    }

    public async Task<IReadOnlyList<TaskViewModel>> GetOpenForUserAsync(
        string currentUserId, int take, CancellationToken cancellationToken = default)
    {
        var tasks = await _context.FarmTasks.AsNoTracking()
            .Include(t => t.Goat)
            .Where(t => t.Status != FarmTaskStatus.Completed &&
                        (t.AssigneeUserId == currentUserId || t.AssigneeUserId == null))
            .OrderByDescending(t => t.Priority)
            .ThenBy(t => t.DueDate == null)
            .ThenBy(t => t.DueDate)
            .Take(take)
            .ToListAsync(cancellationToken);

        var names = (await GetUserOptionsAsync(cancellationToken)).ToDictionary(u => u.Id, u => u.Name);
        return tasks.Select(t => Map(t, names, currentUserId, canEditAny: false)).ToList();
    }

    public async Task<TaskViewModel> CreateAsync(SaveTaskViewModel model, string currentUserId, CancellationToken cancellationToken = default)
    {
        var entity = new FarmTask
        {
            CreatedByUserId = currentUserId,
            Status = FarmTaskStatus.ToDo
        };
        await ApplyAsync(entity, model, cancellationToken);
        _context.FarmTasks.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return await MapSingleAsync(entity.Id, currentUserId, true, cancellationToken);
    }

    public async Task<TaskViewModel?> UpdateAsync(int id, SaveTaskViewModel model, string currentUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _context.FarmTasks.FindAsync([id], cancellationToken);
        if (entity is null) return null;
        await ApplyAsync(entity, model, cancellationToken);
        entity.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return await MapSingleAsync(id, currentUserId, true, cancellationToken);
    }

    public async Task<TaskViewModel?> SetStatusAsync(
        int id, FarmTaskStatus status, string currentUserId, bool canEditAny, CancellationToken cancellationToken = default)
    {
        var entity = await _context.FarmTasks.FindAsync([id], cancellationToken);
        if (entity is null) return null;

        if (!canEditAny && entity.AssigneeUserId is not null && entity.AssigneeUserId != currentUserId)
            throw new UnauthorizedAccessException("Only the person this task is assigned to (or a manager) can change it.");

        if (entity.Status == status)
            return await MapSingleAsync(id, currentUserId, canEditAny, cancellationToken);

        switch (status)
        {
            case FarmTaskStatus.ToDo:
                entity.StartedAtUtc = null;
                entity.CompletedAtUtc = null;
                break;
            case FarmTaskStatus.InProgress:
                entity.StartedAtUtc ??= DateTime.UtcNow;
                entity.CompletedAtUtc = null;
                break;
            case FarmTaskStatus.Completed:
                entity.StartedAtUtc ??= DateTime.UtcNow;
                entity.CompletedAtUtc = DateTime.UtcNow;
                break;
            default:
                throw new InvalidOperationException("Unknown task status.");
        }

        entity.Status = status;
        entity.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return await MapSingleAsync(id, currentUserId, canEditAny, cancellationToken);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.FarmTasks.FindAsync([id], cancellationToken);
        if (entity is null) return false;
        _context.FarmTasks.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task ApplyAsync(FarmTask entity, SaveTaskViewModel model, CancellationToken cancellationToken)
    {
        var title = model.Title?.Trim();
        if (string.IsNullOrEmpty(title))
            throw new InvalidOperationException("Enter a task title.");

        var assignee = string.IsNullOrWhiteSpace(model.AssigneeUserId) ? null : model.AssigneeUserId.Trim();
        if (assignee is not null && !await _context.Users.AnyAsync(u => u.Id == assignee, cancellationToken))
            throw new InvalidOperationException("The selected person no longer exists.");

        int? goatId = null;
        if (!string.IsNullOrWhiteSpace(model.GoatTag))
        {
            var tag = model.GoatTag.Trim().ToLower();
            goatId = await _context.Goats.Where(g => g.Tag.ToLower() == tag)
                .Select(g => (int?)g.Id)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException($"No goat with tag \"{model.GoatTag.Trim()}\".");
        }

        if (!Enum.IsDefined(model.Type) || !Enum.IsDefined(model.Priority))
            throw new InvalidOperationException("Invalid task type or priority.");

        entity.Title = title;
        entity.Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim();
        entity.Type = model.Type;
        entity.Priority = model.Priority;
        entity.DueDate = model.DueDate;
        entity.AssigneeUserId = assignee;
        entity.GoatId = goatId;
    }

    private async Task<List<TaskUserOptionViewModel>> GetUserOptionsAsync(CancellationToken cancellationToken) =>
        await _context.Users.AsNoTracking()
            .OrderBy(u => u.FullName ?? u.Email)
            .Select(u => new TaskUserOptionViewModel { Id = u.Id, Name = u.FullName ?? u.Email ?? u.UserName ?? u.Id })
            .ToListAsync(cancellationToken);

    private async Task<TaskViewModel> MapSingleAsync(int id, string currentUserId, bool canEditAny, CancellationToken cancellationToken)
    {
        var entity = await _context.FarmTasks.AsNoTracking().Include(t => t.Goat)
            .FirstAsync(t => t.Id == id, cancellationToken);
        var names = (await GetUserOptionsAsync(cancellationToken)).ToDictionary(u => u.Id, u => u.Name);
        return Map(entity, names, currentUserId, canEditAny);
    }

    private static IEnumerable<FarmTask> ApplyFilter(IEnumerable<FarmTask> tasks, string filter, string currentUserId, DateOnly today) =>
        filter switch
        {
            "mine" => tasks.Where(t => t.AssigneeUserId == currentUserId),
            "urgent" => tasks.Where(t => t.Priority == FarmTaskPriority.Urgent),
            "overdue" => tasks.Where(t => t.Status != FarmTaskStatus.Completed && t.DueDate < today),
            "reminders" => tasks.Where(t => t.Type == FarmTaskType.Reminder),
            _ when filter.StartsWith("user:", StringComparison.Ordinal) => tasks.Where(t => t.AssigneeUserId == filter[5..]),
            _ => tasks
        };

    private static TaskViewModel Map(FarmTask t, IReadOnlyDictionary<string, string> names, string currentUserId, bool canEditAny)
    {
        var today = Today;
        var overdue = t.Status != FarmTaskStatus.Completed && t.DueDate < today;
        var dueText = t.DueDate is null
            ? "no due date"
            : BreedingHelper.DueText(BreedingHelper.DaysUntil(t.DueDate.Value));

        return new TaskViewModel
        {
            Id = t.Id,
            Title = t.Title,
            Notes = t.Notes,
            Type = t.Type,
            Priority = t.Priority,
            Status = t.Status,
            DueDate = t.DueDate,
            AssigneeUserId = t.AssigneeUserId,
            AssigneeName = t.AssigneeUserId is not null && names.TryGetValue(t.AssigneeUserId, out var n) ? n : "Everyone",
            CreatedByName = t.CreatedByUserId is not null && names.TryGetValue(t.CreatedByUserId, out var c) ? c : null,
            GoatTag = t.Goat?.Tag,
            IsOverdue = overdue,
            DueText = dueText,
            IsMine = t.AssigneeUserId == currentUserId,
            CanChangeStatus = canEditAny || t.AssigneeUserId is null || t.AssigneeUserId == currentUserId,
            CompletedDisplay = t.CompletedAtUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
        };
    }
}

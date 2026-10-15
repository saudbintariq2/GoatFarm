using System.ComponentModel.DataAnnotations;
using GoatFarm.Domain.Enums;

namespace GoatFarm.Application.ViewModels.Tasks;

public class TaskViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public FarmTaskType Type { get; set; }
    public FarmTaskPriority Priority { get; set; }
    public FarmTaskStatus Status { get; set; }
    public DateOnly? DueDate { get; set; }
    public string? DueDateDisplay => DueDate?.ToString("yyyy-MM-dd");
    public string? AssigneeUserId { get; set; }
    public string AssigneeName { get; set; } = "Everyone";
    public string? CreatedByName { get; set; }
    public string? GoatTag { get; set; }
    public bool IsOverdue { get; set; }
    public string DueText { get; set; } = string.Empty;
    public bool IsMine { get; set; }
    public bool CanChangeStatus { get; set; }
    public string? CompletedDisplay { get; set; }
}

public class TaskUserOptionViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class TaskKpisViewModel
{
    public int Open { get; set; }
    public int Mine { get; set; }
    public int Urgent { get; set; }
    public int Overdue { get; set; }
    public int CompletedLast30Days { get; set; }
}

public class FarmReminderRowViewModel
{
    public string Title { get; set; } = string.Empty;
    public string DateDisplay { get; set; } = string.Empty;
    public string? ScopeDisplay { get; set; }
}

public class TasksPageViewModel
{
    public string Filter { get; set; } = "all";
    public TaskKpisViewModel Kpis { get; set; } = new();
    public IReadOnlyList<TaskViewModel> ToDo { get; set; } = [];
    public IReadOnlyList<TaskViewModel> InProgress { get; set; } = [];
    public IReadOnlyList<TaskViewModel> Completed { get; set; } = [];
    public IReadOnlyList<TaskUserOptionViewModel> Users { get; set; } = [];
    public IReadOnlyList<FarmReminderRowViewModel> FarmReminders { get; set; } = [];
}

public class SaveTaskViewModel
{
    [Required(ErrorMessage = "Enter a task title")]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Notes { get; set; }

    public FarmTaskType Type { get; set; } = FarmTaskType.Task;

    public FarmTaskPriority Priority { get; set; } = FarmTaskPriority.Normal;

    public DateOnly? DueDate { get; set; }

    /// <summary>Null or empty = everyone.</summary>
    public string? AssigneeUserId { get; set; }

    [StringLength(50)]
    public string? GoatTag { get; set; }
}

public class SetTaskStatusViewModel
{
    public FarmTaskStatus Status { get; set; }
}

namespace GoatFarm.Domain.Enums;

public enum FarmTaskType
{
    Task = 0,
    Reminder = 1
}

public enum FarmTaskPriority
{
    Normal = 0,
    High = 1,
    Urgent = 2
}

public enum FarmTaskStatus
{
    ToDo = 0,
    InProgress = 1,
    Completed = 2
}

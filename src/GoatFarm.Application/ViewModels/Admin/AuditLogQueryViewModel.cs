namespace GoatFarm.Application.ViewModels.Admin;

public class AuditLogQueryViewModel
{
    public string? Module { get; set; }
    public string? UserSearch { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public int Take { get; set; } = 200;
}

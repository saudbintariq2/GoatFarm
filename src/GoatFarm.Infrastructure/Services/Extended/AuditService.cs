using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Admin;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Infrastructure.Services.Extended;

public class AuditService : IAuditService
{
    private readonly GoatFarmDbContext _context;

    public AuditService(GoatFarmDbContext context) => _context = context;

    public async Task LogAsync(string module, AuditAction action, string summary, string? userId, string? userDisplay,
        string? entityType = null, int? entityId = null, string? detailJson = null, bool showInNotices = true,
        CancellationToken cancellationToken = default)
    {
        _context.AuditLogEntries.Add(new AuditLogEntry
        {
            Module = module,
            Action = action,
            Summary = summary.Trim(),
            EntityType = entityType,
            EntityId = entityId,
            DetailJson = detailJson,
            UserId = userId,
            UserDisplay = userDisplay,
            ShowInNotices = showInNotices,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        });
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<AdminPageViewModel> GetAdminPageAsync(int auditTake, CancellationToken cancellationToken = default)
    {
        var pending = await _context.ApprovalRequests.CountAsync(a => a.Status == ApprovalStatus.Pending, cancellationToken);
        var rows = await _context.AuditLogEntries.AsNoTracking()
            .OrderByDescending(a => a.CreatedDate)
            .Take(Math.Clamp(auditTake, 10, 500))
            .ToListAsync(cancellationToken);

        return new AdminPageViewModel
        {
            PendingApprovals = pending,
            AuditLog = rows.Select(MapRow).ToList()
        };
    }

    public async Task<IReadOnlyList<AuditNoticeViewModel>> GetRecentNoticesAsync(int take, CancellationToken cancellationToken = default)
    {
        var rows = await _context.AuditLogEntries.AsNoTracking()
            .Where(a => a.ShowInNotices)
            .OrderByDescending(a => a.CreatedDate)
            .Take(Math.Clamp(take, 1, 50))
            .ToListAsync(cancellationToken);
        return rows.Select(a => new AuditNoticeViewModel
        {
            Id = a.Id,
            Module = a.Module,
            Summary = a.Summary,
            UserDisplay = a.UserDisplay ?? "System",
            WhenDisplay = a.CreatedDate.ToLocalTime().ToString("g")
        }).ToList();
    }

    private static AuditRowViewModel MapRow(AuditLogEntry a) => new()
    {
        Id = a.Id,
        Module = a.Module,
        Action = a.Action.ToString(),
        Summary = a.Summary,
        UserDisplay = a.UserDisplay ?? "System",
        WhenDisplay = a.CreatedDate.ToLocalTime().ToString("g")
    };
}

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
        var rows = await QueryAuditLogAsync(new AuditLogQueryViewModel { Take = auditTake }, cancellationToken);

        return new AdminPageViewModel
        {
            PendingApprovals = pending,
            AuditLog = rows
        };
    }

    public async Task<IReadOnlyList<AuditRowViewModel>> QueryAuditLogAsync(
        AuditLogQueryViewModel query, CancellationToken cancellationToken = default)
    {
        var q = _context.AuditLogEntries.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Module))
            q = q.Where(a => a.Module == query.Module);

        if (!string.IsNullOrWhiteSpace(query.UserSearch))
        {
            var term = query.UserSearch.Trim().ToLowerInvariant();
            q = q.Where(a =>
                (a.UserDisplay != null && a.UserDisplay.ToLower().Contains(term)) ||
                (a.UserId != null && a.UserId == query.UserSearch!.Trim()));
        }

        if (query.From is DateOnly from)
        {
            var fromUtc = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local).ToUniversalTime();
            q = q.Where(a => a.CreatedDate >= fromUtc);
        }

        if (query.To is DateOnly to)
        {
            var toUtc = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Local).ToUniversalTime();
            q = q.Where(a => a.CreatedDate < toUtc);
        }

        var take = Math.Clamp(query.Take, 10, 2000);
        var rows = await q.OrderByDescending(a => a.CreatedDate).Take(take).ToListAsync(cancellationToken);
        return rows.Select(MapRow).ToList();
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

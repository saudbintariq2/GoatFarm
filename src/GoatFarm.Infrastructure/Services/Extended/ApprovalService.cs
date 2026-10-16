using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Approval;
using GoatFarm.Domain.Constants;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Infrastructure.Services.Extended;

public class ApprovalService : IApprovalService
{
    private readonly GoatFarmDbContext _context;
    private readonly IApprovalExecutionService _execution;

    public ApprovalService(GoatFarmDbContext context, IApprovalExecutionService execution)
    {
        _context = context;
        _execution = execution;
    }

    public Task<bool> NeedsApprovalAsync(string userId, string role, string action, CancellationToken cancellationToken = default)
    {
        if (role is FarmRoles.Admin or FarmRoles.Manager)
            return Task.FromResult(false);
        return Task.FromResult(action is FarmActions.Delete or FarmActions.Edit);
    }

    public Task<int> CountPendingAsync(CancellationToken cancellationToken = default) =>
        _context.ApprovalRequests.AsNoTracking()
            .CountAsync(a => a.Status == ApprovalStatus.Pending, cancellationToken);

    public async Task<ApprovalPageViewModel> GetPageAsync(CancellationToken cancellationToken = default)
    {
        var pending = await _context.ApprovalRequests.AsNoTracking()
            .Where(a => a.Status == ApprovalStatus.Pending)
            .OrderByDescending(a => a.CreatedDate)
            .ToListAsync(cancellationToken);
        var recent = await _context.ApprovalRequests.AsNoTracking()
            .Where(a => a.Status != ApprovalStatus.Pending)
            .OrderByDescending(a => a.UpdatedDate)
            .Take(30)
            .ToListAsync(cancellationToken);

        return new ApprovalPageViewModel
        {
            Pending = pending.Select(Map).ToList(),
            Recent = recent.Select(Map).ToList()
        };
    }

    public async Task<ApprovalRequestViewModel?> SubmitAsync(string module, string actionKey, string payloadJson,
        string userId, string? userDisplay, CancellationToken cancellationToken = default)
    {
        var row = new ApprovalRequest
        {
            Module = module,
            ActionKey = actionKey,
            PayloadJson = payloadJson,
            RequestedByUserId = userId,
            RequestedByDisplay = userDisplay,
            Status = ApprovalStatus.Pending,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };
        _context.ApprovalRequests.Add(row);
        await _context.SaveChangesAsync(cancellationToken);
        return Map(row);
    }

    public async Task<bool> ReviewAsync(int id, bool approve, string reviewerId, string? reviewerDisplay, string? note,
        CancellationToken cancellationToken = default)
    {
        var row = await _context.ApprovalRequests.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (row is null || row.Status != ApprovalStatus.Pending) return false;

        if (approve)
        {
            var (ok, error) = await _execution.ExecuteAsync(row, cancellationToken);
            if (!ok)
            {
                row.ReviewNote = error ?? "Execution failed.";
                row.Status = ApprovalStatus.Rejected;
                row.ReviewedByUserId = reviewerId;
                row.ReviewedAtUtc = DateTime.UtcNow;
                row.UpdatedDate = DateTime.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);
                return false;
            }
        }

        row.Status = approve ? ApprovalStatus.Approved : ApprovalStatus.Rejected;
        row.ReviewedByUserId = reviewerId;
        row.ReviewNote = note;
        row.ReviewedAtUtc = DateTime.UtcNow;
        row.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static ApprovalRequestViewModel Map(ApprovalRequest a) => new()
    {
        Id = a.Id,
        Module = a.Module,
        ActionKey = a.ActionKey,
        Status = a.Status.ToString(),
        RequestedByDisplay = a.RequestedByDisplay ?? a.RequestedByUserId,
        WhenDisplay = a.CreatedDate.ToLocalTime().ToString("g"),
        PayloadJson = a.PayloadJson
    };
}

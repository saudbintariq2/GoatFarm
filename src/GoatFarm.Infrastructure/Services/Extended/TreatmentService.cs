using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Treatments;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Infrastructure.Services.Extended;

public class TreatmentService : ITreatmentService
{
    private readonly GoatFarmDbContext _context;

    public TreatmentService(GoatFarmDbContext context) => _context = context;

    public async Task<TreatmentsPageViewModel> GetPageAsync(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var rows = await _context.GoatTreatments.AsNoTracking().Include(t => t.Goat)
            .OrderByDescending(t => t.StartDate).Take(200).ToListAsync(cancellationToken);

        var active = rows.Where(t => t.EndDate is null || t.EndDate >= today).Select(Map).ToList();
        var history = rows.Where(t => t.EndDate is not null && t.EndDate < today).Select(Map).ToList();

        return new TreatmentsPageViewModel { Active = active, History = history };
    }

    public async Task<object> SaveAsync(SaveTreatmentViewModel model, CancellationToken cancellationToken = default)
    {
        GoatTreatment row;
        if (model.Id is > 0)
            row = await _context.GoatTreatments.FirstAsync(t => t.Id == model.Id, cancellationToken);
        else { row = new GoatTreatment(); _context.GoatTreatments.Add(row); }

        row.GoatId = model.GoatId;
        row.StartDate = model.StartDate;
        row.EndDate = model.EndDate;
        row.Diagnosis = model.Diagnosis.Trim();
        row.Medicine = model.Medicine.Trim();
        row.Cost = model.Cost;
        row.WithdrawalDays = model.WithdrawalDays;
        row.Notes = model.Notes;
        row.UpdatedDate = DateTime.UtcNow;

        var goat = await _context.Goats.FirstAsync(g => g.Id == model.GoatId, cancellationToken);
        goat.HealthState = model.EndDate is null || model.EndDate >= DateOnly.FromDateTime(DateTime.Today)
            ? GoatHealthState.UnderTreatment
            : GoatHealthState.Healthy;
        goat.UpdatedDate = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return new { id = row.Id };
    }

    public async Task<bool> RecoverAsync(int treatmentId, CancellationToken cancellationToken = default)
    {
        var row = await _context.GoatTreatments.Include(t => t.Goat)
            .FirstOrDefaultAsync(t => t.Id == treatmentId, cancellationToken);
        if (row is null) return false;

        var today = DateOnly.FromDateTime(DateTime.Today);
        row.EndDate = today;
        row.UpdatedDate = DateTime.UtcNow;

        var goat = row.Goat;
        goat.HealthState = GoatHealthState.Healthy;
        goat.UpdatedDate = DateTime.UtcNow;

        var stillOpen = await _context.GoatTreatments.AnyAsync(t =>
            t.GoatId == goat.Id && t.Id != row.Id &&
            (t.EndDate == null || t.EndDate >= today), cancellationToken);
        if (stillOpen)
            goat.HealthState = GoatHealthState.UnderTreatment;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var row = await _context.GoatTreatments.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (row is null) return false;
        _context.GoatTreatments.Remove(row);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static TreatmentRowViewModel Map(GoatTreatment t) => new()
    {
        Id = t.Id,
        GoatId = t.GoatId,
        Tag = t.Goat.Tag,
        Diagnosis = t.Diagnosis,
        Medicine = t.Medicine,
        StartDate = t.StartDate.ToString("yyyy-MM-dd"),
        EndDate = t.EndDate?.ToString("yyyy-MM-dd"),
        Cost = t.Cost,
        WithdrawalDays = t.WithdrawalDays
    };
}

using GoatFarm.Application.Common;
using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Treatments;
using GoatFarm.Domain.Constants;
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

    public Task<object> SaveAsync(SaveTreatmentViewModel model, CancellationToken cancellationToken = default) =>
        _context.ExecuteInTransactionAsync(() => SaveCoreAsync(model, cancellationToken), cancellationToken);

    private async Task<object> SaveCoreAsync(SaveTreatmentViewModel model, CancellationToken cancellationToken)
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
        if (DisplayHelper.TryParseHealthLabel(model.HealthStateLabel, out var health))
            goat.HealthState = health;
        else if (model.EndDate is null || model.EndDate >= DateOnly.FromDateTime(DateTime.Today))
            goat.HealthState = GoatHealthState.UnderTreatment;
        else
            goat.HealthState = GoatHealthState.Healthy;
        goat.UpdatedDate = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        var existing = await _context.Expenses.FirstOrDefaultAsync(
            e => e.SourceType == FinanceSourceTypes.Treatment && e.SourceId == row.Id, cancellationToken);
        var existingRef = existing is null ? null : $"fin-expense:{existing.Id}";
        var existingTx = existingRef is null ? null
            : await _context.LedgerTransactions.FirstOrDefaultAsync(t => t.Reference == existingRef, cancellationToken);

        if (model.Cost > 0)
        {
            if (model.AccountId is null or <= 0)
                throw new InvalidOperationException("Choose the Cash/Bank account the medicine was paid from.");

            var account = await _context.MoneyAccounts.FirstOrDefaultAsync(
                a => a.Id == model.AccountId && a.IsActive, cancellationToken)
                ?? throw new InvalidOperationException("Selected account was not found. Add accounts on Finance → Bank & Cash.");

            var note = goat.Tag + " — " + (string.IsNullOrWhiteSpace(row.Diagnosis) ? "treatment" : row.Diagnosis.Trim());
            var expense = existing ?? new Expense
            {
                Type = "Vet / extra medicine",
                SourceType = FinanceSourceTypes.Treatment,
                SourceId = row.Id,
                CreatedDate = DateTime.UtcNow
            };
            expense.Amount = model.Cost;
            expense.Date = model.StartDate;
            expense.Comment = note;
            expense.UpdatedDate = DateTime.UtcNow;
            if (existing is null) _context.Expenses.Add(expense);
            await _context.SaveChangesAsync(cancellationToken);

            var tx = existingTx ?? new LedgerTransaction { Reference = $"fin-expense:{expense.Id}", CreatedDate = DateTime.UtcNow };
            tx.Date = model.StartDate;
            tx.Kind = LedgerTransactionKind.Expense;
            tx.AccountId = account.Id;
            tx.Amount = model.Cost;
            tx.Category = "Vet / extra medicine";
            tx.Notes = "Expense · Vet / extra medicine · " + goat.Tag;
            tx.UpdatedDate = DateTime.UtcNow;
            if (existingTx is null) _context.LedgerTransactions.Add(tx);
            account.OpeningBalanceLocked = true;
            await _context.SaveChangesAsync(cancellationToken);
            return new { id = row.Id };
        }

        if (existing is not null) _context.Expenses.Remove(existing);
        if (existingTx is not null) _context.LedgerTransactions.Remove(existingTx);
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

using GoatFarm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Infrastructure.Services;

public class BackupService : Application.Interfaces.IBackupService
{
    private readonly GoatFarmDbContext _context;

    public BackupService(GoatFarmDbContext context) => _context = context;

    public async Task<object> ExportAsync(CancellationToken cancellationToken = default)
    {
        return new
        {
            goats = await _context.Goats.AsNoTracking().ToListAsync(cancellationToken),
            groups = await _context.GoatGroups.AsNoTracking().Select(g => g.Name).ToListAsync(cancellationToken),
            prices = await _context.FeedPrices.AsNoTracking().ToDictionaryAsync(p => p.FeedType, p => p.PricePerKg, cancellationToken),
            feedBuys = await _context.FeedPurchases.AsNoTracking().ToListAsync(cancellationToken),
            recurringCosts = await _context.RecurringCosts.AsNoTracking().ToListAsync(cancellationToken),
            vaccineBuys = await _context.VaccinePurchases.AsNoTracking().ToListAsync(cancellationToken),
            assets = await _context.Assets.AsNoTracking().ToListAsync(cancellationToken),
            incomes = await _context.Incomes.AsNoTracking().ToListAsync(cancellationToken),
            expenses = await _context.Expenses.AsNoTracking().ToListAsync(cancellationToken),
            ownerInv = await _context.OwnerInvestments.AsNoTracking().ToListAsync(cancellationToken),
            vaccines = await _context.Vaccines.AsNoTracking().ToListAsync(cancellationToken),
            vaccLog = await _context.VaccinationHistories.AsNoTracking().ToListAsync(cancellationToken),
            reminders = await _context.Reminders.AsNoTracking().ToListAsync(cancellationToken),
            milkProd = await _context.MilkProductions.AsNoTracking().ToListAsync(cancellationToken),
            milkSales = await _context.MilkSales.AsNoTracking().ToListAsync(cancellationToken),
            milkWastes = await _context.MilkWastes.AsNoTracking().ToListAsync(cancellationToken),
            lookupSettings = await _context.AppSettings.AsNoTracking()
                .Where(s => s.Key.StartsWith("Lookup."))
                .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken),
            _savedAt = DateTime.UtcNow
        };
    }

    public Task ImportAsync(string json, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Restore via file upload is not yet implemented. Use database backup tools for SQL Server.");
}

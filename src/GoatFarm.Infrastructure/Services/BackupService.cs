using System.Text.Json;
using GoatFarm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Infrastructure.Services;

public class BackupService : Application.Interfaces.IBackupService
{
    private readonly GoatFarmDbContext _context;

    public BackupService(GoatFarmDbContext context) => _context = context;

    public async Task<object> ExportAsync(CancellationToken cancellationToken = default)
    {
        var remindDaysSetting = await _context.AppSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == "RemindDays", cancellationToken);

        return new
        {
            goats = await _context.Goats.AsNoTracking().Include(g => g.Group).ToListAsync(cancellationToken),
            groups = await _context.GoatGroups.AsNoTracking().Select(g => g.Name).ToListAsync(cancellationToken),
            feedPrices = await _context.FeedPrices.AsNoTracking().ToListAsync(cancellationToken),
            prices = await _context.FeedPrices.AsNoTracking().ToDictionaryAsync(p => p.FeedType, p => p.PricePerKg, cancellationToken),
            feedStock = await _context.FeedPrices.AsNoTracking().ToDictionaryAsync(p => p.FeedType, p => p.StockKg, cancellationToken),
            feedPlans = await _context.FeedPlans.AsNoTracking().Include(p => p.Items).ToListAsync(cancellationToken),
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
            remindDays = remindDaysSetting is not null && int.TryParse(remindDaysSetting.Value, out var days) ? days : 30,
            milkProd = await _context.MilkProductions.AsNoTracking().ToListAsync(cancellationToken),
            milkSales = await _context.MilkSales.AsNoTracking().ToListAsync(cancellationToken),
            milkWastes = await _context.MilkWastes.AsNoTracking().ToListAsync(cancellationToken),
            lookupSettings = await _context.AppSettings.AsNoTracking()
                .Where(s => s.Key.StartsWith("Lookup."))
                .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken),
            _savedAt = DateTime.UtcNow
        };
    }

    public async Task ImportAsync(string json, CancellationToken cancellationToken = default)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await BackupImportHelper.ClearFarmDataAsync(_context, cancellationToken);
            await BackupImportHelper.ImportAsync(_context, root, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

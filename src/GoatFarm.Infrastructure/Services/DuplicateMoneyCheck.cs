using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Infrastructure.Services;

public record MoneyDuplicateCandidate(string Tab, string What, DateOnly Date, decimal Amount, string? Party, int Id);

/// <summary>FN16–FN17 — cross-module duplicate amount/date warnings.</summary>
public static class MoneyDuplicateTabs
{
    public const string FeedPurchase = "Feed › Purchases";
    public const string VaccinePurchase = "Health › Vaccine purchase";
    public const string Expense = "Finance › Running costs";
    public const string SupplierBill = "Finance › Supplier bills";
    public const string Income = "Finance › Income";
    public const string MilkSale = "Milk › Sales";
    public const string ProductSale = "Products › Sales";
    public const string GoatSale = "Herd › Goat sale";
    public const string OwnerFunding = "Finance › Capital & funding";
    public const string LedgerManual = "Finance › Bank & Cash";
}

public class DuplicateMoneyCheck
{
    private readonly GoatFarmDbContext _db;

    public DuplicateMoneyCheck(GoatFarmDbContext db) => _db = db;

    public Task<MoneyDuplicateCandidate?> FindCostAsync(
        DateOnly date,
        decimal amount,
        string? party,
        int? ignoreId = null,
        string? ignoreTab = null,
        CancellationToken cancellationToken = default) =>
        FindDuplicateAsync(date, amount, party, ignoreTab, ignoreId, cancellationToken);

    public async Task<MoneyDuplicateCandidate?> FindDuplicateAsync(
        DateOnly date,
        decimal amount,
        string? party,
        string? ignoreTab = null,
        int? ignoreId = null,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0) return null;
        var from = date.AddDays(-3);
        var to = date.AddDays(3);
        var tol = Math.Max(1m, amount * 0.01m);
        var items = new List<MoneyDuplicateCandidate>();

        var feedNames = await _db.FeedPrices.AsNoTracking().ToDictionaryAsync(f => f.FeedType, f => f.DisplayName, cancellationToken);
        var purchases = await _db.FeedPurchases.AsNoTracking()
            .Where(x => x.Date >= from && x.Date <= to)
            .ToListAsync(cancellationToken);
        items.AddRange(purchases.Select(p => new MoneyDuplicateCandidate(
            MoneyDuplicateTabs.FeedPurchase,
            $"{feedNames.GetValueOrDefault(p.FeedType, p.FeedType)} {p.Kg:N0} kg",
            p.Date,
            p.Amount,
            p.Supplier,
            p.Id)));

        items.AddRange(await _db.VaccinePurchases.AsNoTracking()
            .Where(x => x.Date >= from && x.Date <= to)
            .Select(x => new MoneyDuplicateCandidate(
                MoneyDuplicateTabs.VaccinePurchase,
                x.Name + " " + x.Qty + " " + x.Unit,
                x.Date,
                x.Amount,
                x.Supplier,
                x.Id))
            .ToListAsync(cancellationToken));

        items.AddRange(await _db.Expenses.AsNoTracking()
            .Where(x => x.Date >= from && x.Date <= to)
            .Select(x => new MoneyDuplicateCandidate(
                MoneyDuplicateTabs.Expense,
                x.Type + (string.IsNullOrWhiteSpace(x.Comment) ? "" : " " + x.Comment),
                x.Date,
                x.Amount,
                null,
                x.Id))
            .ToListAsync(cancellationToken));

        items.AddRange(await _db.SupplierPayables.AsNoTracking()
            .Where(x => x.Date >= from && x.Date <= to)
            .Select(x => new MoneyDuplicateCandidate(
                MoneyDuplicateTabs.SupplierBill,
                x.Description,
                x.Date,
                x.Amount,
                x.SupplierName,
                x.Id))
            .ToListAsync(cancellationToken));

        items.AddRange(await _db.Incomes.AsNoTracking()
            .Where(x => x.Date >= from && x.Date <= to)
            .Select(x => new MoneyDuplicateCandidate(
                MoneyDuplicateTabs.Income,
                x.Type + (string.IsNullOrWhiteSpace(x.Comment) ? "" : " " + x.Comment),
                x.Date,
                x.Amount,
                null,
                x.Id))
            .ToListAsync(cancellationToken));

        items.AddRange(await _db.MilkSales.AsNoTracking()
            .Where(x => x.Date >= from && x.Date <= to)
            .Select(x => new MoneyDuplicateCandidate(
                MoneyDuplicateTabs.MilkSale,
                $"Milk {x.Liters:N1} L @ {x.Rate:N0}",
                x.Date,
                x.Amount,
                null,
                x.Id))
            .ToListAsync(cancellationToken));

        items.AddRange(await _db.ProductSales.AsNoTracking()
            .Where(x => x.Date >= from && x.Date <= to)
            .Select(x => new MoneyDuplicateCandidate(
                MoneyDuplicateTabs.ProductSale,
                x.ProductName,
                x.Date,
                x.Amount,
                null,
                x.Id))
            .ToListAsync(cancellationToken));

        items.AddRange(await _db.GoatSales.AsNoTracking()
            .Where(x => x.Date >= from && x.Date <= to)
            .Select(x => new MoneyDuplicateCandidate(
                MoneyDuplicateTabs.GoatSale,
                "Goat " + x.Tag,
                x.Date,
                x.Price,
                x.Buyer,
                x.Id))
            .ToListAsync(cancellationToken));

        items.AddRange(await _db.OwnerInvestments.AsNoTracking()
            .Where(x => x.Date >= from && x.Date <= to)
            .Select(x => new MoneyDuplicateCandidate(
                MoneyDuplicateTabs.OwnerFunding,
                x.Note,
                x.Date,
                x.Amount,
                null,
                x.Id))
            .ToListAsync(cancellationToken));

        var manualLedger = await _db.LedgerTransactions.AsNoTracking()
            .Where(x => x.Date >= from && x.Date <= to)
            .Where(x => x.Reference == null || x.Reference == "")
            .ToListAsync(cancellationToken);
        items.AddRange(manualLedger.Select(t =>
        {
            var what = string.IsNullOrWhiteSpace(t.Category) ? t.Kind.ToString() : $"{t.Kind} · {t.Category}";
            if (!string.IsNullOrWhiteSpace(t.Notes))
                what += " — " + t.Notes.Trim();
            return new MoneyDuplicateCandidate(
                MoneyDuplicateTabs.LedgerManual,
                what,
                t.Date,
                t.Amount,
                null,
                t.Id);
        }));

        return items
            .Where(x => ignoreTab is null || ignoreId is null || !(x.Tab == ignoreTab && x.Id == ignoreId))
            .Where(x => Math.Abs(x.Amount - amount) <= tol)
            .OrderBy(x => party != null && x.Party != null && x.Party.Equals(party, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(x => Math.Abs(x.Date.DayNumber - date.DayNumber))
            .FirstOrDefault();
    }

    public static string ConflictMessage(MoneyDuplicateCandidate m) =>
        $"Already recorded in {m.Tab} on {m.Date:dd MMM yyyy}: {m.What} – Rs {m.Amount:N0}" +
        (m.Party is not null ? $" ({m.Party})." : ".");
}
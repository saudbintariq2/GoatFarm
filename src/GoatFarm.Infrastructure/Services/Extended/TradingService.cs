using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Trading;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Infrastructure.Services.Extended;

public class TradingService : ITradingService
{
    private readonly GoatFarmDbContext _context;

    public TradingService(GoatFarmDbContext context) => _context = context;

    public async Task EnsureSeededAsync(CancellationToken cancellationToken = default)
    {
        if (await _context.TradeItems.AnyAsync(cancellationToken)) return;

        _context.TradeItems.Add(new TradeItem
        {
            Name = "Moongphali bhoosa (groundnut straw)",
            Unit = "kg",
            UnitNote = "40 kg = 1 maund",
            DefaultPrice = 0,
            IsActive = true
        });
        _context.TradeStores.Add(new TradeStore { Name = "Trading Godown", IsActive = true });
        _context.TradeAccounts.Add(new TradeAccount { Name = "Trading Cash in Hand", Type = TradeAccountType.Cash, OpeningBalance = 0, IsActive = true });
        _context.TradeAccounts.Add(new TradeAccount { Name = "Trading Bank Account", Type = TradeAccountType.Bank, OpeningBalance = 0, IsActive = true });
        foreach (var name in new[] { "Godown rent", "Labour / mazdoori", "Transport / kiraya", "Phone & travel", "Other" })
            _context.TradeExpenseCategories.Add(new TradeExpenseCategory { Name = name, IsActive = true });
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<TradingPageViewModel> GetPageAsync(string? month, CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken);
        var m = ParseMonth(month);
        var monthStart = new DateOnly(m.Year, m.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        var items = await _context.TradeItems.AsNoTracking().OrderBy(i => i.Name).ToListAsync(cancellationToken);
        var stores = await _context.TradeStores.AsNoTracking().OrderBy(s => s.Name).ToListAsync(cancellationToken);
        var accounts = await _context.TradeAccounts.AsNoTracking().OrderBy(a => a.Name).ToListAsync(cancellationToken);
        var parties = await _context.TradeParties.AsNoTracking().OrderBy(p => p.Name).ToListAsync(cancellationToken);
        var cats = await _context.TradeExpenseCategories.AsNoTracking().OrderBy(c => c.Name).ToListAsync(cancellationToken);
        var entries = await _context.TradeEntries.AsNoTracking()
            .Include(e => e.Item).Include(e => e.Store).Include(e => e.Party).Include(e => e.Account).Include(e => e.Category)
            .Where(e => !e.IsCancelled)
            .OrderByDescending(e => e.Date).ThenByDescending(e => e.Id)
            .Take(200)
            .ToListAsync(cancellationToken);

        var balances = ComputeAccountBalances(accounts, entries);
        var stockValue = ComputeStockValue(items, stores, entries);
        var monthEntries = entries.Where(e => e.Date >= monthStart && e.Date <= monthEnd).ToList();
        var monthSales = monthEntries.Where(e => e.Type == TradeEntryType.Sell).Sum(e => e.Amount);
        var monthBuys = monthEntries.Where(e => e.Type == TradeEntryType.Buy).Sum(e => e.Amount);
        var monthExp = monthEntries.Where(e => e.Type == TradeEntryType.Expense).Sum(e => e.Amount);

        var periodPnL = new List<TradePeriodPnLRowViewModel>();
        for (var i = 0; i < 6; i++)
        {
            var anchor = monthStart.AddMonths(-i);
            var start = new DateOnly(anchor.Year, anchor.Month, 1);
            var end = start.AddMonths(1).AddDays(-1);
            var slice = entries.Where(e => e.Date >= start && e.Date <= end).ToList();
            var sales = slice.Where(e => e.Type == TradeEntryType.Sell).Sum(e => e.Amount);
            var buys = slice.Where(e => e.Type == TradeEntryType.Buy).Sum(e => e.Amount);
            var exp = slice.Where(e => e.Type == TradeEntryType.Expense).Sum(e => e.Amount);
            periodPnL.Add(new TradePeriodPnLRowViewModel
            {
                Month = $"{start.Year:0000}-{start.Month:00}",
                Sales = sales,
                Buys = buys,
                Expenses = exp,
                Profit = sales - buys - exp
            });
        }

        return new TradingPageViewModel
        {
            Month = $"{m.Year:0000}-{m.Month:00}",
            StockValue = stockValue,
            CashBalance = balances.Values.Sum(),
            MonthSales = monthSales,
            MonthBuys = monthBuys,
            MonthExpenses = monthExp,
            MonthProfit = monthSales - monthBuys - monthExp,
            PeriodPnL = periodPnL,
            Items = items.Select(i => new TradeMasterRowViewModel { Id = i.Id, Name = i.Name, Extra = i.Unit, IsActive = i.IsActive }).ToList(),
            Stores = stores.Select(s => new TradeMasterRowViewModel { Id = s.Id, Name = s.Name, IsActive = s.IsActive }).ToList(),
            Accounts = accounts.Select(a => new TradeAccountRowViewModel
            {
                Id = a.Id, Name = a.Name, Type = a.Type.ToString(), OpeningBalance = a.OpeningBalance,
                Balance = balances.GetValueOrDefault(a.Id, a.OpeningBalance), IsActive = a.IsActive
            }).ToList(),
            Parties = parties.Select(p => new TradeMasterRowViewModel { Id = p.Id, Name = p.Name, Extra = p.Role.ToString(), IsActive = p.IsActive }).ToList(),
            Categories = cats.Select(c => new TradeMasterRowViewModel { Id = c.Id, Name = c.Name, IsActive = c.IsActive }).ToList(),
            Entries = entries.Select(e => new TradeEntryRowViewModel
            {
                Id = e.Id,
                Type = e.Type.ToString(),
                DateDisplay = e.Date.ToString("yyyy-MM-dd"),
                Label = EntryLabel(e),
                Qty = e.Qty,
                Amount = e.Amount,
                Notes = e.Notes
            }).ToList()
        };
    }

    public async Task<object> SaveMasterAsync(SaveTradeMasterViewModel model, CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken);
        switch (model.Kind.ToLowerInvariant())
        {
            case "item":
            {
                TradeItem row;
                if (model.Id is > 0)
                    row = await _context.TradeItems.FirstAsync(i => i.Id == model.Id, cancellationToken);
                else
                {
                    row = new TradeItem();
                    _context.TradeItems.Add(row);
                }
                row.Name = model.Name.Trim();
                row.Unit = string.IsNullOrWhiteSpace(model.Unit) ? "kg" : model.Unit.Trim();
                row.IsActive = model.IsActive;
                row.UpdatedDate = DateTime.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);
                return new { id = row.Id };
            }
            case "store":
            {
                TradeStore row;
                if (model.Id is > 0)
                    row = await _context.TradeStores.FirstAsync(i => i.Id == model.Id, cancellationToken);
                else { row = new TradeStore(); _context.TradeStores.Add(row); }
                row.Name = model.Name.Trim();
                row.IsActive = model.IsActive;
                row.UpdatedDate = DateTime.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);
                return new { id = row.Id };
            }
            case "account":
            {
                TradeAccount row;
                if (model.Id is > 0)
                    row = await _context.TradeAccounts.FirstAsync(i => i.Id == model.Id, cancellationToken);
                else { row = new TradeAccount(); _context.TradeAccounts.Add(row); }
                row.Name = model.Name.Trim();
                row.Type = Enum.TryParse<TradeAccountType>(model.Type, true, out var t) ? t : TradeAccountType.Cash;
                if (model.Id is null or 0) row.OpeningBalance = model.OpeningBalance;
                row.IsActive = model.IsActive;
                row.UpdatedDate = DateTime.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);
                return new { id = row.Id };
            }
            case "party":
            {
                TradeParty row;
                if (model.Id is > 0)
                    row = await _context.TradeParties.FirstAsync(i => i.Id == model.Id, cancellationToken);
                else { row = new TradeParty(); _context.TradeParties.Add(row); }
                row.Name = model.Name.Trim();
                row.Role = TradePartyRole.Both;
                row.IsActive = model.IsActive;
                row.UpdatedDate = DateTime.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);
                return new { id = row.Id };
            }
            case "category":
            {
                TradeExpenseCategory row;
                if (model.Id is > 0)
                    row = await _context.TradeExpenseCategories.FirstAsync(i => i.Id == model.Id, cancellationToken);
                else { row = new TradeExpenseCategory(); _context.TradeExpenseCategories.Add(row); }
                row.Name = model.Name.Trim();
                row.IsActive = model.IsActive;
                row.UpdatedDate = DateTime.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);
                return new { id = row.Id };
            }
            default:
                throw new InvalidOperationException("Unknown master kind.");
        }
    }

    public async Task<object> SaveEntryAsync(SaveTradeEntryViewModel model, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<TradeEntryType>(model.Type, true, out var type))
            throw new InvalidOperationException("Invalid entry type.");

        var amount = model.Amount > 0 ? model.Amount : model.Qty * model.Rate;
        if (type is TradeEntryType.Sell or TradeEntryType.Transfer)
        {
            var check = await ValidateStockAsync(type, model.Date, model.ItemId, model.StoreId, model.ToStoreId, model.Qty, model.Id, cancellationToken);
            if (!string.IsNullOrEmpty(check))
                throw new InvalidOperationException(check);
        }

        TradeEntry row;
        if (model.Id is > 0)
            row = await _context.TradeEntries.FirstAsync(e => e.Id == model.Id, cancellationToken);
        else
        {
            row = new TradeEntry();
            _context.TradeEntries.Add(row);
        }

        row.Type = type;
        row.Date = model.Date;
        row.ItemId = model.ItemId;
        row.StoreId = model.StoreId;
        row.ToStoreId = model.ToStoreId;
        row.PartyId = model.PartyId;
        row.AccountId = model.AccountId;
        row.CategoryId = model.CategoryId;
        row.Qty = model.Qty;
        row.Rate = model.Rate;
        row.Amount = amount;
        row.Notes = model.Notes;
        row.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return new { id = row.Id };
    }

    public async Task<bool> DeleteEntryAsync(int id, CancellationToken cancellationToken = default)
    {
        var row = await _context.TradeEntries.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (row is null) return false;
        row.IsCancelled = true;
        row.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string EntryLabel(TradeEntry e) => e.Type switch
    {
        TradeEntryType.Buy => $"{e.Item?.Name ?? "Item"} → {e.Store?.Name ?? "Store"}",
        TradeEntryType.Sell => $"{e.Item?.Name ?? "Item"} from {e.Store?.Name ?? "Store"}",
        TradeEntryType.Expense => e.Category?.Name ?? "Expense",
        TradeEntryType.PaymentIn or TradeEntryType.PaymentOut => e.Party?.Name ?? e.Account?.Name ?? "Payment",
        TradeEntryType.Transfer => $"{e.Store?.Name} → {e.ToStore?.Name}",
        _ => e.Type.ToString()
    };

    private static Dictionary<int, decimal> ComputeAccountBalances(IReadOnlyList<TradeAccount> accounts, IReadOnlyList<TradeEntry> entries)
    {
        var map = accounts.ToDictionary(a => a.Id, a => a.OpeningBalance);
        foreach (var e in entries.OrderBy(x => x.Date).ThenBy(x => x.Id))
        {
            if (e.AccountId is null) continue;
            if (!map.ContainsKey(e.AccountId.Value)) continue;
            map[e.AccountId.Value] += e.Type switch
            {
                TradeEntryType.Sell or TradeEntryType.PaymentIn => e.Amount,
                TradeEntryType.Buy or TradeEntryType.Expense or TradeEntryType.PaymentOut => -e.Amount,
                _ => 0
            };
        }
        return map;
    }

    private static decimal ComputeStockValue(IReadOnlyList<TradeItem> items, IReadOnlyList<TradeStore> stores, IReadOnlyList<TradeEntry> entries)
    {
        var qty = new Dictionary<(int item, int store), decimal>();
        foreach (var e in entries.OrderBy(x => x.Date).ThenBy(x => x.Id))
        {
            if (e.ItemId is null or 0 || e.StoreId is null or 0) continue;
            var key = (e.ItemId.Value, e.StoreId.Value);
            qty.TryAdd(key, 0);
            qty[key] += e.Type switch
            {
                TradeEntryType.Buy => e.Qty,
                TradeEntryType.Sell => -e.Qty,
                TradeEntryType.Transfer when e.ToStoreId is > 0 => -e.Qty,
                _ => 0
            };
            if (e.Type == TradeEntryType.Transfer && e.ToStoreId is > 0 && e.ItemId is > 0)
            {
                var key2 = (e.ItemId.Value, e.ToStoreId.Value);
                qty.TryAdd(key2, 0);
                qty[key2] += e.Qty;
            }
        }

        var priceMap = items.ToDictionary(i => i.Id, i => i.DefaultPrice);
        decimal total = 0;
        foreach (var ((itemId, _), q) in qty)
        {
            if (q <= 0) continue;
            total += q * priceMap.GetValueOrDefault(itemId, 0);
        }
        return total;
    }

    private async Task<string?> ValidateStockAsync(
        TradeEntryType type, DateOnly date, int? itemId, int? storeId, int? toStoreId, decimal qty, int? excludeEntryId,
        CancellationToken cancellationToken)
    {
        if (itemId is null or 0 || storeId is null or 0 || qty <= 0) return null;

        var items = await _context.TradeItems.AsNoTracking().ToListAsync(cancellationToken);
        var stores = await _context.TradeStores.AsNoTracking().ToListAsync(cancellationToken);
        var entries = await _context.TradeEntries.AsNoTracking()
            .Where(e => !e.IsCancelled && (excludeEntryId == null || e.Id != excludeEntryId))
            .ToListAsync(cancellationToken);

        var hypothetical = new TradeEntry
        {
            Type = type,
            Date = date,
            ItemId = itemId,
            StoreId = storeId,
            ToStoreId = toStoreId,
            Qty = qty
        };
        entries.Add(hypothetical);

        var qtyMap = new Dictionary<(int item, int store), decimal>();
        foreach (var e in entries.OrderBy(x => x.Date).ThenBy(x => x.Id))
        {
            if (e.ItemId is null or 0 || e.StoreId is null or 0) continue;
            var key = (e.ItemId.Value, e.StoreId.Value);
            qtyMap.TryAdd(key, 0);
            qtyMap[key] += e.Type switch
            {
                TradeEntryType.Buy => e.Qty,
                TradeEntryType.Sell => -e.Qty,
                TradeEntryType.Transfer => -e.Qty,
                _ => 0
            };
            if (e.Type == TradeEntryType.Transfer && e.ToStoreId is > 0 && e.ItemId is > 0)
            {
                var key2 = (e.ItemId.Value, e.ToStoreId.Value);
                qtyMap.TryAdd(key2, 0);
                qtyMap[key2] += e.Qty;
            }
        }

        var keyCheck = (itemId.Value, storeId.Value);
        if (qtyMap.TryGetValue(keyCheck, out var left) && left < 0)
        {
            var itemName = items.FirstOrDefault(i => i.Id == itemId)?.Name ?? "Item";
            var storeName = stores.FirstOrDefault(s => s.Id == storeId)?.Name ?? "Store";
            return $"Not enough {itemName} in {storeName} (would go to {left:N2}).";
        }

        return null;
    }

    private static DateTime ParseMonth(string? month)
    {
        if (!string.IsNullOrWhiteSpace(month) && DateTime.TryParse(month + "-01", out var parsed))
            return parsed;
        return DateTime.Today;
    }
}

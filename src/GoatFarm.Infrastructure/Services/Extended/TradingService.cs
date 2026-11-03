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

        var allEntries = await _context.TradeEntries.AsNoTracking()

            .Include(e => e.Item).Include(e => e.Store).Include(e => e.Party).Include(e => e.Account).Include(e => e.Category)

            .OrderByDescending(e => e.Date).ThenByDescending(e => e.Id)

            .ToListAsync(cancellationToken);

        var entries = allEntries.Where(e => !e.IsCancelled).OrderByDescending(e => e.Date).ThenByDescending(e => e.Id).Take(200).ToList();



        var balances = ComputeAccountBalances(accounts, allEntries.Where(e => !e.IsCancelled).ToList());

        var stockValue = ComputeStockValue(items, stores, allEntries.Where(e => !e.IsCancelled).ToList());

        var monthEntries = entries.Where(e => e.Date >= monthStart && e.Date <= monthEnd).ToList();

        var monthSales = monthEntries.Where(e => e.Type == TradeEntryType.Sell).Sum(e => e.Qty * e.Rate);

        var monthBuys = monthEntries.Where(e => e.Type == TradeEntryType.Buy).Sum(e => e.Qty * e.Rate + e.Freight + e.Labour);

        var monthExp = monthEntries.Where(e => e.Type == TradeEntryType.Expense).Sum(e => e.Amount);



        var periodPnL = new List<TradePeriodPnLRowViewModel>();

        for (var i = 0; i < 6; i++)

        {

            var anchor = monthStart.AddMonths(-i);

            var start = new DateOnly(anchor.Year, anchor.Month, 1);

            var end = start.AddMonths(1).AddDays(-1);

            var slice = allEntries.Where(e => !e.IsCancelled && e.Date >= start && e.Date <= end).ToList();

            var sales = slice.Where(e => e.Type == TradeEntryType.Sell).Sum(e => e.Qty * e.Rate);

            var buys = slice.Where(e => e.Type == TradeEntryType.Buy).Sum(e => e.Qty * e.Rate + e.Freight + e.Labour);

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

            }).ToList(),

            TradeRecs = TradeRecsMapper.Build(items, stores, accounts, parties, cats, allEntries)

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

                row.UnitNote = model.UnitNote;

                row.DefaultPrice = model.DefaultPrice;

                row.Notes = model.Notes;

                row.IsActive = model.IsActive;

                row.UpdatedDate = DateTime.UtcNow;

                await _context.SaveChangesAsync(cancellationToken);

                return new { id = row.Id.ToString() };

            }

            case "store":

            {

                TradeStore row;

                if (model.Id is > 0)

                    row = await _context.TradeStores.FirstAsync(i => i.Id == model.Id, cancellationToken);

                else { row = new TradeStore(); _context.TradeStores.Add(row); }

                row.Name = model.Name.Trim();

                row.Place = model.Place;

                row.IsActive = model.IsActive;

                row.UpdatedDate = DateTime.UtcNow;

                await _context.SaveChangesAsync(cancellationToken);

                return new { id = row.Id.ToString() };

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

                row.Notes = model.Notes;

                row.IsActive = model.IsActive;

                row.UpdatedDate = DateTime.UtcNow;

                await _context.SaveChangesAsync(cancellationToken);

                return new { id = row.Id.ToString() };

            }

            case "party":

            {

                TradeParty row;

                if (model.Id is > 0)

                    row = await _context.TradeParties.FirstAsync(i => i.Id == model.Id, cancellationToken);

                else { row = new TradeParty(); _context.TradeParties.Add(row); }

                row.Name = model.Name.Trim();

                row.Role = TradeRecsMapper.ParsePartyRole(model.Role);

                row.Phone = model.Phone;

                row.Place = model.Place;

                row.OpeningBalance = model.OpeningBalance;

                row.IsActive = model.IsActive;

                row.UpdatedDate = DateTime.UtcNow;

                await _context.SaveChangesAsync(cancellationToken);

                return new { id = row.Id.ToString() };

            }

            case "category":

            case "cat":

            {

                TradeExpenseCategory row;

                if (model.Id is > 0)

                    row = await _context.TradeExpenseCategories.FirstAsync(i => i.Id == model.Id, cancellationToken);

                else { row = new TradeExpenseCategory(); _context.TradeExpenseCategories.Add(row); }

                row.Name = model.Name.Trim();

                row.IsActive = model.IsActive;

                row.UpdatedDate = DateTime.UtcNow;

                await _context.SaveChangesAsync(cancellationToken);

                return new { id = row.Id.ToString() };

            }

            default:

                throw new InvalidOperationException("Unknown master kind.");

        }

    }



    public async Task<object> SaveEntryAsync(SaveTradeEntryViewModel model, CancellationToken cancellationToken = default)

    {

        if (!TradeRecsMapper.TryParseClientType(model.Type, out var type))

            throw new InvalidOperationException("Invalid entry type.");



        var goods = model.Qty * model.Rate;

        var amount = model.Amount > 0 ? model.Amount : type switch

        {

            TradeEntryType.Buy => goods + model.Freight + model.Labour,

            TradeEntryType.Sell => goods,

            TradeEntryType.Loss => goods,

            _ => model.Amount > 0 ? model.Amount : model.Paid + model.Received

        };



        if (type is TradeEntryType.Sell or TradeEntryType.Loss or TradeEntryType.Transfer)

        {

            var stockType = type == TradeEntryType.Loss ? TradeEntryType.Loss : type;

            var check = await ValidateStockAsync(stockType, model.Date, model.ItemId, model.StoreId, model.ToStoreId, model.Qty, model.Id, model.Reason, cancellationToken);

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

        row.ToAccountId = model.ToAccountId;

        row.PartyId = model.PartyId;

        row.AccountId = model.AccountId;

        row.CategoryId = model.CategoryId;

        row.Qty = model.Qty;

        row.Rate = model.Rate;

        row.Amount = amount;

        row.Freight = model.Freight;

        row.Labour = model.Labour;

        row.PaidAmount = model.Paid;

        row.ReceivedAmount = model.Received;

        row.PaymentMode = model.Payment;

        row.BillNo = model.BillNo;

        row.LossReason = model.Reason;

        row.DueDate = model.DueDate;

        row.Notes = model.Notes;

        row.UpdatedDate = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return new { id = row.Id.ToString() };

    }



    public async Task<bool> DeleteEntryAsync(int id, CancellationToken cancellationToken = default) =>

        await CancelEntryAsync(id, null, cancellationToken);



    public async Task<bool> CancelEntryAsync(int id, string? reason, CancellationToken cancellationToken = default)

    {

        var row = await _context.TradeEntries.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (row is null || row.IsCancelled) return false;



        if (row.Type == TradeEntryType.Buy && row.ItemId is > 0 && row.StoreId is > 0)

        {

            var entries = await _context.TradeEntries.AsNoTracking()

                .Where(e => !e.IsCancelled && e.Id != id)

                .ToListAsync(cancellationToken);

            var qtyMap = ComputeStockQtyMap(entries);

            var key = (row.ItemId.Value, row.StoreId.Value);

            qtyMap.TryAdd(key, 0);

            qtyMap[key] -= row.Qty;

            if (qtyMap[key] < 0)

                throw new InvalidOperationException("Cannot cancel: later sales or losses depend on this stock.");

        }



        row.IsCancelled = true;

        row.CancelReason = reason?.Trim();

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

        TradeEntryType.Loss => e.LossReason ?? "Loss",

        TradeEntryType.Capital => "Money put in",

        TradeEntryType.Drawing => "Money taken out",

        TradeEntryType.AccountTransfer => "Account transfer",

        _ => e.Type.ToString()

    };



    private static Dictionary<int, decimal> ComputeAccountBalances(IReadOnlyList<TradeAccount> accounts, IReadOnlyList<TradeEntry> entries)

    {

        var map = accounts.ToDictionary(a => a.Id, a => a.OpeningBalance);

        foreach (var e in entries.OrderBy(x => x.Date).ThenBy(x => x.Id))

        {

            void Move(int? acctId, decimal delta)

            {

                if (acctId is null or 0 || !map.ContainsKey(acctId.Value)) return;

                map[acctId.Value] += delta;

            }



            var extra = e.Freight + e.Labour;

            switch (e.Type)

            {

                case TradeEntryType.Buy:

                    Move(e.AccountId, -(e.PaidAmount + extra));

                    break;

                case TradeEntryType.Sell:

                    Move(e.AccountId, e.ReceivedAmount - e.Freight);

                    break;

                case TradeEntryType.Expense:

                case TradeEntryType.Drawing:

                case TradeEntryType.PaymentOut:

                    Move(e.AccountId, -e.Amount);

                    break;

                case TradeEntryType.PaymentIn:

                case TradeEntryType.Capital:

                    Move(e.AccountId, e.Amount);

                    break;

                case TradeEntryType.AccountTransfer:

                    Move(e.AccountId, -e.Amount);

                    Move(e.ToAccountId, e.Amount);

                    break;

            }

        }

        return map;

    }



    private static decimal ComputeStockValue(IReadOnlyList<TradeItem> items, IReadOnlyList<TradeStore> stores, IReadOnlyList<TradeEntry> entries)

    {

        var qtyMap = ComputeStockQtyMap(entries);

        var cost = ComputeWeightedCost(entries);

        decimal total = 0;

        foreach (var ((itemId, _), q) in qtyMap)

        {

            if (q <= 0) continue;

            var c = cost.GetValueOrDefault(itemId, 0);

            total += q * c;

        }

        return total;

    }



    private static Dictionary<int, decimal> ComputeWeightedCost(IReadOnlyList<TradeEntry> entries)

    {

        var cost = new Dictionary<int, (decimal qty, decimal value)>();

        foreach (var e in entries.OrderBy(x => x.Date).ThenBy(x => x.Id))

        {

            if (e.ItemId is null or 0) continue;

            var id = e.ItemId.Value;

            cost.TryAdd(id, (0, 0));

            var (q, v) = cost[id];

            if (e.Type == TradeEntryType.Buy)

            {

                var goods = e.Qty * e.Rate;

                q += e.Qty;

                v += goods + e.Freight + e.Labour;

            }

            else if (e.Type == TradeEntryType.Sell || (e.Type == TradeEntryType.Loss && e.LossReason != "count+"))

            {

                var avg = q > 0.0005m ? v / q : 0;

                var take = e.Qty >= q - 0.0005m ? v : avg * e.Qty;

                q -= e.Qty;

                v -= take;

            }

            else if (e.Type == TradeEntryType.Loss && e.LossReason == "count+")

            {

                var avg = q > 0.0005m ? v / q : 0;

                q += e.Qty;

                v += avg * e.Qty;

            }

            cost[id] = (q, v);

        }



        return cost.ToDictionary(kv => kv.Key, kv => kv.Value.qty > 0.0005m ? kv.Value.value / kv.Value.qty : 0);

    }



    private static Dictionary<(int item, int store), decimal> ComputeStockQtyMap(IReadOnlyList<TradeEntry> entries)

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

                TradeEntryType.Loss when e.LossReason == "count+" => e.Qty,

                TradeEntryType.Loss => -e.Qty,

                TradeEntryType.Transfer => -e.Qty,

                _ => 0

            };

            if (e.Type == TradeEntryType.Transfer && e.ToStoreId is > 0)

            {

                var key2 = (e.ItemId.Value, e.ToStoreId.Value);

                qty.TryAdd(key2, 0);

                qty[key2] += e.Qty;

            }

        }

        return qty;

    }



    private async Task<string?> ValidateStockAsync(

        TradeEntryType type, DateOnly date, int? itemId, int? storeId, int? toStoreId, decimal qty, int? excludeEntryId,

        string? lossReason,

        CancellationToken cancellationToken)

    {

        if (itemId is null or 0 || storeId is null or 0 || qty <= 0) return null;

        if (type == TradeEntryType.Loss && lossReason == "count+") return null;



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

            Qty = qty,

            LossReason = lossReason

        };

        entries.Add(hypothetical);



        var qtyMap = ComputeStockQtyMap(entries);

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



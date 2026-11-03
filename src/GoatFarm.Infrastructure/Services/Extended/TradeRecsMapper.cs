using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;

namespace GoatFarm.Infrastructure.Services.Extended;

internal static class TradeRecsMapper
{
    internal static string ClientType(TradeEntryType type) => type switch
    {
        TradeEntryType.Buy => "buy",
        TradeEntryType.Sell => "sell",
        TradeEntryType.Loss => "loss",
        TradeEntryType.PaymentOut => "pay",
        TradeEntryType.PaymentIn => "receive",
        TradeEntryType.Expense => "expense",
        TradeEntryType.Capital => "capital",
        TradeEntryType.Drawing => "drawing",
        TradeEntryType.AccountTransfer => "transfer",
        TradeEntryType.Transfer => "transfer",
        _ => type.ToString().ToLowerInvariant()
    };

    internal static bool TryParseClientType(string type, out TradeEntryType parsed)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            parsed = TradeEntryType.Buy;
            return false;
        }

        if (Enum.TryParse<TradeEntryType>(type, true, out parsed))
            return true;

        parsed = type.ToLowerInvariant() switch
        {
            "buy" => TradeEntryType.Buy,
            "sell" => TradeEntryType.Sell,
            "loss" => TradeEntryType.Loss,
            "pay" => TradeEntryType.PaymentOut,
            "receive" => TradeEntryType.PaymentIn,
            "expense" => TradeEntryType.Expense,
            "capital" => TradeEntryType.Capital,
            "drawing" => TradeEntryType.Drawing,
            "transfer" => TradeEntryType.AccountTransfer,
            _ => TradeEntryType.Buy
        };
        return true;
    }

    internal static string PartyRole(TradePartyRole role) => role switch
    {
        TradePartyRole.Customer => "customer",
        TradePartyRole.Supplier => "supplier",
        _ => "supplier"
    };

    internal static TradePartyRole ParsePartyRole(string? role) =>
        role?.ToLowerInvariant() switch
        {
            "customer" => TradePartyRole.Customer,
            "supplier" => TradePartyRole.Supplier,
            _ => TradePartyRole.Supplier
        };

    internal static List<object> Build(
        IReadOnlyList<TradeItem> items,
        IReadOnlyList<TradeStore> stores,
        IReadOnlyList<TradeAccount> accounts,
        IReadOnlyList<TradeParty> parties,
        IReadOnlyList<TradeExpenseCategory> categories,
        IReadOnlyList<TradeEntry> entries)
    {
        var list = new List<object>();
        foreach (var i in items)
        {
            list.Add(new
            {
                kind = "item",
                id = i.Id.ToString(),
                name = i.Name,
                unit = i.Unit,
                unitNote = i.UnitNote,
                price = i.DefaultPrice,
                note = i.Notes,
                active = i.IsActive
            });
        }

        foreach (var s in stores)
        {
            list.Add(new
            {
                kind = "store",
                id = s.Id.ToString(),
                name = s.Name,
                place = s.Place,
                active = s.IsActive
            });
        }

        foreach (var a in accounts)
        {
            list.Add(new
            {
                kind = "account",
                id = a.Id.ToString(),
                name = a.Name,
                type = a.Type == TradeAccountType.Bank ? "bank" : "cash",
                opening = a.OpeningBalance,
                note = a.Notes,
                active = a.IsActive
            });
        }

        foreach (var p in parties)
        {
            list.Add(new
            {
                kind = "party",
                id = p.Id.ToString(),
                role = PartyRole(p.Role),
                name = p.Name,
                phone = p.Phone,
                place = p.Place,
                opening = p.OpeningBalance,
                active = p.IsActive
            });
        }

        foreach (var c in categories)
        {
            list.Add(new { kind = "cat", id = c.Id.ToString(), name = c.Name, active = c.IsActive });
        }

        foreach (var e in entries)
        {
            var goods = e.Qty * e.Rate;
            var payment = e.PaymentMode;
            if (string.IsNullOrWhiteSpace(payment))
            {
                payment = e.Type switch
                {
                    TradeEntryType.Buy when e.PaidAmount <= 0 => "credit",
                    TradeEntryType.Buy when e.PaidAmount >= goods - 0.01m => "paid",
                    TradeEntryType.Buy => "partial",
                    TradeEntryType.Sell when e.ReceivedAmount <= 0 => "credit",
                    TradeEntryType.Sell when e.ReceivedAmount >= goods - 0.01m => "paid",
                    TradeEntryType.Sell => "partial",
                    _ => ""
                };
            }

            list.Add(new
            {
                kind = "entry",
                id = e.Id.ToString(),
                type = ClientType(e.Type),
                date = e.Date.ToString("yyyy-MM-dd"),
                itemId = e.ItemId?.ToString(),
                storeId = e.StoreId?.ToString(),
                toAccountId = e.ToAccountId?.ToString(),
                partyId = e.PartyId?.ToString(),
                party = e.Party?.Name,
                catId = e.CategoryId?.ToString(),
                accountId = e.AccountId?.ToString(),
                qty = e.Qty,
                rate = e.Rate,
                freight = e.Freight,
                labour = e.Labour,
                paid = e.PaidAmount,
                received = e.ReceivedAmount,
                payment,
                billNo = e.BillNo,
                reason = e.LossReason,
                dueDate = e.DueDate?.ToString("yyyy-MM-dd"),
                note = e.Notes,
                cancelled = e.IsCancelled,
                cancelReason = e.CancelReason,
                amount = e.Amount
            });
        }

        return list;
    }
}

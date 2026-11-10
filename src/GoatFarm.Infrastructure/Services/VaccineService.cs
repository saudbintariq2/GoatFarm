using GoatFarm.Application.Common;
using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Vaccines;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Infrastructure.Services;

public class VaccineService : IVaccineService
{
    private const string RemindDaysKey = "RemindDays";
    private readonly GoatFarmDbContext _context;

    public VaccineService(GoatFarmDbContext context) => _context = context;

    public async Task<VaccinePageViewModel> GetVaccinePageAsync(int? remindDays, string? month = null, CancellationToken cancellationToken = default)
    {
        month ??= MonthHelper.CurrentMonthKey();
        var (monthStart, monthEnd) = MonthHelper.GetMonthRange(month);
        var window = remindDays ?? await GetRemindDaysAsync(cancellationToken);
        var goats = await _context.Goats.AsNoTracking().Where(g => !g.IsArchived).ToListAsync(cancellationToken);
        var vaccines = await _context.Vaccines.AsNoTracking().ToListAsync(cancellationToken);
        var log = await _context.VaccinationHistories.AsNoTracking().ToListAsync(cancellationToken);
        var goatTags = await _context.Goats.AsNoTracking().ToDictionaryAsync(g => g.Id, g => g.Tag, cancellationToken);
        var vaccineNames = vaccines.ToDictionary(v => v.Id, v => v.Name);

        var dueNow = new List<VaccineDueRowViewModel>();
        var upcoming = new List<VaccineUpcomingRowViewModel>();
        var totalDue = 0;
        var upCount = 0;

        foreach (var v in vaccines)
        {
            var dueGoats = GetDueGoats(v, goats, log);
            if (dueGoats.Count > 0)
            {
                totalDue += dueGoats.Count;
                dueNow.Add(new VaccineDueRowViewModel
                {
                    VaccineId = v.Id,
                    VaccineName = v.Name,
                    ScopeDisplay = DisplayHelper.GetScopeLabel(v.Scope),
                    GoatTags = dueGoats.Select(g => g.Tag).ToList(),
                    DueCount = dueGoats.Count
                });
            }

            var upGoats = GetUpcomingGoats(v, goats, log, window);
            if (upGoats.Count > 0)
            {
                upCount += upGoats.Count;
                upGoats.Sort((a, b) => a.Days.CompareTo(b.Days));
                upcoming.Add(new VaccineUpcomingRowViewModel
                {
                    VaccineName = v.Name,
                    ScopeDisplay = DisplayHelper.GetScopeLabel(v.Scope),
                    GoatTags = upGoats.Select(x => x.Goat.Tag).ToList(),
                    Count = upGoats.Count,
                    FirstInDays = upGoats[0].Days,
                    FirstDate = upGoats[0].Date.ToString("yyyy-MM-dd")
                });
            }
        }

        var schedule = vaccines.Select(v => new VaccineViewModel
        {
            Id = v.Id,
            Name = v.Name,
            Scope = v.Scope,
            ScopeDisplay = DisplayHelper.GetScopeLabel(v.Scope),
            RuleType = v.RuleType,
            RuleDisplay = RuleLabel(v),
            Days = v.Days,
            Months = v.Months,
            Note = v.Note
        }).ToList();

        var history = log
            .GroupBy(l => new { l.VaccinationDate, l.VaccineId })
            .Select(g =>
            {
                var v = vaccines.FirstOrDefault(x => x.Id == g.Key.VaccineId);
                return new VaccinationLogViewModel
                {
                    VaccineId = g.Key.VaccineId,
                    VaccinationDate = g.Key.VaccinationDate,
                    VaccineName = v?.Name ?? "(removed)",
                    GoatCount = g.Count()
                };
            })
            .OrderByDescending(h => h.VaccinationDate)
            .Take(15)
            .ToList();

        var tagHistory = log
            .OrderByDescending(h => h.VaccinationDate).ThenBy(h => h.GoatId)
            .Take(200)
            .Select(h => new VaccinationTagHistoryRowViewModel
            {
                Date = h.VaccinationDate,
                GoatTag = goatTags.GetValueOrDefault(h.GoatId, "?"),
                VaccineName = vaccineNames.GetValueOrDefault(h.VaccineId, "(removed)")
            })
            .ToList();

        var moneyAccounts = await LoadMoneyAccountsAsync(cancellationToken);
        var acctName = moneyAccounts.ToDictionary(a => a.Id, a => a.Name);
        var purchaseRows = await _context.VaccinePurchases.AsNoTracking()
            .Where(p => p.Date >= monthStart && p.Date < monthEnd)
            .OrderByDescending(p => p.Date)
            .ToListAsync(cancellationToken);
        var purchases = purchaseRows.Select(p => MapPurchase(p, acctName)).ToList();

        return new VaccinePageViewModel
        {
            VaccineCount = vaccines.Count,
            DueNowCount = totalDue,
            DueNowNote = totalDue > 0 ? "goats waiting for a shot" : "all up to date",
            UpcomingCount = upCount,
            UpcomingNote = $"due within {window} days",
            RemindDays = window,
            DueNow = dueNow,
            Upcoming = upcoming,
            Schedule = schedule,
            History = history,
            TagHistory = tagHistory,
            VaccinePurchases = purchases,
            VaccineBoughtMonthTotal = purchases.Sum(p => p.Amount),
            PurchaseMonth = month,
            GoatGroups = await _context.GoatGroups.AsNoTracking().OrderBy(g => g.Name).Select(g => g.Name).ToListAsync(cancellationToken),
            MoneyAccounts = moneyAccounts
        };
    }

    public async Task<VaccineViewModel> AddVaccineAsync(CreateVaccineViewModel model, CancellationToken cancellationToken = default)
    {
        var entity = new Vaccine
        {
            Name = model.Name.Trim(),
            Scope = model.Scope,
            RuleType = model.RuleType,
            Days = model.RuleType == VaccineRuleType.Age ? model.Value : null,
            Months = model.RuleType == VaccineRuleType.Repeat ? model.Value : null,
            Note = string.IsNullOrWhiteSpace(model.Note) ? null : model.Note.Trim()
        };
        _context.Vaccines.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return MapVaccine(entity);
    }

    public async Task<VaccineViewModel?> UpdateVaccineAsync(int id, CreateVaccineViewModel model, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Vaccines.FindAsync([id], cancellationToken);
        if (entity is null) return null;
        entity.Name = model.Name.Trim();
        entity.Scope = model.Scope;
        entity.RuleType = model.RuleType;
        entity.Days = model.RuleType == VaccineRuleType.Age ? model.Value : null;
        entity.Months = model.RuleType == VaccineRuleType.Repeat ? model.Value : null;
        entity.Note = string.IsNullOrWhiteSpace(model.Note) ? null : model.Note.Trim();
        await _context.SaveChangesAsync(cancellationToken);
        return MapVaccine(entity);
    }

    public async Task<bool> DeleteVaccineAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Vaccines.FindAsync([id], cancellationToken);
        if (entity is null) return false;
        _context.Vaccines.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> GiveVaccineToGroupAsync(GiveVaccineViewModel model, CancellationToken cancellationToken = default)
    {
        var vaccine = await _context.Vaccines.FindAsync([model.VaccineId], cancellationToken);
        if (vaccine is null) return 0;

        var goats = await _context.Goats.AsNoTracking().Include(g => g.Group).Where(g => !g.IsArchived).ToListAsync(cancellationToken);
        var selected = ResolveGoatsForTarget(goats, model.Target).ToList();
        foreach (var g in selected)
        {
            _context.VaccinationHistories.Add(new VaccinationHistory
            {
                GoatId = g.Id,
                VaccineId = model.VaccineId,
                VaccinationDate = model.Date
            });
        }
        await _context.SaveChangesAsync(cancellationToken);
        return selected.Count;
    }

    public async Task MarkVaccineDoneAsync(int vaccineId, CancellationToken cancellationToken = default)
    {
        var vaccine = await _context.Vaccines.FindAsync([vaccineId], cancellationToken);
        if (vaccine is null) return;

        var goats = await _context.Goats.AsNoTracking().Where(g => !g.IsArchived).ToListAsync(cancellationToken);
        var log = await _context.VaccinationHistories.AsNoTracking().ToListAsync(cancellationToken);
        var due = GetDueGoats(vaccine, goats, log);
        var today = DateOnly.FromDateTime(DateTime.Today);

        foreach (var g in due)
        {
            _context.VaccinationHistories.Add(new VaccinationHistory
            {
                GoatId = g.Id,
                VaccineId = vaccineId,
                VaccinationDate = today
            });
        }
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteVaccinationBatchAsync(int vaccineId, DateOnly date, CancellationToken cancellationToken = default)
    {
        var entries = await _context.VaccinationHistories
            .Where(h => h.VaccineId == vaccineId && h.VaccinationDate == date)
            .ToListAsync(cancellationToken);
        if (entries.Count == 0) return false;
        _context.VaccinationHistories.RemoveRange(entries);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> UpdateVaccinationBatchAsync(UpdateVaccinationBatchViewModel model, CancellationToken cancellationToken = default)
    {
        var entries = await _context.VaccinationHistories
            .Where(h => h.VaccineId == model.VaccineId && h.VaccinationDate == model.Date)
            .ToListAsync(cancellationToken);
        if (entries.Count == 0) return false;
        foreach (var entry in entries)
            entry.VaccinationDate = model.NewDate;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task SetReminderWindowAsync(int days, CancellationToken cancellationToken = default)
    {
        var setting = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == RemindDaysKey, cancellationToken);
        if (setting is null)
        {
            _context.AppSettings.Add(new AppSetting { Key = RemindDaysKey, Value = days.ToString() });
        }
        else
        {
            setting.Value = days.ToString();
            setting.UpdatedDate = DateTime.UtcNow;
        }
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<VaccinePurchaseViewModel> AddVaccinePurchaseAsync(CreateVaccinePurchaseViewModel model, CancellationToken cancellationToken = default)
    {
        var mapped = MapPurchasePayment(model);
        var entity = new VaccinePurchase
        {
            Date = model.Date,
            Name = model.Name.Trim(),
            Qty = model.Qty,
            Unit = model.Unit,
            Amount = mapped.Amount,
            Comment = string.IsNullOrWhiteSpace(model.Comment) ? null : model.Comment.Trim(),
            PaymentStatus = mapped.Status,
            PaidAmount = mapped.Paid,
            AccountId = mapped.AccountId,
            Supplier = mapped.Supplier,
            DueDate = model.DueDate
        };
        _context.VaccinePurchases.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        await PostPurchaseMoneyAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return await ToPurchaseVmAsync(entity, cancellationToken);
    }

    public async Task<VaccinePurchaseViewModel?> UpdateVaccinePurchaseAsync(int id, CreateVaccinePurchaseViewModel model, CancellationToken cancellationToken = default)
    {
        var entity = await _context.VaccinePurchases.FindAsync([id], cancellationToken);
        if (entity is null) return null;
        var mapped = MapPurchasePayment(model);
        entity.Date = model.Date;
        entity.Name = model.Name.Trim();
        entity.Qty = model.Qty;
        entity.Unit = model.Unit;
        entity.Amount = mapped.Amount;
        entity.Comment = string.IsNullOrWhiteSpace(model.Comment) ? null : model.Comment.Trim();
        entity.PaymentStatus = mapped.Status;
        entity.PaidAmount = mapped.Paid;
        entity.AccountId = mapped.AccountId;
        entity.Supplier = mapped.Supplier;
        entity.DueDate = model.DueDate;
        entity.UpdatedDate = DateTime.UtcNow;
        await ReversePurchaseMoneyAsync(entity.Id, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await PostPurchaseMoneyAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return await ToPurchaseVmAsync(entity, cancellationToken);
    }

    public async Task<bool> DeleteVaccinePurchaseAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.VaccinePurchases.FindAsync([id], cancellationToken);
        if (entity is null) return false;
        await ReversePurchaseMoneyAsync(entity.Id, cancellationToken);
        _context.VaccinePurchases.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public decimal GetVaccinePurchasedMonthly(string month)
    {
        var (monthStart, monthEnd) = MonthHelper.GetMonthRange(month);
        return _context.VaccinePurchases.AsNoTracking()
            .Where(p => p.Date >= monthStart && p.Date < monthEnd)
            .Sum(p => p.Amount);
    }

    private static VaccineViewModel MapVaccine(Vaccine entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Scope = entity.Scope,
        ScopeDisplay = DisplayHelper.GetScopeLabel(entity.Scope),
        RuleType = entity.RuleType,
        RuleDisplay = RuleLabel(entity),
        Days = entity.Days,
        Months = entity.Months,
        Note = entity.Note
    };

    private static VaccinePurchaseViewModel MapPurchase(
        VaccinePurchase entity, IReadOnlyDictionary<int, string> acctName) => new()
    {
        Id = entity.Id,
        Date = entity.Date,
        Name = entity.Name,
        Qty = entity.Qty,
        Unit = entity.Unit,
        Amount = entity.Amount,
        Comment = entity.Comment,
        PaymentStatus = string.IsNullOrWhiteSpace(entity.PaymentStatus) ? "paid" : entity.PaymentStatus,
        PaidAmount = entity.PaidAmount,
        AccountId = entity.AccountId,
        AccountName = entity.AccountId is > 0 && acctName.TryGetValue(entity.AccountId.Value, out var n)
            ? n
            : (entity.PaidAmount > 0 ? "Account not recorded" : "—"),
        Supplier = entity.Supplier,
        DueDate = entity.DueDate
    };

    private async Task<VaccinePurchaseViewModel> ToPurchaseVmAsync(VaccinePurchase entity, CancellationToken cancellationToken)
    {
        var acctName = entity.AccountId is > 0
            ? await _context.MoneyAccounts.AsNoTracking()
                .Where(a => a.Id == entity.AccountId)
                .Select(a => a.Name)
                .FirstOrDefaultAsync(cancellationToken)
            : null;
        return MapPurchase(entity,
            entity.AccountId is > 0 && acctName is not null
                ? new Dictionary<int, string> { [entity.AccountId.Value] = acctName }
                : new Dictionary<int, string>());
    }

    private async Task<List<VaccineMoneyAccountViewModel>> LoadMoneyAccountsAsync(CancellationToken cancellationToken)
    {
        var accounts = await _context.MoneyAccounts.AsNoTracking()
            .Where(a => a.IsActive)
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken);
        var txs = await _context.LedgerTransactions.AsNoTracking().ToListAsync(cancellationToken);
        var map = accounts.ToDictionary(a => a.Id, a => a.OpeningBalance);
        foreach (var t in txs)
        {
            if (t.AccountId is null || !map.ContainsKey(t.AccountId.Value)) continue;
            map[t.AccountId.Value] += t.Kind switch
            {
                LedgerTransactionKind.Income or LedgerTransactionKind.InvoicePayment => t.Amount,
                LedgerTransactionKind.Expense or LedgerTransactionKind.PayablePayment => -t.Amount,
                LedgerTransactionKind.Transfer => t.Amount,
                _ => 0
            };
        }

        return accounts.Select(a => new VaccineMoneyAccountViewModel
        {
            Id = a.Id,
            Name = a.Name,
            Kind = a.Kind.ToString(),
            Balance = map.GetValueOrDefault(a.Id)
        }).ToList();
    }

    private static (decimal Amount, decimal Paid, string Status, int? AccountId, string Supplier)
        MapPurchasePayment(CreateVaccinePurchaseViewModel model)
    {
        var amount = Math.Round(model.Amount);
        var status = (model.PaymentStatus ?? "paid").Trim().ToLowerInvariant();
        if (status is not "paid" and not "partial" and not "credit") status = "paid";
        var paid = status == "paid" ? amount : status == "credit" ? 0 : Math.Clamp(model.PaidAmount, 0, amount);
        if (paid >= amount) status = "paid";
        else if (paid > 0) status = "partial";
        else status = "credit";
        if (paid > 0 && model.AccountId is not > 0)
            throw new InvalidOperationException("Select Paid from: Cash in Hand or a Bank account.");
        var supplier = string.IsNullOrWhiteSpace(model.Supplier) ? "Vaccine supplier" : model.Supplier.Trim();
        return (amount, paid, status, paid > 0 ? model.AccountId : null, supplier);
    }

    private async Task ReversePurchaseMoneyAsync(int purchaseId, CancellationToken cancellationToken)
    {
        var key = "vaccine_purchase:" + purchaseId;
        var txs = await _context.LedgerTransactions.Where(t => t.Reference == key).ToListAsync(cancellationToken);
        _context.LedgerTransactions.RemoveRange(txs);
        var sid = purchaseId.ToString();
        var pays = await _context.SupplierPayables
            .Where(p => p.SourceType == "vaccine_purchase" && p.SourceId == sid)
            .ToListAsync(cancellationToken);
        _context.SupplierPayables.RemoveRange(pays);
    }

    private async Task PostPurchaseMoneyAsync(VaccinePurchase entity, CancellationToken cancellationToken)
    {
        if (entity.PaidAmount > 0 && entity.AccountId is > 0)
        {
            var acct = await _context.MoneyAccounts.FirstOrDefaultAsync(a => a.Id == entity.AccountId && a.IsActive, cancellationToken)
                ?? throw new InvalidOperationException("That payment account was not found. Set up accounts under Finance → Bank & Cash.");
            _context.LedgerTransactions.Add(new LedgerTransaction
            {
                Date = entity.Date,
                Kind = LedgerTransactionKind.Expense,
                AccountId = acct.Id,
                Amount = entity.PaidAmount,
                Category = "Vaccines & medicine",
                Reference = "vaccine_purchase:" + entity.Id,
                Notes = $"Vaccine purchase · {entity.Name} · {entity.Supplier}"
            });
        }

        if (entity.PaidAmount < entity.Amount)
        {
            _context.SupplierPayables.Add(new SupplierPayable
            {
                SupplierName = entity.Supplier ?? "Vaccine supplier",
                Date = entity.Date,
                DueDate = entity.DueDate,
                Description = $"Vaccine / medicine · {entity.Name}",
                Amount = entity.Amount,
                PaidAmount = entity.PaidAmount,
                Status = entity.PaidAmount > 0 ? PayableStatus.Partial : PayableStatus.Open,
                SourceType = "vaccine_purchase",
                SourceId = entity.Id.ToString()
            });
        }

        await Task.CompletedTask;
    }

    private async Task<int> GetRemindDaysAsync(CancellationToken cancellationToken)
    {
        var setting = await _context.AppSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == RemindDaysKey, cancellationToken);
        return setting is not null && int.TryParse(setting.Value, out var d) ? d : 30;
    }

    private static string RuleLabel(Vaccine v) =>
        v.RuleType == VaccineRuleType.Age ? $"At {v.Days} days old" : $"Every {v.Months} months";

    private static List<Goat> GetDueGoats(Vaccine v, IReadOnlyList<Goat> goats, IReadOnlyList<VaccinationHistory> log) =>
        GoatsInScope(v, goats).Where(g =>
        {
            var next = NextDue(g, v, log);
            return next is not null && next.Value.Days <= 0;
        }).ToList();

    private static List<(Goat Goat, int Days, DateOnly Date)> GetUpcomingGoats(
        Vaccine v, IReadOnlyList<Goat> goats, IReadOnlyList<VaccinationHistory> log, int window) =>
        GoatsInScope(v, goats)
            .Select(g => (Goat: g, Next: NextDue(g, v, log)))
            .Where(x => x.Next is not null && x.Next.Value.Days > 0 && x.Next.Value.Days <= window)
            .Select(x => (x.Goat, x.Next!.Value.Days, x.Next.Value.Date))
            .ToList();

    private static IEnumerable<Goat> GoatsInScope(Vaccine v, IReadOnlyList<Goat> goats) =>
        goats.Where(g => v.Scope == VaccineScope.All || StatusMatchesScope(g.Status, v.Scope));

    private static bool StatusMatchesScope(GoatStatus status, VaccineScope scope) =>
        scope switch
        {
            VaccineScope.Kid => status == GoatStatus.Kid,
            VaccineScope.Milking => status == GoatStatus.Milking,
            VaccineScope.Pregnant => status == GoatStatus.Pregnant,
            VaccineScope.Dry => status == GoatStatus.Dry,
            VaccineScope.Buck => status == GoatStatus.Buck,
            VaccineScope.Sale => status == GoatStatus.Sale,
            _ => false
        };

    private static (DateOnly Date, int Days)? NextDue(Goat g, Vaccine v, IReadOnlyList<VaccinationHistory> log)
    {
        if (v.RuleType == VaccineRuleType.Age)
        {
            if (log.Any(l => l.GoatId == g.Id && l.VaccineId == v.Id)) return null;
            var dueDate = g.EventDate.AddDays(v.Days ?? 0);
            return (dueDate, DaysUntil(dueDate));
        }

        var last = log.Where(l => l.GoatId == g.Id && l.VaccineId == v.Id)
            .Select(l => l.VaccinationDate)
            .OrderByDescending(d => d)
            .FirstOrDefault();
        var baseDate = last == default ? DateOnly.FromDateTime(DateTime.Today) : last;
        var nextDate = baseDate.AddDays((v.Months ?? 0) * 30);
        return (nextDate, DaysUntil(nextDate));
    }

    private static int DaysUntil(DateOnly date)
    {
        var target = date.ToDateTime(TimeOnly.MinValue);
        var now = DateTime.Today;
        return (int)Math.Ceiling((target - now).TotalDays);
    }

    private static IEnumerable<Goat> ResolveGoatsForTarget(IReadOnlyList<Goat> goats, string target)
    {
        if (string.Equals(target, "all", StringComparison.OrdinalIgnoreCase))
            return goats;

        if (target.StartsWith("st:", StringComparison.OrdinalIgnoreCase))
        {
            var status = DisplayHelper.ParseStatusKey(target[3..]);
            return goats.Where(g => g.Status == status);
        }

        if (target.StartsWith("grp:", StringComparison.OrdinalIgnoreCase))
        {
            var groupName = target[4..];
            return goats.Where(g => g.Group?.Name == groupName);
        }

        if (target.StartsWith("tag:", StringComparison.OrdinalIgnoreCase))
        {
            var tag = target[4..].Trim();
            return goats.Where(g => string.Equals(g.Tag, tag, StringComparison.OrdinalIgnoreCase));
        }

        return [];
    }
}

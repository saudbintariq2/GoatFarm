using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Ledger;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;
using GoatFarm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Infrastructure.Services.Extended;

public class LedgerService : ILedgerService
{
    private readonly GoatFarmDbContext _context;

    public LedgerService(GoatFarmDbContext context) => _context = context;

    public async Task EnsureSeededAsync(CancellationToken cancellationToken = default)
    {
        if (await _context.MoneyAccounts.AnyAsync(cancellationToken)) return;
        _context.MoneyAccounts.Add(new MoneyAccount { Name = "Farm Cash", Kind = MoneyAccountKind.Cash, OpeningBalance = 0 });
        _context.MoneyAccounts.Add(new MoneyAccount { Name = "Farm Bank", Kind = MoneyAccountKind.Bank, OpeningBalance = 0 });
        _context.BusinessCustomers.Add(new BusinessCustomer { Name = "Walk-in customer", IsActive = true });
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<LedgerPageViewModel> GetPageAsync(string? month, CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken);
        var m = ParseMonth(month);
        var accounts = await _context.MoneyAccounts.AsNoTracking().OrderBy(a => a.Name).ToListAsync(cancellationToken);
        var tx = await _context.LedgerTransactions.AsNoTracking().Include(t => t.Account)
            .OrderByDescending(t => t.Date).Take(150).ToListAsync(cancellationToken);
        var invoices = await _context.CustomerInvoices.AsNoTracking().Include(i => i.Customer)
            .OrderByDescending(i => i.Date).Take(100).ToListAsync(cancellationToken);
        var payables = await _context.SupplierPayables.AsNoTracking()
            .OrderByDescending(p => p.Date).Take(100).ToListAsync(cancellationToken);
        var budget = await _context.BudgetLines.AsNoTracking()
            .Where(b => b.Year == m.Year).OrderBy(b => b.Month).ToListAsync(cancellationToken);
        var partners = await _context.ProfitSharePartners.AsNoTracking().Where(p => p.IsActive).ToListAsync(cancellationToken);
        var customers = await _context.BusinessCustomers.AsNoTracking().OrderBy(c => c.Name).ToListAsync(cancellationToken);

        var balances = ComputeBalances(accounts, tx);
        var receivables = invoices.Where(i => i.Status is InvoiceStatus.Open or InvoiceStatus.Partial)
            .Sum(i => i.Amount - i.PaidAmount);
        var payablesOpen = payables.Where(p => p.Status is PayableStatus.Open or PayableStatus.Partial)
            .Sum(p => p.Amount - p.PaidAmount);

        return new LedgerPageViewModel
        {
            Month = $"{m.Year:0000}-{m.Month:00}",
            Receivables = receivables,
            Payables = payablesOpen,
            CashOnHand = balances.Where((_, i) => accounts[i].Kind == MoneyAccountKind.Cash).Sum(x => x.Value),
            Accounts = accounts.Select(a => new MoneyAccountRowViewModel
            {
                Id = a.Id, Name = a.Name, Kind = a.Kind.ToString(), Balance = balances.GetValueOrDefault(a.Id, a.OpeningBalance)
            }).ToList(),
            Transactions = tx.Select(t => new LedgerTransactionRowViewModel
            {
                Id = t.Id,
                DateDisplay = t.Date.ToString("yyyy-MM-dd"),
                Kind = t.Kind.ToString(),
                Account = t.Account?.Name ?? "",
                Amount = t.Amount,
                Category = t.Category,
                Notes = t.Notes
            }).ToList(),
            Invoices = invoices.Select(i => new CustomerInvoiceRowViewModel
            {
                Id = i.Id,
                Customer = i.Customer.Name,
                DateDisplay = i.Date.ToString("yyyy-MM-dd"),
                Description = i.Description,
                Amount = i.Amount,
                PaidAmount = i.PaidAmount,
                Status = i.Status.ToString()
            }).ToList(),
            PayablesList = payables.Select(p => new SupplierPayableRowViewModel
            {
                Id = p.Id,
                SupplierName = p.SupplierName,
                DateDisplay = p.Date.ToString("yyyy-MM-dd"),
                Description = p.Description,
                Amount = p.Amount,
                PaidAmount = p.PaidAmount,
                Status = p.Status.ToString()
            }).ToList(),
            Budget = budget.Select(b => new BudgetRowViewModel
            {
                Id = b.Id, Year = b.Year, Month = b.Month, Category = b.Category, PlannedAmount = b.PlannedAmount
            }).ToList(),
            Partners = partners.Select(p => new ProfitPartnerRowViewModel { Id = p.Id, Name = p.Name, SharePercent = p.SharePercent }).ToList(),
            Customers = customers.Select(c => new BusinessCustomerRowViewModel { Id = c.Id, Name = c.Name, Phone = c.Phone }).ToList()
        };
    }

    public async Task<FinancialStatementsViewModel> GetStatementsAsync(string? month, CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken);
        var m = ParseMonth(month);
        var monthStart = new DateOnly(m.Year, m.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        var incomes = await _context.Incomes.AsNoTracking()
            .Where(i => i.Date >= monthStart && i.Date <= monthEnd).SumAsync(i => i.Amount, cancellationToken);
        var expenses = await _context.Expenses.AsNoTracking()
            .Where(i => i.Date >= monthStart && i.Date <= monthEnd).SumAsync(i => i.Amount, cancellationToken);
        var tx = await _context.LedgerTransactions.AsNoTracking()
            .Where(t => t.Date >= monthStart && t.Date <= monthEnd).ToListAsync(cancellationToken);
        var assets = await _context.Assets.AsNoTracking().SumAsync(a => a.Cost, cancellationToken)
            + await _context.FixedAssetRegisters.AsNoTracking().SumAsync(a => a.Cost, cancellationToken);
        var payables = await _context.SupplierPayables.AsNoTracking()
            .Where(p => p.Status != PayableStatus.Void).SumAsync(p => p.Amount - p.PaidAmount, cancellationToken);

        var cashIn = tx.Where(t => t.Kind is LedgerTransactionKind.Income or LedgerTransactionKind.InvoicePayment).Sum(t => t.Amount);
        var cashOut = tx.Where(t => t.Kind is LedgerTransactionKind.Expense or LedgerTransactionKind.PayablePayment).Sum(t => t.Amount);

        return new FinancialStatementsViewModel
        {
            Month = $"{m.Year:0000}-{m.Month:00}",
            Revenue = incomes + cashIn,
            Expenses = expenses + cashOut,
            NetProfit = incomes + cashIn - expenses - cashOut,
            CashIn = cashIn,
            CashOut = cashOut,
            AssetsTotal = assets,
            LiabilitiesTotal = payables,
            EquityEstimate = assets - payables
        };
    }

    public async Task<object> SaveAccountAsync(SaveMoneyAccountViewModel model, CancellationToken cancellationToken = default)
    {
        MoneyAccount row;
        if (model.Id is > 0)
            row = await _context.MoneyAccounts.FirstAsync(a => a.Id == model.Id, cancellationToken);
        else { row = new MoneyAccount(); _context.MoneyAccounts.Add(row); }
        row.Name = model.Name.Trim();
        row.Kind = model.Kind;
        if (model.Id is null or 0) row.OpeningBalance = model.OpeningBalance;
        row.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return new { id = row.Id };
    }

    public async Task<object> SaveTransactionAsync(SaveLedgerTransactionViewModel model, CancellationToken cancellationToken = default)
    {
        LedgerTransaction row;
        if (model.Id is > 0)
            row = await _context.LedgerTransactions.FirstAsync(t => t.Id == model.Id, cancellationToken);
        else { row = new LedgerTransaction(); _context.LedgerTransactions.Add(row); }
        row.Date = model.Date;
        row.Kind = model.Kind;
        row.AccountId = model.AccountId;
        row.CounterAccountId = model.CounterAccountId;
        row.Amount = model.Amount;
        row.Category = model.Category;
        row.Notes = model.Notes;
        row.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return new { id = row.Id };
    }

    public async Task<object> SaveInvoiceAsync(SaveCustomerInvoiceViewModel model, CancellationToken cancellationToken = default)
    {
        CustomerInvoice row;
        if (model.Id is > 0)
            row = await _context.CustomerInvoices.FirstAsync(i => i.Id == model.Id, cancellationToken);
        else { row = new CustomerInvoice(); _context.CustomerInvoices.Add(row); }
        row.CustomerId = model.CustomerId;
        row.Date = model.Date;
        row.DueDate = model.DueDate;
        row.Description = model.Description.Trim();
        row.Amount = model.Amount;
        row.PaidAmount = model.PaidAmount;
        row.Status = row.PaidAmount >= row.Amount ? InvoiceStatus.Paid
            : row.PaidAmount > 0 ? InvoiceStatus.Partial : InvoiceStatus.Open;
        row.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return new { id = row.Id };
    }

    public async Task<object> SavePayableAsync(SaveSupplierPayableViewModel model, CancellationToken cancellationToken = default)
    {
        SupplierPayable row;
        if (model.Id is > 0)
            row = await _context.SupplierPayables.FirstAsync(p => p.Id == model.Id, cancellationToken);
        else { row = new SupplierPayable(); _context.SupplierPayables.Add(row); }
        row.SupplierName = model.SupplierName.Trim();
        row.Date = model.Date;
        row.DueDate = model.DueDate;
        row.Description = model.Description.Trim();
        row.Amount = model.Amount;
        row.PaidAmount = model.PaidAmount;
        row.Status = row.PaidAmount >= row.Amount ? PayableStatus.Paid
            : row.PaidAmount > 0 ? PayableStatus.Partial : PayableStatus.Open;
        row.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return new { id = row.Id };
    }

    public async Task<object> SaveBudgetAsync(SaveBudgetLineViewModel model, CancellationToken cancellationToken = default)
    {
        BudgetLine row;
        if (model.Id is > 0)
            row = await _context.BudgetLines.FirstAsync(b => b.Id == model.Id, cancellationToken);
        else { row = new BudgetLine(); _context.BudgetLines.Add(row); }
        row.Year = model.Year;
        row.Month = model.Month;
        row.Category = model.Category.Trim();
        row.PlannedAmount = model.PlannedAmount;
        row.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return new { id = row.Id };
    }

    public async Task<object> SaveProfitPartnerAsync(SaveProfitSharePartnerViewModel model, CancellationToken cancellationToken = default)
    {
        ProfitSharePartner row;
        if (model.Id is > 0)
            row = await _context.ProfitSharePartners.FirstAsync(p => p.Id == model.Id, cancellationToken);
        else { row = new ProfitSharePartner(); _context.ProfitSharePartners.Add(row); }
        row.Name = model.Name.Trim();
        row.SharePercent = model.SharePercent;
        row.IsActive = true;
        row.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return new { id = row.Id };
    }

    public async Task<object> SaveCustomerAsync(SaveBusinessCustomerViewModel model, CancellationToken cancellationToken = default)
    {
        BusinessCustomer row;
        if (model.Id is > 0)
            row = await _context.BusinessCustomers.FirstAsync(c => c.Id == model.Id, cancellationToken);
        else { row = new BusinessCustomer(); _context.BusinessCustomers.Add(row); }
        row.Name = model.Name.Trim();
        row.Phone = model.Phone;
        row.Place = model.Place;
        row.IsActive = true;
        row.UpdatedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return new { id = row.Id };
    }

    private static Dictionary<int, decimal> ComputeBalances(IReadOnlyList<MoneyAccount> accounts, IReadOnlyList<LedgerTransaction> tx)
    {
        var map = accounts.ToDictionary(a => a.Id, a => a.OpeningBalance);
        foreach (var t in tx.OrderBy(x => x.Date))
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
        return map;
    }

    private static DateTime ParseMonth(string? month)
    {
        if (!string.IsNullOrWhiteSpace(month) && DateTime.TryParse(month + "-01", out var parsed))
            return parsed;
        return DateTime.Today;
    }
}

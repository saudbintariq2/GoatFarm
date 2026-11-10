using GoatFarm.Application.Common;
using GoatFarm.Application.ViewModels.Finance;
using GoatFarm.Domain.Entities;
using GoatFarm.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GoatFarm.Infrastructure.Services;

public partial class FinanceService
{
  public async Task<FinanceIntegrityViewModel> GetIntegrityCheckAsync(CancellationToken cancellationToken = default)
    {
        var issues = new List<FinanceIntegrityIssueViewModel>();
        var links = await _context.LedgerTransactions.AsNoTracking()
            .Where(t => t.Reference != null)
            .Select(t => new { t.Reference, t.AccountId, t.Amount })
            .ToListAsync(cancellationToken);
        var linkRefs = links.Select(l => l.Reference!).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var funding = await _context.OwnerInvestments.AsNoTracking()
            .Where(o => o.Amount > 0)
            .Select(o => new { o.Id, o.Amount, o.Note, o.Date })
            .ToListAsync(cancellationToken);
        var incomes = await _context.Incomes.AsNoTracking()
            .Where(i => i.Amount > 0)
            .Select(i => new { i.Id, i.Amount, i.Type, i.Date })
            .ToListAsync(cancellationToken);
        var expenses = await _context.Expenses.AsNoTracking()
            .Where(e => e.Amount > 0)
            .Select(e => new { e.Id, e.Amount, e.Type, e.Date })
            .ToListAsync(cancellationToken);

        int unlinkedFunding = 0, unlinkedIncome = 0, unlinkedExpense = 0;
        foreach (var o in funding)
        {
            var key = $"fin-funding:{o.Id}";
            if (!linkRefs.Contains(key))
            {
                unlinkedFunding++;
                issues.Add(new FinanceIntegrityIssueViewModel
                {
                    Severity = "critical",
                    Code = "UNLINKED_FUNDING",
                    Message = $"Owner funding Rs {o.Amount:N0} on {o.Date:yyyy-MM-dd} has no Cash/Bank movement ({Truncate(o.Note, 60)})."
                });
            }
        }

        foreach (var i in incomes)
        {
            var key = $"fin-income:{i.Id}";
            if (!linkRefs.Contains(key))
            {
                unlinkedIncome++;
                issues.Add(new FinanceIntegrityIssueViewModel
                {
                    Severity = "critical",
                    Code = "UNLINKED_INCOME",
                    Message = $"Income Rs {i.Amount:N0} ({i.Type}) on {i.Date:yyyy-MM-dd} is not posted to an account."
                });
            }
        }

        foreach (var e in expenses)
        {
            var key = $"fin-expense:{e.Id}";
            if (!linkRefs.Contains(key))
            {
                unlinkedExpense++;
                issues.Add(new FinanceIntegrityIssueViewModel
                {
                    Severity = "critical",
                    Code = "UNLINKED_EXPENSE",
                    Message = $"Expense Rs {e.Amount:N0} ({e.Type}) on {e.Date:yyyy-MM-dd} is not posted to an account."
                });
            }
        }

        foreach (var dup in links.Where(l => l.Reference!.StartsWith("fin-", StringComparison.OrdinalIgnoreCase))
                     .GroupBy(l => l.Reference, StringComparer.OrdinalIgnoreCase)
                     .Where(g => g.Count() > 1))
        {
            issues.Add(new FinanceIntegrityIssueViewModel
            {
                Severity = "critical",
                Code = "DUPLICATE_LEDGER_REF",
                Message = $"Duplicate ledger entries for {dup.Key} ({dup.Count()} rows)."
            });
        }

        foreach (var orphan in links.Where(l => l.Reference!.StartsWith("fin-income:", StringComparison.OrdinalIgnoreCase)))
        {
            if (!int.TryParse(orphan.Reference!["fin-income:".Length..], out var id)) continue;
            if (!await _context.Incomes.AnyAsync(i => i.Id == id, cancellationToken))
                issues.Add(new FinanceIntegrityIssueViewModel
                {
                    Severity = "warn",
                    Code = "ORPHAN_LEDGER",
                    Message = $"Ledger movement references deleted income #{id}."
                });
        }

        foreach (var orphan in links.Where(l => l.Reference!.StartsWith("fin-expense:", StringComparison.OrdinalIgnoreCase)))
        {
            if (!int.TryParse(orphan.Reference!["fin-expense:".Length..], out var id)) continue;
            if (!await _context.Expenses.AnyAsync(e => e.Id == id, cancellationToken))
                issues.Add(new FinanceIntegrityIssueViewModel
                {
                    Severity = "warn",
                    Code = "ORPHAN_LEDGER",
                    Message = $"Ledger movement references deleted expense #{id}."
                });
        }

        foreach (var orphan in links.Where(l => l.Reference!.StartsWith("fin-funding:", StringComparison.OrdinalIgnoreCase)))
        {
            if (!int.TryParse(orphan.Reference!["fin-funding:".Length..], out var id)) continue;
            if (!await _context.OwnerInvestments.AnyAsync(o => o.Id == id, cancellationToken))
                issues.Add(new FinanceIntegrityIssueViewModel
                {
                    Severity = "warn",
                    Code = "ORPHAN_LEDGER",
                    Message = $"Ledger movement references deleted funding #{id}."
                });
        }

        var negativeFeed = await _context.FeedPrices.AsNoTracking()
            .Where(f => f.StockKg < 0)
            .Select(f => f.DisplayName)
            .ToListAsync(cancellationToken);
        foreach (var name in negativeFeed)
        {
            issues.Add(new FinanceIntegrityIssueViewModel
            {
                Severity = "warn",
                Code = "NEGATIVE_FEED_STOCK",
                Message = $"Feed stock for {name} is below zero in the books."
            });
        }

        var overpaid = await _context.SupplierPayables.AsNoTracking()
            .Where(p => p.PaidAmount > p.Amount + 0.01m)
            .Select(p => new { p.SupplierName, p.Amount, p.PaidAmount })
            .Take(20)
            .ToListAsync(cancellationToken);
        foreach (var p in overpaid)
        {
            issues.Add(new FinanceIntegrityIssueViewModel
            {
                Severity = "warn",
                Code = "OVERPAID_PAYABLE",
                Message = $"Payable to {p.SupplierName} is overpaid (owed Rs {p.Amount:N0}, paid Rs {p.PaidAmount:N0})."
            });
        }

        foreach (var row in links.Where(l => l.Reference!.StartsWith("fin-", StringComparison.OrdinalIgnoreCase) && (l.AccountId is null or <= 0)))
        {
            issues.Add(new FinanceIntegrityIssueViewModel
            {
                Severity = "critical",
                Code = "MOVEMENT_NO_ACCOUNT",
                Message = $"Ledger row {row.Reference} has no account."
            });
        }

        var activeAccounts = await _context.MoneyAccounts.AsNoTracking().CountAsync(a => a.IsActive, cancellationToken);
        if (activeAccounts == 0)
        {
            issues.Add(new FinanceIntegrityIssueViewModel
            {
                Severity = "critical",
                Code = "NO_MONEY_ACCOUNTS",
                Message = "No active Cash or Bank account — add Farm Cash and Farm Bank under Bank & Cash."
            });
        }

        var moneyAccounts = await _context.MoneyAccounts.AsNoTracking().Where(a => a.IsActive).ToListAsync(cancellationToken);
        var ledgerTx = await _context.LedgerTransactions.AsNoTracking().ToListAsync(cancellationToken);
        var moneyBalances = ComputeMoneyBalances(moneyAccounts, ledgerTx);
        foreach (var acct in moneyAccounts)
        {
            var bal = moneyBalances.GetValueOrDefault(acct.Id, acct.OpeningBalance);
            if (bal < -0.01m)
            {
                issues.Add(new FinanceIntegrityIssueViewModel
                {
                    Severity = "warn",
                    Code = "LOW_ACCOUNT_BALANCE",
                    Message = $"{acct.Name} is below zero (Rs {bal:N0}). Record funding or fix payments."
                });
            }
        }

        return new FinanceIntegrityViewModel
        {
            Issues = issues.OrderByDescending(i => i.Severity).ThenBy(i => i.Code).ToList(),
            CriticalCount = issues.Count(i => i.Severity == "critical"),
            WarningCount = issues.Count(i => i.Severity != "critical"),
            UnlinkedFundingCount = unlinkedFunding,
            UnlinkedIncomeCount = unlinkedIncome,
            UnlinkedExpenseCount = unlinkedExpense
        };
    }

    private static Dictionary<int, decimal> ComputeMoneyBalances(IReadOnlyList<MoneyAccount> accounts, IReadOnlyList<LedgerTransaction> tx)
    {
        var map = accounts.ToDictionary(a => a.Id, a => a.OpeningBalance);
        foreach (var t in tx.OrderBy(x => x.Date))
        {
            if (t.Kind == LedgerTransactionKind.Transfer)
            {
                if (t.AccountId is int from && map.ContainsKey(from)) map[from] -= t.Amount;
                if (t.CounterAccountId is int to && map.ContainsKey(to)) map[to] += t.Amount;
                continue;
            }
            if (t.AccountId is null || !map.ContainsKey(t.AccountId.Value)) continue;
            map[t.AccountId.Value] += t.Kind switch
            {
                LedgerTransactionKind.Income or LedgerTransactionKind.InvoicePayment => t.Amount,
                LedgerTransactionKind.Expense or LedgerTransactionKind.PayablePayment => -t.Amount,
                _ => 0
            };
        }
        return map;
    }

    public async Task<RelinkUnpostedMoneyResultViewModel> RelinkUnpostedMoneyAsync(
        RelinkUnpostedMoneyViewModel model, CancellationToken cancellationToken = default)
    {
        EnsureMoneyAccount(model.DefaultAccountId);
        var account = await _context.MoneyAccounts.FirstOrDefaultAsync(a => a.Id == model.DefaultAccountId && a.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("Selected account was not found.");

        var linkedRefs = await _context.LedgerTransactions.AsNoTracking()
            .Where(t => t.Reference != null)
            .Select(t => t.Reference!)
            .ToListAsync(cancellationToken);
        var have = linkedRefs.ToHashSet(StringComparer.OrdinalIgnoreCase);

        int funding = 0, income = 0, expense = 0;
        var messages = new List<string> { $"Default account: {account.Name}" };

        if (!model.DryRun)
        {
            foreach (var o in await _context.OwnerInvestments.Where(x => x.Amount > 0).ToListAsync(cancellationToken))
            {
                var key = $"fin-funding:{o.Id}";
                if (have.Contains(key)) continue;
                await SyncCashMovementAsync(key, o.Date, o.Amount, model.DefaultAccountId, LedgerTransactionKind.Income,
                    "Shareholder funding", o.Note, cancellationToken);
                funding++;
                have.Add(key);
            }

            foreach (var i in await _context.Incomes.Where(x => x.Amount > 0).ToListAsync(cancellationToken))
            {
                var key = $"fin-income:{i.Id}";
                if (have.Contains(key)) continue;
                await SyncCashMovementAsync(key, i.Date, i.Amount, model.DefaultAccountId, LedgerTransactionKind.Income,
                    "Linked income", i.Type, cancellationToken);
                income++;
                have.Add(key);
            }

            foreach (var e in await _context.Expenses.Where(x => x.Amount > 0).ToListAsync(cancellationToken))
            {
                var key = $"fin-expense:{e.Id}";
                if (have.Contains(key)) continue;
                await SyncCashMovementAsync(key, e.Date, e.Amount, model.DefaultAccountId, LedgerTransactionKind.Expense,
                    "Linked expense", e.Type, cancellationToken);
                expense++;
                have.Add(key);
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
        else
        {
            foreach (var o in await _context.OwnerInvestments.AsNoTracking().Where(x => x.Amount > 0).ToListAsync(cancellationToken))
                if (!have.Contains($"fin-funding:{o.Id}")) funding++;
            foreach (var i in await _context.Incomes.AsNoTracking().Where(x => x.Amount > 0).ToListAsync(cancellationToken))
                if (!have.Contains($"fin-income:{i.Id}")) income++;
            foreach (var e in await _context.Expenses.AsNoTracking().Where(x => x.Amount > 0).ToListAsync(cancellationToken))
                if (!have.Contains($"fin-expense:{e.Id}")) expense++;
        }

        messages.Add(model.DryRun
            ? $"Dry run: would link {funding} funding, {income} income, {expense} expense rows."
            : $"Linked {funding} funding, {income} income, {expense} expense rows.");

        return new RelinkUnpostedMoneyResultViewModel
        {
            FundingLinked = funding,
            IncomeLinked = income,
            ExpenseLinked = expense,
            Messages = messages
        };
    }

    private async Task<FinanceBalanceSheetViewModel> BuildBalanceSheetAsync(FinancePageViewModel page, CancellationToken cancellationToken)
    {
        var accounts = await _context.MoneyAccounts.AsNoTracking().Where(a => a.IsActive).ToListAsync(cancellationToken);
        var tx = await _context.LedgerTransactions.AsNoTracking().ToListAsync(cancellationToken);
        var balances = ComputeLedgerBalances(accounts, tx);
        var cashAndBank = balances.Values.Sum();

        var receivables = page.Sales.Sum(s => s.Due);
        var livestock = page.LivestockValue;
        var fixedAssets = GetAssetsValue() + page.FixedAssetRegisterValue;
        var projects = page.ProjectSpend;
        var feed = page.FeedStockValue;
        var totalAssets = cashAndBank + receivables + livestock + fixedAssets + projects + feed;

        var payables = await _context.SupplierPayables.AsNoTracking()
            .Where(p => p.Status == PayableStatus.Open || p.Status == PayableStatus.Partial)
            .ToListAsync(cancellationToken);
        var loansOpen = payables
            .Where(p => p.Description.StartsWith("Loan", StringComparison.OrdinalIgnoreCase))
            .Sum(p => Math.Max(0, p.Amount - p.PaidAmount));
        var payablesOpen = payables
            .Where(p => !p.Description.StartsWith("Loan", StringComparison.OrdinalIgnoreCase))
            .Sum(p => Math.Max(0, p.Amount - p.PaidAmount));
        var totalLiabilities = payablesOpen + loansOpen;

        var cumulativeProfit = await GetCumulativeProfitAsync(cancellationToken);
        var ownerFunding = page.OwnerInvestmentTotal;
        var equity = ownerFunding + cumulativeProfit;
        var netWorth = totalAssets - totalLiabilities;
        var imbalance = Math.Abs(netWorth - equity);

        return new FinanceBalanceSheetViewModel
        {
            CashAndBank = cashAndBank,
            Receivables = receivables,
            LivestockValue = livestock,
            FixedAssetsValue = fixedAssets,
            ProjectSpend = projects,
            FeedStockValue = feed,
            TotalAssets = totalAssets,
            PayablesOpen = payablesOpen,
            LoansOpen = loansOpen,
            TotalLiabilities = totalLiabilities,
            OwnerFundingTotal = ownerFunding,
            CumulativeProfit = cumulativeProfit,
            Equity = equity,
            NetWorth = netWorth,
            IsBalanced = imbalance <= 1m,
            ImbalanceAmount = imbalance
        };
    }

    private async Task<decimal> GetCumulativeProfitAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var firstIncome = await _context.Incomes.MinAsync(i => (DateOnly?)i.Date, cancellationToken);
        var firstExpense = await _context.Expenses.MinAsync(e => (DateOnly?)e.Date, cancellationToken);
        var from = new[] { firstIncome, firstExpense, today }.Where(d => d.HasValue).Min() ?? today;
        var stmt = await GetStatementAsync(from, today, cancellationToken);
        return stmt.Income - stmt.Expense;
    }

    private static Dictionary<int, decimal> ComputeLedgerBalances(IReadOnlyList<MoneyAccount> accounts, IReadOnlyList<LedgerTransaction> tx)
    {
        var map = accounts.ToDictionary(a => a.Id, a => a.OpeningBalance);
        foreach (var t in tx.OrderBy(x => x.Date))
        {
            if (t.Kind == LedgerTransactionKind.Transfer)
            {
                if (t.AccountId is int from && map.ContainsKey(from)) map[from] -= t.Amount;
                if (t.CounterAccountId is int to && map.ContainsKey(to)) map[to] += t.Amount;
                continue;
            }

            if (t.AccountId is null || !map.ContainsKey(t.AccountId.Value)) continue;
            map[t.AccountId.Value] += t.Kind switch
            {
                LedgerTransactionKind.Income or LedgerTransactionKind.InvoicePayment => t.Amount,
                LedgerTransactionKind.Expense or LedgerTransactionKind.PayablePayment => -t.Amount,
                _ => 0
            };
        }

        return map;
    }

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..max] + "…";

    public async Task<IReadOnlyList<FinanceMonthlyClosingRowViewModel>> BuildMonthlyClosingRowsAsync(
        string monthKey, CancellationToken cancellationToken = default)
    {
        var (anchorStart, _) = MonthHelper.GetMonthRange(monthKey);
        var accounts = await _context.MoneyAccounts.AsNoTracking().Where(a => a.IsActive).ToListAsync(cancellationToken);
        var txs = await _context.LedgerTransactions.AsNoTracking()
            .OrderBy(t => t.Date).ThenBy(t => t.Id).ToListAsync(cancellationToken);
        var invoices = await _context.CustomerInvoices.AsNoTracking()
            .Where(i => i.Status != InvoiceStatus.Void).ToListAsync(cancellationToken);
        var payables = await _context.SupplierPayables.AsNoTracking()
            .Where(p => p.Status != PayableStatus.Void).ToListAsync(cancellationToken);

        decimal BalanceAt(DateOnly date)
        {
            var map = accounts.ToDictionary(a => a.Id, a => a.OpeningBalance);
            foreach (var t in txs.Where(x => x.Date < date))
            {
                if (t.Kind == LedgerTransactionKind.Transfer)
                {
                    if (t.AccountId is int from && map.ContainsKey(from)) map[from] -= t.Amount;
                    if (t.CounterAccountId is int to && map.ContainsKey(to)) map[to] += t.Amount;
                    continue;
                }

                if (t.AccountId is null || !map.ContainsKey(t.AccountId.Value)) continue;
                map[t.AccountId.Value] += t.Kind switch
                {
                    LedgerTransactionKind.Income or LedgerTransactionKind.InvoicePayment => t.Amount,
                    LedgerTransactionKind.Expense or LedgerTransactionKind.PayablePayment => -t.Amount,
                    _ => 0
                };
            }

            return map.Values.Sum();
        }

        var rows = new List<FinanceMonthlyClosingRowViewModel>();
        for (var i = 5; i >= 0; i--)
        {
            var start = anchorStart.AddMonths(-i);
            var end = start.AddMonths(1);
            var slice = txs.Where(t => t.Date >= start && t.Date < end).ToList();
            var moneyIn = slice.Where(t => t.Kind is LedgerTransactionKind.Income or LedgerTransactionKind.InvoicePayment).Sum(t => t.Amount);
            var moneyOut = slice.Where(t => t.Kind is LedgerTransactionKind.Expense or LedgerTransactionKind.PayablePayment).Sum(t => t.Amount);
            var ar = invoices.Where(inv => inv.Date < end && inv.Status is InvoiceStatus.Open or InvoiceStatus.Partial)
                .Sum(inv => inv.Amount - inv.PaidAmount);
            var ap = payables.Where(p => p.Date < end && p.Status is PayableStatus.Open or PayableStatus.Partial)
                .Sum(p => p.Amount - p.PaidAmount);
            rows.Add(new FinanceMonthlyClosingRowViewModel
            {
                Month = $"{start.Year:0000}-{start.Month:00}",
                OpeningCash = BalanceAt(start),
                MoneyIn = moneyIn,
                MoneyOut = moneyOut,
                ClosingCash = BalanceAt(end),
                ReceivablesOpen = Math.Max(0, ar),
                PayablesOpen = Math.Max(0, ap)
            });
        }

        return rows;
    }
}

using GoatFarm.Application.Common;
using GoatFarm.Application.ViewModels.Finance;
using GoatFarm.Domain.Constants;
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
            .Where(o => o.Amount > 0 && !o.IsNonCash)
            .Select(o => new { o.Id, o.Amount, o.Note, o.Date })
            .ToListAsync(cancellationToken);
        var creditIncomeIds = await CreditSaleIncomeIdsAsync(cancellationToken);
        var incomes = (await _context.Incomes.AsNoTracking()
            .Where(i => i.Amount > 0)
            .Select(i => new { i.Id, i.Amount, i.Type, i.Date })
            .ToListAsync(cancellationToken))
            .Where(i => !creditIncomeIds.Contains(i.Id))
            .ToList();
        var expenses = (await _context.Expenses.AsNoTracking()
            .Where(e => e.Amount > 0)
            .Select(e => new { e.Id, e.Amount, e.Type, e.Date, e.SourceType, e.Comment })
            .ToListAsync(cancellationToken))
            .Where(e => !FinanceSourceTypes.IsNonCashExpense(e.SourceType, e.Type, e.Comment))
            .ToList();

        int unlinkedFunding = 0, unlinkedIncome = 0, unlinkedExpense = 0, unlinkedFeed = 0, unlinkedVacc = 0;

        var cashProductSales = await _context.ProductSales.AsNoTracking()
            .Where(s => !s.IsCredit && s.Amount > 0)
            .Select(s => new { s.Id, s.Amount, s.Date, s.ProductName, s.InvoiceNo })
            .ToListAsync(cancellationToken);
        foreach (var s in cashProductSales.Where(s => !linkRefs.Contains(LedgerSourceKey.ProductSale(s.Id))))
        {
            unlinkedIncome++;
            issues.Add(new FinanceIntegrityIssueViewModel
            {
                Severity = "critical",
                Code = "UNLINKED_PRODUCT_SALE",
                Message = $"Products sale {s.InvoiceNo} ({s.ProductName}) Rs {s.Amount:N0} on {s.Date:yyyy-MM-dd} is not on Bank & Cash."
            });
        }
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

        var feedBuys = await _context.FeedPurchases.AsNoTracking()
            .Where(p => p.PaidAmount > 0)
            .Select(p => new { p.Id, p.PaidAmount, p.Date, p.FeedType })
            .ToListAsync(cancellationToken);
        foreach (var p in feedBuys)
        {
            var key = $"feed_purchase:{p.Id}";
            if (!linkRefs.Contains(key))
            {
                unlinkedFeed++;
                issues.Add(new FinanceIntegrityIssueViewModel
                {
                    Severity = "critical",
                    Code = "UNLINKED_FEED_PURCHASE",
                    Message = $"Feed purchase paid Rs {p.PaidAmount:N0} ({p.FeedType}) on {p.Date:yyyy-MM-dd} is not on Bank & Cash."
                });
            }
        }

        var vaccBuys = await _context.VaccinePurchases.AsNoTracking()
            .Where(v => v.PaidAmount > 0)
            .Select(v => new { v.Id, v.PaidAmount, v.Date, v.Name })
            .ToListAsync(cancellationToken);
        foreach (var v in vaccBuys)
        {
            var key = $"vaccine_purchase:{v.Id}";
            if (!linkRefs.Contains(key))
            {
                unlinkedVacc++;
                issues.Add(new FinanceIntegrityIssueViewModel
                {
                    Severity = "critical",
                    Code = "UNLINKED_VACCINE_PURCHASE",
                    Message = $"Vaccine purchase paid Rs {v.PaidAmount:N0} ({v.Name}) on {v.Date:yyyy-MM-dd} is not on Bank & Cash."
                });
            }
        }

        foreach (var dup in links.Where(l => LedgerSourceKey.TryParse(l.Reference, out _, out _))
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

        var (livestockValue, alreadyFunded, livestockGap) = await ComputeLivestockFundingGapAsync(cancellationToken);
        if (livestockGap > 1m)
        {
            issues.Add(new FinanceIntegrityIssueViewModel
            {
                Severity = "critical",
                Code = "UNFUNDED_LIVESTOCK",
                Message = $"Livestock on the books is Rs {livestockValue:N0} but only Rs {alreadyFunded:N0} is backed by owner funding or goat-purchase payments. Gap Rs {livestockGap:N0} — use Post livestock as owner funding on staging."
            });
        }

        var imbalance = await ComputeBalanceSheetImbalanceAsync(cancellationToken);
        if (imbalance > 1m)
        {
            issues.Add(new FinanceIntegrityIssueViewModel
            {
                Severity = "critical",
                Code = "BALANCE_SHEET_IMBALANCE",
                Message = $"Balance sheet off by Rs {imbalance:N0}. Run Re-link unposted money, post livestock funding, then refresh."
            });
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var cumulative = await GetCumulativeProfitAsync(cancellationToken);
        var stmt = await GetStatementAsync(await GetBooksStartDateAsync(cancellationToken), today, cancellationToken);
        var stmtProfit = stmt.Income - stmt.Expense;
        if (Math.Abs(cumulative - stmtProfit) > 1m)
        {
            issues.Add(new FinanceIntegrityIssueViewModel
            {
                Severity = "warn",
                Code = "PROFIT_STATEMENT_DRIFT",
                Message = $"All-time P&L (Rs {stmtProfit:N0}) does not match balance sheet cumulative profit (Rs {cumulative:N0}). Re-link and rebuild feed stock after import."
            });
        }

        return new FinanceIntegrityViewModel
        {
            Issues = issues.OrderByDescending(i => i.Severity).ThenBy(i => i.Code).ToList(),
            CriticalCount = issues.Count(i => i.Severity == "critical"),
            WarningCount = issues.Count(i => i.Severity != "critical"),
            UnlinkedFundingCount = unlinkedFunding,
            UnlinkedIncomeCount = unlinkedIncome,
            UnlinkedExpenseCount = unlinkedExpense,
            UnlinkedFeedPurchaseCount = unlinkedFeed,
            UnlinkedVaccinePurchaseCount = unlinkedVacc,
            UnfundedLivestockGap = livestockGap,
            StatementProfitAllTime = stmtProfit,
            BalanceSheetCumulativeProfit = cumulative
        };
    }

    public async Task<FundLivestockResultViewModel> FundLivestockAsOwnerContributionAsync(
        FundLivestockAsOwnerViewModel model, CancellationToken cancellationToken = default)
    {
        var (livestockValue, alreadyFunded, gap) = await ComputeLivestockFundingGapAsync(cancellationToken);
        if (gap <= 1m)
        {
            return new FundLivestockResultViewModel
            {
                Success = true,
                DryRun = model.DryRun,
                LivestockValue = livestockValue,
                AlreadyFunded = alreadyFunded,
                AmountToPost = 0,
                Message = "Livestock is already fully backed by funding or purchase payments."
            };
        }

        if (model.DryRun)
        {
            return new FundLivestockResultViewModel
            {
                Success = true,
                DryRun = true,
                LivestockValue = livestockValue,
                AlreadyFunded = alreadyFunded,
                AmountToPost = gap,
                Message = $"Dry run: would post Rs {gap:N0} as a non-cash livestock contribution (no Cash/Bank movement)."
            };
        }

        _context.OwnerInvestments.Add(new OwnerInvestment
        {
            Amount = gap,
            Date = await GetBooksStartDateAsync(cancellationToken),
            Note = "Opening herd · Livestock contribution (non-cash)",
            IsNonCash = true
        });
        await _context.SaveChangesAsync(cancellationToken);

        return new FundLivestockResultViewModel
        {
            Success = true,
            LivestockValue = livestockValue,
            AlreadyFunded = alreadyFunded,
            AmountToPost = gap,
            Message = $"Posted Rs {gap:N0} non-cash livestock contribution (Rs {livestockValue:N0} on herd). Bank & Cash is unchanged."
        };
    }

    private async Task<(decimal LivestockValue, decimal AlreadyFunded, decimal Gap)> ComputeLivestockFundingGapAsync(
        CancellationToken cancellationToken)
    {
        var livestock = GetLivestockValue();
        var goatCash = await _context.Expenses.AsNoTracking()
            .Where(e => e.Type == "Goat purchase")
            .SumAsync(e => (decimal?)e.Amount, cancellationToken) ?? 0;
        var ownerLivestock = (await _context.OwnerInvestments.AsNoTracking()
            .Select(o => new { o.Amount, o.Note })
            .ToListAsync(cancellationToken))
            .Where(o => IsLivestockFundingNote(o.Note))
            .Sum(o => o.Amount);
        var already = goatCash + ownerLivestock;
        var gap = Math.Max(0, livestock - already);
        return (livestock, already, gap);
    }

    private async Task<DateOnly> GetBooksStartDateAsync(CancellationToken cancellationToken)
    {
        var firstIncome = await _context.Incomes.MinAsync(i => (DateOnly?)i.Date, cancellationToken);
        var firstExpense = await _context.Expenses.MinAsync(e => (DateOnly?)e.Date, cancellationToken);
        var firstFunding = await _context.OwnerInvestments.MinAsync(o => (DateOnly?)o.Date, cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var candidates = new[] { firstIncome, firstExpense, firstFunding }.Where(d => d.HasValue).Select(d => d!.Value).ToList();
        return candidates.Count > 0 ? candidates.Min() : today;
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

        int funding = 0, income = 0, expense = 0, feed = 0, vacc = 0;
        var messages = new List<string> { $"Default account: {account.Name}" };
        var creditIncomeIds = await CreditSaleIncomeIdsAsync(cancellationToken);
        bool CashIncome(Income i) => !creditIncomeIds.Contains(i.Id);
        static bool CashExpense(Expense e) => !FinanceSourceTypes.IsNonCashExpense(e.SourceType, e.Type, e.Comment);

        if (!model.DryRun)
        {
            foreach (var s in await _context.ProductSales.Where(x => !x.IsCredit && x.Amount > 0).ToListAsync(cancellationToken))
            {
                var key = LedgerSourceKey.ProductSale(s.Id);
                if (have.Contains(key)) continue;
                s.AccountId ??= model.DefaultAccountId;
                await SyncCashMovementAsync(key, s.Date, s.Amount, s.AccountId, LedgerTransactionKind.Income,
                    "Product sale", $"Sale {s.InvoiceNo} · {s.ProductName}", cancellationToken);
                income++;
                have.Add(key);
            }

            foreach (var o in await _context.OwnerInvestments.Where(x => x.Amount > 0 && !x.IsNonCash).ToListAsync(cancellationToken))
            {
                var key = $"fin-funding:{o.Id}";
                if (have.Contains(key)) continue;
                await SyncCashMovementAsync(key, o.Date, o.Amount, model.DefaultAccountId, LedgerTransactionKind.Income,
                    "Shareholder funding", o.Note, cancellationToken);
                funding++;
                have.Add(key);
            }

            foreach (var i in (await _context.Incomes.Where(x => x.Amount > 0).ToListAsync(cancellationToken)).Where(CashIncome))
            {
                var key = $"fin-income:{i.Id}";
                if (have.Contains(key)) continue;
                await SyncCashMovementAsync(key, i.Date, i.Amount, model.DefaultAccountId, LedgerTransactionKind.Income,
                    "Linked income", i.Type, cancellationToken);
                income++;
                have.Add(key);
            }

            foreach (var e in (await _context.Expenses.Where(x => x.Amount > 0).ToListAsync(cancellationToken)).Where(CashExpense))
            {
                var key = $"fin-expense:{e.Id}";
                if (have.Contains(key)) continue;
                await SyncCashMovementAsync(key, e.Date, e.Amount, model.DefaultAccountId, LedgerTransactionKind.Expense,
                    "Linked expense", e.Type, cancellationToken);
                expense++;
                have.Add(key);
            }

            foreach (var p in await _context.FeedPurchases.Where(x => x.PaidAmount > 0).ToListAsync(cancellationToken))
            {
                var key = $"feed_purchase:{p.Id}";
                if (have.Contains(key)) continue;
                await SyncCashMovementAsync(key, p.Date, p.PaidAmount, p.AccountId ?? model.DefaultAccountId,
                    LedgerTransactionKind.Expense, "Feed purchase", p.Supplier ?? p.FeedType, cancellationToken);
                feed++;
                have.Add(key);
            }

            foreach (var v in await _context.VaccinePurchases.Where(x => x.PaidAmount > 0).ToListAsync(cancellationToken))
            {
                var key = $"vaccine_purchase:{v.Id}";
                if (have.Contains(key)) continue;
                await SyncCashMovementAsync(key, v.Date, v.PaidAmount, v.AccountId ?? model.DefaultAccountId,
                    LedgerTransactionKind.Expense, "Vaccine purchase", v.Name, cancellationToken);
                vacc++;
                have.Add(key);
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
        else
        {
            foreach (var o in await _context.OwnerInvestments.AsNoTracking().Where(x => x.Amount > 0 && !x.IsNonCash).ToListAsync(cancellationToken))
                if (!have.Contains($"fin-funding:{o.Id}")) funding++;
            foreach (var s in await _context.ProductSales.AsNoTracking().Where(x => !x.IsCredit && x.Amount > 0).ToListAsync(cancellationToken))
                if (!have.Contains(LedgerSourceKey.ProductSale(s.Id))) income++;
            foreach (var i in (await _context.Incomes.AsNoTracking().Where(x => x.Amount > 0).ToListAsync(cancellationToken)).Where(CashIncome))
                if (!have.Contains($"fin-income:{i.Id}")) income++;
            foreach (var e in (await _context.Expenses.AsNoTracking().Where(x => x.Amount > 0).ToListAsync(cancellationToken)).Where(CashExpense))
                if (!have.Contains($"fin-expense:{e.Id}")) expense++;
            foreach (var p in await _context.FeedPurchases.AsNoTracking().Where(x => x.PaidAmount > 0).ToListAsync(cancellationToken))
                if (!have.Contains($"feed_purchase:{p.Id}")) feed++;
            foreach (var v in await _context.VaccinePurchases.AsNoTracking().Where(x => x.PaidAmount > 0).ToListAsync(cancellationToken))
                if (!have.Contains($"vaccine_purchase:{v.Id}")) vacc++;
        }

        messages.Add(model.DryRun
            ? $"Dry run: would link {funding} funding, {income} income, {expense} expenses, {feed} feed buys, {vacc} vaccine buys."
            : $"Linked {funding} funding, {income} income, {expense} expenses, {feed} feed buys, {vacc} vaccine buys.");

        return new RelinkUnpostedMoneyResultViewModel
        {
            FundingLinked = funding,
            IncomeLinked = income,
            ExpenseLinked = expense,
            FeedPurchaseLinked = feed,
            VaccinePurchaseLinked = vacc,
            Messages = messages
        };
    }

    private async Task<decimal> ComputeBalanceSheetImbalanceAsync(CancellationToken cancellationToken)
    {
        var month = MonthHelper.CurrentMonthKey();
        var page = await GetFinancePageAsync(month, new FinancePageLoadOptions
        {
            IncludeTabRegisters = true,
            IncludeIntegrityAndBalance = false
        }, cancellationToken);
        var bs = await BuildBalanceSheetAsync(page, cancellationToken);
        return bs.IsBalanced ? 0 : bs.ImbalanceAmount;
    }

    /// <summary>Incomes created by credit goat sales: their cash arrives later as an invoice receipt, never as the income itself.</summary>
    private async Task<HashSet<int>> CreditSaleIncomeIdsAsync(CancellationToken cancellationToken) =>
        (await _context.GoatSales.AsNoTracking()
            .Where(s => s.IsCredit && s.IncomeId != null)
            .Select(s => s.IncomeId!.Value)
            .ToListAsync(cancellationToken))
        .ToHashSet();

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

        var monthKey = MonthHelper.CurrentMonthKey();
        var payrollOpen = await FinancePayablesBuilder.PayrollOutstandingAsync(_context, monthKey, cancellationToken);
        var (_, payablesOpen, loansOpen) = await FinancePayablesBuilder.BuildAsync(_context, payrollOpen, cancellationToken);
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

    private static bool IsLivestockFundingNote(string? note)
    {
        if (string.IsNullOrWhiteSpace(note)) return false;
        return note.Contains("livestock", StringComparison.OrdinalIgnoreCase)
            || (note.Contains("goat", StringComparison.OrdinalIgnoreCase)
                && note.Contains("owner contribution", StringComparison.OrdinalIgnoreCase));
    }

    private async Task<FinanceCashFlowViewModel> BuildCashFlowAsync(DateOnly from, DateOnly endExclusive, CancellationToken cancellationToken)
    {
        var accounts = await _context.MoneyAccounts.AsNoTracking().Where(a => a.IsActive).ToListAsync(cancellationToken);
        var txs = await _context.LedgerTransactions.AsNoTracking().Where(t => t.Date < endExclusive).ToListAsync(cancellationToken);
        var opening = ComputeLedgerBalances(accounts, txs.Where(t => t.Date < from).ToList()).Values.Sum();
        var closing = ComputeLedgerBalances(accounts, txs).Values.Sum();
        var active = accounts.Select(a => a.Id).ToHashSet();
        var slice = txs.Where(t => t.Date >= from && t.Kind != LedgerTransactionKind.Transfer
                                   && t.AccountId is int id && active.Contains(id)).ToList();

        static string Label(LedgerTransaction t)
        {
            var r = t.Reference ?? "";
            if (r.StartsWith("fin-funding:", StringComparison.OrdinalIgnoreCase)) return "You put in (owner funding)";
            if (t.Kind == LedgerTransactionKind.InvoicePayment) return "Customer payments received";
            if (t.Kind == LedgerTransactionKind.PayablePayment)
                return r.StartsWith("payroll:", StringComparison.OrdinalIgnoreCase) ? "Salaries paid" : "Supplier bills paid";
            if (r.StartsWith("capex:", StringComparison.OrdinalIgnoreCase) || r.StartsWith("fixed_asset:", StringComparison.OrdinalIgnoreCase))
                return "Capital projects / assets";
            return string.IsNullOrWhiteSpace(t.Category) ? (t.Kind == LedgerTransactionKind.Income ? "Other money in" : "Other money out") : t.Category!;
        }

        List<FinanceLineViewModel> Lines(IEnumerable<LedgerTransaction> rows) => rows
            .GroupBy(Label, StringComparer.OrdinalIgnoreCase)
            .Select(g => new FinanceLineViewModel { Label = g.Key, Amount = g.Sum(t => t.Amount) })
            .Where(l => l.Amount > 0)
            .OrderByDescending(l => l.Amount)
            .ToList();

        var inRows = slice.Where(t => t.Kind is LedgerTransactionKind.Income or LedgerTransactionKind.InvoicePayment).ToList();
        var outRows = slice.Where(t => t.Kind is LedgerTransactionKind.Expense or LedgerTransactionKind.PayablePayment).ToList();
        return new FinanceCashFlowViewModel
        {
            Opening = opening,
            MoneyIn = inRows.Sum(t => t.Amount),
            MoneyOut = outRows.Sum(t => t.Amount),
            Closing = closing,
            InLines = Lines(inRows),
            OutLines = Lines(outRows)
        };
    }

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

        decimal BalanceAt(DateOnly date) =>
            ComputeLedgerBalances(accounts, txs.Where(x => x.Date < date).ToList()).Values.Sum();
        var activeIds = accounts.Select(a => a.Id).ToHashSet();

        var currentKey = MonthHelper.CurrentMonthKey();
        var payrollNow = await FinancePayablesBuilder.PayrollOutstandingAsync(_context, currentKey, cancellationToken);
        var (_, payablesNow, _) = await FinancePayablesBuilder.BuildAsync(_context, payrollNow, cancellationToken);

        var rows = new List<FinanceMonthlyClosingRowViewModel>();
        for (var i = 5; i >= 0; i--)
        {
            var start = anchorStart.AddMonths(-i);
            var end = start.AddMonths(1);
            var slice = txs.Where(t => t.Date >= start && t.Date < end && t.AccountId is int id && activeIds.Contains(id)).ToList();
            var moneyIn = slice.Where(t => t.Kind is LedgerTransactionKind.Income or LedgerTransactionKind.InvoicePayment).Sum(t => t.Amount);
            var moneyOut = slice.Where(t => t.Kind is LedgerTransactionKind.Expense or LedgerTransactionKind.PayablePayment).Sum(t => t.Amount);
            var ar = invoices.Where(inv => inv.Date < end && inv.Status is InvoiceStatus.Open or InvoiceStatus.Partial)
                .Sum(inv => inv.Amount - inv.PaidAmount);
            var key = $"{start.Year:0000}-{start.Month:00}";
            decimal ap;
            if (key == currentKey)
                ap = payablesNow;
            else
            {
                var payroll = await FinancePayablesBuilder.PayrollOutstandingAsync(_context, key, cancellationToken);
                ap = payables.Where(p => p.Date < end && p.Status is PayableStatus.Open or PayableStatus.Partial
                                         && !p.Description.StartsWith("Loan", StringComparison.OrdinalIgnoreCase))
                         .Sum(p => p.Amount - p.PaidAmount)
                     + payroll.Sum(p => p.Balance);
            }
            rows.Add(new FinanceMonthlyClosingRowViewModel
            {
                Month = key,
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

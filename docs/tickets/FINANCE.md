# Finance — Sandeel fix list (FN1–FN18)

| ID | Item | Status |
|----|------|--------|
| FN1 | Balance sheet: livestock funded, opening balances, feed stock value | **Done (staging)** — `FundLivestockAsOwner`; `UNFUNDED_LIVESTOCK` integrity; Feed rebuild for store value; new goats use **Paid from** |
| FN2 | Out of balance → **critical** integrity issue | Done — `BALANCE_SHEET_IMBALANCE` in `GetIntegrityCheckAsync` |
| FN3 | All-time profit matches P&L accrual | **Done** — same `GetStatementAsync`; `PROFIT_STATEMENT_DRIFT` integrity warning if mismatch |
| FN4–FN6 | Ledger on all money events, cash flow, monthly closing | 🟡 **Relink** now includes feed & vaccine purchases; run on staging; goat/milk/product ledger audit still ops |
| FN7 | CAPEX/OPEX cash paid from ledger only | **Done** — CAPEX/OPEX tab **Cash paid** from ledger; capex rows show cash column |
| FN8 | Payables card = list (+ salary AP) | **Done** — `FinancePayablesBuilder`: feed credit + supplier bills + salary due |
| FN9 | Sales register (goat + product + invoices) | Done — `EnrichAccountingAsync` |
| FN10 | Transactions milk L/Rs from Products | Done — product milk/dairy sales added to month KPI |
| FN11 | Capital & funding cards | Done when `OwnerInvestments` + note tags populated |
| FN12 | Feed P&L = used vs stock on BS | **Done** — P&L line **Feed used** (`GetFeedUsedCostAsync`); **Feed bought** stays on Feed tab / cash only |
| FN13 | Budget “Left” when no budget | Done — total row shows “No budget set” |
| FN14 | Supplier bill + post expense one transaction | Done — `SaveSupplierPayable` + `PostToExpense` |
| FN15 | Death write-off expense | Done — `RecordDeathAsync` |
| FN16–FN17 | Cross-tab duplicate warning | **Done** — supplier bills, feed/vaccine, income/expense, sales, **owner funding**, **manual bank/cash ledger** (linked ledger mirrors excluded; transfers skip dup check) |
| FN18 | Paid from name on expense/income rows | Done — ledger join in `EnrichAccountingAsync` |

## Tests

- `FinancePagePhase1Tests`
- `Phase9FinanceHealthTradingReportsAdminTests`
- `FinancePhaseFnTests` (FN2/FN10/FN14/FN15)

## Staging ops

See **[FINANCE-STAGING.md](FINANCE-STAGING.md)** — dry-run re-link, livestock owner funding, feed rebuild, FN3 sign-off.
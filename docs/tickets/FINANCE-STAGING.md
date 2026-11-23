# Finance staging pass (FN1, FN3, Relink)

Use this checklist **once per staging restore** (or after importing a Sandeel backup).

## 1. Bank & Cash setup

1. Open **Finance → Bank & Cash**.
2. Confirm **Farm Cash** and **Farm Bank** exist.
3. For each account with no ledger activity yet: **Set opening balance** once (locks after save).  
   Opening balances are stored on the account; they are not separate funding rows unless you also record owner funding for audit.

## 2. Integrity review

1. Open **Finance → Overview** (integrity panel).
2. Note counts: unposted funding / income / expense / **feed buys** / **vaccines**.
3. If **UNFUNDED_LIVESTOCK** appears: herd purchase value is not backed by goat-purchase expenses or owner livestock funding.

## 3. Re-link unposted money (FN4–FN6)

1. Choose the **default Cash/Bank account** for historical repair (often the main farm account).
2. Click **Dry run (count only)** — confirm the message lists funding, income, expense, feed, vaccine counts.
3. Click **Re-link to default account**.
4. **Refresh check** — critical unlinked counts should drop to 0 (or only items that truly had no account).

## 4. Post livestock as owner funding (FN1)

Only if integrity shows **UNFUNDED_LIVESTOCK** or balance sheet still off after re-link:

1. Use the same account selector as re-link.
2. Click **Post livestock as owner funding**.
3. One **Owner contribution – livestock (staging bulk)** row is created with ledger movement.

For **new goats**, set **Paid from** on add goat (cash) or record owner funding manually — do not rely on bulk post for day-to-day use.

## 5. Feed stock (FN1c / FN12)

1. **Feed → Rebuild stock** if integrity shows negative feed or BS feed value is zero while store has kg.
2. Re-open Finance balance sheet — **Feed in store** should match Feed tab stock value.

## 6. FN3 — all-time profit vs P&amp;L

1. Balance sheet **Profit / loss (all time)** should match **Finance → P&L** with range *all time* (custom from first book date to today).
2. Both use `GetStatementAsync` (accrual: **Feed used**, salaries, credit purchases via payables — not only cash).
3. If **PROFIT_STATEMENT_DRIFT** warning appears after re-link, refresh once; if it persists, check for duplicate expenses or imports without matching income.

## 7. Sign-off targets

| Check | Pass |
|-------|------|
| Integrity **0 critical** (or documented exceptions) | ☐ |
| Balance sheet **OK** (or imbalance &lt; Rs 1) | ☐ |
| Payables card = list total (feed + bills + salary) | ☐ |
| Monthly closing closing cash = sum of account balances | ☐ |
| TEST Sandeel scenario: cash / payables per FN spec (if applicable) | ☐ |

## API (optional automation)

```http
GET  /Finance/IntegrityCheck
POST /Finance/RelinkUnpostedMoney  { "defaultAccountId": 1, "dryRun": true }
POST /Finance/RelinkUnpostedMoney  { "defaultAccountId": 1, "dryRun": false }
POST /Finance/FundLivestockAsOwner  { "accountId": 1, "dryRun": false }
```
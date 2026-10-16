# V241 parity checklist (vs `index.html` V240)

**AI Voice Assistant** — permanently out of scope (handled elsewhere).

**Legend:** ✅ Done · 🟡 Partial · ⬜ Not started

**Last updated:** After MVP → Done slice **#6** (herd sell workflow, under-treatment panel, treatment history on Search profile).

---

## Quick summary

| Area | Overall |
|------|---------|
| New modules exist (Trading, Products, Projects, Admin, Backup) | 🟡 MVP |
| Cross-cutting (backup v3, approvals, audit) | 🟡 Strong MVP |
| Finance / accounting depth vs V240 | 🟡 |
| Reports depth vs V240 | 🟡 |
| Herd / breeding / feed vs V240 | 🟡 sell + treatment panel on profile |
| Full V240 UI parity | ⬜ not complete |

---

## Your environment (operations)

| Item | Status |
|------|--------|
| PostgreSQL running (e.g. `scripts/start-local-postgres.ps1`) | ⬜ verify locally |
| EF migration `AddV241ExtendedModules` applied | ⬜ verify locally |
| `Seed:DemoData` for sample rows per module | 🟡 optional; in config example |
| Smoke-test modules after rebuild | ⬜ |

---

## MVP → Done sequence (numbered)

| # | Slice | Status | Notes |
|---|--------|--------|--------|
| 1 | Approvals on sensitive deletes + finance edits | ✅ | Staff → queue; Admin/Manager approve; execution wired for herd, finance, milk, feed, vaccines, tasks, projects, trading |
| 2 | Finance tabbed ledger (AR, AP, budget, profit share, cash summary) | 🟡 | Tabs + forms on Finance page; not full COA / balance sheet / bank reconciliation |
| 3 | Trading parties + transfers + period P&amp;L | 🟡 | UI tabs + 6‑month P&amp;L; not full V235 trading UI (payments in/out, deep party ledger) |
| 4 | Products catalog + credit sales → invoices | 🟡 | Catalog tab; credit sale creates `CustomerInvoice`; not full pricing/stock rules vs HTML |
| 5 | Reports CAPEX / projects / profit share with real data | 🟡 | Three tabs populated from DB; other report subtabs were already live; logistics/owners still thin |
| 6 | Herd sell workflow + treatment on goat profile | 🟡 | Sell archives goat + income; paid→ledger, credit→invoice; herd “Under treatment” + Search history |

---

## Cross-cutting

| Item | Status |
|------|--------|
| Backup format **v3** + `v241Extended` in export | ✅ |
| Restore clears + imports v241 extended entities | ✅ |
| Backup Center page + footer link | ✅ |
| Approval queue UI (Admin) | ✅ |
| Approval submit (Staff delete/edit) | ✅ |
| Approval execute on approve | ✅ |
| Audit log list (Admin) | ✅ |
| Change notices (header 📋 → notices / audit) | 🟡 | Not every write logs `ShowInNotices` |
| Dashboard KPIs (receivables, payables, stock, pending approvals) | 🟡 |
| Permissions / `FarmPermissionMap` for new endpoints | 🟡 |
| Demo mode (separate `DemoConnection`) | 🟡 | Config only |
| Staff edit approval beyond Finance | ⬜ | e.g. herd/feed edits not queued |

---

## Modules (vs V240 baseline)

| Module | Status | What you have | Still missing vs V240 (typical) |
|--------|--------|---------------|----------------------------------|
| **Dashboard** | 🟡 | KPIs + activity | Full ratio set / drill-downs from HTML |
| **Herd** | 🟡 | CRUD, groups, weights, death, **sell**, **under-treatment panel** | Bulk extras, full V240 goat card actions |
| **Breeding** | 🟡 | Core breeding UI | HTML depth (extra panels, workflows) |
| **Milk** | 🟡 | Production / sales / waste | Products hub split documented |
| **Products** | 🟡 | Sessions, stock, meat, sales, **catalog**, credit→invoice | Full catalog/pricing/stock UX from HTML |
| **Feed** | 🟡 | Purchases, usage, types | Extra V240 feed analytics / UI |
| **Trading** | 🟡 | Buy/sell/expense, stock check, setup, **parties**, **transfer**, **P&amp;L tab** | Payment in/out UI, full godown analytics, V235 parity |
| **Health / Vaccines** | 🟡 | Vaccines, reminders, treatments by tag, **profile treatment history** | Full health card parity vs HTML |
| **Finance (farm books)** | 🟡 | Capital, income, expense, employees, recurring, owner $ | Every HTML finance panel |
| **Accounting ledger** | 🟡 | **Tabbed** AR/AP, tx, budget, profit share, summary | COA, full statements, edit/delete ledger rows, approvals on ledger |
| **Projects & assets** | 🟡 | Projects, expenditure, fixed assets, solar log | Rich expenditure UI, reports tie-in only partial |
| **Tasks** | 🟡 | Task/reminder CRUD | vs HTML task center depth |
| **Reports** | 🟡 | Most subtabs + **CAPEX/OPEX**, **profit share**, **projects** data | Logistics, investors tabs still light |
| **Admin** | 🟡 | Audit, approvals, change password | Full user/role matrix vs HTML |
| **Backup** | ✅ | Export/import v3 | Round-trip hardening / edge cases |
| **Search** | 🟡 | Baseline | vs HTML |
| **Settings** | 🟡 | Users, lookups | vs HTML |
| **AI assistant** | ⬜ | — | Excluded |

---

## Reports subtabs (finance-visible users)

| Tab | Status |
|-----|--------|
| Dashboard, Herd, Breeding, Growth, Feed, Health | 🟡 (existing server data) |
| Finance, Profitability | 🟡 |
| Inventory | 🟡 |
| **CAPEX & OPEX** | 🟡 live data (this pass) |
| **Profit sharing** | 🟡 live data (this pass) |
| **Projects & assets** | 🟡 live data (this pass) |
| Logistics | ⬜ placeholder |
| Investors / owners | 🟡 note + links |
| Alerts, Comparison | 🟡 |

---

## How to use this doc

- Move rows to ✅ when you’ve verified behavior in the running app (not just “code exists”).
- Keep **#6 Herd** as the next dev slice unless you prioritize COA/statements or Trading payment flows.

Update this file when status changes.

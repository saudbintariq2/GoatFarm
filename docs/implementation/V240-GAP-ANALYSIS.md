# V240 Gap Analysis — Goat Farm Pro vs Sandeel Farms V240 (`index.html`)

Assessment date: 2026-10-13. Baseline: `dotnet build GoatFarm.slnx` succeeded (1 pre-existing warning, CS0618 in `ConnectionStringResolver.cs`).

Classification legend:

| Code | Meaning |
|---|---|
| **OK** | Implemented and verified (UI → controller → permission → service → DB traced) |
| **DEFECT** | Implemented but defective |
| **PARTIAL** | Partially implemented |
| **MISSING** | Not implemented |
| **BLOCKED** | Blocked by an external dependency |
| **DECISION** | Requires a business decision |

"Verified" here means traced through code. *(At the time of this assessment no end-to-end tests existed. Since the stabilization pass the repository has PostgreSQL integration tests and 12 Playwright browser flows; see `V240-TEST-AND-VALIDATION.md` and the "Stabilization pass" section at the end of this file.)*

---

## 0. Cross-cutting defects found during reconnaissance

| # | Finding | Class | Evidence |
|---|---|---|---|
| X1 | Permission filter is **allow-by-default**: any controller action missing from `FarmPermissionMap.ActionPermissions` is allowed to every authenticated user. `Feed.UpdateMixRecipe` (a write), `Goat.GetByTag`, `Search.Lookup` are unmapped. | DEFECT (security) | `Filters/FarmPermissionFilter.cs`, `Authorization/FarmPermissionMap.cs` |
| X2 | All AJAX controllers carry `[IgnoreAntiforgeryToken]`; state-changing JSON endpoints have no CSRF protection beyond `SameSite=Lax`. | DEFECT (security) | 12 controllers in `Controllers/` |
| X3 | Login page prints the default admin email **and password**; `Seed:ResetAdminPassword=true` in the base `appsettings.json` resets the admin password to `Admin123!` on every start, and the bootstrap treated a *missing* key as "reset". (`appsettings.Production.json` already set it to false; Development sets it true.) | DEFECT (critical security) — fixed: base config false, only an explicit `"true"` resets | `Views/Account/Login.cshtml`, `Identity/IdentitySeedHelper.cs`, `appsettings.json` |
| X4 | `PasswordSignInAsync(..., lockoutOnFailure: false)` — no brute-force lockout. | DEFECT (security) | `Controllers/AccountController.cs` |
| X5 | `RememberMe` exists on the model and is passed to sign-in, but the login form has no checkbox → always session cookie. | PARTIAL | `Models/LoginViewModel.cs`, `Login.cshtml` |
| X6 | Auth cookie `SecurePolicy=SameAsRequest` in production. | DEFECT (hardening) | `Program.cs` |
| X7 | JS renders server data (tag, name, notes) into `innerHTML` without escaping → stored XSS vector. | DEFECT (security) | `wwwroot/js/goat-records.js` (breeding, herd, milk renderers) |
| X8 | `tests/GoatFarm.Tests/DoubleEntryGuardTests.cs` exists **without a `.csproj`** and references `DoubleEntryGuardService`, which does not exist. Tests cannot compile or run. | DEFECT | `tests/` |
| X9 | Hard delete of a goat cascades to `VaccinationHistories`, `GoatWeightRecords`, `BreedingEmptyLogs` → silent loss of history. | DEFECT (data integrity) | `EntityConfigurations.cs` |
| X10 | "Server-side pagination" loads **all** live goats into memory, filters in C#, then pages. | PARTIAL (performance) | `GoatService.GetHerdPageAsync` |
| X11 | No version control in the workspace (no `.git`). A zip snapshot was taken to `_backups/` before changes. | BLOCKED (process) | workspace root |
| X12 | `goat-records.css.tmp` (66 KB) shipped in `wwwroot` and in publish output. | DEFECT (hygiene) | `wwwroot/css/` |

---

## 1. Shell, navigation, branding

| Item | Class | Notes / evidence |
|---|---|---|
| Top tab bar with 10 tabs | OK | `Views/Shared/_Layout.cshtml` |
| Left drawer / off-canvas mobile nav | MISSING | |
| Module set: Tasks, Trading, Projects & Assets, Users & Audit | MISSING | `FarmTabs` has no keys for them |
| Farm name from config | OK | `ApplicationSettings:FarmName` |
| Logo upload/branding | MISSING | emoji only |
| Global tag search in shell | PARTIAL | dedicated Search page exists (`SearchController`), not in shell |
| Notifications / change notices | MISSING | no notification entity |
| Conflict indicator | MISSING / DECISION | product is server-authoritative; no sync → no real conflicts to show. Will not be faked. |
| Quick actions | MISSING | |
| Export / Import | OK | `DashboardController.Export/Import`, `BackupService` |
| Auto-backup / Backup Center | MISSING | server-side scheduled backup requires hosting decision (DECISION) |
| AI voice assistant | MISSING / DECISION | requires an external AI provider and key → BLOCKED |

## 2. Authentication / demo

| Item | Class | Notes |
|---|---|---|
| Email login via Identity | OK | |
| Show/hide password | MISSING | |
| Remember Me | PARTIAL | X5 |
| Lockout | DEFECT | X4 |
| Password policy | OK | `UserSettingsService.ValidatePassword`, Settings page |
| Demo mode with isolation | MISSING | Design: separate database selected by config (`Demo:Enabled` + separate connection string). See ARCHITECTURE §6. |

## 3. Dashboard

| Item | Class | Notes |
|---|---|---|
| Herd/breeding/feed/milk/finance/vaccine panels | OK | `DashboardController`, `StatisticsService`, `Dashboard/Index.cshtml` |
| Sparklines, MoM, donut, breeding 12-month chart | MISSING | |
| Today at the farm, Needs attention | MISSING | |
| Tasks panel | MISSING | depends on Tasks module |
| Receivables / payables / inventory value KPIs | MISSING | depend on Accounting ledger |
| Recent activity | MISSING | depends on audit log |

## 4. Tasks

| Item | Class | Notes |
|---|---|---|
| Task entity, assignee, priority, status, board | MISSING | |
| Reminders (title, scope, date) | OK | `Reminder`, `ReminderService`, `ReminderController` (in `VaccineController.cs`) |
| Assignment notifications | MISSING | |

## 5. Herd

| Item | Class | Notes |
|---|---|---|
| Goat CRUD, groups, bulk move, weight, death | OK | `GoatService`, `GoatController`, `Goat/Index.cshtml` |
| `Meat` status | MISSING | `GoatStatus` enum |
| Owner, health state, sire, dam | MISSING | `Goat` entity |
| BCS, wean, young-stock class, temp tag, birth group, feed group, dry-off | MISSING | |
| Treatments panel | MISSING | no Treatment entity |
| Filter chips, health/sire/owner/birth-group filters | MISSING | |
| Wean/classify/graduate/dry-off bulk actions | MISSING | |
| Goat profile card (pedigree, history, actions) | PARTIAL | Search profile shows meta + vaccines + feed + reminders |
| Hard delete | DEFECT | X9 |
| Sell workflow | MISSING | only "For sale" status |

## 6. Breeding

| Item | Class | Notes |
|---|---|---|
| Prep → cross → ultrasound → kidded | PARTIAL | `BreedingService` |
| Cross sets `Status = Pregnant` before any scan | DEFECT | `RecordCrossAsync`, `RecordCrossFromPrepAsync`, `GoatService.BulkMoveAsync br:cross` |
| Next-due computed over unscanned does | DEFECT | `GetBreedingPageAsync` `soonest` uses all `MatedDate` |
| Herd "due ~date" hint for unscanned does | DEFECT | `GoatService.MapGoat` |
| 300-day min age, female check, already-bred check | MISSING | |
| Inbreeding check | MISSING | needs sire/dam |
| Kidding creates kid records | MISSING | `MarkKiddedAsync` only resets doe → Milking |
| Abortions | MISSING | |
| Breeding history (multiple cycles per doe) | MISSING | breeding state lives on the `Goat` row; only empty scans are logged |
| Buck groups, season plan, heat tracking, buck workload | MISSING | |
| Empty-scan list, ration table, 150-day gestation, 145–155 window, 60-day prep | OK | `BreedingHelper` |

## 7. Products / Milk

| Item | Class | Notes |
|---|---|---|
| Milk production / sale / waste CRUD with pagination | OK | `MilkService`, `MilkController` |
| Sessions, rejected milk, tanks, pasteurization, bottling, spoilage reasons | MISSING | |
| Meat (slaughter, split, cuts) | MISSING | |
| Unified sales with customers/credit | MISSING | depends on Accounting AR |
| Stock ledger, locations, reconciliation | MISSING | |
| Breed performance, transport, costing, pricing, catalog | MISSING | |

## 8. Trading — MISSING entirely.

## 9. Feed

| Item | Class | Notes |
|---|---|---|
| Store, stock check, auto usage, plans, recipes, fodder pool, order list | OK | `FeedService` |
| Kid Milk Diet, milk powder, kid starter | MISSING | |
| Custom feeding groups | MISSING | plans keyed by `GoatStatus` only |
| Purchase payment / supplier / payable | MISSING | depends on Accounting |
| Weighted-average cost | MISSING | |
| Stock status column / avg cost column | MISSING | |

## 10. Health

| Item | Class | Notes |
|---|---|---|
| Schedules, due/upcoming, give-to-group, history edit, purchases | OK | `VaccineService` |
| Schedule comment | MISSING | |
| Treatments / illness / withdrawal | MISSING | |
| Purchase via cash/bank or payable | MISSING | depends on Accounting |

## 11. Accounting

| Item | Class | Notes |
|---|---|---|
| Income/expense/assets/owner investment/employees/recurring CRUD | OK | `FinanceService` |
| Chart of accounts, journal, double entry | MISSING | |
| AR/AP, bank & cash, P&L, cash flow, balance sheet, budget, profit sharing, payroll | MISSING | |
| Duplicate-entry guard | DEFECT | X8 — tests exist, service missing |
| Manual "Vaccines" expense type double-counts auto vaccine purchases | DEFECT | `LookupConstants.ExpenseTypes` + `FinanceService.GetFinancePageAsync` |
| "Salary advance" expense alongside auto staff salaries | DECISION. *Since 2026-10-14: blocked, with a read-only reconciliation (D29/D30).* | may double-count |

## 12. Projects & Assets — MISSING (only simple `Asset` list).

## 13. Reports

| Item | Class | Notes |
|---|---|---|
| 11 report tabs, filters, CSV, print | OK | `ReportsService` |
| CAPEX/OPEX, profit sharing, projects, logistics, investors tabs | MISSING | depend on modules above |

## 14. Users & audit

| Item | Class | Notes |
|---|---|---|
| Users CRUD, lock, reset password, role defaults matrix, per-user perms, password policy | OK | `SettingsController`, `UserSettingsService` |
| Audit log | MISSING | |
| Custom roles beyond Admin/Manager/Staff | MISSING | |
| My profile / change own password | MISSING | |

---

## Stabilization pass (2026-10-13) — status changes

| Item | Now | Evidence |
|---|---|---|
| Backup misses breeding events, kidding records, tasks | OK — format v2 includes them, validates before deleting anything, restores in one transaction | `BackupRoundTripPgTests`, `BackupValidationTests` |
| V240 migration on real PostgreSQL | OK on a disposable database (upgrade, re-run, rollback, FK rules). Rollback drops V240 data. | `V240MigrationPgTests` |
| Herd paging loaded all goats into memory | OK — filtering, ordering and paging run in SQL | `HerdPagingTests`, `HerdPagingPgTests` |
| X7 client-side XSS | OK for Reports (all user text escaped) and the remaining `innerHTML` sinks; server-built alert/stock HTML encodes user values | `XssAndCsvTests` |
| CSV formula injection | OK — shared `csvCell` escaper | `XssAndCsvTests` |
| Settings privilege escalation (a Settings editor could make themselves Admin) | OK — `UserAdminGuard` | `UserAdminGuardTests`, browser flow 11 |
| Last Admin could be demoted or locked | OK | `UserAdminPgTests` |
| Duplicate-entry guard | OK — salary, wage and salary advance blocked | `DoubleEntryGuardTests` |
| "Salary advance" expense | Blocked; existing rows **NEEDS OWNER** (D6) | |
| Goat profile 500 on PostgreSQL, Search page JS crash, concurrent birth 500 | Fixed (found by browser tests) | `SearchProfilePgTests`, `BirthIdempotencyPgTests`, browser flows 5 & 8 |
| Staff see farm income/profit on Dashboard and Reports | DECISION (D26) | |
| Manager backup export | DECISION (D23) — Admin only today | |
| Purchased goat date of birth | DECISION (D22) | |
| Dashboard performance (~200 queries per load, occasional 11 s load) | PARTIAL — documented, not changed | `V240-TEST-AND-VALIDATION.md` |

## Hardening phase (2026-10-14) — status changes

| Item | Now | Evidence |
|---|---|---|
| Staff see farm income/profit on Dashboard and Reports | **OK** (D26): redacted on the server for users without Finance‑view, in pages, JSON and CSV; the non-financial dashboard is kept | `FinanceVisibilityTests`, `FinanceVisibilityPgTests`, browser flow 13 |
| Manager backup export | Unchanged, DECISION (D23). No Settings access was granted to provide backup access | |
| New salary-advance expenses | **OK** (D29): blocked on add, update, recurring cost and the expense-type list; legacy types hidden from the dropdown and marked on existing rows | `SalaryReconciliationTests`, `DoubleEntryGuardTests`, browser flow 14 |
| Existing salary-advance rows | **PARTIAL** (D30): a read-only reconciliation report sorts them into confirmed, needs-review and not-double-counted. Correction is manual. Keeping a row but excluding it from totals needs the accounting ledger (MISSING) | `SalaryReconciliationTests`, `SalaryReconciliationPgTests` |
| Purchased goat date of birth | **OK** (D31/D32): optional and estimable; the server rejects future dates; unknown or estimated ages need confirmation before a cross | `DateOfBirthTests`, `DateOfBirthPgTests`, browser flow 20 |
| Dashboard query count | **OK**: 205 → ~75 queries, 0.8–1.0 s → 0.3–0.4 s warm | `DashboardQueryPgTests` |
| Cold start (~4.5 s on the first request) | **PARTIAL**: measured, not fixed; cause not isolated | `V240-TEST-AND-VALIDATION.md` |
| Feed purchase server validation | **OK** (D33). Previously a DEFECT, found by browser flow 17 | |
| Finance page 500 after the legacy-type filter; Reports Profitability tab never opened; view-only users saw empty modules | **Fixed** (found by browser flows 14, 19, 16) | |
| Browser coverage for Feed, Milk, Finance, Vaccines/Health, Reports | **OK**: flows 15–19 cover workflows, validation, reload persistence, permissions, empty states and hostile text | `ModuleFlowTests` |
| Products (beyond Milk), Projects, Bank screens | MISSING, not built, so not tested | |
| Safari, Firefox, real phones | **NOT TESTED** | |
| Trading, Projects & Assets, accounting ledger | Not started (out of scope for this phase) | |

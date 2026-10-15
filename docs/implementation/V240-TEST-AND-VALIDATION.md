# V240 Test & Validation

Only results that were actually executed are recorded as passed. Last full run: 2026-10-14 (hardening phase; see the "Hardening phase" section at the end).

## Results

| Run | Command | Result |
|---|---|---|
| Unit + PostgreSQL + browser | `GOATFARM_TEST_PG=… GOATFARM_E2E=1 dotnet test GoatFarm.slnx` | **199 passed, 0 failed, 0 skipped** (2 m 53 s) |
| Unit only (no infrastructure) | `dotnet test GoatFarm.slnx` | 146 passed, 53 skipped (31 PostgreSQL, 20 browser, 2 PG-backed XSS checks). Each skip reports `BLOCKED: …` with the missing dependency. |
| New module browser flows, repeated | `--filter FullyQualifiedName~ModuleFlowTests`, 3 consecutive runs after the last fix | 8/8, 8/8, 8/8 |
| Build | `dotnet build GoatFarm.slnx` | 0 errors, 0 warnings |
| JS syntax | Esprima parse of `wwwroot/js/goat-records.js` | OK |

*Previous run (2026-10-13, stabilization): 129 passed with infrastructure; 95 passed and 34 skipped without.*

The PostgreSQL and browser tests were run against a **disposable PostgreSQL 18 cluster** (`127.0.0.1:55432`, created under `%TEMP%`), one fresh database per test. The development and production databases were never touched; no migration was applied to them.

## How to run

```powershell
dotnet build GoatFarm.slnx
dotnet test tests\GoatFarm.Tests                       # unit tests; PG and browser tests report BLOCKED

# PostgreSQL tests: a throwaway server on loopback (never a real farm database)
$env:GOATFARM_TEST_PG = "Host=127.0.0.1;Port=55432;Username=postgres"
dotnet test tests\GoatFarm.Tests

# Browser tests (Playwright; uses installed Edge, else Chrome, else bundled Chromium)
$env:GOATFARM_E2E = "1"
dotnet test tests\GoatFarm.Tests --filter "Category=E2E"
```

The browser fixture starts the built `GoatFarm.Web.dll` with a brand-new empty database, strips every `DATABASE_*URL` / `PG*` / connection-string environment variable so it cannot reach another database, forces `Seed:ClearFarmData=false`, and checks that the migrations and Identity tables exist in the throwaway database before any test runs. The app log is written to `%TEMP%\goatfarm-e2e-app.log`.

## What the tests cover

| Class | Tests | Needs | Covers |
|---|---|---|---|
| `DoubleEntryGuardTests` | 17 | — | Salary/wage and salary-advance expenses blocked even when confirmed; vaccines/feed/duplicates warn; "Salary advance" not in default types; unrelated advances allowed |
| `BreedingRulesTests` | 12 | — | Age/sex/status eligibility, unknown age for bought kids, inbreeding detection |
| `BreedingServiceTests` | 11 | — | Cross, scan, birth, temp tags, idempotent birth, all-or-nothing bulk cross |
| `TaskServiceTests` | 3 | — | Assignee rules, KPIs |
| `PermissionMapTests` | 3 | — | Every action mapped (deny-by-default), backup needs Settings permissions |
| `HerdPagingTests` | 3 | — | Every filter/search returns the same goats as before; one page materialized; page clamp |
| `BackupValidationTests` | 15 | — | Version, missing sections, duplicates, broken references, self-parent, legacy duplicates |
| `UserAdminGuardTests` | 10 | — | Self-change, Admin target, Admin role, permission subset, farm-wide security Admin-only |
| `XssAndCsvTests` | 23 | 2 need PG | Shipped JS `esc` and `csvCell` executed in Jint (formulas, numbers, Urdu); server alerts and stock-check detail HTML-encode user tags/feed names |
| `V240MigrationPgTests` | 4 | PG | Upgrade from V41 data, idempotent re-run, rollback, FK delete rules/indexes/defaults |
| `BackupRoundTripPgTests` | 6 | PG | Export → import identical; failure halfway rolls back; invalid file rejected before delete; restore twice no duplicates; no credentials exported; legacy V19 file |
| `HerdPagingPgTests` | 2 | PG | Filters translate to SQL; LIMIT applied in SQL |
| `UserAdminPgTests` | 4 | PG | Last unlocked Admin cannot be demoted/locked/deleted (real Identity) |
| `SearchProfilePgTests` | 3 | PG | Goat profile and daily feed cost load on PostgreSQL |
| `BirthIdempotencyPgTests` | 1 | PG | Four concurrent identical birth submits → one record, two kids, no error |
| `BrowserFlowTests` | 12 | PG + browser | See below |

### Browser flows (all 12 passed)

Each flow also fails on any same-origin console error, page error, failed request, 5xx response, or `fail:`/`crit:` server log line.

1. Login/logout: bad password, password show/hide, sign in, sign out, protected pages redirect.
2. Navigation: desktop collapse; mobile menu opens, Escape closes, no horizontal overflow.
3. Dashboard panels and Stats endpoint.
4. Herd paging: search, page 2, status filter combined with search, out-of-range page clamped.
5. Goat created through the form (Urdu name, HTML in owner) → found by global tag search; duplicate tag rejected.
6. Breeding eligibility: under-age, bought kid, male, female "buck", valid; rejected cross saves nothing.
7. Scans: positive → confirmed; empty → cycle closed.
8. Birth: double-click save → one record, two kids; replay → already recorded; three parallel identical requests → all 200, one record.
9. Bulk breeding: one ineligible doe → whole selection rolled back; then cross of two eligible does.
10. Tasks: validation, create, start, complete (Urdu notes saved).
11. Backup & settings authorization: Admin export v2 without password hashes; Staff gets 403 on export/import/user permissions/expenses and no export button; a Manager with delegated Settings permissions cannot promote self, create an Admin, grant permissions they lack, or change role permissions — nothing created, still one Admin.
12. Validation and empty states, including salary advance → 409 block with no row saved.

## Bugs found by these tests and fixed

| Bug | Found by | Fix |
|---|---|---|
| Every goat profile (Search page) returned 500 on PostgreSQL — untranslatable LINQ in `FeedService.CalculateDailyFeedCost` | Browser flow 5 | Parse the status first, then query |
| Search page JS crashed with a SyntaxError when a tag was in the URL — `initialTag` JSON was HTML-encoded | Browser flow 5 | `Html.Raw` around the serialized JSON |
| Concurrent identical birth submits returned 500 (unique `ClientRequestId` race) | Browser flow 8 | Catch the conflict, return the winning record as "already recorded" |
| Herd save/move left an unhandled promise rejection after the error modal | Browser flows | `try/catch` around those calls (the modal is still shown) |

## Migration verification (disposable PostgreSQL)

- Upgrading a database holding V41 data keeps every legacy goat column byte-identical; breeding stages are derived from existing dates only; a goat whose status says Pregnant but has no mating date is **not** given an invented breeding cycle.
- Re-running the idempotent script creates no duplicate events.
- **Rollback (`Down`) loses data added after the upgrade.** It drops `BreedingEvents`, `KiddingRecords`, `FarmTasks` and the new goat columns (owner, health, sire/dam links, stage, temporary-tag flag). Legacy goat data survives. Take a `pg_dump` before upgrading and restore from it rather than rolling back once the farm has used V240.
- Goat deletion cannot cascade into breeding or kidding history (`RESTRICT`); kid → kidding record and task → goat/user links become NULL.

## Before upgrading the real development database (owner action)

1. `pg_dump -Fc` the database and confirm the dump restores into a scratch database (`pg_restore -d scratch`).
2. Apply `V240-migration-AddV240Foundation.sql` (or start the app, which migrates on startup).
3. Check `SELECT "BreedingStage", count(*) FROM "Goats" GROUP BY 1;` and `SELECT count(*) FROM "BreedingEvents";`.
4. Recovery: restore the dump. Do not rely on `Down` (see above).

## Known gaps (stabilization pass, 2026-10-13)

- **Performance:** *superseded; see "Dashboard performance" below.* At the time, each dashboard load ran ~200 queries, and the first load after start-up took 16 s.
- Not covered by browser tests at the time: Feed, Milk, Finance, Vaccines and Reports. *They are covered since the hardening phase; see below.*
- Remember-me persistence across a browser restart and the 5-attempt lockout were not exercised in the browser. *Still true.*

---

## Hardening phase (2026-10-14)

### New and extended test classes

| Class | Tests | Needs | Covers |
|---|---|---|---|
| `FinanceVisibilityTests` | 8 | — | Dashboard and Reports redaction removes every financial figure but keeps farm data; redaction returns a copy and leaves the shared cached model intact; `Stats`/`GetData` include finance only with Finance‑view; redacted JSON contains no amounts; only Admin and Manager have Finance‑view by default |
| `FinanceVisibilityPgTests` | 1 | PG | A real report flags every financial figure, and redaction removes them |
| `SalaryReconciliationTests` | 26 | — | Classification in English, Roman Urdu and Urdu; recurring-cost guard; legacy types hidden from the dropdown without rewriting the stored list; `AddOption` rejects salary types; reconciliation outcomes and totals; months; no employees means not double counted; read-only (0 `SaveChanges`, data unchanged) |
| `SalaryReconciliationPgTests` | 1 | PG | Same totals on PostgreSQL; expense rows unchanged afterwards |
| `DateOfBirthTests` | 17 | — | Age sources; the 300-day rule with known, estimated and unknown ages; age confirmation shown together with the sire/inbreeding warning; known adult needs no confirmation; cross before DOB refused; bulk cross warning; create/update/clear validation (future date, after purchase, >25 years, ignored for Born); age filters; death age |
| `DateOfBirthPgTests` | 4 | PG | Migration from `AddHerdQueryIndexes` over legacy goats inserted by SQL: legacy columns byte-identical, new columns NULL/false, "age unknown"; rollback drops only the two DOB columns; age filter translates to SQL; backup round trip with DOB, and a future DOB in a backup is rejected |
| `DashboardQueryPgTests` | 5 | PG | Query count and timing; no per-status count queries remain; dashboard and report figures equal the old per-status counting; grouped counts equal SQL counts and exclude archived goats; counts refresh after a save in the same request |
| `BackupRoundTripPgTests` | 6 (extended) | PG | The snapshot now includes `dateOfBirth`/`dateOfBirthIsEstimated` |
| `ModuleFlowTests` | 8 | PG + browser | Browser flows 13–20 below |

### Browser flows 13–20 (all passed; three consecutive runs)

Every flow runs under the same health checks as flows 1–12. Hostile text means `<img src=x onerror="window.__xss=1"> اردو`; each flow asserts that it is shown as text and never executes.

13. **Finance visibility.**
    - Staff: no dashboard finance panel, and `Stats` returns `financeHidden` with zero money. `Reports/GetData` is redacted, the Finance and Profitability tabs are absent, and the CSV lacks Revenue, Net profit, INCOME VS EXPENSES and the seeded amount 123,456. `/Finance` redirects to AccessDenied, and `Finance/GetData` and `Finance/SalaryReconciliation` return 403.
    - Manager: sees all of it.
14. **Salary.** An employee is added in the UI, and legacy rows are inserted by SQL.
    - The type dropdown has no salary types.
    - The reconciliation panel lists the legacy rows, with the matched employee in the JSON.
    - The legacy chip is shown. Editing shows "(no longer accepted)", and saving is refused.
    - A recurring salary is refused, the API returns 409, and `AddOption` returns 400.
15. **Finance expense:** validation, save, persistence after reload, hostile text.
16. **Milk:** validation; a collection and a sale persist after reload; hostile text. A view-only user sees the rows without the add form, and the API returns 403 for them.
17. **Feed and stock:**
    - Validation in the UI; three invalid API purchases return 400.
    - A UI purchase raises stock by exactly 40 kg and persists after reload.
    - Hostile text; empty month (`/Feed?month=2001-01`).
18. **Vaccines and Health:** purchase validation; a purchase with hostile text persists after reload; a goat set to Sick appears under the `health:sick` filter.
19. **Reports and exports:** every tab opens without errors. The CSV escapes a tag starting with `@` (leading apostrophe), and the admin CSV includes INCOME VS EXPENSES.
20. **Date of birth:**
    - The Born source disables the DOB field.
    - An estimated DOB shows "~400" with an "estimated" label, and is saved with the estimated flag.
    - A future DOB is rejected on edit.
    - Unknown age shows "age unknown" and is excluded from the age filter.
    - The breeding check warns "age is unknown". Cancelling the cross saves nothing; confirming saves it.

**Not tested:** Safari, Firefox and real phones. Mobile is covered only by Chromium/Edge with a phone viewport (flow 2). **Products** (beyond Milk), **Projects** and **Bank** are not built: their view folders are empty and they have no controllers, so "Milk and Products" covers Milk only.

### Bugs found by the new tests and fixed

| Bug | Found by | Fix |
|---|---|---|
| The Finance page returned 500 whenever the expense-type list was loaded: `List<string>` was cast to `string[]` in the view, a regression from hiding legacy salary types | Flow 14 | `LookupService` returns an array |
| The Reports Profitability tab never opened: the duplicate id `rep-profit` was used by both the tab panel and a KPI | Flow 19 | KPI renamed `rep-profit-val` |
| View-only users saw an empty module (the whole panel was hidden) | Flow 16 | D34 |
| Finance add handlers left an uncaught promise rejection after the error modal | Flow 15 | `try/catch` (the modal is still shown) |
| The feed purchase API accepted zero/negative kg and unknown feed names | Flow 17 | D33 |

Not fixed, but noted: the Milk, Feed, Vaccine and Employee add handlers have no `try/catch`. Client validation runs first, so a server rejection is unusual there, but if one happens the browser logs an uncaught rejection after the modal. The user sees the error either way.

### Dashboard performance (disposable PostgreSQL, same seeded data)

| Measure | Before | After |
|---|---|---|
| Queries per dashboard build | 205–207 (133 per-status head counts) | 73–76 (no per-status counts) |
| Build time, connection pooling on, warm | 775–990 ms | 294–410 ms |
| Build time, pooling off | 22–23 s | 4.9–9.6 s |

- The per-status counts were replaced by one grouped count per request (`GoatService.GetLiveStatusCounts`, cached for the request and cleared whenever the database context saves). `DashboardQueryPgTests` checks that the figures are identical.
- **Pooling:** the 11–16 s loads seen in browser tests came from `Pooling=false` combined with dashboard cache misses: each query opened a new connection. Production connection strings should keep pooling on (the Npgsql default).
- **Cold start is not resolved.** The first request after start-up takes about 4.5 s, and very little of that is database time. This was measured, not fixed, and the cause (JIT, Razor compilation, EF model building) was not isolated. No claim is made that cold start is improved.
- **Measuring on a server:** set `Diagnostics:ServerTiming=true` (off by default). Each page and API response then carries a `Server-Timing` header with database time, query count and total time, which browser dev tools show under Network → Timing. Each response also gets a log line: `Timing GET /Dashboard: 200, N queries, X ms database, Y ms total`. Turn it off again afterwards.

### Migration `AddGoatDateOfBirth` (disposable PostgreSQL)

- **Up** adds `Goats.DateOfBirth date NULL` and `Goats.DateOfBirthIsEstimated boolean NOT NULL DEFAULT FALSE`, and changes nothing else. Verified on SQL-inserted legacy goats: every existing column is unchanged, and no DOB is filled in.
- **Down** drops only those two columns. Any DOBs entered after the upgrade are lost; take a dump first.
- Reviewable script: `V240-migration-AddGoatDateOfBirth.sql`. It is idempotent and assumes `AddHerdQueryIndexes` is already applied. It was **not** applied to the development or production database.

### Owner upgrade steps for this phase

1. `pg_dump -Fc` the database and check that it restores into a scratch database.
2. Apply the pending migrations: start the app, which migrates on startup, or run the SQL scripts in order.
3. Check: `SELECT count(*) FROM "Goats" WHERE "DateOfBirth" IS NOT NULL;` should return 0 right after the upgrade.
4. Open Finance. If the salary reconciliation panel appears, review it (D30) before deleting anything, and export a backup first.
5. Check that the people who should see farm money have Finance‑view (D26). Staff no longer see it by default.

### Infrastructure used and safety

- Every PostgreSQL and browser test ran against a disposable PostgreSQL 18 cluster at `127.0.0.1:55432` under `%TEMP%`, with one fresh database per test. The cluster was stopped afterwards.
- No test connected to the development or production database, no migration was applied to them, and no restore or import test touched a real farm database.
- No historical financial record is modified by code in this phase. The reconciliation report is read-only, and the only changes are the ones a user makes by hand.

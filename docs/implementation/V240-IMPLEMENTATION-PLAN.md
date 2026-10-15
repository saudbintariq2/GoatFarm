# V240 Implementation Plan & Handover Checklist

Update the checkboxes as work lands. Each phase must build, pass tests, and produce a reviewable migration before the next starts.

## Dependency order

```
Phase 0 security/baseline ─┬─> Phase 1 Tasks ──────────────┐
                           ├─> Phase 2 Herd/Breeding ──────┼─> Phase 3 Shell & Dashboard v2
                           │                               │
                           └─> Phase 4 Audit/Notifications ┴─> Phase 5 Accounting ledger
                                                                 └─> Phase 6 Stock ledger
                                                                       ├─> Phase 7 Products (milk/dairy/meat/sales)
                                                                       ├─> Phase 8 Feed v2 (WAC, kid diet, groups, payables)
                                                                       ├─> Phase 9 Trading
                                                                       └─> Phase 10 Projects & Assets, Reports v2
Phase 11 Health treatments/withdrawal (needs Phase 2 + 7 for milk withholding)
Phase 12 Demo mode isolation
```

## Phase 0 — Security & baseline

- [x] Snapshot source to `_backups/`
- [x] Deny-by-default permission filter; map `Feed.UpdateMixRecipe`, `Goat.GetByTag`, `Search.Lookup`
- [x] Global antiforgery with `X-CSRF-TOKEN` header; remove `[IgnoreAntiforgeryToken]`
- [x] Login: show/hide password, Remember Me checkbox, lockout on failure
- [x] Secure cookies outside Development; sliding persistent cookie
- [x] Stop displaying / auto-resetting default admin password
- [x] Test project `tests/GoatFarm.Tests/GoatFarm.Tests.csproj` added to solution
- [x] `DoubleEntryGuardService` implemented and called from `FinanceController.AddExpense`
- [x] Backup export requires Settings‑view, import requires Settings‑edit (was Dashboard‑view; see D18)
- [x] Permission-map coverage test: every controller action is mapped or explicitly open

## Phase 1 — Tasks

- [x] `FarmTask` entity + configuration + migration
- [x] `ITaskService` / `TaskService`
- [x] `TasksController` + permission map + `tasks` tab
- [x] Tasks page: KPIs, filters, board, create/edit/start/complete/reopen/delete
- [x] Dashboard "My open tasks" panel
- [ ] Notifications on assignment (Phase 4)
- [ ] Recurring tasks

## Phase 2 — Herd & Breeding correctness

- [x] `GoatStatus.Meat`; `GoatHealthState`; `BreedingStage`
- [x] Goat: Owner, HealthState, SireId/DamId (+ free-text parent tags), BreedingStage, IsTemporaryTag
- [x] `BreedingEvent`, `KiddingRecord` entities + migration with stage back-fill
- [x] Cross no longer sets Pregnant; scan confirms
- [x] Eligibility (female, active, ≥300 days, no open event)
- [x] Inbreeding check (parents, siblings, shared grandparents; unknown ≠ none)
- [x] Next-due & herd hint only for confirmed pregnancies
- [x] Record Birth wizard (transactional, idempotent, creates kids)
- [x] Abortion recording
- [x] Herd form: Owner, Health, Sire, Dam; filters `br:waiting`, `br:due30`, health (UI + server)
- [ ] Owner filter dropdown in Herd UI (server filter `owner:{name}` exists; no UI option yet)
- [x] Herd bulk "Ready for cross" / "Crossed" use the same rules as Breeding, all-or-nothing
- [x] Backup import restores Owner/Health/parent links/stage (export already includes the columns)
- [ ] Backup export/import of `BreedingEvents`, `KiddingRecords`, `FarmTasks` (not included yet)
- [x] Goat hard delete blocked when breeding/kidding/offspring history exists
- [ ] Heat tracking & 21-day cycle
- [ ] Buck groups, season plan, buck workload, breed performance
- [ ] BCS, wean/classify/graduate bulk actions, birth groups, feed groups
- [ ] Goat profile modal with pedigree tree

## Phase 3 — Shell & dashboard v2

- [x] Left navigation drawer (desktop collapsible, mobile off-canvas), grouped modules, active highlight
- [x] Global tag search box in top bar → Search page
- [x] Farm-local date in top bar
- [ ] Quick actions panel (wire to real workflows)
- [ ] Dashboard sparklines, donut, breeding chart, Today/Needs-attention

## Verification status (2026-10-13)

| Check | Result |
|---|---|
| `dotnet build GoatFarm.slnx` | Succeeded (1 pre-existing warning: obsolete `TrustServerCertificate`) |
| `dotnet test GoatFarm.slnx` | 40 passed, 0 failed |
| `dotnet ef migrations has-pending-model-changes` | No pending changes |
| Idempotent SQL script | `docs/implementation/V240-migration-AddV240Foundation.sql` — additive only, no `DROP` |
| JS syntax (`goat-records.js`, Esprima parse) | OK |
| App run against PostgreSQL / browser check | **Not done** — would auto-apply the migration to the dev database; needs the owner to run it |

## Phase 4+ — not started (see ARCHITECTURE §4–§6)

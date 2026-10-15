# V240 Architecture

The existing layering is kept as-is:

```
GoatFarm.Domain          entities, enums, constants (no dependencies)
GoatFarm.Application     service interfaces, view models (DTOs), pure helpers
GoatFarm.Infrastructure  EF Core DbContext + configurations + migrations, service implementations
GoatFarm.Web             MVC controllers (Razor page + JSON endpoints), filters, views, wwwroot JS/CSS
tests/GoatFarm.Tests     xUnit, EF Core InMemory
```

Patterns retained:

- One controller per module; `Index` renders the Razor page, other actions are JSON endpoints called from `wwwroot/js/goat-records.js` via `api()`.
- Services are injected as `IXxxService` (scoped), registered in `Infrastructure/DependencyInjection.cs`.
- Entities derive from `BaseEntity` (`int Id`, UTC `CreatedDate`/`UpdatedDate`). Business dates are `DateOnly`. Money is `decimal` with `HasPrecision(18,2)`.
- Authorization: `FarmPermissionFilter` + `FarmPermissionMap` map `Controller.Action` → (tab, action). Tabs are keys in `FarmTabs`; role defaults live in `UserSettingsService.DefaultRolePermissions`. Adding a key to `FarmTabs.All` automatically back-fills defaults for stored role permissions (`EnsureMissingTabs`).

## 1. Security changes (Phase 0)

- **Deny-by-default:** `FarmPermissionFilter` now rejects any non-anonymous action that is not an `Index` page in `IndexTabs` and not in `ActionPermissions`, unless the controller is explicitly listed in `FarmPermissionMap.OpenControllers` (`Home`). A new endpoint without a map entry fails closed (403) instead of open.
- **Antiforgery for AJAX:** global `AutoValidateAntiforgeryTokenAttribute`; token header `X-CSRF-TOKEN`; token emitted in `<meta name="csrf-token">` by `_Layout.cshtml`; `goat-records.js` wraps `fetch` to attach the header on same-origin non-GET requests. All `[IgnoreAntiforgeryToken]` attributes removed.
- **Cookies:** auth + antiforgery cookies `Secure=Always` outside Development; auth cookie 14-day sliding expiry used only when Remember Me is ticked (otherwise session cookie).
- **Lockout:** 5 failed attempts → 15-minute lockout.
- **Admin seed:** default password is no longer shown on the login page and is only reset when `Seed:ResetAdminPassword=true` is set explicitly (default `false`).

## 2. Tasks module (Phase 1)

```
FarmTask : BaseEntity
  Title (200), Notes (2000), Type {Task, Reminder}, Priority {Normal, High, Urgent},
  Status {ToDo, InProgress, Completed}, DueDate DateOnly?,
  AssigneeUserId string? (FK AspNetUsers, SetNull), CreatedByUserId string (FK, Restrict),
  GoatId int? (FK Goats, SetNull), CompletedAtUtc DateTime?
```

- Tab key `tasks`. Controller `TasksController`; service `ITaskService`.
- Existing `Reminder` rows are **not migrated**: they keep working on the Vaccines page (scope-based farm reminders). The Tasks page shows them read-only in a "Farm reminders" side list so nothing is lost. A later migration can convert them if the business wants one list (DECISION recorded).
- Notifications: deferred until the notification/audit entity exists (Phase 4). Assignment is visible immediately in "My tasks".

## 3. Herd & breeding model (Phase 2)

### Goat additions

| Field | Type | Purpose |
|---|---|---|
| `Owner` | string(100)? | investor / owner |
| `HealthState` | enum `GoatHealthState {Healthy, UnderTreatment, Recovering, Sick}` | independent of lifecycle status |
| `SireId`, `DamId` | int? FK → Goats (Restrict) | pedigree |
| `SireTagText`, `DamTagText` | string(50)? | parent known by tag but not in herd |
| `BreedingStage` | enum `BreedingStage {None, Ready, Crossed, ScanPending, ConfirmedPregnant}` | explicit stage, separate from `Status` |
| `IsTemporaryTag` | bool | newborn kids get a temp tag until tagged |

`GoatStatus` gets `Meat = 6` (production purpose / finishing animals). Lifecycle (`Status`), health (`HealthState`) and breeding (`BreedingStage`) are three independent fields.

### Breeding events (history)

```
BreedingEvent : BaseEntity
  DoeId FK, BuckId FK?, BuckTagText, MatedDate, ScanDate?, ScanResultKids int?,
  Outcome {Open, ConfirmedPregnant, Empty, Kidded, Aborted, Cancelled}, OutcomeDate?, Notes
KiddingRecord : BaseEntity
  BreedingEventId FK?, DoeId FK, KiddingDate, LiveKids, Stillborn, Ease {Normal, Assisted, Difficult},
  Notes, ClientRequestId Guid (unique → idempotent submit)
KiddingRecord.Kids -> Goat.KiddingRecordId (int?, SetNull)
```

The `Goat` columns `PrepCrossDate`, `MatedDate`, `BuckTag`, `KidsCount`, `UltrasoundDate` stay as the **current-cycle projection** used by the existing pages, reports and backup format. Every write goes through `BreedingService`, which updates both the projection and the `BreedingEvent` row in one `SaveChanges`.

### Stage mapping for existing data (migration)

| Existing state | New `BreedingStage` |
|---|---|
| `PrepCrossDate` set, `MatedDate` null | `Ready` |
| `MatedDate` set, `KidsCount` null | `ScanPending` (crossed, awaiting scan) |
| `MatedDate` set, `KidsCount > 0` | `ConfirmedPregnant` |
| otherwise | `None` |

`Status=Pregnant` with no scan is **left unchanged** in the migration (we don't silently rewrite lifecycle status); the herd page now shows those does as "crossed — awaiting scan" via `BreedingStage`. An open `BreedingEvent` is back-filled for each doe with `MatedDate`.

### Rules (enforced in `BreedingService`)

- Eligible doe: female, not archived, status not `Buck`/`Meat`/`Sale`, age ≥ 300 days (`BreedingHelper.MinBreedingAgeDays`), no open breeding event.
- Cross → `BreedingStage=ScanPending`; **status is not changed to Pregnant**.
- Scan with kids → `ConfirmedPregnant`, `Status=Pregnant`; empty → event `Empty`, `BreedingEmptyLog`, `Status=Dry`.
- Due dates: confirmed → "due"; unconfirmed → "est. scan by mated+45 / est. kidding (unconfirmed)".
- Inbreeding: doe and buck checked for parent/child, full/half siblings, and shared grandparents when pedigree exists. Missing parents → "pedigree unknown", never "no relation".
- Record birth: one transaction creates the `KiddingRecord`, the live kid `Goat` rows (`Status=Kid`, `Source=Born`, `DamId`, `SireId`, `EventDate`=birth date, temp tag if blank), closes the `BreedingEvent` (`Kidded`), clears the doe's projection and sets her `Status=Milking`. Duplicate `ClientRequestId` returns the original result.

## 4. Planned later phases (designs fixed now so schema stays coherent)

### Accounting ledger (Phase 5)

`LedgerAccount` (code, name, type Asset/Liability/Equity/Income/Expense, parent), `JournalEntry` (date, source type/id, description, status Draft/Posted/Reversed, reversal FK), `JournalLine` (account, debit, credit, party). `PostingService.PostAsync` validates Σdebit = Σcredit, writes inside a transaction, and posted entries are immutable (reversal entries only). Existing `Income`/`Expense`/`FeedPurchase`/`VaccinePurchase`/`MilkSale` get a nullable `JournalEntryId` and are posted through adapters; history is migrated by a one-off back-post. Reports switch to the ledger only after reconciliation tests pass.

### Stock ledger (Phase 6, shared by Products / Feed / Trading)

`StockItem`, `StockLocation`, `StockMovement` (type, qty, unit cost, from/to location, batch, source doc, `IdempotencyKey`). Balances are derived from movements; a cached balance is allowed only if updated in the same transaction. Trading uses separate `StockOwner=Trading` so trading goods never mix with farm production.

## 5. Front-end

Razor + vanilla JS module (`GoatRecords`) + `goat-records.css`. No SPA framework is introduced. New modules add an `initXxx` function to `goat-records.js`. All new renderers use `esc()` for any user-supplied text.

## 6. Demo mode (design, not yet implemented)

`Demo:Enabled=true` + `ConnectionStrings:DemoConnection` → the app binds to a separate database; startup refuses to enable demo mode if `DemoConnection` equals `DefaultConnection` or the environment is Production without `Demo:AllowInProduction`. A "Reset demo" admin action drops and re-seeds that database only. Banner shown on every page. No client-side flag.

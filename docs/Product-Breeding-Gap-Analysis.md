# Product vs Breeding — Gap Analysis

**Assessment date:** 2026-11-11  
**Product UI reference:** `sandeel-V243-index.html` (`#view-milk`, `prod-hero`, `prod-tabs`, `products.css`)  
**Breeding functional reference:** `/Breeding` (`Views/Breeding/Index.cshtml`, `wwwroot/js/breeding-page.js`, `farm-pro.css`) — aligned with Sandeel breeding v16 direction, not a 1:1 clone  

**Phase status**

| Phase | Status |
|-------|--------|
| 1 — Breeding audit | Complete (this document) |
| 2 — Product audit | Complete (this document) |
| 3 — Gap matrix | Complete (this document) |
| 4 — Implementation | **P0+P1+P2 complete** (2026-11-11) — see §Phase 4 record |
| 7–11 — Post-implementation verification | Placeholder sections at end |

---

## Executive summary

Goat Farm Pro treats **Breeding** and **Products** as separate top-level modules with different jobs:

| Module | Primary job | UI shell |
|--------|-------------|----------|
| **Breeding** | Reproductive lifecycle: season, groups, cross, scan, kidding, buck workload | `breed-subnav` + `breed-view` sections (`farm-pro.css`) |
| **Products** | Production & commercial ops: milk line, meat line, sales, stock, transport, costing, pricing | `prod-hero` + `prod-tabs` + `prod-pane` (`products.css`, `#view-milk`) |

**Mandatory rule (from task):** Breeding is the **functional** reference where overlap exists; Products is the **UI/UX** template. Do **not** import Breeding layouts, sub-nav, or `farm-pro` breeding styling into Products.

**Conclusion:** Most Breeding capabilities are **not applicable** inside Products and must remain on `/Breeding`. The meaningful overlap is **breed as an analytic dimension** (milk/meat production by breed), **herd-linked slaughter**, and **cross-module navigation/export** — not duplication of season plan, scan batches, or buck groups inside Products.

Products is largely **implemented end-to-end** for core flows (milking → tanks → pasteurize → sale; slaughter → sale; stock movements; transport; costing; pricing; catalog). Notable **Product-only** gaps vs V243 (not Breeding parity) include **meat split/cut save buttons without JS handlers** and optional **product flow guide** markup.

**Build/tests at audit time:** `dotnet build GoatFarm.slnx` → succeeded (5 warnings). Filtered tests `BreedingServiceTests` + `Phase8FeedProductsTests` → **19 passed**.

---

## Architecture snapshot

```
┌─────────────────┐     ┌──────────────────┐     ┌─────────────────────┐
│  Goat / Herd    │────▶│  Breeding        │     │  Products           │
│  (master data)  │     │  pregnancy cycle │     │  milk/meat/sales    │
└────────┬────────┘     └────────┬─────────┘     └──────────┬──────────┘
         │                       │                            │
         │    breed, tag, status   │    kids, scans, groups     │ sessions, stock, invoices
         └───────────────────────┴────────────────────────────┘
                              GoatFarmDbContext
```

**Shared data, separate surfaces:** Goat `Breed`, pregnancy fields, and `BreedingEvent` rows drive Breeding UI. Products reads **herd goats**, **milking sessions**, **slaughters**, **sales**, and **stock movements** for production analytics — it does **not** call `IBreedingService` today.

---

## Phase 1 — Breeding module inventory

### 1.1 Screens and navigation

| Area | Route / query | View section | Client |
|------|----------------|--------------|--------|
| Hub | `/Breeding`, `?batch=` | All | `breeding-page.js` |
| Today | default / `?view=today` | `#breedViewToday` | Stats, dated to-do, season snippet, buck workload |
| Season plan | `?view=plan` | `#breedViewPlan` | Batch tabs, timeline, duplicate batch, groups-at-a-glance |
| Bucks | `?view=bucks` | `#breedViewBucks` | Buck cards, doe picker, ranking table |
| Does | `?view=does`, `?scan=` | `#breedViewDoes` | Pipeline, bulk scan, defer cross, group cards |
| Kidding | `?view=kidding` | `#breedViewKidding` | Birth recording, rations, kidding list |

Sub-nav: `breed-subnav` with counts on Today/Does (`Index.cshtml`).

**Not implemented (vs Sandeel breeding v16 prototype):** dedicated buck-check panel, 3-step season wizard, mother-groups UX, Breeding **Reports** sub-tab, full v16 kidding/mother workflows. Tracked as Breeding backlog, not Product scope.

### 1.2 Core workflows and business rules

| Workflow | Service / writer | Rules (verified in code/tests) |
|----------|------------------|--------------------------------|
| Record prep | `RecordPrepAsync` | Doe pipeline staging |
| Record cross | `RecordCrossAsync`, `CheckCrossAsync` | Inbreeding warnings; buck required for bulk/group (`HERD-BREEDING.md` B6) |
| Ultrasound / scan | `RecordUltrasoundAsync`, `SaveScanBatchAsync` | Pregnancy confirmation; bulk scan sheet |
| Birth | `RecordBirthAsync` | Confirmation flow; links to kidding |
| Abortion / returned heat | `RecordAbortionAsync`, `RecordReturnedToHeatAsync` | Status transitions |
| Mark kidded | `MarkKiddedAsync` | Post-birth herd update |
| Season / batch | `UpdateSeasonAsync` | New batch id returned (B5); duplicate batch support in assembler |
| Reconcile pregnancy | `ReconcilePregnancyAsync` | Herd vs breeding alignment (B1) |
| Pipeline actions | `AddDoeToPipelineAsync`, `ApplyStageActionAsync`, `ClearBreedingMistakeAsync` | Stage machine |
| Groups | Create/close/delete, buck in/out, remove member | JSON settings `BreedingGroups` + group DTOs |
| Remove prep/cross | `RemovePrepAsync`, `RemoveCrossAsync` | Undo mistaken entries |

Cross does **not** set pregnant before positive scan (`BreedingCycleWriter` — ticket H note).

### 1.3 Dashboard and cross-module touchpoints

- Dashboard panels may surface breeding stats via `StatisticsService` / herd counts (not duplicated in Products).
- Herd: tag scan, bulk birth batch, heat/BCS, sale/death — feed Breeding indirectly.
- Backup/export: breeding-related entities in backup round-trip tests; not Products UI.

### 1.4 API surface (`BreedingController`)

All mapped in `FarmPermissionMap` under `FarmTabs.Breeding`.

| HTTP | Action | Permission |
|------|--------|------------|
| GET | `Index`, `GetData` | View |
| GET | `LookupTag`, `CheckCross` | View |
| GET | `Groups`, `GroupCards`, `ScanSheet` | View |
| POST | `RecordPrep`, `RecordCross`, `RecordUltrasound`, `RecordBirth`, `RecordAbortion`, `RecordReturnedToHeat`, `MarkKidded`, `CrossFromPrep`, `UpdateSeason`, `ReconcilePregnancy`, `AddDoe`, `StageAction`, `ClearMistake`, `SaveScanBatch`, `CreateGroup`, `CloseGroup`, `TakeBuckOut`, `PutBuckIn`, `RemoveGroupMember` | Add/Edit per action |
| DELETE | `RemovePrep`, `RemoveCross`, `DeleteGroup` | Delete |

### 1.5 Backend services and persistence

| Layer | Primary files |
|-------|----------------|
| Interface | `src/GoatFarm.Application/Interfaces/IBreedingService.cs` |
| Implementation | `src/GoatFarm.Infrastructure/Services/BreedingService.cs` (+ helpers e.g. `BreedingPageAssembler`, `PregnancyRules`, `BreedingCycleWriter`) |
| View models | `src/GoatFarm.Application/ViewModels/Breeding/*` |
| Entities | `BreedingEvent`, `BreedingEmptyLog`, goat pregnancy fields on `Goat` (`Entities.cs`) |
| Settings | `AppSettingKeys.BreedingSeason`, `BreedingGroups` (JSON in app settings) |

### 1.6 Frontend files

| File | Role |
|------|------|
| `Views/Breeding/Index.cshtml` | Server-rendered shell + initial model |
| `wwwroot/js/breeding-page.js` | Sub-nav, AJAX refresh, scan batch, group actions |
| `wwwroot/css/farm-pro.css` | Breeding-specific layout (`breed-*`, `breed-today-*`, plan/bucks) |
| `Views/Breeding/BreedingSeasonRuleRenderer.cshtml` | Season rule rendering (no `@functions` in view) |

### 1.7 Permissions

`FarmPermissionMap`: all `Breeding.*` actions → `FarmTabs.Breeding` with `View` / `Add` / `Edit` / `Delete` as appropriate.

### 1.8 Tests

`tests/GoatFarm.Tests/BreedingServiceTests.cs` — pregnancy rules, season, groups, scan batch, etc. (included in audit test run).

### 1.9 Error handling and confirmations

- `ConfirmationRequiredException` → HTTP 409 with `requiresConfirmation` for birth and stage actions.
- `InvalidOperationException` → 400 with `{ error }`.
- Client must resubmit with confirm flags where implemented in `breeding-page.js`.

---

## Phase 2 — Product module inventory

### 2.1 UI template (must preserve)

| Element | Implementation |
|---------|----------------|
| Wrapper | `Views/Products/Index.cshtml` → `#view-milk` |
| Shell | `Views/Products/_ProductsShell.cshtml` — `prod-hero`, 9 `prod-tab` buttons, panes |
| Styles | `wwwroot/css/products.css` (V243-scoped rules) |
| Script | `wwwroot/js/products-module.js` |
| Data bootstrap | `#products-page-json` serialized from `ProductsPageViewModel` |

**Tabs (match V243):** Milk & Dairy, Meat, Sales, Stock & Movements, **Breed Performance**, Transport, Costs & Expenses, Pricing & Clients, Product Setup.

### 2.2 Tab-by-tab verification (UI → API → service → DB)

| Tab | UI | Primary endpoints | Backend | Status |
|-----|-----|-------------------|---------|--------|
| Milk | Sub-nav: milking, pasteurize, move, make, spoil, tanks, records | `SaveMilkingSession`, `SavePasteurization`, `SaveStockMovement`, `SaveMilkingSessionNames` | `ProductsService` | **Implemented** — save handlers wired in JS |
| Meat | Flow KPIs, slaughter / split / cut forms | `SaveSlaughter`, `SaveSale`, `SaveStockMovement` (transfers/adj) | `ProductsService` | **Partial** — slaughter + sales wired; **split/cut buttons not wired** (see §3) |
| Sales | Invoice desk, credit sales | `SaveSale` | `ProductsService` + `LedgerService` | **Implemented** — invoice format tested (`SF-YYYYMM-DEV-NNNN`) |
| Stock | Ledger, transfers, adjustments | `SaveStockMovement` | Stock movements + warehouses | **Implemented** |
| Breed Performance | `renderBreedPerformance()` | Uses page JSON only (no separate API) | Client aggregation over sessions, legacy milk, slaughters, sales | **Implemented** for **production** KPIs by breed |
| Transport | Shipments, routes | `SaveTransportShipment`, `DeleteTransportShipment`, `SaveDeliveryRoute`, `DeleteDeliveryRoute` | `ProductsService` | **Implemented** |
| Costing | Allocations | `SaveProductCostAllocation`, `DeleteProductCostAllocation` | **Implemented** |
| Pricing | Profiles, client prices | `SaveProductPricing`, `DeleteProductPricing`, `SaveClientProductPrice`, `DeleteClientProductPrice` | **Implemented** |
| Catalog | SKU, warehouses | `SaveCatalog`, `DeleteCatalog`, `SaveWarehouse`, `SaveProductDropdownOptions` | **Implemented** |

### 2.3 API surface (`ProductsController`)

All mapped under `FarmTabs.Products` in `FarmPermissionMap` (`Products.GetData` … `Products.SaveSale`).

### 2.4 Backend

| Layer | Files |
|-------|--------|
| Controller | `src/GoatFarm.Web/Controllers/ProductsController.cs` |
| Service | `src/GoatFarm.Infrastructure/Services/Extended/ProductsService.cs` (and related assemblers) |
| View models | `src/GoatFarm.Application/ViewModels/Products/*` |
| Entities | `ExtendedModuleEntities.cs`: `ProductCatalogItem`, `Warehouse`, `StockMovement`, `MilkingSession`, `SlaughterRecord`, `ProductSale`, `ProductCostAllocation`, `ProductPricingProfile`, `ClientProductPrice`, transport entities |
| Legacy milk | `MilkProduction` + movements for backward compatibility |

### 2.5 Breed Performance tab (important distinction)

**What it does:** Month/breed/mode filters; KPIs, chart, table, methodology blurb, `bp-export` CSV — computed from:

- `analyticsMilkingSessions`, `analyticsLegacyMilk`, `analyticsSlaughters`, `analyticsGoatSales` in page JSON.

**What it does not do:** Conception rate, scan coverage, kidding interval, buck workload, season batch progress — those are **Breeding module** concerns.

This matches V243’s Products “Breed Performance” intent (production economics by breed), not the Breeding hub.

### 2.6 Permissions

`data-needs` attributes on Product buttons (e.g. `milk:create`) align with client-side gating; server enforces `FarmPermissionMap` on `Products.*` actions.

### 2.7 Tests

`tests/GoatFarm.Tests/Phase8FeedProductsTests.cs` — feed season helper, sale invoice, `SaveProductSale` stock issue. Broader Playwright flows referenced in `docs/implementation/V240-TEST-AND-VALIDATION.md` (not re-run for this audit).

### 2.8 V243 alignment gaps (Product template only)

| Item | V243 | App | Notes |
|------|------|-----|--------|
| `product-flow-guide` / `product-help-note` | CSS + typical markup in prototype | CSS in `products.css` only | **Partial** — markup not in `_ProductsShell.cshtml` |
| 9 prod tabs + milk sub-nav | Present | Present | **OK** |
| Meat split / cut | Forms + save | Forms in shell; **no `sl-sp-save` / `sl-ct-save` listeners** | **Partial / defect** |

---

## Phase 3 — Gap analysis matrix

**Legend:** **Implemented** | **Partial** | **Missing** | **N/A** (Breeding-specific; no Product equivalent)

### 3.1 Reproductive and season workflows

| # | Breeding feature | Product equivalent | Status | Missing / notes | UI change (Products template) | Frontend | Backend | DB |
|---|------------------|-------------------|--------|-----------------|------------------------------|----------|---------|-----|
| B01 | Today dashboard (jobs, stats) | — | N/A | Operational breeding cockpit | None | — | — | — |
| B02 | Season plan / batches | — | N/A | | None | — | — | Settings JSON |
| B03 | Buck groups & workload | — | N/A | | None | — | `IBreedingService` | Settings |
| B04 | Doe pipeline & bulk scan | — | N/A | | None | — | Breeding | Events |
| B05 | Kidding & birth recording | — | N/A | Birth stays Breeding; Products sees new goats via Herd | None | — | — | Goat |
| B06 | Cross check / inbreeding warn | — | N/A | | None | — | — | — |
| B07 | Pregnancy reconcile tool | — | N/A | | None | — | — | — |
| B08 | Breeding groups CRUD | — | N/A | | None | — | — | — |
| B09 | Breeding reports sub-tab (v16) | — | N/A | Use Reports module / future Breeding work | None | — | — | — |

### 3.2 Breed dimension and analytics (applicable overlap)

| # | Breeding feature | Product equivalent | Status | Missing / notes | UI change | Frontend | Backend | DB |
|---|------------------|-------------------|--------|-----------------|-----------|----------|---------|-----|
| A01 | Breed on animals (master) | Herd goat list in page JSON; milk-by-breed entry | **Implemented** | | None | `products-module.js`, shell milking grids | `GoatService` / assembler | Goat.Breed |
| A02 | Production KPIs by breed (milk) | Breed Performance → milk mode | **Implemented** | Legacy + session milk both considered | Existing `bp-*` controls | `renderBreedPerformance` | Page assembler analytics | MilkingSession, MilkProduction |
| A03 | Production KPIs by breed (meat) | Breed Performance → meat mode | **Partial** | Depends on slaughter/sales data quality; no live breeding status | Same tab | JS aggregation | `SaveSlaughter` / sales | SlaughterRecord, ProductSale |
| A04 | Reproductive KPIs by breed (conception, scan %) | Breed Performance tab | **N/A** | Wrong module; would duplicate Breeding | **Do not** add Breeding KPIs to `prod-tabs` without product decision | — | Would need new read API from breeding stats | BreedingEvent |
| A05 | Export breed analytics | `bp-export` CSV | **Implemented** (production only) | Not breeding season export | Button exists in shell | `products-module.js` | — | — |
| A06 | Deep link: breed → Herd filter | Optional cross-nav | **Missing** | UX capability, not Breeding UI port | Add `prod-` styled link in Breed Performance panel | Shell + JS | — | — |
| A07 | Deep link: breed → Breeding season context | Optional cross-nav | **Missing** | Link only; no embedded season UI | Same | JS `window.location` | — | — |

### 3.3 Lifecycle touchpoints (herd ↔ production)

| # | Breeding feature | Product equivalent | Status | Missing / notes | UI change | Frontend | Backend | DB |
|---|------------------|-------------------|--------|-----------------|-----------|----------|---------|-----|
| L01 | Slaughter removes goat from herd | Meat → Slaughter form → `SaveSlaughter` | **Implemented** | | Meat sub-forms | `sl-sg-save` | `ProductsService` | SlaughterRecord, Goat status |
| L02 | Birth adds kids | Herd add / breeding birth | **N/A** in Products | | None | — | Breeding/Herd | Goat |
| L03 | Milk from “milking” does | Record milking modes | **Implemented** | | Milk tab | `sl-mg-save` | `SaveMilkingSession` | MilkingSession |
| L04 | Cost per litre/kg by breed | Costing tab + Breed Performance | **Partial** | Allocation exists; not all costs auto-tagged by breed | Existing costing panels | products JS | `ProductCostAllocation` | Allocations |
| L05 | Sale of live goat vs carcass | Sales type picker | **Implemented** | Breeding status not shown on sale desk | Sales tab | JS | `SaveSale` | ProductSale |

### 3.4 Shared cross-cutting concerns

| # | Topic | Breeding | Products | Status |
|---|-------|----------|----------|--------|
| C01 | Permissions tab separation | `FarmTabs.Breeding` | `FarmTabs.Products` | **Implemented** |
| C02 | Allow-by-default unmapped actions | Project-wide (`V240-GAP-ANALYSIS.md` X1) | Same risk for any unmapped `Products.*` | **Partial** (security backlog) |
| C03 | CSRF on JSON POST | `[IgnoreAntiforgeryToken]` | Same | **Partial** |
| C04 | Notifications | No centralized notify | No centralized notify | **Missing** (both) |
| C05 | Attachments on records | Notes on events | Notes on movements/sales | **Partial** (text only) |
| C06 | Audit history UI | Limited | Limited | **Partial** |

### 3.5 Product module defects found during audit (not Breeding parity)

| # | Issue | Status | Evidence | Recommended fix (Phase 4) |
|---|-------|--------|----------|---------------------------|
| P01 | Split carcass save | **Missing** FE | `sl-sp-save` in `_ProductsShell.cshtml`; no listener in `products-module.js` | Implement POST via `SaveStockMovement` or dedicated DTO; keep meat sub-nav UI |
| P02 | Save cuts | **Missing** FE | `sl-ct-save` same pattern | Same |
| P03 | Product flow guide | **Partial** vs V243 | CSS only | Add guide block under `prod-hero` using existing classes |
| P04 | Meat stage “Split into halves” / “Cut” opens forms | **Partial** | `data-sl-open` switches forms; save incomplete | Tie to P01–P02 |

---

## Phase 4 — Implementation backlog (proposed, not started)

Prioritized work that respects **Products UI template** and **does not port Breeding layout**.

| Priority | ID | Scope | Type | Files (expected) | DB |
|----------|-----|-------|------|------------------|-----|
| P0 | P01–P02 | Wire meat split + cut saves | Product / V243 | `products-module.js`, possibly `ProductsService`, `ProductsController` | Maybe new movement types or slaughter line items — **analyze before migrate** |
| P1 | P03 | Add `product-flow-guide` markup | Product / V243 | `_ProductsShell.cshtml` | None |
| P2 | A06–A07 | Breed Performance links to Herd (`?breed=`) and Breeding (`?view=does` + breed filter if supported) | Cross-module UX | `_ProductsShell.cshtml`, `products-module.js` | None |
| P3 | A04 | Reproductive KPIs on Products tab | **Decision required** | Default: **reject** — keep N/A; implement in Reports or Breeding instead | Breeding aggregates |
| — | B* | v16 Breeding gaps (buck check, reports tab, wizard) | Breeding only | `Breeding/*` | Per feature |

**Explicitly out of scope for Product UI:**

- `breed-subnav`, `breed-view`, season timeline, scan sheet, buck ranking table inside `#view-milk`.
- Copying `farm-pro.css` breeding blocks into `products.css` for parity.

---

## Phase 4 — Implementation record

*To be filled after approved implementation.*

| Item | Done | Notes |
|------|------|-------|
| P01–P02 Meat split/cut | Yes | `SaveMeatSplit` / `SaveMeatCut` + JS handlers |
| P03 Flow guide | Yes | `_ProductsShell.cshtml` |
| A06–A07 Deep links | Yes | Breed Performance → Herd `?q=` / Breeding `?view=does` |
| Slaughter warehouse stock | Yes | `SaveSlaughter` receives MEAT kg to chiller |
| Migration | Yes | `20261111171339_SlaughterMeatStockFields` |
| Files modified | See below | |
| Tests | 7 passed | `Phase8FeedProductsTests` (+2 meat tests) |

**Key files:** `ProductsService.cs`, `ProductsController.cs`, `products-module.js`, `_ProductsShell.cshtml`, `products.css`, `ExtendedModuleEntities.cs`, `ExtendedModuleViewModels.cs`, `FarmPermissionMap.cs`, `Phase8FeedProductsTests.cs`

---

## 7. UI/UX verification checklist (post-implementation)

- [ ] Products still uses `#view-milk`, `prod-hero`, `prod-tabs`, `products.css`
- [ ] No `breed-subnav` / `breed-view` introduced under Products
- [ ] New controls match existing `panel`, `sl-*`, `btn-green` patterns
- [ ] Meat split/cut completes stock ledger updates
- [ ] Breed Performance unchanged in layout; only additive links if approved
- [ ] Breeding `/Breeding` unchanged unless shared bug fix
- [ ] Responsive rules in `products.css` still apply

---

## 8. Testing plan and results

### 8.1 Audit-time results (2026-11-11)

| Check | Result |
|-------|--------|
| `dotnet build GoatFarm.slnx` (output to `.build-out/gap-doc`) | Succeeded, 5 warnings |
| `BreedingServiceTests` + `Phase8FeedProductsTests` | 19 passed |

### 8.2 Planned after Phase 4

| Check | Command / method |
|-------|------------------|
| Full unit suite | `dotnet test tests/GoatFarm.Tests` |
| Postgres integration | CI / local PG tests |
| Playwright Products flows | `E2E/BrowserFlowTests` milk/sales paths |
| Manual CRUD | Slaughter → split → cut → sale → stock refresh |
| Regression Breeding | Today, scan batch, `UpdateSeason`, groups |
| Permissions | User without `Products` Add cannot save |

*Post-implementation results: TBD.*

---

## 9. Documentation maintenance

This file is the single source for Product↔Breeding parity decisions. After implementation, update:

1. §3 matrix statuses  
2. §Phase 4 implementation record  
3. §8.2 test results  
4. §10 remaining limitations  

Related docs: `docs/tickets/HERD-BREEDING.md`, `docs/tickets/FEED-PRODUCTS.md`, `docs/implementation/V240-GAP-ANALYSIS.md`.

---

## 10. Remaining limitations and open decisions

| ID | Limitation | Owner |
|----|------------|-------|
| D1 | Should **reproductive** KPIs ever appear on Products “Breed Performance”? | Product owner — default **no** (N/A) |
| D2 | Meat split/cut backend model: pure stock movements vs slaughter line entity | Engineering — required for P01–P02 |
| D3 | Breeding v16 reports sub-tab | Breeding backlog, not Products |
| D4 | Global permission allow-by-default (X1) | Security — affects all modules |

---

## 11. Definition of done tracking

| Criterion | Audit | After Phase 4 |
|-----------|-------|---------------|
| Every Breeding capability evaluated vs Product | Yes | — |
| Applicable gaps addressed | Identified | Pending |
| Product UI template preserved | Yes (no implementation yet) | Re-verify |
| Backend persistence for new work | N/A | TBD |
| Tests pass | Subset passed | Full suite TBD |
| This doc updated | Initial complete | After implementation |

---

## Appendix A — File index

### Breeding

- `src/GoatFarm.Web/Views/Breeding/Index.cshtml`
- `src/GoatFarm.Web/wwwroot/js/breeding-page.js`
- `src/GoatFarm.Web/wwwroot/css/farm-pro.css`
- `src/GoatFarm.Web/Controllers/BreedingController.cs`
- `src/GoatFarm.Infrastructure/Services/BreedingService.cs`
- `tests/GoatFarm.Tests/BreedingServiceTests.cs`

### Products

- `src/GoatFarm.Web/Views/Products/Index.cshtml`
- `src/GoatFarm.Web/Views/Products/_ProductsShell.cshtml`
- `src/GoatFarm.Web/wwwroot/js/products-module.js`
- `src/GoatFarm.Web/wwwroot/css/products.css`
- `src/GoatFarm.Web/Controllers/ProductsController.cs`
- `src/GoatFarm.Infrastructure/Services/Extended/ProductsService.cs`
- `tests/GoatFarm.Tests/Phase8FeedProductsTests.cs`

### Reference prototype

- `sandeel-V243-index.html` — Products visual spec  
- `sandeel-breeding-v16.html` — Breeding functional target (separate from Products)

---

## Appendix B — Clarification for stakeholders

**Feature parity ≠ UI duplication.** Products should gain **applicable** capabilities (production analytics by breed, herd-linked meat line, cross-links, exports) while **reproductive management** remains on the Breeding module with its own UI. The “Breed Performance” tab name refers to **farm output by breed**, not a second Breeding dashboard.

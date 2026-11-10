# Feed & Products — Sandeel fix list (F1–F4, P2)

| ID | Item | Status |
|----|------|--------|
| F1 | Stock reconcile / rebuild store books vs purchases & usage | Done — `RebuildFeedStockAsync`, UI **Rebuild stock**, `Phase4FeedTests` |
| F2 | Binola in feed catalog | Done — `FeedTypes.Binola` |
| F3 | Direct feeds active only in selected months | Done — `FeedSettingsDto.ActiveMonthsByFeed`, `UpdateFeedSeason`, plan UI **Set months**, auto-usage respects season |
| F4 | Green fodder pool paid date / allocate months / Finance | Done — `FodderPoolItemDto`, `SyncFodderPoolFinanceAsync`, feed UI |
| P2 | Product sale invoice `SF-YYYYMM-DEV-NNNN` | Done — `FarmRecordNumberFormat.FormatSaleInvoice`, `ProductSale.InvoiceNo`, `SaveProductSaleAsync` |
| — | Sale issues stock + blocks oversell | Done — stock **Issue** when `productId` + `warehouseId`; server validation |
| GS-004 | Wire numbering into product sales | Done (trading bills still open elsewhere) |

**Ops:** After bad legacy data, use **Feed → Rebuild stock** once; set bulk-feed seasons before winter bhusa/toori planning.

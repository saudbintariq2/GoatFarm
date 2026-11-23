# Feed & Products — Sandeel fix list (F1–F7, P2)

| ID | Item | Status |
|----|------|--------|
| F1 | Stock reconcile / rebuild store books vs purchases & usage | Done — `RebuildFeedStockAsync`, UI **Rebuild stock**, `Phase4FeedTests` |
| F2 | Binola in feed catalog | Done — `FeedTypes.Binola` |
| F3 | Direct feeds active only in selected months | Done — `FeedSettingsDto.ActiveMonthsByFeed`, `UpdateFeedSeason`, plan UI **Set months**, auto-usage respects season |
| F4 | Green fodder pool paid date / allocate months / Finance (V243) | Done — monthly spread 1–60, dedupe, Feed UI parity, Finance read-only |
| F5 | Feed purchase → cash/bank + supplier payable + P&L spread | Done — `PostPurchaseMoneyAsync`, `SyncFeedPurchasePlExpensesAsync` |
| F6 | Feed cost per litre uses milking sessions + legacy milk | Done — `FarmMetricsService.MilkLitresAsync`, Feed banner via `FeedCostPerLitreAsync` |
| F7 | Dashboard/Reports feed stock KPIs match Feed tab | Done — `GetFeedStockRowsAsync`, `GetFeedStockBookValueAsync`; herd-based plan headcount (meat kids, custom groups) |
| P2 | Product sale invoice `SF-YYYYMM-DEV-NNNN` | Done — `FarmRecordNumberFormat.FormatSaleInvoice`, `ProductSale.InvoiceNo`, `SaveProductSaleAsync` |
| — | Sale issues stock + blocks oversell | Done — stock **Issue** when `productId` + `warehouseId`; server validation |
| GS-004 | Wire numbering into product sales | Done — product `SF-…` + trading `TR-…` (server); see GENERAL-SECURITY GS-004 |

**Ops:** After bad legacy data, use **Feed → Rebuild stock** once; set bulk-feed seasons before winter bhusa/toori planning.

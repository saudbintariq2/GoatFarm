# Finance · Health · Trading · Reports · Admin (Nov 2026)

## Done this batch

| ID | Item |
|----|------|
| Fi6 | Monthly closing summary on Finance → Bank & cash (6-month carry-forward) |
| Fi7 | Overview KPI order: spent → funding → income |
| Fi (vaccines) | Block manual `Vaccines` expense; exclude from totals/reports |
| V2 | Per-goat vaccination history on Health page |
| T1 | Server trading balance-sheet check + UI subtitle |
| T2 | Server `TR-yyyyMM-DEV-NNN` on new sells |
| R (logistics) | Reports logistics tab from `TransportShipments` |
| R (owners) | Reports investors tab from funding + profit partners |
| A5 / GS-010 | Unified header bell (`NotificationSummary`) |

## Verify manually

- Reports CSV/print (R3)
| Fi8 | Low-balance warnings on integrity check, Bank & Cash tab, dashboard cash list |
| Fi9 | `OpeningBalanceLocked` + one-time Set opening balance; auto-lock on ledger activity |
- A3 Users vs Admin separation (UX only)

## Tests

`tests/GoatFarm.Tests/Phase9FinanceHealthTradingReportsAdminTests.cs`

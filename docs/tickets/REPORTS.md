# Reports module (R1–R8)

| ID | Summary | Status |
|----|---------|--------|
| R1 | Revenue/expense totals use `FinanceService.GetStatementAsync` (product sales, invoices, milk, salaries) | **Done** |
| R2 | Feed cost/L via `IFarmMetricsService.FeedCostPerLitreAsync`; health includes `GoatTreatment.Cost` | **Done** |
| R3 | Dashboard feed KPI: planned-out feeds before “N d” minimum days | **Done** |
| R4 | Feed section stock rows from `GetFeedStockRowsAsync` (aligned with Feed tab) | **Done** |
| R5 | `Employee.StartDate`/`EndDate` + `StaffSalaryAccrual` in comparison/trend/Finance statement | **Done** |
| R6 | Collected milk from `MilkLitresAsync` (sessions + legacy); comparison includes sessions | **Done** |
| R7 | Herd: sales movement row, sold changes, bucks by `GoatStatus.Buck` | **Done** |
| R8 | Alerts: out-of-stock only when `KgPerDay > 0`; build-up via `NextCrossDate` + `HerdRules.BuildUpDays` | **Done** |

## Tests

- `GoatFarm.Tests/ReportsPhaseTests.cs` — salary accrual, Finance/Reports income parity

## Notes

- Legacy `BuildIncomeMap` / `BuildExpenseMap` remain for reference but page totals come from the finance statement.
- Profitability goat sales still read manual income types; live `GoatSale` rows appear in herd movement.
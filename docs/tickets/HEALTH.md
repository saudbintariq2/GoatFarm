# Health — Sandeel fix list (V1–V2)

| ID | Item | Status |
|----|------|--------|
| V1 | Due counts respect **MinAgeDays**; repeat vaccines without history are not “due today”; shared `VaccineDueHelper` for Health, Dashboard, Reports | Done |
| V2 | ET pre-kidding rule uses **BeforeKidding** (days before expected kidding), confirmed-pregnant scope | Done |

## Notes

- `VaccinationHistory` gains `IsOpeningRecord` for future goat-level opening dose UI (audit spec).
- Existing production schedules are patched in migration SQL (PPR/FMD/CCPP/Deworming min age; ET booster rule).

## Tests

`tests/GoatFarm.Tests/VaccineDueTests.cs` — also `Phase9FinanceHealthTradingReportsAdminTests.VaccinePage_IncludesTagHistory` (V2 UI table).